using OpenSimCity.Application.Commands;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Districts
{
    public sealed class CreateDistrictCommand : ICityCommand
    {
        public CreateDistrictCommand(StableId districtId, string name, uint colorRgba)
        {
            DistrictId = districtId;
            Name = name;
            ColorRgba = colorRgba;
        }

        public StableId DistrictId { get; }
        public string Name { get; }
        public uint ColorRgba { get; }
    }
}
