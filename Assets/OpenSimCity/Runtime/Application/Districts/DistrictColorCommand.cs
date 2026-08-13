using OpenSimCity.Application.Commands;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictColorCommand : ICityCommand
    {
        public DistrictColorCommand(StableId districtId, uint rgba)
        {
            DistrictId = districtId;
            Rgba = rgba;
        }
        public StableId DistrictId { get; }
        public uint Rgba { get; }
    }
}
