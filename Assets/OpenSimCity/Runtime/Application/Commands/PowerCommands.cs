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
        RoadOccupiesCell, ProducerOccupiesCell, ProducerNotFound, InsufficientFunds, StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class PowerCommandResult
    {
        internal PowerCommandResult(bool isValid, bool isApplied, PowerCommandFailureReason reason,
            StableId producerId, CellCoord anchor, int cost, int fundsBefore, int fundsAfter, string message)
        {
            IsValid = isValid;
            IsApplied = isApplied;
            FailureReason = reason;
            ProducerId = producerId;
            Anchor = anchor;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            Message = message ?? string.Empty;
        }
        public bool IsValid { get; }
        public bool IsApplied { get; }
        public PowerCommandFailureReason FailureReason { get; }
        public StableId ProducerId { get; }
        public CellCoord Anchor { get; }
        public int Cost { get; }
        public int FundsBefore { get; }
        public int FundsAfter { get; }
        public string Message { get; }
        internal static PowerCommandResult Valid(StableId id, CellCoord anchor, int cost, int fundsBefore) =>
            new PowerCommandResult(true, false, PowerCommandFailureReason.None, id, anchor, cost, fundsBefore, fundsBefore - cost, string.Empty);
        internal static PowerCommandResult Applied(StableId id, CellCoord anchor, int cost, int fundsBefore, int fundsAfter) =>
            new PowerCommandResult(true, true, PowerCommandFailureReason.None, id, anchor, cost, fundsBefore, fundsAfter, string.Empty);
        internal static PowerCommandResult Rejected(PowerCommandFailureReason reason, StableId id, CellCoord anchor, int funds = 0, int cost = 0) =>
            new PowerCommandResult(false, false, reason, id, anchor, cost, funds, funds, MessageFor(reason));
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
                case PowerCommandFailureReason.InsufficientFunds: return "The city balance cannot cover this power producer.";
                default: return "The power producer state changed before commit.";
            }
        }
    }
}
