using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Districts
{
    public sealed class EraseDistrictCommand : ICityCommand
    {
        public EraseDistrictCommand(StableId districtId, IEnumerable<CellCoord> cells)
        {
            DistrictId = districtId;
            Cells = PaintDistrictCommand.Normalize(cells);
        }

        public StableId DistrictId { get; }
        public IReadOnlyList<CellCoord> Cells { get; }
    }
}
