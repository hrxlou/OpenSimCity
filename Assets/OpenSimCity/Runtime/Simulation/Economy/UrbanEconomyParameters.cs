using System;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Per-sector tax rate input port, expressed in permille (0..1000). This is a
    /// decoupled input only: the market/RCI model reads it as a demand penalty and
    /// never performs the actual fiscal money calculation, which is deferred to the
    /// municipal economy issue. Defaults to zero for every sector.
    /// </summary>
    [Serializable]
    public readonly struct UrbanEconomyTaxRates
    {
        public const int MaxPermille = 1000;

        public UrbanEconomyTaxRates(int residentialPermille, int commercialPermille, int industrialPermille)
        {
            ResidentialPermille = ClampPermille(residentialPermille, nameof(residentialPermille));
            CommercialPermille = ClampPermille(commercialPermille, nameof(commercialPermille));
            IndustrialPermille = ClampPermille(industrialPermille, nameof(industrialPermille));
        }

        public int ResidentialPermille { get; }
        public int CommercialPermille { get; }
        public int IndustrialPermille { get; }

        public static UrbanEconomyTaxRates Zero => new UrbanEconomyTaxRates(0, 0, 0);

        public int ForSector(EconomicSector sector)
        {
            switch (sector)
            {
                case EconomicSector.Residential:
                    return ResidentialPermille;
                case EconomicSector.Commercial:
                    return CommercialPermille;
                case EconomicSector.Industrial:
                    return IndustrialPermille;
                default:
                    return 0;
            }
        }

        private static int ClampPermille(int value, string name)
        {
            if (value < 0 || value > MaxPermille)
                throw new ArgumentOutOfRangeException(name, "A tax rate must be within 0..1000 permille.");
            return value;
        }
    }

    /// <summary>
    /// Deterministic integer tuning for the single-commodity market and the RCI
    /// demand model. Every value is an integer weight so the whole monthly pass is
    /// pure integer arithmetic and cannot differ across platform floats.
    /// </summary>
    [Serializable]
    public sealed class UrbanEconomyParameters
    {
        public UrbanEconomyParameters(
            int goodsPerIndustrialWorker = 1,
            int consumptionPerResident = 1,
            int throughputPerCommercialJob = 1,
            int demandPointScale = 1,
            int taxDemandSensitivity = 100,
            int rciClampMagnitude = 1000,
            int smoothingNumerator = 1,
            int smoothingDenominator = 4)
        {
            if (goodsPerIndustrialWorker < 0)
                throw new ArgumentOutOfRangeException(nameof(goodsPerIndustrialWorker));
            if (consumptionPerResident < 0)
                throw new ArgumentOutOfRangeException(nameof(consumptionPerResident));
            if (throughputPerCommercialJob < 0)
                throw new ArgumentOutOfRangeException(nameof(throughputPerCommercialJob));
            if (demandPointScale < 0)
                throw new ArgumentOutOfRangeException(nameof(demandPointScale));
            if (taxDemandSensitivity < 0)
                throw new ArgumentOutOfRangeException(nameof(taxDemandSensitivity));
            if (rciClampMagnitude < 0)
                throw new ArgumentOutOfRangeException(nameof(rciClampMagnitude));
            if (smoothingNumerator < 0 || smoothingNumerator > smoothingDenominator)
                throw new ArgumentOutOfRangeException(nameof(smoothingNumerator));
            if (smoothingDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(smoothingDenominator));

            GoodsPerIndustrialWorker = goodsPerIndustrialWorker;
            ConsumptionPerResident = consumptionPerResident;
            ThroughputPerCommercialJob = throughputPerCommercialJob;
            DemandPointScale = demandPointScale;
            TaxDemandSensitivity = taxDemandSensitivity;
            RciClampMagnitude = rciClampMagnitude;
            SmoothingNumerator = smoothingNumerator;
            SmoothingDenominator = smoothingDenominator;
        }

        /// <summary>Goods produced per filled industrial job in a month.</summary>
        public int GoodsPerIndustrialWorker { get; }

        /// <summary>Goods demanded (consumption power) per resident in a month.</summary>
        public int ConsumptionPerResident { get; }

        /// <summary>Goods a single commercial job can move to consumers in a month.</summary>
        public int ThroughputPerCommercialJob { get; }

        /// <summary>Demand points a single count unit (a vacant job, empty home, ...) contributes.</summary>
        public int DemandPointScale { get; }

        /// <summary>
        /// Tax demand penalty numerator. Penalty = permille * sensitivity / 1000
        /// with integer division truncating toward zero.
        /// </summary>
        public int TaxDemandSensitivity { get; }

        /// <summary>Symmetric clamp bound applied to each raw RCI pressure before smoothing.</summary>
        public int RciClampMagnitude { get; }

        /// <summary>Exponential smoothing step numerator (fraction of the gap closed per month).</summary>
        public int SmoothingNumerator { get; }

        /// <summary>Exponential smoothing step denominator.</summary>
        public int SmoothingDenominator { get; }

        public static UrbanEconomyParameters Default => new UrbanEconomyParameters();
    }
}
