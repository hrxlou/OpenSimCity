using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Simulation.Roads
{
    [Serializable]
    public readonly struct RoadCellState : IEquatable<RoadCellState>
    {
        public RoadCellState(CellCoord coordinate, int index, StableId definitionId)
            : this(coordinate, index, definitionId, RoadTravelDirection.Bidirectional)
        {
        }

        public RoadCellState(
            CellCoord coordinate,
            int index,
            StableId definitionId,
            RoadTravelDirection travelDirection)
        {
            if (!RoadTravelDirectionRules.IsDefined(travelDirection))
                throw new ArgumentOutOfRangeException(nameof(travelDirection));

            Coordinate = coordinate;
            Index = index;
            DefinitionId = definitionId;
            TravelDirection = definitionId.IsValid
                ? travelDirection
                : RoadTravelDirection.Bidirectional;
        }

        public CellCoord Coordinate { get; }

        public CellCoord Coord => Coordinate;

        public int Index { get; }

        public StableId DefinitionId { get; }

        public StableId RoadDefinitionId => DefinitionId;

        public RoadTravelDirection TravelDirection { get; }

        public bool IsOccupied => DefinitionId.IsValid;

        public bool Equals(RoadCellState other)
        {
            return Coordinate == other.Coordinate &&
                   Index == other.Index &&
                   DefinitionId == other.DefinitionId &&
                   TravelDirection == other.TravelDirection;
        }

        public override bool Equals(object obj) => obj is RoadCellState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Coordinate.GetHashCode();
                hashCode = (hashCode * 397) ^ Index;
                hashCode = (hashCode * 397) ^ DefinitionId.GetHashCode();
                return (hashCode * 397) ^ (int)TravelDirection;
            }
        }

        public static bool operator ==(RoadCellState left, RoadCellState right) => left.Equals(right);

        public static bool operator !=(RoadCellState left, RoadCellState right) => !left.Equals(right);
    }

    /// <summary>
    /// Runtime road occupancy indexed by the world's row-major cell index.
    /// Presentation and input code cannot access the backing arrays; application
    /// command handlers apply validated batches through the methods below.
    /// </summary>
    [Serializable]
    public sealed class RoadNetworkState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly StableId[] _definitionIds;
        private readonly RoadTravelDirection[] _travelDirections;
        private readonly RoadCellState[] _cells;
        private readonly IReadOnlyList<StableId> _readOnlyDefinitionIds;
        private readonly IReadOnlyList<RoadTravelDirection> _readOnlyTravelDirections;
        private readonly IReadOnlyList<RoadCellState> _readOnlyCells;

        public RoadNetworkState()
            : this(WorldState.DefaultWidth, WorldState.DefaultHeight)
        {
        }

        public RoadNetworkState(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Road network width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Road network height must be positive.");

            var cellCount = checked(width * height);
            Width = width;
            Height = height;
            CellCount = cellCount;
            _definitionIds = new StableId[cellCount];
            _travelDirections = new RoadTravelDirection[cellCount];
            _cells = new RoadCellState[cellCount];
            _readOnlyDefinitionIds = Array.AsReadOnly(_definitionIds);
            _readOnlyTravelDirections = Array.AsReadOnly(_travelDirections);
            _readOnlyCells = Array.AsReadOnly(_cells);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    _cells[index] = EmptyCell(new CellCoord(x, y), index);
                }
            }
        }

        public RoadNetworkState(WorldState world)
            : this(GetWidth(world), GetHeight(world))
        {
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount { get; }

        /// <summary>
        /// Increments exactly once for each successful placement, demolition, or
        /// definition/direction replacement batch.
        /// </summary>
        public int Version { get; private set; }

        public IReadOnlyList<StableId> DefinitionIds => _readOnlyDefinitionIds;

        public IReadOnlyList<StableId> RoadDefinitionIds => _readOnlyDefinitionIds;

        public IReadOnlyList<RoadTravelDirection> TravelDirections => _readOnlyTravelDirections;

        public IReadOnlyList<RoadCellState> Cells => _readOnlyCells;

        public bool IsInBounds(CellCoord coordinate)
        {
            return (uint)coordinate.X < (uint)Width && (uint)coordinate.Y < (uint)Height;
        }

        public bool TryGetIndex(CellCoord coordinate, out int index)
        {
            if (!IsInBounds(coordinate))
            {
                index = -1;
                return false;
            }

            index = coordinate.Y * Width + coordinate.X;
            return true;
        }

        public int GetIndex(CellCoord coordinate)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    $"Cell coordinate {coordinate} is outside {Width}x{Height} road bounds.");
            }

            return index;
        }

        public bool TryGetRoadDefinitionId(CellCoord coordinate, out StableId definitionId)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                definitionId = StableId.Invalid;
                return false;
            }

            definitionId = _definitionIds[index];
            return true;
        }

        public bool TryGetDefinitionId(CellCoord coordinate, out StableId definitionId)
        {
            return TryGetRoadDefinitionId(coordinate, out definitionId);
        }

        public StableId GetRoadDefinitionId(CellCoord coordinate)
        {
            return _definitionIds[GetIndex(coordinate)];
        }

        public StableId GetDefinitionId(CellCoord coordinate)
        {
            return GetRoadDefinitionId(coordinate);
        }

        public RoadTravelDirection GetTravelDirection(CellCoord coordinate)
        {
            return _travelDirections[GetIndex(coordinate)];
        }

        public bool IsOccupied(CellCoord coordinate)
        {
            return GetRoadDefinitionId(coordinate).IsValid;
        }

        public bool HasRoad(CellCoord coordinate)
        {
            return IsOccupied(coordinate);
        }

        public bool TryGetCell(CellCoord coordinate, out RoadCellState cell)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                cell = default;
                return false;
            }

            cell = _cells[index];
            return true;
        }

        public RoadCellState GetCell(CellCoord coordinate)
        {
            return _cells[GetIndex(coordinate)];
        }

        public bool CanTravel(CellCoord from, CellCoord to)
        {
            if (!TryGetIndex(from, out var fromIndex) ||
                !TryGetIndex(to, out var toIndex) ||
                !_definitionIds[fromIndex].IsValid ||
                !_definitionIds[toIndex].IsValid)
            {
                return false;
            }

            return RoadTravelDirectionRules.AllowsTravel(
                _travelDirections[fromIndex],
                from,
                to);
        }

        public bool TryPlaceRoad(CellCoord coordinate, StableId definitionId)
        {
            return TryPlaceRoad(
                coordinate,
                definitionId,
                RoadTravelDirection.Bidirectional);
        }

        public bool TryPlaceRoad(
            CellCoord coordinate,
            StableId definitionId,
            RoadTravelDirection travelDirection)
        {
            return TryApplyPlacement(
                new[] { coordinate },
                definitionId,
                travelDirection);
        }

        public bool TryDemolishRoad(CellCoord coordinate)
        {
            return TryApplyDemolition(new[] { coordinate });
        }

        /// <summary>
        /// Applies a complete placement atomically. New roads are bidirectional by
        /// default for backward compatibility.
        /// </summary>
        public bool TryApplyPlacement(IReadOnlyList<CellCoord> cells, StableId definitionId)
        {
            return TryApplyPlacement(
                cells,
                definitionId,
                RoadTravelDirection.Bidirectional);
        }

        public bool TryApplyPlacement(
            IReadOnlyList<CellCoord> cells,
            StableId definitionId,
            RoadTravelDirection travelDirection)
        {
            if (!definitionId.IsValid ||
                !RoadTravelDirectionRules.IsDefined(travelDirection) ||
                cells == null ||
                cells.Count == 0)
            {
                return false;
            }
            if (!CanApplyPlacement(cells))
                return false;

            for (var index = 0; index < cells.Count; index++)
                SetCell(cells[index], definitionId, travelDirection);

            Version = checked(Version + 1);
            return true;
        }

        /// <summary>
        /// Replaces definition and direction on existing cells without removing
        /// occupancy. Every input is validated before any state mutation.
        /// </summary>
        public bool TryApplyReplacement(
            IReadOnlyList<CellCoord> cells,
            StableId definitionId,
            IReadOnlyList<RoadTravelDirection> travelDirections)
        {
            if (!definitionId.IsValid ||
                cells == null ||
                travelDirections == null ||
                cells.Count == 0 ||
                cells.Count != travelDirections.Count ||
                !CanApplyReplacement(cells, travelDirections))
            {
                return false;
            }

            for (var index = 0; index < cells.Count; index++)
                SetCell(cells[index], definitionId, travelDirections[index]);

            Version = checked(Version + 1);
            return true;
        }

        /// <summary>
        /// Applies a complete demolition atomically. A partial demolition is
        /// valid when the selected cells are an occupied subset of a longer road.
        /// </summary>
        public bool TryApplyDemolition(IReadOnlyList<CellCoord> cells)
        {
            if (cells == null || cells.Count == 0)
                return false;
            if (!CanApplyDemolition(cells))
                return false;

            for (var index = 0; index < cells.Count; index++)
            {
                var cellIndex = GetIndex(cells[index]);
                _definitionIds[cellIndex] = StableId.Invalid;
                _travelDirections[cellIndex] = RoadTravelDirection.Bidirectional;
                _cells[cellIndex] = EmptyCell(cells[index], cellIndex);
            }

            Version = checked(Version + 1);
            return true;
        }

        public bool CanApplyPlacement(IReadOnlyList<CellCoord> cells)
        {
            return CanApplyBatch(cells, requireOccupied: false);
        }

        public bool CanApplyDemolition(IReadOnlyList<CellCoord> cells)
        {
            return CanApplyBatch(cells, requireOccupied: true);
        }

        public bool CanApplyReplacement(
            IReadOnlyList<CellCoord> cells,
            IReadOnlyList<RoadTravelDirection> travelDirections)
        {
            if (cells == null ||
                travelDirections == null ||
                cells.Count == 0 ||
                cells.Count != travelDirections.Count ||
                !CanApplyBatch(cells, requireOccupied: true))
            {
                return false;
            }

            for (var index = 0; index < travelDirections.Count; index++)
            {
                if (!RoadTravelDirectionRules.IsDefined(travelDirections[index]))
                    return false;
            }
            return true;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, Width);
            AddInt(ref hash, Height);
            for (var index = 0; index < _cells.Length; index++)
            {
                AddUlong(ref hash, _definitionIds[index].Value);
                AddByte(ref hash, (byte)_travelDirections[index]);
            }
            return hash;
        }

        private bool CanApplyBatch(IReadOnlyList<CellCoord> cells, bool requireOccupied)
        {
            if (cells == null || cells.Count == 0)
                return false;

            var seen = new HashSet<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                if (!IsInBounds(coordinate) || !seen.Add(coordinate))
                    return false;

                var occupied = _definitionIds[GetIndex(coordinate)].IsValid;
                if (requireOccupied != occupied)
                    return false;
            }

            return true;
        }

        private void SetCell(
            CellCoord coordinate,
            StableId definitionId,
            RoadTravelDirection travelDirection)
        {
            var cellIndex = GetIndex(coordinate);
            _definitionIds[cellIndex] = definitionId;
            _travelDirections[cellIndex] = travelDirection;
            _cells[cellIndex] = new RoadCellState(
                coordinate,
                cellIndex,
                definitionId,
                travelDirection);
        }

        private static RoadCellState EmptyCell(CellCoord coordinate, int index)
        {
            return new RoadCellState(
                coordinate,
                index,
                StableId.Invalid,
                RoadTravelDirection.Bidirectional);
        }

        private static int GetWidth(WorldState world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            return world.Width;
        }

        private static int GetHeight(WorldState world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            return world.Height;
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime64;
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }
    }
}
