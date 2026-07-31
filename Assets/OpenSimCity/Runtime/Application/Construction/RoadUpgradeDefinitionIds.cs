using OpenSimCity.Core;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Construction
{
    /// <summary>
    /// Application-facing stable IDs for the three P5 road tiers. UI and Gameplay
    /// select these IDs without referencing Simulation definition classes.
    /// </summary>
    public static class RoadUpgradeDefinitionIds
    {
        public static readonly StableId Small = RoadHierarchyDefaults.SmallRoadDefinitionId;
        public static readonly StableId Street = RoadHierarchyDefaults.StreetDefinitionId;
        public static readonly StableId Avenue = RoadHierarchyDefaults.AvenueDefinitionId;
    }
}
