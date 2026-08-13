using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Districts
{
    public sealed class DistrictCollection
    {
        private readonly List<DistrictState> _districts = new List<DistrictState>();
        private readonly Dictionary<StableId, DistrictState> _byId = new Dictionary<StableId, DistrictState>();
        private readonly Dictionary<CellCoord, StableId> _membership = new Dictionary<CellCoord, StableId>();
        private readonly IReadOnlyList<DistrictState> _readOnlyDistricts;

        public DistrictCollection() { _readOnlyDistricts = _districts.AsReadOnly(); }
        public IReadOnlyList<DistrictState> Districts => _readOnlyDistricts;
        public int Version { get; private set; }
        public bool TryGet(StableId id, out DistrictState district) => _byId.TryGetValue(id, out district);
        public bool TryGetDistrictAt(CellCoord cell, out StableId districtId) => _membership.TryGetValue(cell, out districtId);

        public bool Create(StableId id, string name, uint colorRgba)
        {
            if (!id.IsValid || _byId.ContainsKey(id)) return false;
            var district = new DistrictState(id, name, colorRgba);
            var index = _districts.BinarySearch(district, DistrictComparer.Instance);
            _districts.Insert(index >= 0 ? index : ~index, district);
            _byId.Add(id, district);
            Touch();
            return true;
        }

        public bool Rename(StableId id, string name)
        {
            if (!_byId.TryGetValue(id, out var district)) return false;
            district.Rename(name); Touch(); return true;
        }

        public bool SetColor(StableId id, uint colorRgba)
        {
            if (!_byId.TryGetValue(id, out var district)) return false;
            district.SetColor(colorRgba); Touch(); return true;
        }

        public bool Paint(StableId id, IEnumerable<CellCoord> cells)
        {
            if (!_byId.TryGetValue(id, out var district) || cells == null) return false;
            var ordered = NormalizeCells(cells);
            for (var index = 0; index < ordered.Count; index++)
                if (_membership.TryGetValue(ordered[index], out var owner) && owner != id) return false;
            var changed = false;
            for (var index = 0; index < ordered.Count; index++)
            {
                var cell = ordered[index];
                if (_membership.ContainsKey(cell)) continue;
                _membership.Add(cell, id); district.AddCell(cell); changed = true;
            }
            if (changed) Touch();
            return true;
        }

        public bool Erase(StableId id, IEnumerable<CellCoord> cells)
        {
            if (!_byId.TryGetValue(id, out var district) || cells == null) return false;
            var ordered = NormalizeCells(cells); var changed = false;
            for (var index = 0; index < ordered.Count; index++)
            {
                var cell = ordered[index];
                if (!_membership.TryGetValue(cell, out var owner) || owner != id) continue;
                _membership.Remove(cell); district.RemoveCell(cell); changed = true;
            }
            if (changed) Touch();
            return true;
        }

        public bool Delete(StableId id)
        {
            if (!_byId.TryGetValue(id, out var district)) return false;
            for (var index = 0; index < district.Cells.Count; index++) _membership.Remove(district.Cells[index]);
            _byId.Remove(id); _districts.Remove(district); Touch(); return true;
        }

        public void ClearAndRestore(IEnumerable<DistrictState> districts)
        {
            if (districts == null) throw new ArgumentNullException(nameof(districts));
            var source = new List<DistrictState>(districts); source.Sort(DistrictComparer.Instance);
            var byId = new Dictionary<StableId, DistrictState>();
            var membership = new Dictionary<CellCoord, StableId>();
            StableId previous = default;
            for (var index = 0; index < source.Count; index++)
            {
                var district = source[index] ?? throw new ArgumentException("District rows cannot be null.", nameof(districts));
                if (!district.Id.IsValid || (previous.IsValid && district.Id <= previous) || byId.ContainsKey(district.Id))
                    throw new ArgumentException("District IDs must be unique and ordered.", nameof(districts));
                byId.Add(district.Id, district);
                for (var cellIndex = 0; cellIndex < district.Cells.Count; cellIndex++)
                {
                    var cell = district.Cells[cellIndex];
                    if (membership.ContainsKey(cell)) throw new ArgumentException("A cell cannot belong to multiple districts.", nameof(districts));
                    membership.Add(cell, district.Id);
                }
                previous = district.Id;
            }
            _districts.Clear(); _districts.AddRange(source);
            _byId.Clear(); foreach (var pair in byId) _byId.Add(pair.Key, pair.Value);
            _membership.Clear(); foreach (var pair in membership) _membership.Add(pair.Key, pair.Value);
            Touch();
        }

        private static List<CellCoord> NormalizeCells(IEnumerable<CellCoord> cells)
        {
            var result = new List<CellCoord>(cells); result.Sort();
            for (var index = result.Count - 1; index > 0; index--) if (result[index] == result[index - 1]) result.RemoveAt(index);
            return result;
        }

        private void Touch() => Version = checked(Version + 1);
        private sealed class DistrictComparer : IComparer<DistrictState>
        {
            public static readonly DistrictComparer Instance = new DistrictComparer();
            public int Compare(DistrictState x, DistrictState y) => x.Id.CompareTo(y.Id);
        }
    }
}
