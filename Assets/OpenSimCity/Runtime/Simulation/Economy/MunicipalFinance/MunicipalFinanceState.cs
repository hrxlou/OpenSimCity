using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>Expense categories that are accrued into one deterministic monthly municipal ledger row.</summary>
    public enum MunicipalConstructionCategory : byte
    {
        Road = 1,
        PowerProducer = 2
    }

    /// <summary>
    /// One settled municipal month. Construction is charged immediately by its
    /// command, but remains in this row so opening balance plus signed totals
    /// always equals closing balance.
    /// </summary>
    [Serializable]
    public sealed class MunicipalMonthlyLedgerEntry
    {
        public MunicipalMonthlyLedgerEntry(
            SimulationDate month,
            int openingBalance,
            int residentialTaxIncome,
            int commercialTaxIncome,
            int industrialTaxIncome,
            int roadConstructionExpense,
            int powerConstructionExpense,
            int roadMaintenanceExpense,
            int powerMaintenanceExpense,
            int closingBalance)
        {
            Month = month;
            OpeningBalance = openingBalance;
            ResidentialTaxIncome = residentialTaxIncome;
            CommercialTaxIncome = commercialTaxIncome;
            IndustrialTaxIncome = industrialTaxIncome;
            RoadConstructionExpense = roadConstructionExpense;
            PowerConstructionExpense = powerConstructionExpense;
            RoadMaintenanceExpense = roadMaintenanceExpense;
            PowerMaintenanceExpense = powerMaintenanceExpense;
            ClosingBalance = closingBalance;
        }

        public SimulationDate Month { get; }
        public int OpeningBalance { get; }
        public int ResidentialTaxIncome { get; }
        public int CommercialTaxIncome { get; }
        public int IndustrialTaxIncome { get; }
        public int RoadConstructionExpense { get; }
        public int PowerConstructionExpense { get; }
        public int RoadMaintenanceExpense { get; }
        public int PowerMaintenanceExpense { get; }
        public int ClosingBalance { get; }

        public int TaxIncome => checked(ResidentialTaxIncome + CommercialTaxIncome + IndustrialTaxIncome);
        public int ConstructionExpense => checked(RoadConstructionExpense + PowerConstructionExpense);
        public int MaintenanceExpense => checked(RoadMaintenanceExpense + PowerMaintenanceExpense);
        public int TotalExpense => checked(ConstructionExpense + MaintenanceExpense);
        public int NetChange => checked(TaxIncome - TotalExpense);

        public MunicipalMonthlyLedgerEntryDto ToDto()
        {
            return new MunicipalMonthlyLedgerEntryDto
            {
                Year = Month.Year,
                Month = Month.Month,
                OpeningBalance = OpeningBalance,
                ResidentialTaxIncome = ResidentialTaxIncome,
                CommercialTaxIncome = CommercialTaxIncome,
                IndustrialTaxIncome = IndustrialTaxIncome,
                RoadConstructionExpense = RoadConstructionExpense,
                PowerConstructionExpense = PowerConstructionExpense,
                RoadMaintenanceExpense = RoadMaintenanceExpense,
                PowerMaintenanceExpense = PowerMaintenanceExpense,
                ClosingBalance = ClosingBalance
            };
        }
    }

    [Serializable]
    public sealed class MunicipalMonthlyLedgerEntryDto
    {
        public int Year;
        public int Month;
        public int OpeningBalance;
        public int ResidentialTaxIncome;
        public int CommercialTaxIncome;
        public int IndustrialTaxIncome;
        public int RoadConstructionExpense;
        public int PowerConstructionExpense;
        public int RoadMaintenanceExpense;
        public int PowerMaintenanceExpense;
        public int ClosingBalance;
    }

    /// <summary>
    /// City-owned fiscal state. It implements the existing construction-funds port
    /// so public construction commands share one balance, while private market
    /// state remains in <see cref="UrbanEconomySystem"/>.
    /// </summary>
    [Serializable]
    public sealed class MunicipalFinanceState : IConstructionFunds
    {
        private readonly List<MunicipalMonthlyLedgerEntry> _ledger =
            new List<MunicipalMonthlyLedgerEntry>();
        private readonly IReadOnlyList<MunicipalMonthlyLedgerEntry> _readOnlyLedger;

        private int _periodOpeningBalance;
        private int _roadConstructionExpense;
        private int _powerConstructionExpense;

        public MunicipalFinanceState(
            int initialBalance,
            UrbanEconomyTaxRates initialTaxRates = default(UrbanEconomyTaxRates))
        {
            if (initialBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(initialBalance), "The starting municipal balance cannot be negative.");

            Balance = initialBalance;
            _periodOpeningBalance = initialBalance;
            ActiveTaxRates = initialTaxRates;
            _readOnlyLedger = _ledger.AsReadOnly();
        }

        public int Balance { get; private set; }

        /// <summary>Compatibility name for the pre-P3 construction budget seam.</summary>
        public int Funds => Balance;

        public int AvailableFunds => Balance;

        public UrbanEconomyTaxRates ActiveTaxRates { get; private set; }

        public bool HasPendingTaxRateChange { get; private set; }

        public UrbanEconomyTaxRates PendingTaxRates { get; private set; }

        public SimulationDate PendingTaxEffectiveMonth { get; private set; }

        public bool HasSettledMonth => _ledger.Count > 0;

        public SimulationDate LastSettledMonth { get; private set; }

        public int Version { get; private set; }

        public IReadOnlyList<MunicipalMonthlyLedgerEntry> Ledger => _readOnlyLedger;

        public MunicipalMonthlyLedgerEntry LatestLedgerEntry =>
            _ledger.Count == 0 ? null : _ledger[_ledger.Count - 1];

        public bool CanAfford(int cost) => cost >= 0 && Balance >= cost;

        /// <summary>Debits an approved construction command; its category is accrued separately on commit.</summary>
        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost))
                return false;

            Balance = checked(Balance - cost);
            Version = checked(Version + 1);
            return true;
        }

        /// <summary>Restores an unexpected failed construction commit atomically.</summary>
        public bool TryRefund(int amount)
        {
            if (amount < 0 || (long)Balance + amount > int.MaxValue)
                return false;

            Balance += amount;
            Version = checked(Version + 1);
            return true;
        }

        public void RecordConstructionExpense(MunicipalConstructionCategory category, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            switch (category)
            {
                case MunicipalConstructionCategory.Road:
                    _roadConstructionExpense = checked(_roadConstructionExpense + amount);
                    break;
                case MunicipalConstructionCategory.PowerProducer:
                    _powerConstructionExpense = checked(_powerConstructionExpense + amount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }

            Version = checked(Version + 1);
        }

        /// <summary>
        /// Replaces a not-yet-effective tax request. The requested rate is applied
        /// only when the scheduler reaches <paramref name="effectiveMonth"/>.
        /// </summary>
        public void ScheduleTaxRates(UrbanEconomyTaxRates rates, SimulationDate effectiveMonth)
        {
            if (HasSettledMonth && effectiveMonth.CompareTo(LastSettledMonth) <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectiveMonth), "Tax rates must take effect in a future month.");

            PendingTaxRates = rates;
            PendingTaxEffectiveMonth = effectiveMonth;
            HasPendingTaxRateChange = true;
            Version = checked(Version + 1);
        }

        /// <summary>Called before the market/RCI pass for the given monthly period.</summary>
        public bool ApplyTaxRatesForMonth(SimulationDate month)
        {
            if (!HasPendingTaxRateChange || month.CompareTo(PendingTaxEffectiveMonth) < 0)
                return false;

            ActiveTaxRates = PendingTaxRates;
            PendingTaxRates = default(UrbanEconomyTaxRates);
            PendingTaxEffectiveMonth = default(SimulationDate);
            HasPendingTaxRateChange = false;
            Version = checked(Version + 1);
            return true;
        }

        /// <summary>
        /// Settles one strictly increasing month. Construction has already reduced
        /// <see cref="Balance"/>, so the ledger invariant includes its accrued
        /// amount without debiting it a second time.
        /// </summary>
        public MunicipalMonthlyLedgerEntry SettleMonth(
            SimulationDate month,
            int residentialTaxIncome,
            int commercialTaxIncome,
            int industrialTaxIncome,
            int roadMaintenanceExpense,
            int powerMaintenanceExpense)
        {
            if (HasSettledMonth && month.CompareTo(LastSettledMonth) <= 0)
                throw new InvalidOperationException("Municipal ledger settlement requires strictly increasing months.");
            if (residentialTaxIncome < 0 || commercialTaxIncome < 0 || industrialTaxIncome < 0 ||
                roadMaintenanceExpense < 0 || powerMaintenanceExpense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(residentialTaxIncome), "Ledger inputs must be non-negative.");
            }

            var expectedPreSettlement = checked(
                _periodOpeningBalance - _roadConstructionExpense - _powerConstructionExpense);
            if (Balance != expectedPreSettlement)
            {
                throw new InvalidOperationException(
                    "Municipal balance changed without a classified construction ledger entry.");
            }

            var income = checked(residentialTaxIncome + commercialTaxIncome + industrialTaxIncome);
            var maintenance = checked(roadMaintenanceExpense + powerMaintenanceExpense);
            Balance = checked(Balance + income - maintenance);

            var entry = new MunicipalMonthlyLedgerEntry(
                month,
                _periodOpeningBalance,
                residentialTaxIncome,
                commercialTaxIncome,
                industrialTaxIncome,
                _roadConstructionExpense,
                _powerConstructionExpense,
                roadMaintenanceExpense,
                powerMaintenanceExpense,
                Balance);

            if (entry.ClosingBalance != checked(entry.OpeningBalance + entry.NetChange))
                throw new InvalidOperationException("Municipal ledger balance invariant failed.");

            _ledger.Add(entry);
            LastSettledMonth = month;
            _periodOpeningBalance = Balance;
            _roadConstructionExpense = 0;
            _powerConstructionExpense = 0;
            Version = checked(Version + 1);
            return entry;
        }

        public MunicipalFinanceStateDto ToDto()
        {
            var rows = new MunicipalMonthlyLedgerEntryDto[_ledger.Count];
            for (var index = 0; index < rows.Length; index++)
                rows[index] = _ledger[index].ToDto();

            return new MunicipalFinanceStateDto
            {
                Balance = Balance,
                ActiveResidentialTaxPermille = ActiveTaxRates.ResidentialPermille,
                ActiveCommercialTaxPermille = ActiveTaxRates.CommercialPermille,
                ActiveIndustrialTaxPermille = ActiveTaxRates.IndustrialPermille,
                HasPendingTaxRateChange = HasPendingTaxRateChange,
                PendingResidentialTaxPermille = PendingTaxRates.ResidentialPermille,
                PendingCommercialTaxPermille = PendingTaxRates.CommercialPermille,
                PendingIndustrialTaxPermille = PendingTaxRates.IndustrialPermille,
                PendingEffectiveYear = HasPendingTaxRateChange ? PendingTaxEffectiveMonth.Year : 0,
                PendingEffectiveMonth = HasPendingTaxRateChange ? PendingTaxEffectiveMonth.Month : 0,
                Ledger = rows
            };
        }
    }

    [Serializable]
    public sealed class MunicipalFinanceStateDto
    {
        public int Balance;
        public int ActiveResidentialTaxPermille;
        public int ActiveCommercialTaxPermille;
        public int ActiveIndustrialTaxPermille;
        public bool HasPendingTaxRateChange;
        public int PendingResidentialTaxPermille;
        public int PendingCommercialTaxPermille;
        public int PendingIndustrialTaxPermille;
        public int PendingEffectiveYear;
        public int PendingEffectiveMonth;
        public MunicipalMonthlyLedgerEntryDto[] Ledger = Array.Empty<MunicipalMonthlyLedgerEntryDto>();
    }
}
