using System;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// UI-safe aggregate for the P3 GlobalHud. Every field is a primitive or an
    /// Application DTO, so UI assemblies never need a Simulation assembly
    /// reference to display finance, RCI, population or development diagnostics.
    /// </summary>
    [Serializable]
    public sealed class MunicipalHudSnapshotDto
    {
        public int CurrentYear;
        public int CurrentMonth;
        public bool HasSettledLedger;
        public int LedgerYear;
        public int LedgerMonth;
        public int Balance;
        public int MonthlyIncome;
        public int MonthlyExpense;
        public int ActiveResidentialTaxPermille;
        public int ActiveCommercialTaxPermille;
        public int ActiveIndustrialTaxPermille;
        public bool HasPendingTaxRateChange;
        public int PendingResidentialTaxPermille;
        public int PendingCommercialTaxPermille;
        public int PendingIndustrialTaxPermille;
        public int PendingEffectiveYear;
        public int PendingEffectiveMonth;
        public int Population;
        public int ResidentialRci;
        public int CommercialRci;
        public int IndustrialRci;
        public bool HasDevelopmentStatus;
        public DevelopmentAvailabilityStatus DevelopmentStatus;
        public int TotalFeasibleParcelCount;
        public int AverageAccessibilityFactor;
        public bool HasReachableExternalRoad;
        public bool HasConnectedPowerProducer;
        public MunicipalHudBlockReasonDto[] TopBlockReasons = Array.Empty<MunicipalHudBlockReasonDto>();
        public int ResidentialDemandChange;
        public int CommercialDemandChange;
        public int IndustrialDemandChange;
        public int FeasibleParcelChange;
        public bool ExternalRoadChanged;
        public bool PowerConnectionChanged;

        public SimulationDate CurrentDate => new SimulationDate(CurrentYear, CurrentMonth);
    }

    [Serializable]
    public sealed class MunicipalHudBlockReasonDto
    {
        public string Code;
        public int ParcelCount;
    }

    public sealed class MunicipalHudViewModel
    {
        private readonly MunicipalFinanceReadModel _finance;
        private readonly PopulationEmploymentReadModel _population;
        private readonly UrbanEconomyReadModel _urbanEconomy;
        private readonly DevelopmentStatusViewModel _development;
        private readonly Func<SimulationDate> _currentDate;

        private int _version = -1;
        private MunicipalHudSnapshotDto _snapshot;

        public MunicipalHudViewModel(
            MunicipalFinanceReadModel finance,
            PopulationEmploymentReadModel population,
            UrbanEconomyReadModel urbanEconomy,
            DevelopmentStatusViewModel development,
            Func<SimulationDate> currentDate)
        {
            _finance = finance ?? throw new ArgumentNullException(nameof(finance));
            _population = population ?? throw new ArgumentNullException(nameof(population));
            _urbanEconomy = urbanEconomy ?? throw new ArgumentNullException(nameof(urbanEconomy));
            _development = development ?? throw new ArgumentNullException(nameof(development));
            _currentDate = currentDate ?? throw new ArgumentNullException(nameof(currentDate));
        }

        public int Version
        {
            get { RefreshIfNeeded(); return _version; }
        }

        public MunicipalHudSnapshotDto Snapshot
        {
            get { RefreshIfNeeded(); return _snapshot; }
        }

        public bool RefreshIfNeeded()
        {
            var date = _currentDate();
            var financeVersion = _finance.Version;
            var populationVersion = _population.Version;
            var economyVersion = _urbanEconomy.Version;
            _development.RefreshIfNeeded();
            var developmentVersion = _development.HasSnapshot
                ? _development.Snapshot.Date.GetHashCode()
                : 0;
            var nextVersion = Combine(financeVersion, populationVersion, economyVersion, developmentVersion, date);
            if (_version == nextVersion)
                return false;

            _snapshot = Build(date);
            _version = nextVersion;
            return true;
        }

        private MunicipalHudSnapshotDto Build(SimulationDate date)
        {
            var finance = _finance.Snapshot;
            var result = new MunicipalHudSnapshotDto
            {
                CurrentYear = date.Year,
                CurrentMonth = date.Month,
                Balance = finance.Balance,
                ActiveResidentialTaxPermille = finance.ActiveResidentialTaxPermille,
                ActiveCommercialTaxPermille = finance.ActiveCommercialTaxPermille,
                ActiveIndustrialTaxPermille = finance.ActiveIndustrialTaxPermille,
                HasPendingTaxRateChange = finance.HasPendingTaxRateChange,
                PendingResidentialTaxPermille = finance.PendingResidentialTaxPermille,
                PendingCommercialTaxPermille = finance.PendingCommercialTaxPermille,
                PendingIndustrialTaxPermille = finance.PendingIndustrialTaxPermille,
                PendingEffectiveYear = finance.PendingEffectiveYear,
                PendingEffectiveMonth = finance.PendingEffectiveMonth
            };

            var ledger = finance.Ledger;
            if (ledger != null && ledger.Length > 0)
            {
                var entry = ledger[ledger.Length - 1];
                result.HasSettledLedger = true;
                result.LedgerYear = entry.Year;
                result.LedgerMonth = entry.Month;
                result.MonthlyIncome = checked(
                    entry.ResidentialTaxIncome + entry.CommercialTaxIncome + entry.IndustrialTaxIncome);
                result.MonthlyExpense = checked(
                    entry.RoadConstructionExpense + entry.PowerConstructionExpense +
                    entry.RoadMaintenanceExpense + entry.PowerMaintenanceExpense);
            }

            if (_population.HasSnapshot)
                result.Population = _population.Snapshot.Population.TotalPopulation;

            if (_urbanEconomy.HasSnapshot)
            {
                var rci = _urbanEconomy.Snapshot.Rci;
                result.ResidentialRci = rci.Residential.SmoothedDemand;
                result.CommercialRci = rci.Commercial.SmoothedDemand;
                result.IndustrialRci = rci.Industrial.SmoothedDemand;
            }

            if (_development.HasSnapshot)
                CopyDevelopment(result, _development.Snapshot);
            return result;
        }

        private static void CopyDevelopment(
            MunicipalHudSnapshotDto target,
            DevelopmentStatusSnapshotDto source)
        {
            target.HasDevelopmentStatus = true;
            target.DevelopmentStatus = source.OverallStatus;
            target.TotalFeasibleParcelCount = source.TotalFeasibleParcelCount;
            target.AverageAccessibilityFactor = source.AverageAccessibilityFactor;
            target.HasReachableExternalRoad = source.HasReachableExternalRoad;
            target.HasConnectedPowerProducer = source.HasConnectedPowerProducer;
            target.ResidentialDemandChange = source.ResidentialDemandChange;
            target.CommercialDemandChange = source.CommercialDemandChange;
            target.IndustrialDemandChange = source.IndustrialDemandChange;
            target.FeasibleParcelChange = source.FeasibleParcelChange;
            target.ExternalRoadChanged = source.ExternalRoadChanged;
            target.PowerConnectionChanged = source.PowerConnectionChanged;

            var reasons = source.TopBlockReasons ?? Array.Empty<DevelopmentBlockReasonSummaryDto>();
            var rows = new MunicipalHudBlockReasonDto[reasons.Length];
            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = new MunicipalHudBlockReasonDto
                {
                    Code = reasons[index].Reason.ToString(),
                    ParcelCount = reasons[index].ParcelCount
                };
            }

            target.TopBlockReasons = rows;
        }

        private static int Combine(int finance, int population, int economy, int development, SimulationDate date)
        {
            unchecked
            {
                var hash = finance;
                hash = hash * 397 ^ population;
                hash = hash * 397 ^ economy;
                hash = hash * 397 ^ development;
                hash = hash * 397 ^ date.Year;
                return hash * 397 ^ date.Month;
            }
        }
    }
}
