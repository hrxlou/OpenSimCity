using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Application-owned immutable view of one zoning cell. Presentation uses
    /// this value rather than reading or recalculating Simulation state.
    /// </summary>
    public readonly struct ZoneCellReadModel : IEquatable<ZoneCellReadModel>
    {
        internal ZoneCellReadModel(ZoneCellState cell)
        {
            Cell = cell.Coordinate;
            ZoneType = cell.ZoneType;
            DefinitionId = cell.DefinitionId;
            IsZoned = cell.IsZoned;
        }

        public CellCoord Cell { get; }

        public CellCoord Coordinate => Cell;

        public ZoneType ZoneType { get; }

        public ZoneType Type => ZoneType;

        public StableId DefinitionId { get; }

        public StableId ZoneDefinitionId => DefinitionId;

        public bool IsZoned { get; }

        public bool Equals(ZoneCellReadModel other)
        {
            return Cell == other.Cell &&
                   ZoneType == other.ZoneType &&
                   DefinitionId == other.DefinitionId &&
                   IsZoned == other.IsZoned;
        }

        public override bool Equals(object obj)
        {
            return obj is ZoneCellReadModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Cell.GetHashCode() * 397) ^ (int)ZoneType) * 397 ^
                       DefinitionId.GetHashCode() * 397 ^ IsZoned.GetHashCode();
            }
        }

        public static bool operator ==(ZoneCellReadModel left, ZoneCellReadModel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ZoneCellReadModel left, ZoneCellReadModel right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Versioned bulk zone read model. Each refresh copies the complete cell
    /// state and exposes the exact row-major cells whose public value changed.
    /// </summary>
    public class ZoneReadModel
    {
        private readonly ZoneNetworkState _zones;
        private ZoneCellReadModel[] _cells = Array.Empty<ZoneCellReadModel>();
        private IReadOnlyList<ZoneCellReadModel> _readOnlyCells =
            Array.AsReadOnly(Array.Empty<ZoneCellReadModel>());
        private IReadOnlyList<CellCoord> _changedCells =
            Array.AsReadOnly(Array.Empty<CellCoord>());
        private int _zoneVersion = -1;

        public ZoneReadModel(ZoneNetworkState zones)
        {
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            RefreshIfNeeded();
        }

        public int Width => _zones.Width;

        public int Height => _zones.Height;

        public int Version
        {
            get
            {
                RefreshIfNeeded();
                return _zoneVersion;
            }
        }

        public int ZoneVersion => Version;

        public IReadOnlyList<ZoneCellReadModel> Cells
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyCells;
            }
        }

        public IReadOnlyList<CellCoord> ChangedCells
        {
            get
            {
                RefreshIfNeeded();
                return _changedCells;
            }
        }

        public IReadOnlyList<CellCoord> DirtyCells => ChangedCells;

        public bool HasChangedCells => ChangedCells.Count != 0;

        public bool RefreshIfNeeded()
        {
            var currentVersion = _zones.Version;
            if (_zoneVersion == currentVersion)
                return false;

            var sourceCells = _zones.Cells;
            var cells = new ZoneCellReadModel[sourceCells.Count];
            var changedCells = new List<CellCoord>();
            for (var index = 0; index < sourceCells.Count; index++)
            {
                var cell = new ZoneCellReadModel(sourceCells[index]);
                cells[index] = cell;
                if (_zoneVersion < 0 || !_cells[index].Equals(cell))
                    changedCells.Add(cell.Cell);
            }

            _cells = cells;
            _readOnlyCells = Array.AsReadOnly(_cells);
            _changedCells = changedCells.AsReadOnly();
            _zoneVersion = currentVersion;
            return true;
        }

        public bool TryGet(CellCoord cell, out ZoneCellReadModel result)
        {
            RefreshIfNeeded();
            if ((uint)cell.X >= (uint)Width || (uint)cell.Y >= (uint)Height)
            {
                result = default;
                return false;
            }

            result = _cells[cell.Y * Width + cell.X];
            return true;
        }

        public ZoneCellReadModel Get(CellCoord cell)
        {
            if (!TryGet(cell, out var result))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cell),
                    cell,
                    $"Cell coordinate {cell} is outside {Width}x{Height} zone bounds.");
            }

            return result;
        }
    }

    public sealed class ZoneStateReadModel : ZoneReadModel
    {
        public ZoneStateReadModel(ZoneNetworkState zones)
            : base(zones)
        {
        }
    }
}
