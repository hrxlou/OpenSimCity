using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Simulation.Parcels
{
    /// <summary>
    /// Deterministically partitions zoned cells into simple 1x1 through 2x2
    /// rectangular parcels and derives road-frontage development eligibility.
    /// Equal parcels are retained across refreshes so changes outside the exact
    /// dirty set neither replace state objects nor change stable IDs.
    /// </summary>
    public sealed class ParcelNetworkState
    {
        public const int MaximumParcelWidth = 2;
        public const int MaximumParcelHeight = 2;

        private const ulong ParcelIdNamespace = 0x5000000000000000UL;

        private readonly ZoneNetworkState _zones;
        private readonly RoadConnectivityGraph _roads;
        private ParcelState[] _parcels = Array.Empty<ParcelState>();
        private IReadOnlyList<ParcelState> _readOnlyParcels = Array.AsReadOnly(Array.Empty<ParcelState>());
        private IReadOnlyList<StableId> _changedParcelIds = Array.AsReadOnly(Array.Empty<StableId>());
        private IReadOnlyList<CellCoord> _dirtyCells = Array.AsReadOnly(Array.Empty<CellCoord>());
        private int _zoneVersion = -1;
        private int _roadGraphVersion = -1;

        public ParcelNetworkState(ZoneNetworkState zones, RoadConnectivityGraph roads)
        {
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            if (_zones.Width != _roads.Width || _zones.Height != _roads.Height)
                throw new ArgumentException("Zone and road graph dimensions must match.", nameof(roads));

            RefreshIfNeeded();
        }

        public int Width => _zones.Width;

        public int Height => _zones.Height;

        public int Version { get; private set; }

        public IReadOnlyList<ParcelState> Parcels
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyParcels;
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
            var zoneVersion = _zones.Version;
            var roadGraphVersion = _roads.GraphVersion;
            if (_zoneVersion == zoneVersion && _roadGraphVersion == roadGraphVersion)
                return false;

            var candidates = BuildParcels();
            ApplyDiff(candidates);
            _zoneVersion = zoneVersion;
            _roadGraphVersion = roadGraphVersion;
            return true;
        }

        public bool TryGet(StableId id, out ParcelState parcel)
        {
            RefreshIfNeeded();
            var low = 0;
            var high = _parcels.Length - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var comparison = _parcels[middle].Id.CompareTo(id);
                if (comparison == 0)
                {
                    parcel = _parcels[middle];
                    return true;
                }

                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            parcel = null;
            return false;
        }

        public bool TryGetAtCell(CellCoord cell, out ParcelState parcel)
        {
            RefreshIfNeeded();
            for (var parcelIndex = 0; parcelIndex < _parcels.Length; parcelIndex++)
            {
                var candidate = _parcels[parcelIndex];
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

        private ParcelState[] BuildParcels()
        {
            var assigned = new bool[checked(Width * Height)];
            var parcels = new List<ParcelState>();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var anchor = new CellCoord(x, y);
                    var anchorIndex = y * Width + x;
                    var zoneType = _zones.GetZoneType(anchor);
                    if (assigned[anchorIndex] || !zoneType.IsPaintable())
                        continue;

                    var parcelWidth = CanUseCell(x + 1, y, zoneType, assigned) ? 2 : 1;
                    var parcelHeight = CanUseRow(x, y + 1, parcelWidth, zoneType, assigned) ? 2 : 1;
                    var cells = new CellCoord[parcelWidth * parcelHeight];
                    var cellOffset = 0;
                    for (var parcelY = 0; parcelY < parcelHeight; parcelY++)
                    {
                        for (var parcelX = 0; parcelX < parcelWidth; parcelX++)
                        {
                            var cell = new CellCoord(x + parcelX, y + parcelY);
                            assigned[cell.Y * Width + cell.X] = true;
                            cells[cellOffset++] = cell;
                        }
                    }

                    parcels.Add(BuildParcel(anchor, parcelWidth, parcelHeight, zoneType, cells));
                }
            }

            parcels.Sort((left, right) => left.Id.CompareTo(right.Id));
            return parcels.ToArray();
        }

        private bool CanUseRow(
            int x,
            int y,
            int width,
            ZoneType zoneType,
            bool[] assigned)
        {
            if (y >= Height)
                return false;
            for (var offset = 0; offset < width; offset++)
            {
                if (!CanUseCell(x + offset, y, zoneType, assigned))
                    return false;
            }

            return true;
        }

        private bool CanUseCell(int x, int y, ZoneType zoneType, bool[] assigned)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return false;
            var index = y * Width + x;
            return !assigned[index] && _zones.GetZoneType(new CellCoord(x, y)) == zoneType;
        }

        private ParcelState BuildParcel(
            CellCoord anchor,
            int width,
            int height,
            ZoneType zoneType,
            CellCoord[] cells)
        {
            var frontages = new List<ParcelFrontage>();
            for (var index = 0; index < cells.Length; index++)
            {
                AddFrontage(cells[index], 0, 1, ParcelFrontageDirection.North, frontages);
                AddFrontage(cells[index], 1, 0, ParcelFrontageDirection.East, frontages);
                AddFrontage(cells[index], 0, -1, ParcelFrontageDirection.South, frontages);
                AddFrontage(cells[index], -1, 0, ParcelFrontageDirection.West, frontages);
            }

            frontages.Sort(CompareFrontages);
            var failureReason = frontages.Count == 0
                ? ParcelDevelopmentFailureReason.NoRoadFrontage
                : ParcelDevelopmentFailureReason.NoExternalRoadConnection;
            var distance = RoadConnectivityGraph.UnreachableDistance;
            var nearestGateId = StableId.Invalid;
            var band = ExternalGateDistanceBand.Unreachable;
            var eligible = false;
            for (var index = 0; index < frontages.Count; index++)
            {
                var frontage = frontages[index];
                if (!frontage.IsReachable)
                    continue;
                if (!eligible || frontage.NetworkDistanceToExternalGate < distance ||
                    (frontage.NetworkDistanceToExternalGate == distance &&
                     frontage.NearestExternalGateId < nearestGateId))
                {
                    eligible = true;
                    failureReason = ParcelDevelopmentFailureReason.None;
                    distance = frontage.NetworkDistanceToExternalGate;
                    nearestGateId = frontage.NearestExternalGateId;
                    band = frontage.DistanceBand;
                }
            }

            return new ParcelState(
                CreateStableId(anchor, zoneType),
                zoneType,
                anchor,
                width,
                height,
                cells,
                frontages.ToArray(),
                eligible,
                failureReason,
                distance,
                nearestGateId,
                band);
        }

        private void AddFrontage(
            CellCoord parcelCell,
            int deltaX,
            int deltaY,
            ParcelFrontageDirection direction,
            ICollection<ParcelFrontage> frontages)
        {
            var roadCell = new CellCoord(parcelCell.X + deltaX, parcelCell.Y + deltaY);
            if (!_roads.TryGet(roadCell, out var road) || !road.HasRoad)
                return;

            frontages.Add(new ParcelFrontage(
                parcelCell,
                roadCell,
                direction,
                road.NetworkDistanceToExternalGate,
                road.NearestExternalGateId,
                road.DistanceBand,
                road.UnreachableReason));
        }

        private void ApplyDiff(ParcelState[] candidates)
        {
            var previousById = new Dictionary<StableId, ParcelState>();
            for (var index = 0; index < _parcels.Length; index++)
                previousById.Add(_parcels[index].Id, _parcels[index]);

            var currentIds = new HashSet<StableId>();
            var changedIds = new List<StableId>();
            var dirtyCells = new HashSet<CellCoord>();
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                currentIds.Add(candidate.Id);
                if (previousById.TryGetValue(candidate.Id, out var previous) && previous.Equals(candidate))
                {
                    candidates[index] = previous;
                    continue;
                }

                changedIds.Add(candidate.Id);
                AddCells(candidate.Cells, dirtyCells);
                if (previous != null)
                    AddCells(previous.Cells, dirtyCells);
            }

            for (var index = 0; index < _parcels.Length; index++)
            {
                var removed = _parcels[index];
                if (currentIds.Contains(removed.Id))
                    continue;
                changedIds.Add(removed.Id);
                AddCells(removed.Cells, dirtyCells);
            }

            changedIds.Sort();
            var sortedDirtyCells = new List<CellCoord>(dirtyCells);
            sortedDirtyCells.Sort();
            _parcels = candidates;
            _readOnlyParcels = Array.AsReadOnly(_parcels);
            _changedParcelIds = changedIds.AsReadOnly();
            _dirtyCells = sortedDirtyCells.AsReadOnly();
            if (changedIds.Count != 0)
                Version++;
        }

        private static void AddCells(IReadOnlyList<CellCoord> source, ISet<CellCoord> destination)
        {
            for (var index = 0; index < source.Count; index++)
                destination.Add(source[index]);
        }

        private static int CompareFrontages(ParcelFrontage left, ParcelFrontage right)
        {
            var parcelCell = left.ParcelCell.CompareTo(right.ParcelCell);
            if (parcelCell != 0)
                return parcelCell;
            var direction = left.Direction.CompareTo(right.Direction);
            return direction != 0 ? direction : left.RoadCell.CompareTo(right.RoadCell);
        }

        private StableId CreateStableId(CellCoord anchor, ZoneType zoneType)
        {
            var anchorIndex = checked(anchor.Y * Width + anchor.X);
            var value = ParcelIdNamespace |
                        ((ulong)(uint)anchorIndex << 2) |
                        (byte)zoneType;
            return new StableId(value);
        }
    }
}
