using System;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Definitions.Buildings
{
    /// <summary>
    /// Authoring-only static building schema. Runtime occupancy, power supply,
    /// and construction progress belong to simulation state and must never be
    /// added to this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildingDefinition",
        menuName = "OpenSimCity/Definitions/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField]
        private ulong definitionId;

        [SerializeField]
        private ZoneType zoneType = ZoneType.ResidentialLow;

        [SerializeField, Min(1)]
        private int footprintWidth = 1;

        [SerializeField, Min(1)]
        private int footprintHeight = 1;

        [SerializeField, Min(0)]
        private int occupancyCapacity;

        [SerializeField, Min(0)]
        private int jobCapacity;

        [SerializeField, Min(1)]
        private int powerDemand = 1;

        [SerializeField]
        private string presentationKey = string.Empty;

        public ulong DefinitionIdValue => definitionId;

        public StableId DefinitionId => definitionId == 0 ? StableId.Invalid : new StableId(definitionId);

        public ZoneType ZoneType => zoneType;

        public int FootprintWidth => footprintWidth;

        public int FootprintHeight => footprintHeight;

        public int OccupancyCapacity => occupancyCapacity;

        public int JobCapacity => jobCapacity;

        public int PowerDemand => powerDemand;

        public string PresentationKey => presentationKey ?? string.Empty;

        public BuildingDefinitionData ToData()
        {
            return new BuildingDefinitionData(
                new StableId(definitionId),
                zoneType,
                new BuildingFootprint(footprintWidth, footprintHeight),
                occupancyCapacity,
                jobCapacity,
                presentationKey,
                powerDemand);
        }
    }
}
