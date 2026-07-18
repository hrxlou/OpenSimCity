using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Population;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Buildings
{
    /// <summary>
    /// Application boundary between Unity-authored definition assets and pure
    /// simulation state. Catalog enumeration is deterministic by stable ID.
    /// </summary>
    public sealed class BuildingDefinitionCatalog :
        IBuildingPowerProfileResolver,
        IPopulationEmploymentProfileResolver,
        IUrbanEconomyProfileResolver
    {
        private readonly Dictionary<StableId, BuildingDefinitionData> _byId;
        private readonly IReadOnlyList<BuildingDefinitionData> _definitions;

        public BuildingDefinitionCatalog(IEnumerable<BuildingDefinitionData> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<StableId, BuildingDefinitionData>();
            var ordered = new List<BuildingDefinitionData>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("A building definition cannot be null.", nameof(definitions));
                if (_byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        $"Building definition {definition.Id} is registered more than once.",
                        nameof(definitions));
                }

                _byId.Add(definition.Id, definition);
                ordered.Add(definition);
            }

            ordered.Sort((left, right) => left.Id.CompareTo(right.Id));
            _definitions = ordered.AsReadOnly();
        }

        public int Count => _byId.Count;

        public IReadOnlyList<BuildingDefinitionData> Definitions => _definitions;

        public bool TryGet(StableId id, out BuildingDefinitionData definition)
        {
            return _byId.TryGetValue(id, out definition);
        }

        public BuildingDefinitionData Get(StableId id)
        {
            if (!TryGet(id, out var definition))
                throw new KeyNotFoundException($"Building definition {id} is not registered.");
            return definition;
        }

        public bool TryResolve(BuildingState state, out BuildingDefinitionData definition)
        {
            if (state == null)
            {
                definition = null;
                return false;
            }

            return TryGet(state.DefinitionId, out definition);
        }

        public bool TryGetPowerProfile(StableId definitionId, out BuildingPowerProfile profile)
        {
            if (TryGet(definitionId, out var definition))
            {
                profile = new BuildingPowerProfile(definition.Id, definition.Footprint, definition.PowerDemand);
                return true;
            }

            profile = default;
            return false;
        }

        public bool TryGetPopulationEmploymentProfile(
            StableId definitionId,
            out PopulationEmploymentProfile profile)
        {
            if (TryGet(definitionId, out var definition))
            {
                profile = new PopulationEmploymentProfile(
                    definition.Id,
                    definition.OccupancyCapacity,
                    definition.JobCapacity);
                return true;
            }

            profile = default;
            return false;
        }

        public bool TryGetUrbanEconomyProfile(StableId definitionId, out UrbanEconomyProfile profile)
        {
            if (TryGet(definitionId, out var definition))
            {
                profile = new UrbanEconomyProfile(
                    definition.Id,
                    ToSector(definition.ZoneType),
                    definition.OccupancyCapacity,
                    definition.JobCapacity);
                return true;
            }

            profile = default;
            return false;
        }

        private static EconomicSector ToSector(ZoneType zoneType)
        {
            switch (zoneType)
            {
                case ZoneType.ResidentialLow:
                    return EconomicSector.Residential;
                case ZoneType.CommercialLow:
                    return EconomicSector.Commercial;
                case ZoneType.IndustrialLow:
                    return EconomicSector.Industrial;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(zoneType),
                        $"Zone type {zoneType} has no economic sector.");
            }
        }
    }
}
