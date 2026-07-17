using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Commands
{
    public class ZoneAreaCommand : ICityCommand
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        public ZoneAreaCommand(ZoneType zoneType, IEnumerable<CellCoord> cells)
            : this(zoneType, ZoneDefinitionIds.For(zoneType), cells)
        {
        }

        public ZoneAreaCommand(ZoneType zoneType, ZoneBrush brush)
            : this(zoneType, ZoneDefinitionIds.For(zoneType), brush == null ? null : brush.Cells)
        {
        }

        public ZoneAreaCommand(ZoneType zoneType, CellCoord start, CellCoord end)
            : this(zoneType, ZoneBrush.Area(start, end))
        {
        }

        public ZoneAreaCommand(StableId definitionId, IEnumerable<CellCoord> cells)
            : this(ZoneType.None, definitionId, cells)
        {
        }

        public ZoneAreaCommand(StableId definitionId, ZoneBrush brush)
            : this(ZoneType.None, definitionId, brush == null ? null : brush.Cells)
        {
        }

        public ZoneAreaCommand(StableId definitionId, CellCoord start, CellCoord end)
            : this(definitionId, ZoneBrush.Area(start, end))
        {
        }

        public ZoneAreaCommand(
            ZoneType zoneType,
            StableId definitionId,
            IEnumerable<CellCoord> cells)
        {
            ZoneType = zoneType;
            DefinitionId = definitionId;
            _cells = ZoneCommandCells.Normalize(cells);
        }

        public ZoneAreaCommand(ZoneDefinition definition, IEnumerable<CellCoord> cells)
            : this(
                definition == null ? ZoneType.None : definition.ZoneType,
                definition == null ? StableId.Invalid : definition.Id,
                cells)
        {
        }

        public ZoneType ZoneType { get; }

        public ZoneType Type => ZoneType;

        public StableId DefinitionId { get; }

        public StableId ZoneDefinitionId => DefinitionId;

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<CellCoord> BrushCells => _cells;

        public IReadOnlyList<CellCoord> RequestedCells => _cells;

        public int Count => _cells.Count;

        public bool IsEmpty => _cells.Count == 0;

        public CellCoord Start => IsEmpty ? CellCoord.Zero : _cells[0];

        public CellCoord End => IsEmpty ? CellCoord.Zero : _cells[_cells.Count - 1];

        public static ZoneAreaCommand FromPath(ZoneType zoneType, CellCoord start, CellCoord end)
        {
            return new ZoneAreaCommand(zoneType, ZoneBrush.Path(start, end));
        }

        public static ZoneAreaCommand FromArea(ZoneType zoneType, CellCoord start, CellCoord end)
        {
            return new ZoneAreaCommand(zoneType, ZoneBrush.Area(start, end));
        }
    }

    /// <summary>
    /// Explicit path alias for input adapters that expose a line brush.
    /// </summary>
    public sealed class ZonePathCommand : ZoneAreaCommand
    {
        public ZonePathCommand(ZoneType zoneType, CellCoord start, CellCoord end)
            : base(zoneType, ZoneBrush.Path(start, end))
        {
        }

        public ZonePathCommand(ZoneType zoneType, IEnumerable<CellCoord> cells)
            : base(zoneType, cells)
        {
        }
    }

    public class ClearZoneCommand : ICityCommand
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        public ClearZoneCommand(IEnumerable<CellCoord> cells)
        {
            _cells = ZoneCommandCells.Normalize(cells);
        }

        public ClearZoneCommand(ZoneBrush brush)
            : this(brush == null ? null : brush.Cells)
        {
        }

        public ClearZoneCommand(CellCoord start, CellCoord end)
            : this(ZoneBrush.Area(start, end))
        {
        }

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<CellCoord> BrushCells => _cells;

        public IReadOnlyList<CellCoord> RequestedCells => _cells;

        public int Count => _cells.Count;

        public bool IsEmpty => _cells.Count == 0;

        public CellCoord Start => IsEmpty ? CellCoord.Zero : _cells[0];

        public CellCoord End => IsEmpty ? CellCoord.Zero : _cells[_cells.Count - 1];

        public static ClearZoneCommand FromPath(CellCoord start, CellCoord end)
        {
            return new ClearZoneCommand(ZoneBrush.Path(start, end));
        }

        public static ClearZoneCommand FromArea(CellCoord start, CellCoord end)
        {
            return new ClearZoneCommand(ZoneBrush.Area(start, end));
        }
    }

    public sealed class ClearZoneAreaCommand : ClearZoneCommand
    {
        public ClearZoneAreaCommand(CellCoord start, CellCoord end)
            : base(ZoneBrush.Area(start, end))
        {
        }

        public ClearZoneAreaCommand(IEnumerable<CellCoord> cells)
            : base(cells)
        {
        }
    }

    public enum ZoneCommandOperation
    {
        Paint,
        Zone = Paint,
        Clear,
        Unzone = Clear
    }

    public enum ZoneCommandResultStatus
    {
        Valid,
        Applied,
        Rejected
    }

    public enum ZoneCommandFailureReason
    {
        None,
        InvalidCommand,
        InvalidDefinition,
        InvalidZoneType,
        EmptyBrush,
        InvalidBrush = EmptyBrush,
        EmptyArea = EmptyBrush,
        OutOfBounds,
        RoadOccupied,
        RoadCell = RoadOccupied,
        RoadOverlap = RoadOccupied,
        DifferentZone,
        ZoneAlreadyExists = DifferentZone,
        ExistingZone = DifferentZone,
        ZoneOverlap = DifferentZone,
        NoZone,
        StateChangedBeforeCommit
    }

    [Serializable]
    public readonly struct ZoneCellRejection : IEquatable<ZoneCellRejection>
    {
        public ZoneCellRejection(CellCoord coordinate, ZoneCommandFailureReason reason)
        {
            Coordinate = coordinate;
            Reason = reason;
        }

        public CellCoord Coordinate { get; }

        public CellCoord Cell => Coordinate;

        public ZoneCommandFailureReason Reason { get; }

        public ZoneCommandFailureReason FailureReason => Reason;

        public bool Equals(ZoneCellRejection other)
        {
            return Coordinate == other.Coordinate && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            return obj is ZoneCellRejection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Coordinate.GetHashCode() * 397 ^ (int)Reason;
            }
        }

        public static bool operator ==(ZoneCellRejection left, ZoneCellRejection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ZoneCellRejection left, ZoneCellRejection right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Preview/commit read model for zoning. Zone commands are free in P2-03,
    /// so cost and balance remain zero and no municipal finance state is owned.
    /// </summary>
    [Serializable]
    public sealed class ZoneCommandResult
    {
        private ZoneCommandResult(
            ZoneCommandOperation operation,
            ZoneCommandResultStatus status,
            ZoneCommandFailureReason failureReason,
            ZoneType zoneType,
            StableId definitionId,
            int cost,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> changedCells,
            IReadOnlyList<CellCoord> rejectedCells,
            string message)
        {
            Operation = operation;
            Status = status;
            FailureReason = failureReason;
            ZoneType = zoneType;
            DefinitionId = definitionId;
            Cost = cost;
            FundsBefore = 0;
            FundsAfter = 0;
            RequestedCells = ZoneCommandCells.Copy(requestedCells);
            ChangedCells = ZoneCommandCells.Copy(changedCells);
            RejectedCells = ZoneCommandCells.Copy(rejectedCells);

            var rejections = new List<ZoneCellRejection>();
            if (rejectedCells != null && failureReason != ZoneCommandFailureReason.None)
            {
                for (var index = 0; index < rejectedCells.Count; index++)
                    rejections.Add(new ZoneCellRejection(rejectedCells[index], failureReason));
            }

            Rejections = rejections.AsReadOnly();
            Message = message ?? string.Empty;
        }

        public ZoneCommandOperation Operation { get; }

        public ZoneCommandResultStatus Status { get; }

        public ZoneCommandFailureReason FailureReason { get; }

        public bool IsValid => FailureReason == ZoneCommandFailureReason.None;

        public bool IsApplied => Status == ZoneCommandResultStatus.Applied;

        public bool StateChanged => IsApplied && ChangedCells.Count != 0;

        public ZoneType ZoneType { get; }

        public ZoneType Type => ZoneType;

        public StableId DefinitionId { get; }

        public StableId ZoneDefinitionId => DefinitionId;

        public int Cost { get; }

        public int FundsBefore { get; }

        public int FundsAfter { get; }

        public IReadOnlyList<CellCoord> RequestedCells { get; }

        public IReadOnlyList<CellCoord> ChangedCells { get; }

        public IReadOnlyList<CellCoord> AffectedCells => ChangedCells;

        public IReadOnlyList<CellCoord> AppliedCells => ChangedCells;

        public IReadOnlyList<CellCoord> RejectedCells { get; }

        public IReadOnlyList<ZoneCellRejection> Rejections { get; }

        public IReadOnlyList<ZoneCellRejection> RejectedCellDetails => Rejections;

        public string Message { get; }

        internal static ZoneCommandResult Preview(
            ZoneCommandOperation operation,
            ZoneType zoneType,
            StableId definitionId,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> changedCells)
        {
            return new ZoneCommandResult(
                operation,
                ZoneCommandResultStatus.Valid,
                ZoneCommandFailureReason.None,
                zoneType,
                definitionId,
                0,
                requestedCells,
                changedCells,
                Array.Empty<CellCoord>(),
                string.Empty);
        }

        internal static ZoneCommandResult Applied(
            ZoneCommandOperation operation,
            ZoneType zoneType,
            StableId definitionId,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> changedCells)
        {
            return new ZoneCommandResult(
                operation,
                ZoneCommandResultStatus.Applied,
                ZoneCommandFailureReason.None,
                zoneType,
                definitionId,
                0,
                requestedCells,
                changedCells,
                Array.Empty<CellCoord>(),
                string.Empty);
        }

        internal static ZoneCommandResult Rejected(
            ZoneCommandOperation operation,
            ZoneType zoneType,
            StableId definitionId,
            ZoneCommandFailureReason reason,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> rejectedCells)
        {
            return new ZoneCommandResult(
                operation,
                ZoneCommandResultStatus.Rejected,
                reason,
                zoneType,
                definitionId,
                0,
                requestedCells,
                Array.Empty<CellCoord>(),
                rejectedCells,
                ZoneCommandMessages.For(reason));
        }
    }

    internal static class ZoneCommandCells
    {
        public static IReadOnlyList<CellCoord> Normalize(IEnumerable<CellCoord> cells)
        {
            return ZoneBrushCalculator.Normalize(cells);
        }

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

    internal static class ZoneCommandMessages
    {
        public static string For(ZoneCommandFailureReason reason)
        {
            switch (reason)
            {
                case ZoneCommandFailureReason.InvalidCommand:
                    return "The zone command is null or unsupported.";
                case ZoneCommandFailureReason.InvalidDefinition:
                    return "The zone definition is missing or not registered.";
                case ZoneCommandFailureReason.InvalidZoneType:
                    return "The command must select a low-density residential, commercial, or industrial zone.";
                case ZoneCommandFailureReason.EmptyBrush:
                    return "A zone command must contain at least one cell.";
                case ZoneCommandFailureReason.OutOfBounds:
                    return "At least one selected zone cell is outside the world bounds.";
                case ZoneCommandFailureReason.RoadOccupied:
                    return "A zone cannot be painted on an occupied road cell.";
                case ZoneCommandFailureReason.DifferentZone:
                    return "A zone command cannot overwrite another zone type.";
                case ZoneCommandFailureReason.NoZone:
                    return "At least one selected cell does not contain a zone.";
                case ZoneCommandFailureReason.StateChangedBeforeCommit:
                    return "The zone state changed before the command could be committed.";
                default:
                    return string.Empty;
            }
        }
    }
}
