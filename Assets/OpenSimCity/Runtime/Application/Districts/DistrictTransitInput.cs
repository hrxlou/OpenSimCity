using System;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Districts;
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictTransitInput : ITransitGeneralizedCostPolicy
    {
        private readonly CityRuntime _runtime;
        private readonly DistrictCollection _districts;
        private readonly DistrictPolicyCollection _states;
        private readonly DistrictPolicyParameters _parameters;
        public DistrictTransitInput(CityRuntime runtime, DistrictCollection districts, DistrictPolicyCollection states, DistrictPolicyParameters parameters)
        { _runtime = runtime; _districts = districts; _states = states; _parameters = parameters; }
        public int Version => unchecked((_districts.Version * 397) ^ _states.Version);

        public int AdjustBusCostSeconds(TransitOdKey key, SimulationDate month, int baseCostSeconds)
        {
            if (baseCostSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(baseCostSeconds));
            if (HasAt(key.OriginId, key.OriginKind, month) || HasAt(key.DestinationId, key.DestinationKind, month))
                return Math.Max(1, checked(baseCostSeconds * _parameters.TransitBusCostPermille / 1000));
            return baseCostSeconds;
        }

        private bool HasAt(StableId id, TripEndpointKind kind, SimulationDate month)
        {
            if (kind != TripEndpointKind.Building || !id.IsValid || !_runtime.Buildings.TryGet(id, out var building)) return false;
            if (!_districts.TryGetDistrictAt(building.Anchor, out var districtId)) return false;
            return _states.TryGet(districtId, DistrictPolicyKind.TransitSubsidy, out var state) &&
                   state.IsFundedAndEffective && state.LastOperatingMonth == month;
        }
    }
}
