using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Roads
{
    [Flags]
    public enum RoadConnectionMask : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public enum ExternalGateDistanceBand : byte
    {
        Unreachable,
        Gate,
        Near,
        Medium,
        Far
    }

    public enum RoadUnreachableReason : byte
    {
        None,
        NoRoad,
        NoEnabledExternalRoadGate,
        DisconnectedFromExternalRoadGate
    }

    /// <summary>
    /// Inclusive row-major cell bounds changed by the last graph rebuild.
    /// An empty region has no coordinates and is returned when no cell-level
    /// query value changed.
    /// </summary>
    [Serializable]
    public readonly struct RoadGraphDirtyRegion : IEquatable<RoadGraphDirtyRegion>
    {
        private RoadGraphDirtyRegion(
            bool hasValue,
            CellCoord minimum,
            CellCoord maximum)
        {
            HasValue = hasValue;
            Minimum = minimum;
            Maximum = maximum;
        }

        public static RoadGraphDirtyRegion Empty => default;

        public bool HasValue { get; }

        public CellCoord Minimum { get; }

        public CellCoord Maximum { get; }

        public int Width => HasValue ? checked(Maximum.X - Minimum.X + 1) : 0;

        public int Height => HasValue ? checked(Maximum.Y - Minimum.Y + 1) : 0;

        public bool Contains(CellCoord coordinate)
        {
            return HasValue &&
                   coordinate.X >= Minimum.X && coordinate.X <= Maximum.X &&
                   coordinate.Y >= Minimum.Y && coordinate.Y <= Maximum.Y;
        }

        internal static RoadGraphDirtyRegion FromSortedCells(CellCoord[] cells)
        {
            if (cells == null || cells.Length == 0)
                return Empty;

            var minimumX = cells[0].X;
            var maximumX = cells[0].X;
            var minimumY = cells[0].Y;
            var maximumY = cells[0].Y;

            for (var index = 1; index < cells.Length; index++)
            {
                var cell = cells[index];
                minimumX = Math.Min(minimumX, cell.X);
                maximumX = Math.Max(maximumX, cell.X);
                minimumY = Math.Min(minimumY, cell.Y);
                maximumY = Math.Max(maximumY, cell.Y);
            }

            return new RoadGraphDirtyRegion(
                true,
                new CellCoord(minimumX, minimumY),
                new CellCoord(maximumX, maximumY));
        }

        public bool Equals(RoadGraphDirtyRegion other)
        {
            return HasValue == other.HasValue &&
                   Minimum == other.Minimum &&
                   Maximum == other.Maximum;
        }

        public override bool Equals(object obj)
        {
            return obj is RoadGraphDirtyRegion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((HasValue.GetHashCode() * 397) ^ Minimum.GetHashCode()) * 397 ^ Maximum.GetHashCode();
            }
        }

        public static bool operator ==(RoadGraphDirtyRegion left, RoadGraphDirtyRegion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoadGraphDirtyRegion left, RoadGraphDirtyRegion right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Public, Unity-independent road accessibility query result. ComponentId
    /// is a rebuildable graph-local label and must not be persisted as an
    /// entity identifier.
    /// </summary>
    [Serializable]
    public readonly struct RoadConnectivityResult : IEquatable<RoadConnectivityResult>
    {
        public RoadConnectivityResult(
            CellCoord cell,
            RoadConnectionMask connectionMask,
            int componentId,
            int networkDistanceToExternalGate,
            StableId nearestExternalGateId,
            ExternalGateDistanceBand distanceBand,
            RoadUnreachableReason unreachableReason)
        {
            Cell = cell;
            ConnectionMask = connectionMask;
            ComponentId = componentId;
            NetworkDistanceToExternalGate = networkDistanceToExternalGate;
            NearestExternalGateId = nearestExternalGateId;
            DistanceBand = distanceBand;
            UnreachableReason = unreachableReason;
        }

        public CellCoord Cell { get; }

        public RoadConnectionMask ConnectionMask { get; }

        public int ComponentId { get; }

        public bool HasRoad => ComponentId >= 0;

        public bool IsReachable => UnreachableReason == RoadUnreachableReason.None;

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public ExternalGateDistanceBand DistanceBand { get; }

        public RoadUnreachableReason UnreachableReason { get; }

        public bool Equals(RoadConnectivityResult other)
        {
            return Cell == other.Cell &&
                   ConnectionMask == other.ConnectionMask &&
                   ComponentId == other.ComponentId &&
                   NetworkDistanceToExternalGate == other.NetworkDistanceToExternalGate &&
                   NearestExternalGateId == other.NearestExternalGateId &&
                   DistanceBand == other.DistanceBand &&
                   UnreachableReason == other.UnreachableReason;
        }

        public override bool Equals(object obj)
        {
            return obj is RoadConnectivityResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Cell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)ConnectionMask;
                hashCode = (hashCode * 397) ^ ComponentId;
                hashCode = (hashCode * 397) ^ NetworkDistanceToExternalGate;
                hashCode = (hashCode * 397) ^ NearestExternalGateId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)DistanceBand;
                return (hashCode * 397) ^ (int)UnreachableReason;
            }
        }

        public static bool operator ==(RoadConnectivityResult left, RoadConnectivityResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoadConnectivityResult left, RoadConnectivityResult right)
        {
            return !left.Equals(right);
        }
    }
}
