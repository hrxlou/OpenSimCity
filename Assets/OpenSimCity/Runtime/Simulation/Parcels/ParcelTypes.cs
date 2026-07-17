using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Simulation.Parcels
{
    public enum ParcelFrontageDirection : byte
    {
        North,
        East,
        South,
        West
    }

    public enum ParcelDevelopmentFailureReason : byte
    {
        None,
        NoRoadFrontage,
        NoExternalRoadConnection
    }

    [Serializable]
    public readonly struct ParcelFrontage : IEquatable<ParcelFrontage>
    {
        public ParcelFrontage(
            CellCoord parcelCell,
            CellCoord roadCell,
            ParcelFrontageDirection direction,
            int networkDistanceToExternalGate,
            StableId nearestExternalGateId,
            ExternalGateDistanceBand distanceBand,
            RoadUnreachableReason unreachableReason)
        {
            ParcelCell = parcelCell;
            RoadCell = roadCell;
            Direction = direction;
            NetworkDistanceToExternalGate = networkDistanceToExternalGate;
            NearestExternalGateId = nearestExternalGateId;
            DistanceBand = distanceBand;
            UnreachableReason = unreachableReason;
        }

        public CellCoord ParcelCell { get; }

        public CellCoord RoadCell { get; }

        public ParcelFrontageDirection Direction { get; }

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public ExternalGateDistanceBand DistanceBand { get; }

        public RoadUnreachableReason UnreachableReason { get; }

        public bool IsReachable => UnreachableReason == RoadUnreachableReason.None;

        public bool Equals(ParcelFrontage other)
        {
            return ParcelCell == other.ParcelCell &&
                   RoadCell == other.RoadCell &&
                   Direction == other.Direction &&
                   NetworkDistanceToExternalGate == other.NetworkDistanceToExternalGate &&
                   NearestExternalGateId == other.NearestExternalGateId &&
                   DistanceBand == other.DistanceBand &&
                   UnreachableReason == other.UnreachableReason;
        }

        public override bool Equals(object obj) => obj is ParcelFrontage other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ParcelCell.GetHashCode();
                hashCode = (hashCode * 397) ^ RoadCell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Direction;
                hashCode = (hashCode * 397) ^ NetworkDistanceToExternalGate;
                hashCode = (hashCode * 397) ^ NearestExternalGateId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)DistanceBand;
                return (hashCode * 397) ^ (int)UnreachableReason;
            }
        }

        public static bool operator ==(ParcelFrontage left, ParcelFrontage right) => left.Equals(right);

        public static bool operator !=(ParcelFrontage left, ParcelFrontage right) => !left.Equals(right);
    }

    /// <summary>
    /// Immutable simulation state for one simple rectangular parcel. Cells and
    /// frontages are stored in deterministic row-major order.
    /// </summary>
    [Serializable]
    public sealed class ParcelState : IEquatable<ParcelState>
    {
        private readonly CellCoord[] _cells;
        private readonly ParcelFrontage[] _frontages;
        private readonly IReadOnlyList<CellCoord> _readOnlyCells;
        private readonly IReadOnlyList<ParcelFrontage> _readOnlyFrontages;

        internal ParcelState(
            StableId id,
            ZoneType zoneType,
            CellCoord anchor,
            int width,
            int height,
            CellCoord[] cells,
            ParcelFrontage[] frontages,
            bool isDevelopmentEligible,
            ParcelDevelopmentFailureReason failureReason,
            int networkDistanceToExternalGate,
            StableId nearestExternalGateId,
            ExternalGateDistanceBand distanceBand)
        {
            Id = id;
            ZoneType = zoneType;
            Anchor = anchor;
            Width = width;
            Height = height;
            _cells = cells;
            _frontages = frontages;
            _readOnlyCells = Array.AsReadOnly(_cells);
            _readOnlyFrontages = Array.AsReadOnly(_frontages);
            IsDevelopmentEligible = isDevelopmentEligible;
            FailureReason = failureReason;
            NetworkDistanceToExternalGate = networkDistanceToExternalGate;
            NearestExternalGateId = nearestExternalGateId;
            DistanceBand = distanceBand;
        }

        public StableId Id { get; }

        public ZoneType ZoneType { get; }

        public CellCoord Anchor { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<CellCoord> Cells => _readOnlyCells;

        public IReadOnlyList<ParcelFrontage> Frontages => _readOnlyFrontages;

        public bool HasRoadFrontage => _frontages.Length != 0;

        public bool IsDevelopmentEligible { get; }

        public ParcelDevelopmentFailureReason FailureReason { get; }

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public ExternalGateDistanceBand DistanceBand { get; }

        public bool Equals(ParcelState other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other == null || Id != other.Id || ZoneType != other.ZoneType ||
                Anchor != other.Anchor || Width != other.Width || Height != other.Height ||
                IsDevelopmentEligible != other.IsDevelopmentEligible ||
                FailureReason != other.FailureReason ||
                NetworkDistanceToExternalGate != other.NetworkDistanceToExternalGate ||
                NearestExternalGateId != other.NearestExternalGateId ||
                DistanceBand != other.DistanceBand ||
                _cells.Length != other._cells.Length || _frontages.Length != other._frontages.Length)
            {
                return false;
            }

            for (var index = 0; index < _cells.Length; index++)
            {
                if (_cells[index] != other._cells[index])
                    return false;
            }

            for (var index = 0; index < _frontages.Length; index++)
            {
                if (_frontages[index] != other._frontages[index])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as ParcelState);

        public override int GetHashCode() => Id.GetHashCode();
    }
}
