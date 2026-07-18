using System;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Static per-definition economy profile: which aggregate sector a building
    /// belongs to and its housing/job capacity. Capacities mirror the population
    /// profile; the sector lets the market and RCI model split residential,
    /// commercial and industrial contributions without inspecting zoning cells.
    /// </summary>
    [Serializable]
    public readonly struct UrbanEconomyProfile
    {
        public UrbanEconomyProfile(
            StableId definitionId,
            EconomicSector sector,
            int housingCapacity,
            int jobCapacity)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A profile requires a valid definition ID.", nameof(definitionId));
            if (housingCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(housingCapacity));
            if (jobCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(jobCapacity));

            DefinitionId = definitionId;
            Sector = sector;
            HousingCapacity = housingCapacity;
            JobCapacity = jobCapacity;
        }

        public StableId DefinitionId { get; }
        public EconomicSector Sector { get; }
        public int HousingCapacity { get; }
        public int JobCapacity { get; }
    }

    public interface IUrbanEconomyProfileResolver
    {
        bool TryGetUrbanEconomyProfile(StableId definitionId, out UrbanEconomyProfile profile);
    }
}
