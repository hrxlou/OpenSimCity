using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Simulation.Conditions;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Conditions
{
    public sealed class BuildingConditionCommandHandler : ICityCommandHandler
    {
        private readonly BuildingConditionSystem _conditions;
        private readonly IConstructionFunds _funds;
        private readonly MunicipalFinanceState _municipalFinance;
        private readonly int _redevelopmentCost;

        public BuildingConditionCommandHandler(
            BuildingConditionSystem conditions,
            IConstructionFunds funds,
            int redevelopmentCost = 500)
        {
            _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            _funds = funds ?? throw new ArgumentNullException(nameof(funds));
            _municipalFinance = funds as MunicipalFinanceState;
            if (redevelopmentCost < 0)
                throw new ArgumentOutOfRangeException(nameof(redevelopmentCost));
            _redevelopmentCost = redevelopmentCost;
        }

        public int RedevelopmentCost => _redevelopmentCost;

        public bool CanHandle(ICityCommand command) =>
            command is RequestBuildingRedevelopmentCommand;

        public BuildingConditionCommandResult Preview(RequestBuildingRedevelopmentCommand command)
        {
            if (command == null || !command.BuildingId.IsValid)
            {
                return BuildingConditionCommandResult.Rejected(
                    command?.BuildingId ?? default,
                    BuildingConditionCommandFailureReason.InvalidCommand,
                    _redevelopmentCost,
                    _funds.Funds);
            }
            if (!_conditions.State.TryGet(command.BuildingId, out var state))
            {
                return BuildingConditionCommandResult.Rejected(
                    command.BuildingId,
                    BuildingConditionCommandFailureReason.BuildingNotFound,
                    _redevelopmentCost,
                    _funds.Funds);
            }
            if (state.Stage == BuildingConditionStage.Redeveloping)
            {
                return BuildingConditionCommandResult.Rejected(
                    command.BuildingId,
                    BuildingConditionCommandFailureReason.AlreadyRedeveloping,
                    _redevelopmentCost,
                    _funds.Funds);
            }
            if (!state.IsRedevelopmentEligible || state.Stage != BuildingConditionStage.Abandoned)
            {
                return BuildingConditionCommandResult.Rejected(
                    command.BuildingId,
                    BuildingConditionCommandResult.FromBlock(state.RedevelopmentBlockReason),
                    _redevelopmentCost,
                    _funds.Funds);
            }
            if (_funds.Funds < _redevelopmentCost)
            {
                return BuildingConditionCommandResult.Rejected(
                    command.BuildingId,
                    BuildingConditionCommandFailureReason.InsufficientFunds,
                    _redevelopmentCost,
                    _funds.Funds);
            }
            return BuildingConditionCommandResult.Valid(
                command.BuildingId,
                _redevelopmentCost,
                _funds.Funds);
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            if (!(command.Command is RequestBuildingRedevelopmentCommand request))
                return CommandValidationResult.Unsupported("No condition handler can execute this command.");
            var preview = Preview(request);
            return preview.IsValid
                ? CommandValidationResult.Valid
                : CommandValidationResult.Invalid(preview.Message, preview);
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            if (!(command.Command is RequestBuildingRedevelopmentCommand request))
                return CommandExecutionResult.Unsupported("No condition handler can execute this command.");
            var preview = Preview(request);
            if (!preview.IsValid)
                return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);

            var fundsBefore = _funds.Funds;
            if (!_funds.TrySpend(preview.Cost))
            {
                var rejected = BuildingConditionCommandResult.Rejected(
                    request.BuildingId,
                    BuildingConditionCommandFailureReason.InsufficientFunds,
                    preview.Cost,
                    fundsBefore);
                return CommandExecutionResult.Rejected(rejected.Message).WithDetails(rejected);
            }

            if (!_conditions.TryRequestRedevelopment(request.BuildingId, out _))
            {
                if (!_funds.TryRefund(preview.Cost))
                    return CommandExecutionResult.Failed("Redevelopment rollback could not refund municipal funds.");
                var rejected = BuildingConditionCommandResult.Rejected(
                    request.BuildingId,
                    BuildingConditionCommandFailureReason.StateChangedBeforeCommit,
                    preview.Cost,
                    fundsBefore);
                return CommandExecutionResult.Rejected(rejected.Message).WithDetails(rejected);
            }

            // The P3 ledger currently has a generic infrastructure construction
            // bucket. Condition-specific expense remains explicit in this result and
            // can move to a dedicated ledger column without changing command order.
            _municipalFinance?.RecordConstructionExpense(
                MunicipalConstructionCategory.Road,
                preview.Cost);
            var result = BuildingConditionCommandResult.Applied(
                request.BuildingId,
                preview.Cost,
                fundsBefore,
                _funds.Funds);
            return CommandExecutionResult.Applied(result);
        }
    }
}
