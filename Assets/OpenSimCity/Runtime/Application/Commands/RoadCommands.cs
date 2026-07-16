using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Commands
{
    public sealed class PlaceRoadCommand : ICityCommand
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        public PlaceRoadCommand(StableId roadDefinitionId, IEnumerable<CellCoord> cells)
        {
            RoadDefinitionId = roadDefinitionId;
            _cells = RoadCommandCells.Copy(cells);
        }

        public PlaceRoadCommand(StableId roadDefinitionId, CellCoord start, CellCoord end)
            : this(roadDefinitionId, StraightRoadPathCalculator.Calculate(start, end).Cells)
        {
        }

        public StableId RoadDefinitionId { get; }

        public StableId DefinitionId => RoadDefinitionId;

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<CellCoord> Path => _cells;

        public IReadOnlyList<CellCoord> RoadCells => _cells;

        public bool IsEmpty => _cells.Count == 0;

        public CellCoord Start => IsEmpty ? CellCoord.Zero : _cells[0];

        public CellCoord End => IsEmpty ? CellCoord.Zero : _cells[_cells.Count - 1];
    }

    public sealed class DemolishRoadCommand : ICityCommand
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        public DemolishRoadCommand(IEnumerable<CellCoord> cells)
        {
            _cells = RoadCommandCells.Copy(cells);
        }

        public DemolishRoadCommand(CellCoord start, CellCoord end)
            : this(StraightRoadPathCalculator.Calculate(start, end).Cells)
        {
        }

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<CellCoord> Path => _cells;

        public IReadOnlyList<CellCoord> RoadCells => _cells;

        public bool IsEmpty => _cells.Count == 0;

        public CellCoord Start => IsEmpty ? CellCoord.Zero : _cells[0];

        public CellCoord End => IsEmpty ? CellCoord.Zero : _cells[_cells.Count - 1];
    }

    public enum RoadCommandOperation
    {
        Place,
        Demolish
    }

    public enum RoadCommandResultStatus
    {
        Valid,
        Applied,
        Rejected
    }

    public enum RoadCommandFailureReason
    {
        None,
        InvalidCommand,
        InvalidDefinition,
        EmptyPath,
        InvalidPath,
        OutOfBounds,
        DuplicateCell,
        RoadAlreadyExists,
        NoRoad,
        CostOverflow,
        InsufficientFunds,
        StateChangedBeforeCommit
    }

    /// <summary>
    /// Shared preview/commit DTO. A valid preview and its subsequent commit carry
    /// the same cost, reason-free cell set, and requested path. Only the commit
    /// changes status to Applied and deducts funds.
    /// </summary>
    [Serializable]
    public sealed class RoadCommandResult
    {
        private RoadCommandResult(
            RoadCommandOperation operation,
            RoadCommandResultStatus status,
            RoadCommandFailureReason failureReason,
            StableId roadDefinitionId,
            int cost,
            int fundsBefore,
            int fundsAfter,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> changedCells,
            string message)
        {
            Operation = operation;
            Status = status;
            FailureReason = failureReason;
            RoadDefinitionId = roadDefinitionId;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            RequestedCells = RoadCommandCells.Copy(requestedCells);
            ChangedCells = RoadCommandCells.Copy(changedCells);
            Message = message ?? string.Empty;
        }

        public RoadCommandOperation Operation { get; }

        public RoadCommandResultStatus Status { get; }

        public RoadCommandFailureReason FailureReason { get; }

        public bool IsValid => FailureReason == RoadCommandFailureReason.None;

        public bool IsApplied => Status == RoadCommandResultStatus.Applied;

        public bool StateChanged => IsApplied;

        public StableId RoadDefinitionId { get; }

        public int Cost { get; }

        public int FundsBefore { get; }

        public int FundsAfter { get; }

        public IReadOnlyList<CellCoord> RequestedCells { get; }

        public IReadOnlyList<CellCoord> ChangedCells { get; }

        public IReadOnlyList<CellCoord> AffectedCells => ChangedCells;

        public string Message { get; }

        internal static RoadCommandResult Preview(
            RoadCommandOperation operation,
            StableId roadDefinitionId,
            int cost,
            int funds,
            IReadOnlyList<CellCoord> requestedCells)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Valid,
                RoadCommandFailureReason.None,
                roadDefinitionId,
                cost,
                funds,
                funds,
                requestedCells,
                requestedCells,
                string.Empty);
        }

        internal static RoadCommandResult Applied(
            RoadCommandOperation operation,
            StableId roadDefinitionId,
            int cost,
            int fundsBefore,
            int fundsAfter,
            IReadOnlyList<CellCoord> requestedCells)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Applied,
                RoadCommandFailureReason.None,
                roadDefinitionId,
                cost,
                fundsBefore,
                fundsAfter,
                requestedCells,
                requestedCells,
                string.Empty);
        }

        internal static RoadCommandResult Rejected(
            RoadCommandOperation operation,
            StableId roadDefinitionId,
            RoadCommandFailureReason reason,
            int cost,
            int funds,
            IReadOnlyList<CellCoord> requestedCells)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Rejected,
                reason,
                roadDefinitionId,
                cost,
                funds,
                funds,
                requestedCells,
                Array.Empty<CellCoord>(),
                RoadCommandMessages.For(reason));
        }
    }

    internal static class RoadCommandCells
    {
        public static IReadOnlyList<CellCoord> Copy(IEnumerable<CellCoord> cells)
        {
            var copy = new List<CellCoord>();
            if (cells != null)
            {
                foreach (var cell in cells)
                    copy.Add(cell);
            }

            return copy.AsReadOnly();
        }
    }

    internal static class RoadCommandMessages
    {
        public static string For(RoadCommandFailureReason reason)
        {
            switch (reason)
            {
                case RoadCommandFailureReason.InvalidCommand:
                    return "The road command is null or unsupported.";
                case RoadCommandFailureReason.InvalidDefinition:
                    return "The road definition is missing or not registered.";
                case RoadCommandFailureReason.EmptyPath:
                    return "A road command must contain at least one cell.";
                case RoadCommandFailureReason.InvalidPath:
                    return "A road path must be a contiguous horizontal or vertical line.";
                case RoadCommandFailureReason.OutOfBounds:
                    return "At least one road cell is outside the world bounds.";
                case RoadCommandFailureReason.DuplicateCell:
                    return "A road command cannot contain the same cell more than once.";
                case RoadCommandFailureReason.RoadAlreadyExists:
                    return "At least one selected cell already contains a road.";
                case RoadCommandFailureReason.NoRoad:
                    return "At least one selected cell does not contain a road.";
                case RoadCommandFailureReason.CostOverflow:
                    return "The road cost is too large to represent safely.";
                case RoadCommandFailureReason.InsufficientFunds:
                    return "The city does not have enough funds for this road.";
                case RoadCommandFailureReason.StateChangedBeforeCommit:
                    return "The road state changed before the command could be committed.";
                default:
                    return string.Empty;
            }
        }
    }
}
