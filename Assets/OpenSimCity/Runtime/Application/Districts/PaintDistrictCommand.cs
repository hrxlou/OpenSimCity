using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Districts
{
    public sealed class PaintDistrictCommand : ICityCommand
    {
        public PaintDistrictCommand(StableId districtId, IEnumerable<CellCoord> cells)
        {
            DistrictId = districtId;
            Cells = Normalize(cells);
        }

        public StableId DistrictId { get; }
        public IReadOnlyList<CellCoord> Cells { get; }

        internal static IReadOnlyList<CellCoord> Normalize(IEnumerable<CellCoord> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            var rows = new List<CellCoord>(cells);
            rows.Sort();
            for (var index = rows.Count - 1; index > 0; index--)
                if (rows[index] == rows[index - 1]) rows.RemoveAt(index);
            return rows.AsReadOnly();
        }
    }
}
