using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Simulation.Development
{
    public enum DensityStage : byte
    {
        Low,
        Medium,
        High
    }

    public enum DensityUse : byte
    {
        Residential,
        Commercial,
        Industrial
    }

    public enum DensityUpgradeBlockReason : byte
    {
        None,
        UnsupportedBuilding,
        CityPopulationTooLow,
        NoWater,
        LandValueTooLow,
        RoadTierTooLow,
        ServicesTooLow,
        AlreadyMaximum,
        UpgradeInProgress
    }

    public enum DensityUpgradeFactorKind : byte
    {
        CityPopulation,
        WaterSupply,
        LandValue,
        RoadTier,
        ServiceCoverage
    }

    [Serializable]
    public readonly struct DensityBuildingProfile
    {
        public DensityBuildingProfile(
            DensityUse use,
            int baseHousingCapacity,
            int baseJobCapacity)
        {
            if (baseHousingCapacity < 0 || baseJobCapacity < 0 ||
                (baseHousingCapacity == 0 && baseJobCapacity == 0))
                throw new ArgumentOutOfRangeException(nameof(baseHousingCapacity));
            Use = use;
            BaseHousingCapacity = baseHousingCapacity;
            BaseJobCapacity = baseJobCapacity;
        }
        public DensityUse Use { get; }
        public int BaseHousingCapacity { get; }
        public int BaseJobCapacity { get; }
    }

    public interface IDensityBuildingProfileResolver
    {
        bool TryGetDensityProfile(StableId definitionId, out DensityBuildingProfile profile);
    }

    [Serializable]
    public readonly struct DensityUpgradeFactor : IComparable<DensityUpgradeFactor>
    {
        public DensityUpgradeFactor(
            DensityUpgradeFactorKind kind,
            int currentValue,
            int requiredValue,
            bool isSatisfied)
        {
            Kind = kind;
            CurrentValue = currentValue;
            RequiredValue = requiredValue;
            IsSatisfied = isSatisfied;
        }
        public DensityUpgradeFactorKind Kind { get; }
        public int CurrentValue { get; }
        public int RequiredValue { get; }
        public bool IsSatisfied { get; }
        public int CompareTo(DensityUpgradeFactor other) => Kind.CompareTo(other.Kind);
    }

    [Serializable]
    public sealed class DensityEligibility
    {
        public DensityEligibility(
            DensityStage targetStage,
            DensityUpgradeBlockReason topReason,
            IReadOnlyList<DensityUpgradeFactor> factors)
        {
            if (factors == null) throw new ArgumentNullException(nameof(factors));
            TargetStage = targetStage;
            TopReason = topReason;
            var rows = new DensityUpgradeFactor[factors.Count];
            for (var index = 0; index < rows.Length; index++) rows[index] = factors[index];
            Array.Sort(rows);
            Factors = Array.AsReadOnly(rows);
        }
        public DensityStage TargetStage { get; }
        public DensityUpgradeBlockReason TopReason { get; }
        public IReadOnlyList<DensityUpgradeFactor> Factors { get; }
        public bool IsEligible => TopReason == DensityUpgradeBlockReason.None;
    }

    public interface IDensityEligibilityResolver
    {
        DensityEligibility Resolve(
            BuildingState building,
            DensityBuildingProfile profile,
            DensityStage currentStage,
            DensityStage targetStage);
    }

    [Serializable]
    public sealed class DensityUpgradeParameters
    {
        public DensityUpgradeParameters(
            int mediumPopulation = 20,
            int highPopulation = 80,
            int mediumWaterPermille = 800,
            int highWaterPermille = 1000,
            int mediumLandValue = 500,
            int highLandValue = 650,
            int mediumRoadTier = 1,
            int highRoadTier = 2,
            int mediumServicePermille = 500,
            int highServicePermille = 750,
            int mediumUpgradeMonths = 2,
            int highUpgradeMonths = 3,
            int maximumStartsPerMonth = 2)
        {
            if (mediumPopulation < 0 || highPopulation < mediumPopulation ||
                mediumWaterPermille < 0 || highWaterPermille < mediumWaterPermille ||
                mediumLandValue < 0 || highLandValue < mediumLandValue ||
                mediumRoadTier < 0 || highRoadTier < mediumRoadTier ||
                mediumServicePermille < 0 || highServicePermille < mediumServicePermille ||
                mediumUpgradeMonths <= 0 || highUpgradeMonths <= 0 ||
                maximumStartsPerMonth <= 0)
                throw new ArgumentOutOfRangeException(nameof(mediumPopulation));
            MediumPopulation = mediumPopulation;
            HighPopulation = highPopulation;
            MediumWaterPermille = mediumWaterPermille;
            HighWaterPermille = highWaterPermille;
            MediumLandValue = mediumLandValue;
            HighLandValue = highLandValue;
            MediumRoadTier = mediumRoadTier;
            HighRoadTier = highRoadTier;
            MediumServicePermille = mediumServicePermille;
            HighServicePermille = highServicePermille;
            MediumUpgradeMonths = mediumUpgradeMonths;
            HighUpgradeMonths = highUpgradeMonths;
            MaximumStartsPerMonth = maximumStartsPerMonth;
        }

        public int MediumPopulation { get; }
        public int HighPopulation { get; }
        public int MediumWaterPermille { get; }
        public int HighWaterPermille { get; }
        public int MediumLandValue { get; }
        public int HighLandValue { get; }
        public int MediumRoadTier { get; }
        public int HighRoadTier { get; }
        public int MediumServicePermille { get; }
        public int HighServicePermille { get; }
        public int MediumUpgradeMonths { get; }
        public int HighUpgradeMonths { get; }
        public int MaximumStartsPerMonth { get; }
        public static DensityUpgradeParameters Default => new DensityUpgradeParameters();

        public int RequiredPopulation(DensityStage target) =>
            target == DensityStage.Medium ? MediumPopulation : HighPopulation;
        public int RequiredWater(DensityStage target) =>
            target == DensityStage.Medium ? MediumWaterPermille : HighWaterPermille;
        public int RequiredLandValue(DensityStage target) =>
            target == DensityStage.Medium ? MediumLandValue : HighLandValue;
        public int RequiredRoadTier(DensityStage target) =>
            target == DensityStage.Medium ? MediumRoadTier : HighRoadTier;
        public int RequiredService(DensityStage target) =>
            target == DensityStage.Medium ? MediumServicePermille : HighServicePermille;
        public int UpgradeMonths(DensityStage target) =>
            target == DensityStage.Medium ? MediumUpgradeMonths : HighUpgradeMonths;
        public int CapacityMultiplier(DensityStage stage)
        {
            switch (stage)
            {
                case DensityStage.Low: return 1;
                case DensityStage.Medium: return 2;
                default: return 4;
            }
        }
    }

    [Serializable]
    public sealed class DensityBuildingState
    {
        private IReadOnlyList<DensityUpgradeFactor> _factors =
            Array.AsReadOnly(Array.Empty<DensityUpgradeFactor>());

        public DensityBuildingState(StableId buildingId)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Density state requires building ID.", nameof(buildingId));
            BuildingId = buildingId;
            Stage = DensityStage.Low;
            TargetStage = DensityStage.Low;
            TopReason = DensityUpgradeBlockReason.None;
        }

        public StableId BuildingId { get; }
        public DensityStage Stage { get; private set; }
        public DensityStage TargetStage { get; private set; }
        public int UpgradeMonthsRemaining { get; private set; }
        public int StageAgeMonths { get; private set; }
        public DensityUpgradeBlockReason TopReason { get; private set; }
        public IReadOnlyList<DensityUpgradeFactor> Factors => _factors;
        public bool IsUpgradeInProgress => TargetStage > Stage && UpgradeMonthsRemaining > 0;

        internal void AdvanceAge() => StageAgeMonths = checked(StageAgeMonths + 1);

        internal void SetEligibility(DensityEligibility eligibility)
        {
            TopReason = eligibility.TopReason;
            _factors = eligibility.Factors;
        }

        internal void StartUpgrade(DensityStage target, int months)
        {
            if (target <= Stage || months <= 0) throw new InvalidOperationException("Density upgrade target must advance.");
            TargetStage = target;
            UpgradeMonthsRemaining = months;
            TopReason = DensityUpgradeBlockReason.UpgradeInProgress;
        }

        internal bool ProgressUpgrade()
        {
            if (!IsUpgradeInProgress) return false;
            UpgradeMonthsRemaining--;
            if (UpgradeMonthsRemaining > 0) return false;
            Stage = TargetStage;
            TargetStage = Stage;
            StageAgeMonths = 0;
            TopReason = Stage == DensityStage.High
                ? DensityUpgradeBlockReason.AlreadyMaximum
                : DensityUpgradeBlockReason.None;
            _factors = Array.AsReadOnly(Array.Empty<DensityUpgradeFactor>());
            return true;
        }

        internal void Restore(
            DensityStage stage,
            DensityStage targetStage,
            int monthsRemaining,
            int stageAgeMonths,
            DensityUpgradeBlockReason topReason,
            IReadOnlyList<DensityUpgradeFactor> factors)
        {
            if (targetStage < stage || monthsRemaining < 0 || stageAgeMonths < 0)
                throw new ArgumentException("Invalid density state snapshot.");
            if (monthsRemaining == 0 && targetStage != stage)
                throw new ArgumentException("A non-progressing density row cannot retain a future target.");
            Stage = stage;
            TargetStage = targetStage;
            UpgradeMonthsRemaining = monthsRemaining;
            StageAgeMonths = stageAgeMonths;
            TopReason = topReason;
            var rows = new DensityUpgradeFactor[factors?.Count ?? 0];
            for (var index = 0; index < rows.Length; index++) rows[index] = factors[index];
            Array.Sort(rows);
            _factors = Array.AsReadOnly(rows);
        }
    }

    public sealed class DensityUpgradeState
    {
        private readonly SortedDictionary<StableId, DensityBuildingState> _byId =
            new SortedDictionary<StableId, DensityBuildingState>();
        public IReadOnlyList<DensityBuildingState> Buildings =>
            new List<DensityBuildingState>(_byId.Values).AsReadOnly();
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int Version { get; private set; }

        public bool TryGet(StableId id, out DensityBuildingState state) => _byId.TryGetValue(id, out state);
        internal DensityBuildingState GetOrAdd(StableId id)
        {
            if (_byId.TryGetValue(id, out var state)) return state;
            state = new DensityBuildingState(id);
            _byId.Add(id, state);
            Version = checked(Version + 1);
            return state;
        }
        internal void RemoveMissing(ISet<StableId> existing)
        {
            var remove = new List<StableId>();
            foreach (var pair in _byId)
            {
                if (!existing.Contains(pair.Key)) remove.Add(pair.Key);
            }
            for (var index = 0; index < remove.Count; index++) _byId.Remove(remove[index]);
            if (remove.Count > 0) Version = checked(Version + 1);
        }
        internal void CompleteMonth(SimulationDate month)
        {
            HasProcessedMonth = true;
            LastProcessedMonth = month;
            Version = checked(Version + 1);
        }
        internal void ClearAndRestore(IEnumerable<DensityBuildingState> rows, bool hasMonth, SimulationDate month)
        {
            _byId.Clear();
            foreach (var row in rows)
            {
                if (row == null || _byId.ContainsKey(row.BuildingId))
                    throw new ArgumentException("Density rows require unique IDs.", nameof(rows));
                _byId.Add(row.BuildingId, row);
            }
            HasProcessedMonth = hasMonth;
            LastProcessedMonth = hasMonth ? month : default;
            Version = _byId.Count > 0 || hasMonth ? 1 : 0;
        }
    }

    public sealed class DensityUpgradeSystem : IMonthlySimulationSystem
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private readonly BuildingStateCollection _buildings;
        private readonly IDensityBuildingProfileResolver _profiles;
        private readonly IDensityEligibilityResolver _eligibility;
        private readonly DensityUpgradeParameters _parameters;

        public DensityUpgradeSystem(
            BuildingStateCollection buildings,
            IDensityBuildingProfileResolver profiles,
            IDensityEligibilityResolver eligibility,
            DensityUpgradeParameters parameters = null)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            _parameters = parameters ?? DensityUpgradeParameters.Default;
            State = new DensityUpgradeState();
        }

        public DensityUpgradeState State { get; }
        public DensityUpgradeParameters Parameters => _parameters;

        public void RunMonth(SimulationDate month)
        {
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Density processing requires strictly increasing months.");

            var existing = new SortedSet<StableId>();
            var buildings = _buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                existing.Add(buildings[index].Id);
                if (_profiles.TryGetDensityProfile(buildings[index].DefinitionId, out _))
                    State.GetOrAdd(buildings[index].Id);
            }
            State.RemoveMissing(existing);

            var completedThisMonth = new HashSet<StableId>();
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (!State.TryGet(building.Id, out var state) ||
                    !_profiles.TryGetDensityProfile(building.DefinitionId, out var profile))
                    continue;
                state.AdvanceAge();
                if (!state.IsUpgradeInProgress) continue;
                var eligibility = _eligibility.Resolve(building, profile, state.Stage, state.TargetStage);
                state.SetEligibility(eligibility);
                if (!eligibility.IsEligible) continue;
                if (state.ProgressUpgrade()) completedThisMonth.Add(building.Id);
            }

            var starts = 0;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (completedThisMonth.Contains(building.Id) ||
                    !State.TryGet(building.Id, out var state) ||
                    !_profiles.TryGetDensityProfile(building.DefinitionId, out var profile))
                    continue;
                if (state.IsUpgradeInProgress) continue;
                if (state.Stage == DensityStage.High)
                {
                    state.SetEligibility(new DensityEligibility(
                        DensityStage.High,
                        DensityUpgradeBlockReason.AlreadyMaximum,
                        Array.AsReadOnly(Array.Empty<DensityUpgradeFactor>())));
                    continue;
                }
                var target = state.Stage == DensityStage.Low ? DensityStage.Medium : DensityStage.High;
                var eligibility = _eligibility.Resolve(building, profile, state.Stage, target);
                state.SetEligibility(eligibility);
                if (!eligibility.IsEligible || starts >= _parameters.MaximumStartsPerMonth) continue;
                state.StartUpgrade(target, _parameters.UpgradeMonths(target));
                starts++;
            }
            State.CompleteMonth(month);
        }

        public int CapacityMultiplier(StableId buildingId)
        {
            return State.TryGet(buildingId, out var state)
                ? _parameters.CapacityMultiplier(state.Stage)
                : 1;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, State.HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, State.HasProcessedMonth ? State.LastProcessedMonth.Year : 0);
            AddInt(ref hash, State.HasProcessedMonth ? State.LastProcessedMonth.Month : 0);
            var rows = State.Buildings;
            AddInt(ref hash, rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                AddUlong(ref hash, row.BuildingId.Value);
                AddByte(ref hash, (byte)row.Stage);
                AddByte(ref hash, (byte)row.TargetStage);
                AddInt(ref hash, row.UpgradeMonthsRemaining);
                AddInt(ref hash, row.StageAgeMonths);
                AddByte(ref hash, (byte)row.TopReason);
                AddInt(ref hash, row.Factors.Count);
                for (var factorIndex = 0; factorIndex < row.Factors.Count; factorIndex++)
                {
                    var factor = row.Factors[factorIndex];
                    AddByte(ref hash, (byte)factor.Kind);
                    AddInt(ref hash, factor.CurrentValue);
                    AddInt(ref hash, factor.RequiredValue);
                    AddByte(ref hash, factor.IsSatisfied ? (byte)1 : (byte)0);
                }
            }
            return hash;
        }

        private static void AddByte(ref ulong hash, byte value) { hash ^= value; hash *= FnvPrime64; }
        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value); AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16)); AddByte(ref hash, (byte)(value >> 24));
            }
        }
        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(value >> shift));
        }
    }

    [Serializable]
    public sealed class DensityUpgradeFactorDto
    {
        public int Kind;
        public int CurrentValue;
        public int RequiredValue;
        public bool IsSatisfied;
    }

    [Serializable]
    public sealed class DensityBuildingStateDto
    {
        public ulong BuildingId;
        public int Stage;
        public int TargetStage;
        public int UpgradeMonthsRemaining;
        public int StageAgeMonths;
        public int TopReason;
        public DensityUpgradeFactorDto[] Factors = Array.Empty<DensityUpgradeFactorDto>();
    }

    [Serializable]
    public sealed class DensityRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public bool HasProcessedMonth;
        public int LastProcessedYear;
        public int LastProcessedMonth;
        public DensityBuildingStateDto[] Buildings = Array.Empty<DensityBuildingStateDto>();
    }
}
