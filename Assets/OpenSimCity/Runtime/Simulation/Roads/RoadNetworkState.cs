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
        {
            Coordinate = coordinate;
            Index = index;
            DefinitionId = definitionId;
        }

        public CellCoord Coordinate { get; }

        public CellCoord Coord => Coordinate;

        public int Index { get; }

        public StableId DefinitionId { get; }

        public StableId RoadDefinitionId => DefinitionId;

        public bool IsOccupied => DefinitionId.IsValid;

        public bool Equals(RoadCellState other)
        {
            return Coordinate == other.Coordinate && Index == other.Index && DefinitionId == other.DefinitionId;
        }

        public override bool Equals(object obj) => obj is RoadCellState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Coordinate.GetHashCode() * 397) ^ Index) * 397 ^ DefinitionId.GetHashCode();
            }
        }

        public static bool operator ==(RoadCellState left, RoadCellState right) => left.Equals(right);

        public static bool operator !=(RoadCellState left, RoadCellState right) => !left.Equals(right);
    }

    /// <summary>
    /// Runtime road occupancy indexed by the world's row-major cell index.
    /// Presentation and input code cannot access the backing array; application
    /// command handlers apply validated batches through the methods below.
    /// </summary>
    [Serializable]
    public sealed class RoadNetworkState
    {
        private readonly StableId[] _definitionIds;
        private readonly RoadCellState[] _cells;
        private readonly IReadOnlyList<StableId> _readOnlyDefinitionIds;
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
            _cells = new RoadCellState[cellCount];
            _readOnlyDefinitionIds = Array.AsReadOnly(_definitionIds);
            _readOnlyCells = Array.AsReadOnly(_cells);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    _cells[index] = new RoadCellState(
                        new CellCoord(x, y),
                        index,
                        StableId.Invalid);
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
        /// Increments once for each successfully applied placement or demolition
        /// batch. It is available for later graph invalidation in P2-02.
        /// </summary>
        public int Version { get; private set; }

        public IReadOnlyList<StableId> DefinitionIds => _readOnlyDefinitionIds;

        public IReadOnlyList<StableId> RoadDefinitionIds => _readOnlyDefinitionIds;

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

        public bool TryPlaceRoad(CellCoord coordinate, StableId definitionId)
        {
            return TryApplyPlacement(new[] { coordinate }, definitionId);
        }

        public bool TryDemolishRoad(CellCoord coordinate)
        {
            return TryApplyDemolition(new[] { coordinate });
        }

        /// <summary>
        /// Applies a complete placement atomically. All cells are checked before
        /// any backing storage is changed, so a failed batch cannot partially
        /// install a road.
        /// </summary>
        public bool TryApplyPlacement(IReadOnlyList<CellCoord> cells, StableId definitionId)
        {
            if (!definitionId.IsValid || cells == null || cells.Count == 0)
                return false;
            if (!CanApplyPlacement(cells))
                return false;

            for (var index = 0; index < cells.Count; index++)
            {
                var cellIndex = GetIndex(cells[index]);
                _definitionIds[cellIndex] = definitionId;
                _cells[cellIndex] = new RoadCellState(cells[index], cellIndex, definitionId);
            }

            Version++;
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
                _cells[cellIndex] = new RoadCellState(cells[index], cellIndex, StableId.Invalid);
            }

            Version++;
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
    }
}
