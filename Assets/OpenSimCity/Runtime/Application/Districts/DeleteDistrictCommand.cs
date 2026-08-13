using OpenSimCity.Application.Commands;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Districts
{
    public sealed class DeleteDistrictCommand : ICityCommand
    {
        public DeleteDistrictCommand(StableId districtId) { DistrictId = districtId; }
        public StableId DistrictId { get; }
    }
}
