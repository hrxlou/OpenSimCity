namespace OpenSimCity.Simulation.Districts
{
    public enum DistrictPolicyKind : byte
    {
        HeavyVehicleRestriction,
        TransitSubsidy
    }

    public enum DistrictPolicyOperatingReason : byte
    {
        None,
        Disabled,
        Pending,
        InsufficientFunding,
        DistrictRemoved
    }
}
