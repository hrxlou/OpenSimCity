using System;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.Commands
{
    public sealed class PowerCommandHandler : ICityCommandHandler
    {
        private readonly PowerProducerCollection _producers;
        private readonly UtilityDefinitionCatalog _definitions;
        private readonly RoadNetworkState _roads;
        public PowerCommandHandler(PowerProducerCollection producers, UtilityDefinitionCatalog definitions, RoadNetworkState roads)
        { _producers = producers ?? throw new ArgumentNullException(nameof(producers)); _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions)); _roads = roads ?? throw new ArgumentNullException(nameof(roads)); }
        public bool CanHandle(ICityCommand command) => command is PlacePowerProducerCommand || command is RemovePowerProducerCommand;

        public PowerCommandResult Preview(PlacePowerProducerCommand command)
        {
            if (command == null) return PowerCommandResult.Rejected(PowerCommandFailureReason.InvalidCommand, default, default);
            if (!command.ProducerId.IsValid) return PowerCommandResult.Rejected(PowerCommandFailureReason.InvalidId, command.ProducerId, command.Anchor);
            if (!_definitions.TryGet(command.DefinitionId, out _)) return PowerCommandResult.Rejected(PowerCommandFailureReason.UnknownDefinition, command.ProducerId, command.Anchor);
            if (!_roads.IsInBounds(command.Anchor)) return PowerCommandResult.Rejected(PowerCommandFailureReason.OutOfBounds, command.ProducerId, command.Anchor);
            if (_roads.IsOccupied(command.Anchor)) return PowerCommandResult.Rejected(PowerCommandFailureReason.RoadOccupiesCell, command.ProducerId, command.Anchor);
            if (_producers.TryGet(command.ProducerId, out _) || _producers.TryGetAt(command.Anchor, out _)) return PowerCommandResult.Rejected(PowerCommandFailureReason.ProducerOccupiesCell, command.ProducerId, command.Anchor);
            return PowerCommandResult.Valid(command.ProducerId, command.Anchor);
        }

        public PowerCommandResult Preview(RemovePowerProducerCommand command)
        {
            if (command == null) return PowerCommandResult.Rejected(PowerCommandFailureReason.InvalidCommand, default, default);
            if (!_producers.TryGet(command.ProducerId, out var producer)) return PowerCommandResult.Rejected(PowerCommandFailureReason.ProducerNotFound, command.ProducerId, default);
            return PowerCommandResult.Valid(command.ProducerId, producer.Anchor);
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            PowerCommandResult result;
            if (command.Command is PlacePowerProducerCommand place) result = Preview(place);
            else if (command.Command is RemovePowerProducerCommand remove) result = Preview(remove);
            else return CommandValidationResult.Unsupported("No power handler can execute this command.");
            return result.IsValid ? CommandValidationResult.Valid : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope envelope)
        {
            if (envelope.Command is PlacePowerProducerCommand place)
            {
                var preview = Preview(place);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                var definition = _definitions.Get(place.DefinitionId);
                _producers.Add(new PowerProducerState(place.ProducerId, place.DefinitionId, place.Anchor, definition.PowerCapacity));
                var result = PowerCommandResult.Applied(place.ProducerId, place.Anchor);
                return CommandExecutionResult.Applied(result);
            }
            if (envelope.Command is RemovePowerProducerCommand remove)
            {
                var preview = Preview(remove);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                if (!_producers.Remove(remove.ProducerId)) return CommandExecutionResult.Failed("Power producer removal failed.");
                return CommandExecutionResult.Applied(PowerCommandResult.Applied(remove.ProducerId, preview.Anchor));
            }
            return CommandExecutionResult.Unsupported("No power handler can execute this command.");
        }
    }
}
