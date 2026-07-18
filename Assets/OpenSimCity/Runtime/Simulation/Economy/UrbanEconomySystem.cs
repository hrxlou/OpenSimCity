using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Aggregate single-commodity market and RCI demand for one month. It reads the
    /// building occupancy/employment counts the population system already persisted
    /// this month, splits them by sector via <see cref="IUrbanEconomyProfileResolver"/>,
    /// computes the goods market, then derives residential/commercial/industrial
    /// demand.
    /// <para>
    /// Calculation order per sector is fixed: sum signed factor contributions into a
    /// raw pressure, clamp it symmetrically, then move the persistent smoothed demand
    /// one integer step toward the clamped value. The only rounding sites in the whole
    /// pass are the tax-penalty integer division, the business-utilisation permille
    /// division and the smoothing-step division; each truncates toward zero. There is
    /// no floating-point arithmetic, so the same input yields the same result on every
    /// platform. RCI is pure simulation state and never triggers development; P3-05
    /// growth applies the cap.
    /// </para>
    /// </summary>
    public sealed class UrbanEconomySystem : IMonthlySimulationSystem
    {
        public const string VacantJobsFactor = "VacantJobs";
        public const string VacantHousingFactor = "VacantHousing";
        public const string UnemploymentFactor = "Unemployment";
        public const string CommercialDemandGapFactor = "CommercialDemandGap";
        public const string VacantCommercialJobsFactor = "VacantCommercialJobs";
        public const string GoodsShortageFactor = "GoodsShortage";
        public const string GoodsSurplusFactor = "GoodsSurplus";
        public const string TaxFactor = "Tax";

        private readonly BuildingStateCollection _buildings;
        private readonly IUrbanEconomyProfileResolver _profiles;
        private readonly UrbanEconomyParameters _parameters;

        public UrbanEconomySystem(
            BuildingStateCollection buildings,
            IUrbanEconomyProfileResolver profiles,
            UrbanEconomyParameters parameters = null)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _parameters = parameters ?? UrbanEconomyParameters.Default;
            Market = new MarketState();
            Rci = new RciDemandState();
            TaxRates = UrbanEconomyTaxRates.Zero;
        }

        public MarketState Market { get; }
        public RciDemandState Rci { get; }

        /// <summary>Decoupled tax-rate input port (see <see cref="UrbanEconomyTaxRates"/>).</summary>
        public UrbanEconomyTaxRates TaxRates { get; set; }

        public UrbanEconomyParameters Parameters => _parameters;
        public SimulationDate LastProcessedMonth { get; private set; }
        public bool HasProcessedMonth { get; private set; }
        public int Version { get; private set; }

        public void RunMonth(SimulationDate month)
        {
            if (HasProcessedMonth && month.CompareTo(LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Monthly economy processing requires a strictly increasing month.");

            var source = _buildings.Buildings;
            var residents = 0;
            var housingCapacity = 0;
            var commercialJobCapacity = 0;
            var commercialFilledJobs = 0;
            var industrialJobCapacity = 0;
            var industrialFilledJobs = 0;

            for (var index = 0; index < source.Count; index++)
            {
                var building = source[index];
                if (!_profiles.TryGetUrbanEconomyProfile(building.DefinitionId, out var profile))
                    throw new InvalidOperationException($"Building {building.Id} has no urban-economy profile.");

                switch (profile.Sector)
                {
                    case EconomicSector.Residential:
                        residents = checked(residents + Math.Min(building.Occupancy, profile.HousingCapacity));
                        housingCapacity = checked(housingCapacity + profile.HousingCapacity);
                        break;
                    case EconomicSector.Commercial:
                        commercialJobCapacity = checked(commercialJobCapacity + profile.JobCapacity);
                        commercialFilledJobs = checked(commercialFilledJobs + Math.Min(building.FilledJobs, profile.JobCapacity));
                        break;
                    case EconomicSector.Industrial:
                        industrialJobCapacity = checked(industrialJobCapacity + profile.JobCapacity);
                        industrialFilledJobs = checked(industrialFilledJobs + Math.Min(building.FilledJobs, profile.JobCapacity));
                        break;
                    default:
                        throw new InvalidOperationException($"Building {building.Id} has an unknown economic sector.");
                }
            }

            var employed = commercialFilledJobs + industrialFilledJobs;
            var totalJobCapacity = commercialJobCapacity + industrialJobCapacity;
            var unemployment = Math.Max(0, residents - employed);
            var vacantJobs = Math.Max(0, totalJobCapacity - employed);
            var vacantHousing = Math.Max(0, housingCapacity - residents);
            var vacantCommercialJobs = Math.Max(0, commercialJobCapacity - commercialFilledJobs);

            var goodsSupply = industrialFilledJobs * _parameters.GoodsPerIndustrialWorker;
            var consumptionPower = residents * _parameters.ConsumptionPerResident;
            var commercialThroughputCapacity = commercialJobCapacity * _parameters.ThroughputPerCommercialJob;
            Market.Set(goodsSupply, consumptionPower, commercialThroughputCapacity);

            var commercialDemandGap = Math.Max(0, consumptionPower - commercialThroughputCapacity);
            var scale = _parameters.DemandPointScale;

            ApplyDemand(Rci.Residential, new[]
            {
                new RciDemandFactor(VacantJobsFactor, scale * vacantJobs),
                new RciDemandFactor(VacantHousingFactor, -scale * vacantHousing),
                new RciDemandFactor(UnemploymentFactor, -scale * unemployment),
                new RciDemandFactor(TaxFactor, -TaxPenalty(EconomicSector.Residential))
            });

            ApplyDemand(Rci.Commercial, new[]
            {
                new RciDemandFactor(CommercialDemandGapFactor, scale * commercialDemandGap),
                new RciDemandFactor(VacantCommercialJobsFactor, -scale * vacantCommercialJobs),
                new RciDemandFactor(TaxFactor, -TaxPenalty(EconomicSector.Commercial))
            });

            ApplyDemand(Rci.Industrial, new[]
            {
                new RciDemandFactor(GoodsShortageFactor, scale * Market.GoodsShortage),
                new RciDemandFactor(GoodsSurplusFactor, -scale * Market.GoodsSurplus),
                new RciDemandFactor(TaxFactor, -TaxPenalty(EconomicSector.Industrial))
            });

            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        private void ApplyDemand(RciSectorDemand sector, RciDemandFactor[] factors)
        {
            var raw = 0;
            for (var index = 0; index < factors.Length; index++)
                raw = checked(raw + factors[index].Contribution);

            var clamped = Clamp(raw, _parameters.RciClampMagnitude);
            var smoothed = SmoothStep(
                sector.SmoothedDemand,
                clamped,
                _parameters.SmoothingNumerator,
                _parameters.SmoothingDenominator);
            sector.Set(factors, raw, clamped, smoothed);
        }

        private int TaxPenalty(EconomicSector sector)
        {
            return TaxRates.ForSector(sector) * _parameters.TaxDemandSensitivity / 1000;
        }

        private static int Clamp(int value, int magnitude)
        {
            if (value > magnitude)
                return magnitude;
            if (value < -magnitude)
                return -magnitude;
            return value;
        }

        /// <summary>
        /// Deterministic integer exponential smoothing: move <paramref name="current"/>
        /// toward <paramref name="target"/> by a fraction of the gap, truncating toward
        /// zero but always moving at least one unit while the gap is non-zero. The step
        /// magnitude never exceeds the gap, so convergence is monotonic and cannot
        /// overshoot.
        /// </summary>
        private static int SmoothStep(int current, int target, int numerator, int denominator)
        {
            var delta = target - current;
            if (delta == 0)
                return current;

            var magnitude = Math.Abs(delta) * numerator / denominator;
            if (magnitude == 0)
                magnitude = 1;
            return current + (delta > 0 ? magnitude : -magnitude);
        }
    }
}
