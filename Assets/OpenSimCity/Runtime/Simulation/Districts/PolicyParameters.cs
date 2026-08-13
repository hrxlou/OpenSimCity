using System;

namespace OpenSimCity.Simulation.Districts
{
    [Serializable]
    public sealed class DistrictPolicyParameters
    {
        public DistrictPolicyParameters(int heavyVehicleCost = 20, int transitSubsidyCost = 80, int heavyVehiclePenaltyPermille = 1600, int transitBusCostPermille = 800)
        {
            if (heavyVehicleCost < 0 || transitSubsidyCost < 0) throw new ArgumentOutOfRangeException(nameof(heavyVehicleCost));
            if (heavyVehiclePenaltyPermille < 1000 || heavyVehiclePenaltyPermille > 5000) throw new ArgumentOutOfRangeException(nameof(heavyVehiclePenaltyPermille));
            if (transitBusCostPermille < 250 || transitBusCostPermille > 1000) throw new ArgumentOutOfRangeException(nameof(transitBusCostPermille));
            HeavyVehicleCost = heavyVehicleCost;
            TransitSubsidyCost = transitSubsidyCost;
            HeavyVehiclePenaltyPermille = heavyVehiclePenaltyPermille;
            TransitBusCostPermille = transitBusCostPermille;
        }
        public int HeavyVehicleCost { get; }
        public int TransitSubsidyCost { get; }
        public int HeavyVehiclePenaltyPermille { get; }
        public int TransitBusCostPermille { get; }
        public int MonthlyCost(DistrictPolicyKind kind) => kind == DistrictPolicyKind.HeavyVehicleRestriction ? HeavyVehicleCost : TransitSubsidyCost;
        public static DistrictPolicyParameters Default => new DistrictPolicyParameters();
    }
}
