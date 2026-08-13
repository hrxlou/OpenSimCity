using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Simulation.Spatial;

namespace OpenSimCity.Application.Spatial
{
    public sealed class SpatialFieldCommandHandler : ICityCommandHandler
    {
        private readonly SpatialFieldSystem _fields;

        public SpatialFieldCommandHandler(SpatialFieldSystem fields)
        {
            _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public bool CanHandle(ICityCommand command) =>
            command is UpsertSpatialSourceCommand || command is RemoveSpatialSourceCommand;

        public SpatialFieldCommandResult Preview(ICityCommand command)
        {
            if (command is UpsertSpatialSourceCommand upsert)
                return Evaluate(upsert);
            if (command is RemoveSpatialSourceCommand remove)
                return Evaluate(remove);
            return SpatialFieldCommandResult.Rejected(
                SpatialFieldCommandOperation.Upsert,
                SpatialFieldCommandFailureReason.InvalidCommand,
                default,
                default,
                default);
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            if (!CanHandle(command.Command))
                return CommandValidationResult.Unsupported("No spatial-field handler can execute this command.");
            var result = Preview(command.Command);
            return result.IsValid
                ? CommandValidationResult.Valid
                : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            var preview = Preview(command.Command);
            if (!preview.IsValid)
                return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);

            bool changed;
            if (command.Command is UpsertSpatialSourceCommand upsert)
            {
                changed = _fields.UpsertSource(new SpatialFieldSourceState(
                    upsert.SourceId,
                    upsert.DefinitionId,
                    upsert.Cell,
                    upsert.IsEnabled));
            }
            else if (command.Command is RemoveSpatialSourceCommand remove)
            {
                changed = _fields.RemoveSource(remove.SourceId);
            }
            else
            {
                return CommandExecutionResult.Unsupported("No spatial-field handler can execute this command.");
            }

            if (!changed)
            {
                var rejected = SpatialFieldCommandResult.Rejected(
                    preview.Operation,
                    SpatialFieldCommandFailureReason.StateChangedBeforeCommit,
                    preview.SourceId,
                    preview.DefinitionId,
                    preview.Cell);
                return CommandExecutionResult.Rejected(rejected.Message).WithDetails(rejected);
            }

            _fields.RefreshDirty();
            var result = SpatialFieldCommandResult.Applied(
                preview.Operation,
                preview.SourceId,
                preview.DefinitionId,
                preview.Cell);
            return CommandExecutionResult.Applied(result);
        }

        private SpatialFieldCommandResult Evaluate(UpsertSpatialSourceCommand command)
        {
            if (!command.SourceId.IsValid || !command.DefinitionId.IsValid)
                return Reject(SpatialFieldCommandOperation.Upsert,
                    SpatialFieldCommandFailureReason.InvalidCommand,
                    command.SourceId, command.DefinitionId, command.Cell);
            if (SpatialDerivedSourceIds.IsDerived(command.SourceId))
                return Reject(SpatialFieldCommandOperation.Upsert,
                    SpatialFieldCommandFailureReason.ReservedDerivedId,
                    command.SourceId, command.DefinitionId, command.Cell);
            if (!_fields.Definitions.TryGet(command.DefinitionId, out _))
                return Reject(SpatialFieldCommandOperation.Upsert,
                    SpatialFieldCommandFailureReason.UnknownDefinition,
                    command.SourceId, command.DefinitionId, command.Cell);
            if (!_fields.World.IsInBounds(command.Cell))
                return Reject(SpatialFieldCommandOperation.Upsert,
                    SpatialFieldCommandFailureReason.OutOfBounds,
                    command.SourceId, command.DefinitionId, command.Cell);
            return SpatialFieldCommandResult.Valid(
                SpatialFieldCommandOperation.Upsert,
                command.SourceId,
                command.DefinitionId,
                command.Cell);
        }

        private SpatialFieldCommandResult Evaluate(RemoveSpatialSourceCommand command)
        {
            if (!command.SourceId.IsValid)
                return Reject(SpatialFieldCommandOperation.Remove,
                    SpatialFieldCommandFailureReason.InvalidCommand,
                    command.SourceId, default, default);
            if (SpatialDerivedSourceIds.IsDerived(command.SourceId))
                return Reject(SpatialFieldCommandOperation.Remove,
                    SpatialFieldCommandFailureReason.ReservedDerivedId,
                    command.SourceId, default, default);
            if (!_fields.TryGetSource(command.SourceId, out var source))
                return Reject(SpatialFieldCommandOperation.Remove,
                    SpatialFieldCommandFailureReason.SourceNotFound,
                    command.SourceId, default, default);
            return SpatialFieldCommandResult.Valid(
                SpatialFieldCommandOperation.Remove,
                command.SourceId,
                source.DefinitionId,
                source.Cell);
        }

        private static SpatialFieldCommandResult Reject(
            SpatialFieldCommandOperation operation,
            SpatialFieldCommandFailureReason reason,
            OpenSimCity.Core.StableId sourceId,
            OpenSimCity.Core.StableId definitionId,
            OpenSimCity.Core.Mathematics.CellCoord cell) =>
            SpatialFieldCommandResult.Rejected(operation, reason, sourceId, definitionId, cell);
    }
}
