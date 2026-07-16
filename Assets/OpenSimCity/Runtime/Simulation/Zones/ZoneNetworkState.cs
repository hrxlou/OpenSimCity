using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Simulation.Zones
{
    /// <summary>
    /// Runtime zoning state indexed by the world's row-major cell index.
    /// Presentation code receives copies/read models and cannot mutate these
    /// arrays directly.
    /// </summary>
    [Serializable]
    public class ZoneNetworkState
    {
        private readonly ZoneType[] _zoneTypes;
        private readonly StableId[] _definitionIds;
        private readonly ZoneCellState[] _cells;
        private readonly IReadOnlyList<ZoneType> _readOnlyZoneTypes;
        private readonly IReadOnlyList<StableId> _readOnlyDefinitionIds;
        private readonly IReadOnlyList<ZoneCellState> _readOnlyCells;

        public ZoneNetworkState()
            : this(WorldState.DefaultWidth, WorldState.DefaultHeight)
        {
        }

        public ZoneNetworkState(WorldState world)
            : this(GetWidth(world), GetHeight(world))
        {
        }

        public ZoneNetworkState(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Zone network width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Zone network height must be positive.");

            var cellCount = checked(width * height);
            Width = width;
            Height = height;
            CellCount = cellCount;
            _zoneTypes = new ZoneType[cellCount];
            _definitionIds = new StableId[cellCount];
            _cells = new ZoneCellState[cellCount];
            _readOnlyZoneTypes = Array.AsReadOnly(_zoneTypes);
            _readOnlyDefinitionIds = Array.AsReadOnly(_definitionIds);
            _readOnlyCells = Array.AsReadOnly(_cells);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    _zoneTypes[index] = ZoneType.None;
                    _definitionIds[index] = StableId.Invalid;
                    _cells[index] = new ZoneCellState(
                        new CellCoord(x, y),
                        index,
                        ZoneType.None,
                        StableId.Invalid);
                }
            }
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount { get; }

        public int Version { get; private set; }

        public int ZoneVersion => Version;

        public IReadOnlyList<ZoneType> ZoneTypes => _readOnlyZoneTypes;

        public IReadOnlyList<ZoneType> Types => _readOnlyZoneTypes;

        public IReadOnlyList<StableId> DefinitionIds => _readOnlyDefinitionIds;

        public IReadOnlyList<StableId> ZoneDefinitionIds => _readOnlyDefinitionIds;

        public IReadOnlyList<ZoneCellState> Cells => _readOnlyCells;

        public bool IsInBounds(CellCoord coordinate)
        {
            return (uint)coordinate.X < (uint)Width &&
                   (uint)coordinate.Y < (uint)Height;
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
                    $"Cell coordinate {coordinate} is outside {Width}x{Height} zone bounds.");
            }

            return index;
        }

        public bool TryGetZoneType(CellCoord coordinate, out ZoneType zoneType)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                zoneType = ZoneType.None;
                return false;
            }

            zoneType = _zoneTypes[index];
            return true;
        }

        public bool TryGetZone(CellCoord coordinate, out ZoneType zoneType)
        {
            return TryGetZoneType(coordinate, out zoneType);
        }

        public ZoneType GetZoneType(CellCoord coordinate)
        {
            return _zoneTypes[GetIndex(coordinate)];
        }

        public ZoneType GetZone(CellCoord coordinate) => GetZoneType(coordinate);

        public bool IsZoned(CellCoord coordinate)
        {
            return GetZoneType(coordinate).IsPaintable();
        }

        public bool HasZone(CellCoord coordinate) => IsZoned(coordinate);

        public bool TryGetDefinitionId(CellCoord coordinate, out StableId definitionId)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                definitionId = StableId.Invalid;
                return false;
            }

            definitionId = _definitionIds[index];
            return true;
        }

        public bool TryGetZoneDefinitionId(CellCoord coordinate, out StableId definitionId)
        {
            return TryGetDefinitionId(coordinate, out definitionId);
        }

        public StableId GetDefinitionId(CellCoord coordinate)
        {
            return _definitionIds[GetIndex(coordinate)];
        }

        public StableId GetZoneDefinitionId(CellCoord coordinate) => GetDefinitionId(coordinate);

        public bool TryGetCell(CellCoord coordinate, out ZoneCellState cell)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                cell = default;
                return false;
            }

            cell = _cells[index];
            return true;
        }

        public ZoneCellState GetCell(CellCoord coordinate)
        {
            return _cells[GetIndex(coordinate)];
        }

        public bool TryGetCellAtIndex(int index, out ZoneCellState cell)
        {
            if (!IsValidCellIndex(index))
            {
                cell = default;
                return false;
            }

            cell = _cells[index];
            return true;
        }

        public ZoneCellState GetCellAtIndex(int index)
        {
            if (!TryGetCellAtIndex(index, out var cell))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Cell index {index} is outside the range 0..{CellCount - 1}.");
            }

            return cell;
        }

        public bool TryApplyZone(IReadOnlyList<CellCoord> cells, ZoneType zoneType)
        {
            return TryApplyZone(
                cells,
                zoneType,
                global::OpenSimCity.Simulation.Zones.ZoneDefinitionIds.For(zoneType));
        }

        /// <summary>
        /// Applies a complete zone batch atomically. Existing cells with the
        /// same type are idempotent; another type is rejected by the state.
        /// Road overlap is an application-level rule because the road state is
        /// a separate simulation subsystem.
        /// </summary>
        public bool TryApplyZone(
            IReadOnlyList<CellCoord> cells,
            ZoneType zoneType,
            StableId definitionId)
        {
            if (!zoneType.IsPaintable() || !definitionId.IsValid || !CanApplyZone(cells, zoneType))
                return false;

            var changed = false;
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                var cellIndex = GetIndex(coordinate);
                if (_zoneTypes[cellIndex] != zoneType || _definitionIds[cellIndex] != definitionId)
                {
                    _zoneTypes[cellIndex] = zoneType;
                    _definitionIds[cellIndex] = definitionId;
                    _cells[cellIndex] = new ZoneCellState(coordinate, cellIndex, zoneType, definitionId);
                    changed = true;
                }
            }

            if (changed)
                Version++;
            return true;
        }

        public bool TryPaintZone(IReadOnlyList<CellCoord> cells, ZoneType zoneType)
        {
            return TryApplyZone(cells, zoneType);
        }

        public bool TryPaintZone(
            IReadOnlyList<CellCoord> cells,
            ZoneType zoneType,
            StableId definitionId)
        {
            return TryApplyZone(cells, zoneType, definitionId);
        }

        public bool TryApplyClear(IReadOnlyList<CellCoord> cells)
        {
            if (!CanApplyClear(cells))
                return false;

            var changed = false;
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                var cellIndex = GetIndex(coordinate);
                if (!_zoneTypes[cellIndex].IsPaintable())
                    continue;

                _zoneTypes[cellIndex] = ZoneType.None;
                _definitionIds[cellIndex] = StableId.Invalid;
                _cells[cellIndex] = new ZoneCellState(
                    coordinate,
                    cellIndex,
                    ZoneType.None,
                    StableId.Invalid);
                changed = true;
            }

            if (changed)
                Version++;
            return true;
        }

        public bool TryClearZone(IReadOnlyList<CellCoord> cells)
        {
            return TryApplyClear(cells);
        }

        public bool CanApplyZone(IReadOnlyList<CellCoord> cells, ZoneType zoneType)
        {
            if (!zoneType.IsPaintable() || cells == null || cells.Count == 0)
                return false;

            var seen = new HashSet<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                if (!IsInBounds(coordinate) || !seen.Add(coordinate))
                    return false;

                var current = _zoneTypes[GetIndex(coordinate)];
                if (current.IsPaintable() && current != zoneType)
                    return false;
            }

            return true;
        }

        public bool CanApplyClear(IReadOnlyList<CellCoord> cells)
        {
            if (cells == null || cells.Count == 0)
                return false;

            var seen = new HashSet<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (!IsInBounds(cells[index]) || !seen.Add(cells[index]))
                    return false;
            }

            return true;
        }

        private bool IsValidCellIndex(int index) => (uint)index < (uint)CellCount;

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

    /// <summary>
    /// Short compatibility name for callers that refer to zoning as a state
    /// rather than a network.
    /// </summary>
    [Serializable]
    public sealed class ZoneState : ZoneNetworkState
    {
        public ZoneState()
        {
        }

        public ZoneState(WorldState world)
            : base(world)
        {
        }

        public ZoneState(int width, int height)
            : base(width, height)
        {
        }
    }
}
