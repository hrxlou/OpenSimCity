using OpenSimCity.Application.Commands;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Districts
{
    public sealed class RenameDistrictCommand : ICityCommand
    {
        public RenameDistrictCommand(StableId districtId, string name)
        {
            DistrictId = districtId;
            Name = name;
        }

        public StableId DistrictId { get; }
        public string Name { get; }
    }
}
