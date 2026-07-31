using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Commands
{
    /// <summary>
    /// Application boundary for road placement, demolition, and no-demolition
    /// definition/direction replacement. It owns neither presentation objects nor
    /// raw simulation arrays; all changes use RoadNetworkState atomic batches.
    /// </summary>
    public sealed class RoadCommandHandler : ICityCommandHandler
    {
        private readonly RoadNetworkState _roadNetwork;
        private readonly RoadDefinitionCatalog _definitions;
        private readonly IConstructionFunds _funds;
        private readonly MunicipalFinanceState _municipalFinance;

        public RoadCommandHandler(
            RoadNetworkState roadNetwork,
            RoadDefinitionCatalog definitions,
            RoadConstructionBudget budget)
            : this(roadNetwork, definitions, (IConstructionFunds)budget)
        {
        }

        public RoadCommandHandler(
            RoadNetworkState roadNetwork,
            RoadDefinitionCatalog definitions,
            IConstructionFunds funds)
        {
            _roadNetwork = roadNetwork ?? throw new ArgumentNullException(nameof(roadNetwork));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _funds = funds ?? throw new ArgumentNullException(nameof(funds));
            _municipalFinance = funds as MunicipalFinanceState;
        }

        public RoadCommandHandler(
            RoadNetworkState roadNetwork,
            IEnumerable<RoadDefinition> definitions,
            int initialFunds)
            : this(
                roadNetwork,
                new RoadDefinitionCatalog(definitions),
                new RoadConstructionBudget(initialFunds))
        {
        }

        public RoadNetworkState RoadNetwork => _roadNetwork;
        public RoadDefinitionCatalog Definitions => _definitions;
        public RoadConstructionBudget Budget => _funds as RoadConstructionBudget;
        public IConstructionFunds Funds => _funds;
        public RoadCommandResult LastResult { get; private set; }

        public bool CanHandle(ICityCommand command)
        {
            return command is PlaceRoadCommand ||
                   command is DemolishRoadCommand ||
                   command is UpgradeRoadCommand;
        }

        public RoadCommandResult Preview(PlaceRoadCommand command)
        {
            if (command == null)
            {
                return Reject(
                    RoadCommandOperation.Place,
                    StableId.Invalid,
                    RoadCommandFailureReason.InvalidCommand,
                    0,
                    null);
            }

            return EvaluatePlacement(command);
        }

        public RoadCommandResult Preview(DemolishRoadCommand command)
        {
            if (command == null)
            {
                return Reject(
                    RoadCommandOperation.Demolish,
                    StableId.Invalid,
                    RoadCommandFailureReason.InvalidCommand,
                    0,
                    null);
            }

            return EvaluateDemolition(command);
        }

        public RoadCommandResult Preview(UpgradeRoadCommand command)
        {
            if (command == null)
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    StableId.Invalid,
                    RoadCommandFailureReason.InvalidCommand,
                    0,
                    null);
            }

            return EvaluateUpgrade(command);
        }

        public RoadCommandResult Execute(PlaceRoadCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid ? CommitPlacement(command, preview) : preview;
            LastResult = result;
            return result;
        }

        public RoadCommandResult Execute(DemolishRoadCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid ? CommitDemolition(command, preview) : preview;
            LastResult = result;
            return result;
        }

        public RoadCommandResult Execute(UpgradeRoadCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid ? CommitUpgrade(command, preview) : preview;
            LastResult = result;
            return result;
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            if (command.Command == null || !CanHandle(command.Command))
                return CommandValidationResult.Unsupported("No road handler can execute this command.");

            RoadCommandResult result;
            if (command.Command is PlaceRoadCommand placement)
                result = Preview(placement);
            else if (command.Command is DemolishRoadCommand demolition)
                result = Preview(demolition);
            else
                result = Preview((UpgradeRoadCommand)command.Command);

            return result.IsValid
                ? CommandValidationResult.Valid
                : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            RoadCommandResult result;
            if (command.Command is PlaceRoadCommand placement)
                result = Execute(placement);
            else if (command.Command is DemolishRoadCommand demolition)
                result = Execute(demolition);
            else if (command.Command is UpgradeRoadCommand upgrade)
                result = Execute(upgrade);
            else
                return CommandExecutionResult.Unsupported("No road handler can execute this command.");

            return result.IsApplied
                ? CommandExecutionResult.Applied(result)
                : CommandExecutionResult.Rejected(result.Message).WithDetails(result);
        }

        private RoadCommandResult CommitPlacement(PlaceRoadCommand command, RoadCommandResult preview)
        {
            if (!_funds.TrySpend(preview.Cost))
            {
                return Reject(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    preview.Cost,
                    command.Cells);
            }

            if (_roadNetwork.TryApplyPlacement(command.Cells, command.RoadDefinitionId))
            {
                RecordRoadExpense(preview.Cost);
                return RoadCommandResult.Applied(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    preview.Cost,
                    preview.FundsBefore,
                    _funds.Funds,
                    command.Cells);
            }

            _funds.TryRefund(preview.Cost);
            return Reject(
                RoadCommandOperation.Place,
                command.RoadDefinitionId,
                RoadCommandFailureReason.StateChangedBeforeCommit,
                preview.Cost,
                command.Cells);
        }

        private RoadCommandResult CommitDemolition(
            DemolishRoadCommand command,
            RoadCommandResult preview)
        {
            if (_roadNetwork.TryApplyDemolition(command.Cells))
            {
                return RoadCommandResult.Applied(
                    RoadCommandOperation.Demolish,
                    StableId.Invalid,
                    0,
                    preview.FundsBefore,
                    _funds.Funds,
                    command.Cells);
            }

            return Reject(
                RoadCommandOperation.Demolish,
                StableId.Invalid,
                RoadCommandFailureReason.StateChangedBeforeCommit,
                0,
                command.Cells);
        }

        private RoadCommandResult CommitUpgrade(
            UpgradeRoadCommand command,
            RoadCommandResult preview)
        {
            if (!_funds.TrySpend(preview.Cost))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    preview.Cost,
                    command.Cells,
                    command.DirectionMode);
            }

            var directions = BuildDirections(command);
            if (_roadNetwork.TryApplyReplacement(
                    command.Cells,
                    command.TargetRoadDefinitionId,
                    directions))
            {
                RecordRoadExpense(preview.Cost);
                return RoadCommandResult.Applied(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    preview.Cost,
                    preview.FundsBefore,
                    _funds.Funds,
                    command.Cells,
                    command.DirectionMode);
            }

            _funds.TryRefund(preview.Cost);
            return Reject(
                RoadCommandOperation.Upgrade,
                command.TargetRoadDefinitionId,
                RoadCommandFailureReason.StateChangedBeforeCommit,
                preview.Cost,
                command.Cells,
                command.DirectionMode);
        }

        private RoadCommandResult EvaluatePlacement(PlaceRoadCommand command)
        {
            var common = ValidateCommon(command.Cells);
            if (common != RoadCommandFailureReason.None)
                return Reject(RoadCommandOperation.Place, command.RoadDefinitionId, common, 0, command.Cells);
            if (!command.RoadDefinitionId.IsValid ||
                !_definitions.TryGet(command.RoadDefinitionId, out var definition))
            {
                return Reject(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    RoadCommandFailureReason.InvalidDefinition,
                    0,
                    command.Cells);
            }

            for (var index = 0; index < command.Cells.Count; index++)
            {
                if (_roadNetwork.IsOccupied(command.Cells[index]))
                {
                    return Reject(
                        RoadCommandOperation.Place,
                        command.RoadDefinitionId,
                        RoadCommandFailureReason.RoadAlreadyExists,
                        0,
                        command.Cells);
                }
            }

            if (!TryMultiplyCost(definition.BuildCost, command.Cells.Count, out var cost))
            {
                return Reject(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    RoadCommandFailureReason.CostOverflow,
                    0,
                    command.Cells);
            }
            if (!_funds.CanAfford(cost))
            {
                return Reject(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    cost,
                    command.Cells);
            }

            return RoadCommandResult.Preview(
                RoadCommandOperation.Place,
                command.RoadDefinitionId,
                cost,
                _funds.Funds,
                command.Cells);
        }

        private RoadCommandResult EvaluateDemolition(DemolishRoadCommand command)
        {
            var common = ValidateCommon(command.Cells);
            if (common != RoadCommandFailureReason.None)
                return Reject(RoadCommandOperation.Demolish, StableId.Invalid, common, 0, command.Cells);

            for (var index = 0; index < command.Cells.Count; index++)
            {
                if (!_roadNetwork.IsOccupied(command.Cells[index]))
                {
                    return Reject(
                        RoadCommandOperation.Demolish,
                        StableId.Invalid,
                        RoadCommandFailureReason.NoRoad,
                        0,
                        command.Cells);
                }
            }

            return RoadCommandResult.Preview(
                RoadCommandOperation.Demolish,
                StableId.Invalid,
                0,
                _funds.Funds,
                command.Cells);
        }

        private RoadCommandResult EvaluateUpgrade(UpgradeRoadCommand command)
        {
            var common = ValidateCommon(command.Cells);
            if (common != RoadCommandFailureReason.None)
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    common,
                    0,
                    command.Cells,
                    command.DirectionMode);
            }
            if (!command.TargetRoadDefinitionId.IsValid ||
                !_definitions.TryGet(command.TargetRoadDefinitionId, out var targetDefinition))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.InvalidDefinition,
                    0,
                    command.Cells,
                    command.DirectionMode);
            }
            if (!IsValidDirectionMode(command.DirectionMode))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.InvalidDirection,
                    0,
                    command.Cells,
                    command.DirectionMode);
            }
            if (command.DirectionMode != RoadDirectionMode.Bidirectional && command.Cells.Count < 2)
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.DirectionRequiresPath,
                    0,
                    command.Cells,
                    command.DirectionMode);
            }

            for (var index = 0; index < command.Cells.Count; index++)
            {
                if (!_roadNetwork.IsOccupied(command.Cells[index]))
                {
                    return Reject(
                        RoadCommandOperation.Upgrade,
                        command.TargetRoadDefinitionId,
                        RoadCommandFailureReason.NoRoad,
                        0,
                        command.Cells,
                        command.DirectionMode);
                }
            }

            if (!TryCalculateUpgradeCost(command.Cells, targetDefinition, out var cost))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.CostOverflow,
                    0,
                    command.Cells,
                    command.DirectionMode);
            }

            var directions = BuildDirections(command);
            if (!HasReplacementChange(command.Cells, command.TargetRoadDefinitionId, directions))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.NoChange,
                    cost,
                    command.Cells,
                    command.DirectionMode);
            }
            if (!_funds.CanAfford(cost))
            {
                return Reject(
                    RoadCommandOperation.Upgrade,
                    command.TargetRoadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    cost,
                    command.Cells,
                    command.DirectionMode);
            }

            return RoadCommandResult.Preview(
                RoadCommandOperation.Upgrade,
                command.TargetRoadDefinitionId,
                cost,
                _funds.Funds,
                command.Cells,
                command.DirectionMode);
        }

        private RoadCommandFailureReason ValidateCommon(IReadOnlyList<CellCoord> cells)
        {
            if (cells == null || cells.Count == 0)
                return RoadCommandFailureReason.EmptyPath;

            var seen = new HashSet<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (!seen.Add(cells[index]))
                    return RoadCommandFailureReason.DuplicateCell;
                if (!_roadNetwork.IsInBounds(cells[index]))
                    return RoadCommandFailureReason.OutOfBounds;
            }

            return RoadPath.IsStraight(cells)
                ? RoadCommandFailureReason.None
                : RoadCommandFailureReason.InvalidPath;
        }

        private bool TryCalculateUpgradeCost(
            IReadOnlyList<CellCoord> cells,
            RoadDefinition targetDefinition,
            out int cost)
        {
            long total = 0;
            for (var index = 0; index < cells.Count; index++)
            {
                var current = _definitions.Get(_roadNetwork.GetRoadDefinitionId(cells[index]));
                total += Math.Max(0, targetDefinition.BuildCost - current.BuildCost);
                if (total > int.MaxValue)
                {
                    cost = 0;
                    return false;
                }
            }
            cost = (int)total;
            return true;
        }

        private bool HasReplacementChange(
            IReadOnlyList<CellCoord> cells,
            StableId targetDefinitionId,
            IReadOnlyList<RoadTravelDirection> directions)
        {
            for (var index = 0; index < cells.Count; index++)
            {
                var state = _roadNetwork.GetCell(cells[index]);
                if (state.DefinitionId != targetDefinitionId ||
                    state.TravelDirection != directions[index])
                {
                    return true;
                }
            }
            return false;
        }

        private static IReadOnlyList<RoadTravelDirection> BuildDirections(UpgradeRoadCommand command)
        {
            var direction = RoadTravelDirection.Bidirectional;
            if (command.DirectionMode != RoadDirectionMode.Bidirectional)
            {
                direction = RoadTravelDirectionRules.FromPath(
                    command.Start,
                    command.End,
                    command.DirectionMode == RoadDirectionMode.Reverse);
            }

            var directions = new RoadTravelDirection[command.Cells.Count];
            for (var index = 0; index < directions.Length; index++)
                directions[index] = direction;
            return Array.AsReadOnly(directions);
        }

        private static bool IsValidDirectionMode(RoadDirectionMode mode)
        {
            return mode >= RoadDirectionMode.Bidirectional && mode <= RoadDirectionMode.Reverse;
        }

        private static bool TryMultiplyCost(int unitCost, int count, out int cost)
        {
            var total = (long)unitCost * count;
            if (total > int.MaxValue)
            {
                cost = 0;
                return false;
            }
            cost = (int)total;
            return true;
        }

        private void RecordRoadExpense(int cost)
        {
            if (cost > 0)
            {
                _municipalFinance?.RecordConstructionExpense(
                    MunicipalConstructionCategory.Road,
                    cost);
            }
        }

        private RoadCommandResult Reject(
            RoadCommandOperation operation,
            StableId definitionId,
            RoadCommandFailureReason reason,
            int cost,
            IReadOnlyList<CellCoord> cells,
            RoadDirectionMode directionMode = RoadDirectionMode.Bidirectional)
        {
            return RoadCommandResult.Rejected(
                operation,
                definitionId,
                reason,
                cost,
                _funds.Funds,
                cells,
                directionMode);
        }
    }
}
