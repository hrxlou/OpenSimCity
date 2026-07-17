using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Commands
{
    public sealed class PlacePowerProducerCommand : ICityCommand
    {
        public PlacePowerProducerCommand(StableId producerId, StableId definitionId, CellCoord anchor)
        { ProducerId = producerId; DefinitionId = definitionId; Anchor = anchor; }
        public StableId ProducerId { get; }
        public StableId DefinitionId { get; }
        public CellCoord Anchor { get; }
    }

    public sealed class RemovePowerProducerCommand : ICityCommand
    {
        public RemovePowerProducerCommand(StableId producerId) { ProducerId = producerId; }
        public StableId ProducerId { get; }
    }

    public enum PowerCommandFailureReason
    {
        None, InvalidCommand, InvalidId, UnknownDefinition, OutOfBounds,
        RoadOccupiesCell, ProducerOccupiesCell, ProducerNotFound, StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class PowerCommandResult
    {
        internal PowerCommandResult(bool isValid, bool isApplied, PowerCommandFailureReason reason,
            StableId producerId, CellCoord anchor, string message)
        { IsValid = isValid; IsApplied = isApplied; FailureReason = reason; ProducerId = producerId; Anchor = anchor; Message = message ?? string.Empty; }
        public bool IsValid { get; }
        public bool IsApplied { get; }
        public PowerCommandFailureReason FailureReason { get; }
        public StableId ProducerId { get; }
        public CellCoord Anchor { get; }
        public string Message { get; }
        internal static PowerCommandResult Valid(StableId id, CellCoord anchor) => new PowerCommandResult(true, false, PowerCommandFailureReason.None, id, anchor, string.Empty);
        internal static PowerCommandResult Applied(StableId id, CellCoord anchor) => new PowerCommandResult(true, true, PowerCommandFailureReason.None, id, anchor, string.Empty);
        internal static PowerCommandResult Rejected(PowerCommandFailureReason reason, StableId id, CellCoord anchor) =>
            new PowerCommandResult(false, false, reason, id, anchor, MessageFor(reason));
        private static string MessageFor(PowerCommandFailureReason reason)
        {
            switch (reason)
            {
                case PowerCommandFailureReason.InvalidCommand: return "The power command is invalid.";
                case PowerCommandFailureReason.InvalidId: return "A valid producer ID is required.";
                case PowerCommandFailureReason.UnknownDefinition: return "The utility definition is not registered.";
                case PowerCommandFailureReason.OutOfBounds: return "The power producer anchor is outside the world bounds.";
                case PowerCommandFailureReason.RoadOccupiesCell: return "A power producer cannot occupy a road cell.";
                case PowerCommandFailureReason.ProducerOccupiesCell: return "A power producer already occupies this cell.";
                case PowerCommandFailureReason.ProducerNotFound: return "The power producer does not exist.";
                default: return "The power producer state changed before commit.";
            }
        }
    }
}
