using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictViewData
    {
        public StableId DistrictId { get; set; }
        public string Name { get; set; }
        public uint ColorRgba { get; set; }
        public IReadOnlyList<CellCoord> Cells { get; set; }
    }

    public sealed class DistrictReadModelSnapshot
    {
        public int Version { get; set; }
        public IReadOnlyList<DistrictViewData> Districts { get; set; } = Array.AsReadOnly(Array.Empty<DistrictViewData>());
    }

    public sealed class DistrictReadModel
    {
        private readonly DistrictCollection _source;
        private int _observedVersion = -1;
        public DistrictReadModel(DistrictCollection source) { _source = source ?? throw new ArgumentNullException(nameof(source)); Snapshot = new DistrictReadModelSnapshot(); }
        public DistrictReadModelSnapshot Snapshot { get; private set; }
        public bool RefreshIfNeeded()
        {
            if (_observedVersion == _source.Version) return false;
            var rows = new List<DistrictViewData>();
            for (var index = 0; index < _source.Districts.Count; index++)
            {
                var district = _source.Districts[index];
                var cells = new CellCoord[district.Cells.Count];
                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++) cells[cellIndex] = district.Cells[cellIndex];
                rows.Add(new DistrictViewData { DistrictId = district.Id, Name = district.Name, ColorRgba = district.ColorRgba, Cells = Array.AsReadOnly(cells) });
            }
            Snapshot = new DistrictReadModelSnapshot { Version = Snapshot.Version + 1, Districts = rows.AsReadOnly() };
            _observedVersion = _source.Version;
            return true;
        }
    }
}
