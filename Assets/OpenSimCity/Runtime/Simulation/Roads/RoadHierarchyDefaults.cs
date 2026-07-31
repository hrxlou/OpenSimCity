using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Roads
{
    public enum RoadHierarchyTier : byte
    {
        Small = 0,
        Street = 1,
        Avenue = 2
    }

    /// <summary>
    /// Stable P5 road hierarchy. The original street ID and its existing runtime
    /// economics remain unchanged for save compatibility; small road and avenue
    /// receive new IDs in the same namespace.
    /// </summary>
    public static class RoadHierarchyDefaults
    {
        private const ulong RoadDefinitionNamespace = 0x0100_0000_0000_0000UL;

        public static readonly StableId StreetDefinitionId =
            new StableId(RoadDefinitionNamespace | 0x01UL);
        public static readonly StableId SmallRoadDefinitionId =
            new StableId(RoadDefinitionNamespace | 0x02UL);
        public static readonly StableId AvenueDefinitionId =
            new StableId(RoadDefinitionNamespace | 0x03UL);

        public static IReadOnlyList<RoadDefinition> CreateDefinitions()
        {
            return Array.AsReadOnly(new[]
            {
                new RoadDefinition(
                    SmallRoadDefinitionId,
                    buildCost: 6,
                    presentationKey: "road.small",
                    monthlyMaintenanceCost: 1,
                    capacityPerMonth: 500,
                    freeFlowSpeedCellsPerHour: 20,
                    noiseEmission: 15),
                new RoadDefinition(
                    StreetDefinitionId,
                    buildCost: 10,
                    presentationKey: "road.street",
                    monthlyMaintenanceCost: 1,
                    capacityPerMonth: 1000,
                    freeFlowSpeedCellsPerHour: 30,
                    noiseEmission: 30),
                new RoadDefinition(
                    AvenueDefinitionId,
                    buildCost: 25,
                    presentationKey: "road.avenue",
                    monthlyMaintenanceCost: 5,
                    capacityPerMonth: 2400,
                    freeFlowSpeedCellsPerHour: 45,
                    noiseEmission: 70)
            });
        }

        public static RoadDefinitionCatalog CreateCatalog()
        {
            return new RoadDefinitionCatalog(CreateDefinitions());
        }

        public static RoadHierarchyTier GetTier(StableId definitionId)
        {
            if (definitionId == SmallRoadDefinitionId)
                return RoadHierarchyTier.Small;
            if (definitionId == StreetDefinitionId)
                return RoadHierarchyTier.Street;
            if (definitionId == AvenueDefinitionId)
                return RoadHierarchyTier.Avenue;
            throw new ArgumentOutOfRangeException(nameof(definitionId), definitionId, "Unknown road hierarchy definition.");
        }
    }
}
