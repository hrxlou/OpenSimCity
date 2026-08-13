using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class SetDistrictPolicyCommand : ICityCommand
    {
        public SetDistrictPolicyCommand(StableId districtId, DistrictPolicyKind kind, bool enabled)
        {
            DistrictId = districtId;
            Kind = kind;
            Enabled = enabled;
        }
        public StableId DistrictId { get; }
        public DistrictPolicyKind Kind { get; }
        public bool Enabled { get; }
    }
}
