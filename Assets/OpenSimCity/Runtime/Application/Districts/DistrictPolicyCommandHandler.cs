using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictPolicyCommandHandler : ICityCommandHandler
    {
        private readonly CityRuntime _runtime;
        private readonly DistrictRuntimeModule _districts;
        private readonly DistrictPolicyCollection _states;
        public DistrictPolicyCommandHandler(CityRuntime runtime, DistrictRuntimeModule districts, DistrictPolicyCollection states)
        { _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); _districts = districts ?? throw new ArgumentNullException(nameof(districts)); _states = states ?? throw new ArgumentNullException(nameof(states)); }

        public bool CanHandle(ICityCommand command) => command is SetDistrictPolicyCommand;
        public CommandValidationResult Validate(CommandEnvelope envelope)
        {
            if (!(envelope.Command is SetDistrictPolicyCommand command)) return CommandValidationResult.Unsupported("Unsupported district policy command.");
            if (!command.DistrictId.IsValid || !_districts.Districts.TryGet(command.DistrictId, out _)) return CommandValidationResult.Invalid("District was not found.");
            return CommandValidationResult.Valid;
        }
        public CommandExecutionResult Execute(CommandEnvelope envelope)
        {
            var validation = Validate(envelope);
            if (!validation.IsValid) return CommandExecutionResult.Rejected(validation.Message);
            var command = (SetDistrictPolicyCommand)envelope.Command;
            _states.Schedule(command.DistrictId, command.Kind, command.Enabled, _runtime.MonthlyScheduler.NextMonth);
            return CommandExecutionResult.Applied();
        }
    }
}
