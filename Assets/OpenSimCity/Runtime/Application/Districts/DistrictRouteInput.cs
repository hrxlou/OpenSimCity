using System;
using OpenSimCity.Simulation.Districts;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictRouteInput : ITrafficRoutePolicy
    {
        private readonly DistrictCollection _districts;
        private readonly DistrictPolicyCollection _states;
        private readonly DistrictPolicyParameters _parameters;
        public DistrictRouteInput(DistrictCollection districts, DistrictPolicyCollection states, DistrictPolicyParameters parameters)
        { _districts = districts; _states = states; _parameters = parameters; }
        public int Version => unchecked((_districts.Version * 397) ^ _states.Version);
        public int RoutePreferencePermille(TripDemandKind kind, TrafficRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (kind != TripDemandKind.Goods) return 1000;
            for (var i = 0; i < route.Cells.Count; i++)
            {
                if (!_districts.TryGetDistrictAt(route.Cells[i], out var id)) continue;
                if (_states.TryGet(id, DistrictPolicyKind.HeavyVehicleRestriction, out var row) && row.IsFundedAndEffective)
                    return _parameters.HeavyVehiclePenaltyPermille;
            }
            return 1000;
        }
    }
}
