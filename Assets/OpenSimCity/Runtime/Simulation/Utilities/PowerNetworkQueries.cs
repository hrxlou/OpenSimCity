using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Utilities
{
    public static class PowerNetworkQueries
    {
        public static BuildingPowerResult GetBuildingSupply(this PowerNetwork value, StableId id)
        {
            return value.GetBuilding(id);
        }
    }
}
