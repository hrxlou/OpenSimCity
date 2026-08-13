using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Districts
{
    [Serializable]
    public sealed class DistrictState
    {
        private readonly List<CellCoord> _cells = new List<CellCoord>();
        private readonly IReadOnlyList<CellCoord> _readOnlyCells;

        public DistrictState(StableId id, string name, uint colorRgba)
            : this(id, name, colorRgba, Array.Empty<CellCoord>())
        {
        }

        public DistrictState(StableId id, string name, uint colorRgba, IEnumerable<CellCoord> cells)
        {
            if (!id.IsValid) throw new ArgumentException("District requires a stable ID.", nameof(id));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            Id = id;
            Name = NormalizeName(name);
            ColorRgba = colorRgba;
            _readOnlyCells = _cells.AsReadOnly();
            var ordered = new List<CellCoord>(cells);
            ordered.Sort();
            for (var index = 0; index < ordered.Count; index++)
            {
                if (index > 0 && ordered[index] == ordered[index - 1])
                    throw new ArgumentException("District cells must be unique.", nameof(cells));
                _cells.Add(ordered[index]);
            }
        }

        public StableId Id { get; }
        public string Name { get; private set; }
        public uint ColorRgba { get; private set; }
        public IReadOnlyList<CellCoord> Cells => _readOnlyCells;
        public int CellCount => _cells.Count;

        internal void Rename(string name) => Name = NormalizeName(name);
        internal void SetColor(uint colorRgba) => ColorRgba = colorRgba;

        internal void AddCell(CellCoord cell)
        {
            var index = _cells.BinarySearch(cell);
            if (index < 0) _cells.Insert(~index, cell);
        }

        internal bool RemoveCell(CellCoord cell)
        {
            var index = _cells.BinarySearch(cell);
            if (index < 0) return false;
            _cells.RemoveAt(index);
            return true;
        }

        private static string NormalizeName(string name)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0) throw new ArgumentException("District name cannot be empty.", nameof(name));
            if (value.Length > 48) throw new ArgumentOutOfRangeException(nameof(name));
            return value;
        }
    }
}
