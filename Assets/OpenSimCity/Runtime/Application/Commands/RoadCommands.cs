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

    public enum RoadDirectionMode : byte
    {
        Bidirectional = 0,
        Forward = 1,
        Reverse = 2
    }

    /// <summary>
    /// Replaces the static road definition and/or travel direction on an existing
    /// straight path. Occupancy is never removed and no demolition command is
    /// synthesized internally.
    /// </summary>
    public sealed class UpgradeRoadCommand : ICityCommand
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        public UpgradeRoadCommand(
            StableId targetRoadDefinitionId,
            RoadDirectionMode directionMode,
            IEnumerable<CellCoord> cells)
        {
            TargetRoadDefinitionId = targetRoadDefinitionId;
            DirectionMode = directionMode;
            _cells = RoadCommandCells.Copy(cells);
        }

        public UpgradeRoadCommand(
            StableId targetRoadDefinitionId,
            RoadDirectionMode directionMode,
            CellCoord start,
            CellCoord end)
            : this(
                targetRoadDefinitionId,
                directionMode,
                StraightRoadPathCalculator.Calculate(start, end).Cells)
        {
        }

        public StableId TargetRoadDefinitionId { get; }
        public StableId RoadDefinitionId => TargetRoadDefinitionId;
        public RoadDirectionMode DirectionMode { get; }
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
        Demolish,
        Upgrade
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
        InvalidDirection,
        DirectionRequiresPath,
        NoChange,
        CostOverflow,
        InsufficientFunds,
        StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class RoadCommandResult
    {
        private RoadCommandResult(
            RoadCommandOperation operation,
            RoadCommandResultStatus status,
            RoadCommandFailureReason failureReason,
            StableId roadDefinitionId,
            RoadDirectionMode directionMode,
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
            DirectionMode = directionMode;
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
        public RoadDirectionMode DirectionMode { get; }
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
            IReadOnlyList<CellCoord> requestedCells,
            RoadDirectionMode directionMode = RoadDirectionMode.Bidirectional)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Valid,
                RoadCommandFailureReason.None,
                roadDefinitionId,
                directionMode,
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
            IReadOnlyList<CellCoord> requestedCells,
            RoadDirectionMode directionMode = RoadDirectionMode.Bidirectional)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Applied,
                RoadCommandFailureReason.None,
                roadDefinitionId,
                directionMode,
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
            IReadOnlyList<CellCoord> requestedCells,
            RoadDirectionMode directionMode = RoadDirectionMode.Bidirectional)
        {
            return new RoadCommandResult(
                operation,
                RoadCommandResultStatus.Rejected,
                reason,
                roadDefinitionId,
                directionMode,
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
                case RoadCommandFailureReason.InvalidDirection:
                    return "The requested road direction is not supported.";
                case RoadCommandFailureReason.DirectionRequiresPath:
                    return "A one-way direction requires at least two distinct road cells.";
                case RoadCommandFailureReason.NoChange:
                    return "The selected roads already use the requested definition and direction.";
                case RoadCommandFailureReason.CostOverflow:
                    return "The road cost is too large to represent safely.";
                case RoadCommandFailureReason.InsufficientFunds:
                    return "The city does not have enough funds for this road change.";
                case RoadCommandFailureReason.StateChangedBeforeCommit:
                    return "The road state changed before the command could be committed.";
                default:
                    return string.Empty;
            }
        }
    }
}
