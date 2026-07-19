using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Commands
{
    /// <summary>
    /// Application boundary for road placement and demolition. It owns neither
    /// presentation objects nor raw simulation arrays; all state changes go
    /// through RoadNetworkState's validated batch methods.
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

        /// <summary>Compatibility accessor for P2 callers using a standalone budget.</summary>
        public RoadConstructionBudget Budget => _funds as RoadConstructionBudget;

        public IConstructionFunds Funds => _funds;

        public RoadCommandResult LastResult { get; private set; }

        public bool CanHandle(ICityCommand command)
        {
            return command is PlaceRoadCommand || command is DemolishRoadCommand;
        }

        public RoadCommandResult Preview(PlaceRoadCommand command)
        {
            if (command == null)
            {
                return RoadCommandResult.Rejected(
                    RoadCommandOperation.Place,
                    StableId.Invalid,
                    RoadCommandFailureReason.InvalidCommand,
                    0,
                    _funds.Funds,
                    null);
            }

            return Evaluate(
                RoadCommandOperation.Place,
                command.RoadDefinitionId,
                command.Cells,
                requiresDefinition: true);
        }

        public RoadCommandResult Preview(DemolishRoadCommand command)
        {
            if (command == null)
            {
                return RoadCommandResult.Rejected(
                    RoadCommandOperation.Demolish,
                    StableId.Invalid,
                    RoadCommandFailureReason.InvalidCommand,
                    0,
                    _funds.Funds,
                    null);
            }

            return Evaluate(
                RoadCommandOperation.Demolish,
                StableId.Invalid,
                command.Cells,
                requiresDefinition: false);
        }

        public RoadCommandResult Execute(PlaceRoadCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid
                ? CommitPlacement(command, preview)
                : preview;
            LastResult = result;
            return result;
        }

        public RoadCommandResult Execute(DemolishRoadCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid
                ? CommitDemolition(command, preview)
                : preview;
            LastResult = result;
            return result;
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            var result = Preview(command.Command as PlaceRoadCommand);
            if (command.Command is DemolishRoadCommand demolition)
                result = Preview(demolition);
            if (command.Command == null || !CanHandle(command.Command))
                return CommandValidationResult.Unsupported("No road handler can execute this command.");

            return result.IsValid
                ? CommandValidationResult.Valid
                : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            if (command.Command is PlaceRoadCommand placement)
            {
                var result = Execute(placement);
                return result.IsApplied
                    ? CommandExecutionResult.Applied(result)
                    : CommandExecutionResult.Rejected(result.Message).WithDetails(result);
            }

            if (command.Command is DemolishRoadCommand demolition)
            {
                var result = Execute(demolition);
                return result.IsApplied
                    ? CommandExecutionResult.Applied(result)
                    : CommandExecutionResult.Rejected(result.Message).WithDetails(result);
            }

            return CommandExecutionResult.Unsupported("No road handler can execute this command.");
        }

        private RoadCommandResult CommitPlacement(PlaceRoadCommand command, RoadCommandResult preview)
        {
            if (!_funds.TrySpend(preview.Cost))
            {
                return RoadCommandResult.Rejected(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    preview.Cost,
                    _funds.Funds,
                    command.Cells);
            }

            if (_roadNetwork.TryApplyPlacement(command.Cells, command.RoadDefinitionId))
            {
                _municipalFinance?.RecordConstructionExpense(
                    MunicipalConstructionCategory.Road,
                    preview.Cost);
                return RoadCommandResult.Applied(
                    RoadCommandOperation.Place,
                    command.RoadDefinitionId,
                    preview.Cost,
                    preview.FundsBefore,
                    _funds.Funds,
                    command.Cells);
            }

            _funds.TryRefund(preview.Cost);
            return RoadCommandResult.Rejected(
                RoadCommandOperation.Place,
                command.RoadDefinitionId,
                RoadCommandFailureReason.StateChangedBeforeCommit,
                preview.Cost,
                _funds.Funds,
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

            return RoadCommandResult.Rejected(
                RoadCommandOperation.Demolish,
                StableId.Invalid,
                RoadCommandFailureReason.StateChangedBeforeCommit,
                0,
                _funds.Funds,
                command.Cells);
        }

        private RoadCommandResult Evaluate(
            RoadCommandOperation operation,
            StableId roadDefinitionId,
            IReadOnlyList<CellCoord> cells,
            bool requiresDefinition)
        {
            var funds = _funds.Funds;
            if (cells == null || cells.Count == 0)
            {
                return RoadCommandResult.Rejected(
                    operation,
                    roadDefinitionId,
                    RoadCommandFailureReason.EmptyPath,
                    0,
                    funds,
                    cells);
            }

            RoadDefinition definition = null;
            if (requiresDefinition &&
                (!roadDefinitionId.IsValid || !_definitions.TryGet(roadDefinitionId, out definition)))
            {
                return RoadCommandResult.Rejected(
                    operation,
                    roadDefinitionId,
                    RoadCommandFailureReason.InvalidDefinition,
                    0,
                    funds,
                    cells);
            }

            var seen = new HashSet<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (!seen.Add(cells[index]))
                {
                    return RoadCommandResult.Rejected(
                        operation,
                        roadDefinitionId,
                        RoadCommandFailureReason.DuplicateCell,
                        0,
                        funds,
                        cells);
                }

                if (!_roadNetwork.IsInBounds(cells[index]))
                {
                    return RoadCommandResult.Rejected(
                        operation,
                        roadDefinitionId,
                        RoadCommandFailureReason.OutOfBounds,
                        0,
                        funds,
                        cells);
                }
            }

            if (!RoadPath.IsStraight(cells))
            {
                return RoadCommandResult.Rejected(
                    operation,
                    roadDefinitionId,
                    RoadCommandFailureReason.InvalidPath,
                    0,
                    funds,
                    cells);
            }

            var cost = 0;
            if (requiresDefinition)
            {
                var totalCost = (long)definition.BuildCost * cells.Count;
                if (totalCost > int.MaxValue)
                {
                    return RoadCommandResult.Rejected(
                        operation,
                        roadDefinitionId,
                        RoadCommandFailureReason.CostOverflow,
                        0,
                        funds,
                        cells);
                }

                cost = (int)totalCost;
            }

            for (var index = 0; index < cells.Count; index++)
            {
                var occupied = _roadNetwork.IsOccupied(cells[index]);
                if (operation == RoadCommandOperation.Place && occupied)
                {
                    return RoadCommandResult.Rejected(
                        operation,
                        roadDefinitionId,
                        RoadCommandFailureReason.RoadAlreadyExists,
                        cost,
                        funds,
                        cells);
                }

                if (operation == RoadCommandOperation.Demolish && !occupied)
                {
                    return RoadCommandResult.Rejected(
                        operation,
                        roadDefinitionId,
                        RoadCommandFailureReason.NoRoad,
                        0,
                        funds,
                        cells);
                }
            }

            if (requiresDefinition && !_funds.CanAfford(cost))
            {
                return RoadCommandResult.Rejected(
                    operation,
                    roadDefinitionId,
                    RoadCommandFailureReason.InsufficientFunds,
                    cost,
                    funds,
                    cells);
            }

            return RoadCommandResult.Preview(
                operation,
                roadDefinitionId,
                cost,
                funds,
                cells);
        }
    }
}
