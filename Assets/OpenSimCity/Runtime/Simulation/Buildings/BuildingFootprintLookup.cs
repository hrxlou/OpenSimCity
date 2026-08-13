using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Buildings
{
    public static class BuildingFootprintLookup
    {
        private static readonly Dictionary<StableId, BuildingFootprint> Values = new Dictionary<StableId, BuildingFootprint>();

        public static void Register(StableId definitionId, BuildingFootprint footprint)
        {
            Values[definitionId] = footprint;
        }

        public static BuildingFootprint Resolve(StableId definitionId)
        {
            return Values[definitionId];
        }
    }
}
