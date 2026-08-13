using System;
namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictSaveData
    {
        public int Format { get; set; } = 1;
        public DistrictRow[] Items { get; set; } = Array.Empty<DistrictRow>();
    }
}
