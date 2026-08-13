using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Districts
{
    public sealed class DistrictPolicyCollection
    {
        private readonly List<DistrictPolicyState> _rows = new List<DistrictPolicyState>();
        private readonly IReadOnlyList<DistrictPolicyState> _readOnlyRows;
        public DistrictPolicyCollection() { _readOnlyRows = _rows.AsReadOnly(); }
        public IReadOnlyList<DistrictPolicyState> Rows => _readOnlyRows;
        public int Version { get; private set; }

        public DistrictPolicyState GetOrCreate(StableId districtId, DistrictPolicyKind kind)
        {
            var index = Find(districtId, kind);
            if (index >= 0) return _rows[index];
            var row = new DistrictPolicyState(districtId, kind);
            _rows.Insert(~index, row);
            Touch();
            return row;
        }

        public bool TryGet(StableId districtId, DistrictPolicyKind kind, out DistrictPolicyState row)
        {
            var index = Find(districtId, kind);
            row = index >= 0 ? _rows[index] : null;
            return index >= 0;
        }

        public void Schedule(StableId districtId, DistrictPolicyKind kind, bool enabled, OpenSimCity.Core.Time.SimulationDate effectiveMonth)
        {
            GetOrCreate(districtId, kind).Schedule(enabled, effectiveMonth);
            Touch();
        }

        public bool RemoveDistrict(StableId districtId)
        {
            var changed = false;
            for (var index = _rows.Count - 1; index >= 0; index--)
            {
                if (_rows[index].DistrictId != districtId) continue;
                _rows.RemoveAt(index); changed = true;
            }
            if (changed) Touch();
            return changed;
        }

        public void Replace(IEnumerable<DistrictPolicyState> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var source = new List<DistrictPolicyState>(rows);
            source.Sort(Compare);
            for (var index = 1; index < source.Count; index++)
                if (Compare(source[index - 1], source[index]) == 0) throw new ArgumentException("Policy rows must be unique.", nameof(rows));
            _rows.Clear(); _rows.AddRange(source); Touch();
        }

        public void Touch() => Version = checked(Version + 1);

        private int Find(StableId districtId, DistrictPolicyKind kind)
        {
            var low = 0; var high = _rows.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var row = _rows[mid];
                var compare = row.DistrictId.CompareTo(districtId);
                if (compare == 0) compare = row.Kind.CompareTo(kind);
                if (compare == 0) return mid;
                if (compare < 0) low = mid + 1; else high = mid - 1;
            }
            return ~low;
        }

        private static int Compare(DistrictPolicyState left, DistrictPolicyState right)
        {
            var compare = left.DistrictId.CompareTo(right.DistrictId);
            return compare != 0 ? compare : left.Kind.CompareTo(right.Kind);
        }
    }
}
