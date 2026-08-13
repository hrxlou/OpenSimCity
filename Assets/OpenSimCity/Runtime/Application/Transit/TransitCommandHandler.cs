using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Transit
{
    /// <summary>
    /// Application command boundary for persistent transit state. Successful costs
    /// debit the shared municipal balance; the existing P3 ledger classifies these
    /// as infrastructure construction until the later service-ledger expansion.
    /// </summary>
    public sealed class TransitCommandHandler : ICityCommandHandler
    {
        private readonly TransitNetworkState _network;
        private readonly RoadNetworkState _roads;
        private readonly RoadConnectivityGraph _graph;
        private readonly DeterministicRoutePlanner _planner;
        private readonly IConstructionFunds _funds;
        private readonly MunicipalFinanceState _municipalFinance;
        private readonly Func<SimulationDate> _currentMonth;
        private readonly TransitModeChoiceParameters _parameters;

        public TransitCommandHandler(
            TransitNetworkState network,
            RoadNetworkState roads,
            RoadConnectivityGraph graph,
            RoadDefinitionCatalog roadDefinitions,
            IConstructionFunds funds,
            Func<SimulationDate> currentMonth,
            TransitModeChoiceParameters parameters = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _planner = new DeterministicRoutePlanner(
                roads,
                graph,
                roadDefinitions ?? throw new ArgumentNullException(nameof(roadDefinitions)));
            _funds = funds ?? throw new ArgumentNullException(nameof(funds));
            _municipalFinance = funds as MunicipalFinanceState;
            _currentMonth = currentMonth ?? throw new ArgumentNullException(nameof(currentMonth));
            _parameters = parameters ?? TransitModeChoiceParameters.Default;
        }

        public bool CanHandle(ICityCommand command) =>
            command is PlaceTransitStopCommand ||
            command is RemoveTransitStopCommand ||
            command is UpsertTransitLineCommand ||
            command is DeleteTransitLineCommand ||
            command is SetTransitBudgetCommand;

        public TransitCommandResult Preview(ICityCommand command)
        {
            if (command is PlaceTransitStopCommand place) return Evaluate(place);
            if (command is RemoveTransitStopCommand remove) return Evaluate(remove);
            if (command is UpsertTransitLineCommand upsert) return Evaluate(upsert);
            if (command is DeleteTransitLineCommand delete) return Evaluate(delete);
            if (command is SetTransitBudgetCommand budget) return Evaluate(budget);
            return TransitCommandResult.Rejected(
                TransitCommandOperation.PlaceStop,
                TransitCommandFailureReason.InvalidCommand,
                0,
                _funds.Funds);
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            if (!CanHandle(command.Command))
                return CommandValidationResult.Unsupported("No transit handler can execute this command.");
            var result = Preview(command.Command);
            return result.IsValid
                ? CommandValidationResult.Valid
                : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope envelope)
        {
            var preview = Preview(envelope.Command);
            if (!preview.IsValid)
                return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);

            var fundsBefore = _funds.Funds;
            if (preview.Cost > 0 && !_funds.TrySpend(preview.Cost))
            {
                var rejected = TransitCommandResult.Rejected(
                    preview.Operation,
                    TransitCommandFailureReason.InsufficientFunds,
                    preview.Cost,
                    fundsBefore,
                    preview.StopId,
                    preview.LineId);
                return CommandExecutionResult.Rejected(rejected.Message).WithDetails(rejected);
            }

            var applied = Apply(envelope.Command);
            if (!applied)
            {
                if (preview.Cost > 0 && !_funds.TryRefund(preview.Cost))
                    return CommandExecutionResult.Failed("Transit rollback could not refund municipal funds.");
                var rejected = TransitCommandResult.Rejected(
                    preview.Operation,
                    TransitCommandFailureReason.StateChangedBeforeCommit,
                    preview.Cost,
                    fundsBefore,
                    preview.StopId,
                    preview.LineId);
                return CommandExecutionResult.Rejected(rejected.Message).WithDetails(rejected);
            }

            if (preview.Cost > 0)
            {
                _network.RecordConstructionExpense(preview.Cost);
                _municipalFinance?.RecordConstructionExpense(
                    MunicipalConstructionCategory.Road,
                    preview.Cost);
            }
            var result = TransitCommandResult.Applied(
                preview.Operation,
                preview.Cost,
                fundsBefore,
                _funds.Funds,
                preview.StopId,
                preview.LineId);
            return CommandExecutionResult.Applied(result);
        }

        private TransitCommandResult Evaluate(PlaceTransitStopCommand command)
        {
            if (!command.StopId.IsValid)
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.InvalidCommand,
                    _parameters.StopConstructionCost, command.StopId);
            if (!_roads.IsInBounds(command.RoadCell))
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.OutOfBounds,
                    _parameters.StopConstructionCost, command.StopId);
            if (!_roads.HasRoad(command.RoadCell))
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.RoadRequired,
                    _parameters.StopConstructionCost, command.StopId);
            if (_network.TryGetStop(command.StopId, out _))
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.DuplicateStopId,
                    _parameters.StopConstructionCost, command.StopId);
            if (_network.TryFindStopAt(command.RoadCell, out _))
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.StopCellOccupied,
                    _parameters.StopConstructionCost, command.StopId);
            if (!_funds.CanAfford(_parameters.StopConstructionCost))
                return Reject(TransitCommandOperation.PlaceStop, TransitCommandFailureReason.InsufficientFunds,
                    _parameters.StopConstructionCost, command.StopId);
            return TransitCommandResult.Valid(
                TransitCommandOperation.PlaceStop,
                _parameters.StopConstructionCost,
                _funds.Funds,
                command.StopId);
        }

        private TransitCommandResult Evaluate(RemoveTransitStopCommand command)
        {
            if (!command.StopId.IsValid)
                return Reject(TransitCommandOperation.RemoveStop, TransitCommandFailureReason.InvalidCommand, 0, command.StopId);
            if (!_network.TryGetStop(command.StopId, out _))
                return Reject(TransitCommandOperation.RemoveStop, TransitCommandFailureReason.StopNotFound, 0, command.StopId);
            var lines = _network.Lines;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                for (var stopIndex = 0; stopIndex < lines[lineIndex].StopIds.Count; stopIndex++)
                {
                    if (lines[lineIndex].StopIds[stopIndex] == command.StopId)
                        return Reject(TransitCommandOperation.RemoveStop, TransitCommandFailureReason.StopInUse, 0, command.StopId);
                }
            }
            return TransitCommandResult.Valid(TransitCommandOperation.RemoveStop, 0, _funds.Funds, command.StopId);
        }

        private TransitCommandResult Evaluate(UpsertTransitLineCommand command)
        {
            if (!command.LineId.IsValid)
                return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.InvalidCommand, 0,
                    default(TransitStopId), command.LineId);
            if (command.ServiceUnits < 1)
                return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.InvalidServiceUnits, 0,
                    default(TransitStopId), command.LineId);
            if (command.StopIds.Count < 2)
                return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.InvalidStopOrder, 0,
                    default(TransitStopId), command.LineId);

            var seen = new HashSet<TransitStopId>();
            for (var index = 0; index < command.StopIds.Count; index++)
            {
                if (!command.StopIds[index].IsValid || !seen.Add(command.StopIds[index]))
                    return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.InvalidStopOrder, 0,
                        default(TransitStopId), command.LineId);
                if (!_network.TryGetStop(command.StopIds[index], out _))
                    return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.UnknownStop, 0,
                        command.StopIds[index], command.LineId);
            }

            _graph.RefreshIfNeeded();
            for (var index = 0; index < command.StopIds.Count - 1; index++)
            {
                _network.TryGetStop(command.StopIds[index], out var from);
                _network.TryGetStop(command.StopIds[index + 1], out var to);
                if (!_planner.GetRoutes(from.RoadCell, to.RoadCell).IsReachable)
                    return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.BrokenRoadPath, 0,
                        default(TransitStopId), command.LineId);
            }

            var cost = _network.TryGetLine(command.LineId, out _) ? 0 : _parameters.LineSetupCost;
            if (!_funds.CanAfford(cost))
                return Reject(TransitCommandOperation.UpsertLine, TransitCommandFailureReason.InsufficientFunds, cost,
                    default(TransitStopId), command.LineId);
            return TransitCommandResult.Valid(
                TransitCommandOperation.UpsertLine,
                cost,
                _funds.Funds,
                default(TransitStopId),
                command.LineId);
        }

        private TransitCommandResult Evaluate(DeleteTransitLineCommand command)
        {
            if (!command.LineId.IsValid)
                return Reject(TransitCommandOperation.DeleteLine, TransitCommandFailureReason.InvalidCommand, 0,
                    default(TransitStopId), command.LineId);
            return _network.TryGetLine(command.LineId, out _)
                ? TransitCommandResult.Valid(TransitCommandOperation.DeleteLine, 0, _funds.Funds,
                    default(TransitStopId), command.LineId)
                : Reject(TransitCommandOperation.DeleteLine, TransitCommandFailureReason.LineNotFound, 0,
                    default(TransitStopId), command.LineId);
        }

        private TransitCommandResult Evaluate(SetTransitBudgetCommand command)
        {
            if (command.BudgetPermille < 0 || command.BudgetPermille > 1000)
                return Reject(TransitCommandOperation.SetBudget, TransitCommandFailureReason.InvalidBudget, 0);
            return TransitCommandResult.Valid(TransitCommandOperation.SetBudget, 0, _funds.Funds);
        }

        private bool Apply(ICityCommand command)
        {
            if (command is PlaceTransitStopCommand place)
                return _network.TryAddStop(new TransitStopState(place.StopId, place.RoadCell, place.Name));
            if (command is RemoveTransitStopCommand remove)
                return _network.TryRemoveStop(remove.StopId);
            if (command is UpsertTransitLineCommand upsert)
            {
                return _network.TryUpsertLine(new TransitLineState(
                    upsert.LineId,
                    upsert.Name,
                    upsert.StopIds,
                    upsert.ServiceUnits,
                    upsert.IsEnabled));
            }
            if (command is DeleteTransitLineCommand delete)
                return _network.TryRemoveLine(delete.LineId);
            if (command is SetTransitBudgetCommand budget)
            {
                _network.ScheduleBudget(budget.BudgetPermille, _currentMonth());
                return true;
            }
            return false;
        }

        private TransitCommandResult Reject(
            TransitCommandOperation operation,
            TransitCommandFailureReason reason,
            int cost,
            TransitStopId stopId = default(TransitStopId),
            TransitLineId lineId = default(TransitLineId)) =>
            TransitCommandResult.Rejected(operation, reason, cost, _funds.Funds, stopId, lineId);
    }
}
