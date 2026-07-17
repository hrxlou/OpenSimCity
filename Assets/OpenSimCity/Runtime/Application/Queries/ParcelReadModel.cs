using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Queries
{
    public readonly struct ParcelFrontageReadModel : IEquatable<ParcelFrontageReadModel>
    {
        internal ParcelFrontageReadModel(ParcelFrontage frontage)
        {
            ParcelCell = frontage.ParcelCell;
            RoadCell = frontage.RoadCell;
            Direction = frontage.Direction;
            IsReachable = frontage.IsReachable;
            NetworkDistanceToExternalGate = frontage.NetworkDistanceToExternalGate;
            NearestExternalGateId = frontage.NearestExternalGateId;
            DistanceBand = frontage.DistanceBand;
        }

        public CellCoord ParcelCell { get; }

        public CellCoord RoadCell { get; }

        public ParcelFrontageDirection Direction { get; }

        public bool IsReachable { get; }

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public ExternalGateDistanceBand DistanceBand { get; }

        public bool Equals(ParcelFrontageReadModel other)
        {
            return ParcelCell == other.ParcelCell && RoadCell == other.RoadCell &&
                   Direction == other.Direction && IsReachable == other.IsReachable &&
                   NetworkDistanceToExternalGate == other.NetworkDistanceToExternalGate &&
                   NearestExternalGateId == other.NearestExternalGateId &&
                   DistanceBand == other.DistanceBand;
        }

        public override bool Equals(object obj)
        {
            return obj is ParcelFrontageReadModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ParcelCell.GetHashCode();
                hashCode = (hashCode * 397) ^ RoadCell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Direction;
                hashCode = (hashCode * 397) ^ IsReachable.GetHashCode();
                hashCode = (hashCode * 397) ^ NetworkDistanceToExternalGate;
                hashCode = (hashCode * 397) ^ NearestExternalGateId.GetHashCode();
                return (hashCode * 397) ^ (int)DistanceBand;
            }
        }
    }

    /// <summary>
    /// Application-owned immutable parcel snapshot for overlays and future
    /// development systems. It contains only stable IDs and value data.
    /// </summary>
    public sealed class ParcelDevelopmentReadModel
    {
        private readonly IReadOnlyList<CellCoord> _cells;
        private readonly IReadOnlyList<ParcelFrontageReadModel> _frontages;

        internal ParcelDevelopmentReadModel(ParcelState parcel)
        {
            Id = parcel.Id;
            ZoneType = parcel.ZoneType;
            Anchor = parcel.Anchor;
            Width = parcel.Width;
            Height = parcel.Height;
            IsDevelopmentEligible = parcel.IsDevelopmentEligible;
            FailureReason = parcel.FailureReason;
            NetworkDistanceToExternalGate = parcel.NetworkDistanceToExternalGate;
            NearestExternalGateId = parcel.NearestExternalGateId;
            DistanceBand = parcel.DistanceBand;

            var cells = new CellCoord[parcel.Cells.Count];
            for (var index = 0; index < cells.Length; index++)
                cells[index] = parcel.Cells[index];
            _cells = Array.AsReadOnly(cells);

            var frontages = new ParcelFrontageReadModel[parcel.Frontages.Count];
            for (var index = 0; index < frontages.Length; index++)
                frontages[index] = new ParcelFrontageReadModel(parcel.Frontages[index]);
            _frontages = Array.AsReadOnly(frontages);
        }

        public StableId Id { get; }

        public ZoneType ZoneType { get; }

        public CellCoord Anchor { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<ParcelFrontageReadModel> Frontages => _frontages;

        public bool HasRoadFrontage => _frontages.Count != 0;

        public bool IsDevelopmentEligible { get; }

        public ParcelDevelopmentFailureReason FailureReason { get; }

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public ExternalGateDistanceBand DistanceBand { get; }
    }

    /// <summary>
    /// Versioned bulk parcel query. Presentation refreshes this snapshot and
    /// uses ChangedParcelIds/DirtyCells instead of inspecting simulation arrays.
    /// </summary>
    public sealed class ParcelReadModel
    {
        private readonly ParcelNetworkState _parcels;
        private ParcelDevelopmentReadModel[] _items = Array.Empty<ParcelDevelopmentReadModel>();
        private IReadOnlyList<ParcelDevelopmentReadModel> _readOnlyItems =
            Array.AsReadOnly(Array.Empty<ParcelDevelopmentReadModel>());
        private IReadOnlyList<StableId> _changedParcelIds = Array.AsReadOnly(Array.Empty<StableId>());
        private IReadOnlyList<CellCoord> _dirtyCells = Array.AsReadOnly(Array.Empty<CellCoord>());
        private int _version = -1;

        public ParcelReadModel(ParcelNetworkState parcels)
        {
            _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
            RefreshIfNeeded();
        }

        public int Width => _parcels.Width;

        public int Height => _parcels.Height;

        public int Version
        {
            get
            {
                RefreshIfNeeded();
                return _version;
            }
        }

        public IReadOnlyList<ParcelDevelopmentReadModel> Parcels
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyItems;
            }
        }

        public IReadOnlyList<StableId> ChangedParcelIds
        {
            get
            {
                RefreshIfNeeded();
                return _changedParcelIds;
            }
        }

        public IReadOnlyList<CellCoord> DirtyCells
        {
            get
            {
                RefreshIfNeeded();
                return _dirtyCells;
            }
        }

        public bool RefreshIfNeeded()
        {
            _parcels.RefreshIfNeeded();
            if (_version == _parcels.Version)
                return false;

            var source = _parcels.Parcels;
            var items = new ParcelDevelopmentReadModel[source.Count];
            for (var index = 0; index < items.Length; index++)
                items[index] = new ParcelDevelopmentReadModel(source[index]);

            _items = items;
            _readOnlyItems = Array.AsReadOnly(_items);
            _changedParcelIds = Copy(_parcels.ChangedParcelIds);
            _dirtyCells = Copy(_parcels.DirtyCells);
            _version = _parcels.Version;
            return true;
        }

        public bool TryGet(StableId id, out ParcelDevelopmentReadModel parcel)
        {
            RefreshIfNeeded();
            for (var index = 0; index < _items.Length; index++)
            {
                if (_items[index].Id != id)
                    continue;
                parcel = _items[index];
                return true;
            }

            parcel = null;
            return false;
        }

        public bool TryGetAtCell(CellCoord cell, out ParcelDevelopmentReadModel parcel)
        {
            RefreshIfNeeded();
            for (var index = 0; index < _items.Length; index++)
            {
                var candidate = _items[index];
                if (cell.X >= candidate.Anchor.X && cell.X < candidate.Anchor.X + candidate.Width &&
                    cell.Y >= candidate.Anchor.Y && cell.Y < candidate.Anchor.Y + candidate.Height)
                {
                    parcel = candidate;
                    return true;
                }
            }

            parcel = null;
            return false;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }
    }
}
