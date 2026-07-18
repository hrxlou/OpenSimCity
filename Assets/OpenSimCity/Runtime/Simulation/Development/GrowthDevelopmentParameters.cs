using System;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// Static tuning for the growth loop. These are definition-time constants; the
    /// current per-parcel factors and selection results are runtime state, not stored
    /// here. Accessibility weights are intentionally weak so a far-but-reachable parcel
    /// still develops — accessibility never gates development or picks a definition.
    /// </summary>
    [Serializable]
    public sealed class GrowthDevelopmentParameters
    {
        public GrowthDevelopmentParameters(
            int maxDevelopmentsPerMonth = 4,
            int constructionMonths = 2,
            int demandThreshold = 1,
            int baseSuitabilityWeight = 8,
            int residentialAccessibilityWeight = 2,
            int commercialAccessibilityWeight = 3,
            int industrialAccessibilityWeight = 1)
        {
            if (maxDevelopmentsPerMonth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDevelopmentsPerMonth), "The monthly growth cap must be positive.");
            if (constructionMonths <= 0)
                throw new ArgumentOutOfRangeException(nameof(constructionMonths), "Construction must take at least one month.");
            if (demandThreshold < 1)
                throw new ArgumentOutOfRangeException(nameof(demandThreshold), "The demand threshold must be at least one.");
            if (baseSuitabilityWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseSuitabilityWeight), "The base suitability weight must be positive so far parcels keep a nonzero weight.");
            if (residentialAccessibilityWeight < 0 || commercialAccessibilityWeight < 0 || industrialAccessibilityWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(residentialAccessibilityWeight), "Accessibility weights cannot be negative.");

            MaxDevelopmentsPerMonth = maxDevelopmentsPerMonth;
            ConstructionMonths = constructionMonths;
            DemandThreshold = demandThreshold;
            BaseSuitabilityWeight = baseSuitabilityWeight;
            ResidentialAccessibilityWeight = residentialAccessibilityWeight;
            CommercialAccessibilityWeight = commercialAccessibilityWeight;
            IndustrialAccessibilityWeight = industrialAccessibilityWeight;
        }

        public int MaxDevelopmentsPerMonth { get; }

        public int ConstructionMonths { get; }

        public int DemandThreshold { get; }

        public int BaseSuitabilityWeight { get; }

        public int ResidentialAccessibilityWeight { get; }

        public int CommercialAccessibilityWeight { get; }

        public int IndustrialAccessibilityWeight { get; }

        public static GrowthDevelopmentParameters Default => new GrowthDevelopmentParameters();

        public int AccessibilityWeight(EconomicSector sector)
        {
            switch (sector)
            {
                case EconomicSector.Residential:
                    return ResidentialAccessibilityWeight;
                case EconomicSector.Commercial:
                    return CommercialAccessibilityWeight;
                case EconomicSector.Industrial:
                    return IndustrialAccessibilityWeight;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }

        /// <summary>
        /// Higher for parcels closer to an external gate. Reachable parcels always score
        /// at least one, so the weighted factor stays positive and closeness only nudges
        /// selection probability.
        /// </summary>
        public static int BandScore(ExternalGateDistanceBand band)
        {
            switch (band)
            {
                case ExternalGateDistanceBand.Gate:
                    return 4;
                case ExternalGateDistanceBand.Near:
                    return 3;
                case ExternalGateDistanceBand.Medium:
                    return 2;
                case ExternalGateDistanceBand.Far:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Weighted selection factor for a reachable parcel. Always positive for a
        /// reachable band, so accessibility differences change the odds but never remove
        /// a parcel from the candidate pool.
        /// </summary>
        public int SuitabilityFactor(EconomicSector sector, ExternalGateDistanceBand band)
        {
            return BaseSuitabilityWeight + AccessibilityWeight(sector) * BandScore(band);
        }
    }
}
