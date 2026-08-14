using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Population;

namespace OpenSimCity.Simulation.Conditions
{
    [Serializable]
    public sealed class BuildingConditionState
    {
        private BuildingConditionFactor[] _factors = Array.Empty<BuildingConditionFactor>();
        private IReadOnlyList<BuildingConditionFactor> _readOnlyFactors =
            Array.AsReadOnly(Array.Empty<BuildingConditionFactor>());

        public BuildingConditionState(StableId buildingId)
        {
            if (!buildingId.IsValid)
                throw new ArgumentException("Condition state requires a building ID.", nameof(buildingId));
            BuildingId = buildingId;
            Stage = BuildingConditionStage.Healthy;
            RedevelopmentBlockReason = RedevelopmentBlockReason.NotAbandoned;
        }

        public StableId BuildingId { get; }
        public BuildingConditionStage Stage { get; private set; }
        public int AgeMonths { get; private set; }
        public int AdverseMonths { get; private set; }
        public int RecoveryMonths { get; private set; }
        public int RedevelopmentMonthsRemaining { get; private set; }
        public int LastAdverseScore { get; private set; }
        public BuildingConditionReason TopReason { get; private set; }
        public bool IsRedevelopmentEligible { get; private set; }
        public RedevelopmentBlockReason RedevelopmentBlockReason { get; private set; }
        public IReadOnlyList<BuildingConditionFactor> Factors => _readOnlyFactors;
        public int Version { get; private set; }

        internal void ApplyMonth(
            BuildingConditionStage stage,
            int ageMonths,
            int adverseMonths,
            int recoveryMonths,
            int redevelopmentMonthsRemaining,
            int adverseScore,
            BuildingConditionReason topReason,
            bool redevelopmentEligible,
            RedevelopmentBlockReason redevelopmentBlockReason,
            IReadOnlyList<BuildingConditionFactor> factors)
        {
            if (ageMonths < 0 || adverseMonths < 0 || recoveryMonths < 0 ||
                redevelopmentMonthsRemaining < 0 || adverseScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ageMonths));
            }
            var factorArray = new BuildingConditionFactor[factors?.Count ?? 0];
            if (factors != null)
            {
                for (var index = 0; index < factorArray.Length; index++)
                    factorArray[index] = factors[index];
            }

            Stage = stage;
            AgeMonths = ageMonths;
            AdverseMonths = adverseMonths;
            RecoveryMonths = recoveryMonths;
            RedevelopmentMonthsRemaining = redevelopmentMonthsRemaining;
            LastAdverseScore = adverseScore;
            TopReason = topReason;
            IsRedevelopmentEligible = redevelopmentEligible;
            RedevelopmentBlockReason = redevelopmentBlockReason;
            _factors = factorArray;
            _readOnlyFactors = Array.AsReadOnly(_factors);
            Version = checked(Version + 1);
        }

        internal void BeginRedevelopment(int durationMonths)
        {
            if (!IsRedevelopmentEligible || Stage != BuildingConditionStage.Abandoned)
                throw new InvalidOperationException("Building is not eligible for redevelopment.");
            if (durationMonths < 1)
                throw new ArgumentOutOfRangeException(nameof(durationMonths));
            Stage = BuildingConditionStage.Redeveloping;
            RedevelopmentMonthsRemaining = durationMonths;
            IsRedevelopmentEligible = false;
            RedevelopmentBlockReason = RedevelopmentBlockReason.None;
            RecoveryMonths = 0;
            Version = checked(Version + 1);
        }

        public BuildingConditionStateDto ToDto()
        {
            var factors = new BuildingConditionFactorDto[_factors.Length];
            for (var index = 0; index < factors.Length; index++)
            {
                factors[index] = new BuildingConditionFactorDto
                {
                    Reason = (int)_factors[index].Reason,
                    Contribution = _factors[index].Contribution
                };
            }
            return new BuildingConditionStateDto
            {
                BuildingId = BuildingId.Value,
                Stage = (int)Stage,
                AgeMonths = AgeMonths,
                AdverseMonths = AdverseMonths,
                RecoveryMonths = RecoveryMonths,
                RedevelopmentMonthsRemaining = RedevelopmentMonthsRemaining,
                LastAdverseScore = LastAdverseScore,
                TopReason = (int)TopReason,
                IsRedevelopmentEligible = IsRedevelopmentEligible,
                RedevelopmentBlockReason = (int)RedevelopmentBlockReason,
                Factors = factors
            };
        }

        public static BuildingConditionState FromDto(BuildingConditionStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            if (dto.BuildingId == 0 ||
                !IsDefinedByteEnum<BuildingConditionStage>(dto.Stage) ||
                !IsDefinedByteEnum<BuildingConditionReason>(dto.TopReason) ||
                !IsDefinedByteEnum<RedevelopmentBlockReason>(dto.RedevelopmentBlockReason))
            {
                throw new ArgumentException("Condition state DTO contains an unknown identity or enum.", nameof(dto));
            }
            var factorsSource = dto.Factors ?? Array.Empty<BuildingConditionFactorDto>();
            var factors = new BuildingConditionFactor[factorsSource.Length];
            for (var index = 0; index < factors.Length; index++)
            {
                var factor = factorsSource[index] ??
                             throw new ArgumentException("Condition factors cannot contain null.", nameof(dto));
                if (!IsDefinedByteEnum<BuildingConditionReason>(factor.Reason) ||
                    factor.Reason == (int)BuildingConditionReason.None)
                {
                    throw new ArgumentException("Condition factor uses an unknown reason.", nameof(dto));
                }
                factors[index] = new BuildingConditionFactor(
                    (BuildingConditionReason)factor.Reason,
                    factor.Contribution);
            }

            var state = new BuildingConditionState(new StableId(dto.BuildingId));
            state.ApplyMonth(
                (BuildingConditionStage)dto.Stage,
                dto.AgeMonths,
                dto.AdverseMonths,
                dto.RecoveryMonths,
                dto.RedevelopmentMonthsRemaining,
                dto.LastAdverseScore,
                (BuildingConditionReason)dto.TopReason,
                dto.IsRedevelopmentEligible,
                (RedevelopmentBlockReason)dto.RedevelopmentBlockReason,
                factors);
            return state;
        }

        private static bool IsDefinedByteEnum<TEnum>(int value) where TEnum : struct
        {
            if (value < byte.MinValue || value > byte.MaxValue)
                return false;
            return Enum.IsDefined(typeof(TEnum), Enum.ToObject(typeof(TEnum), (byte)value));
        }
    }

    public sealed class BuildingConditionStateCollection
    {
        private readonly SortedDictionary<StableId, BuildingConditionState> _states =
            new SortedDictionary<StableId, BuildingConditionState>();

        public IReadOnlyList<BuildingConditionState> States =>
            new List<BuildingConditionState>(_states.Values).AsReadOnly();
        public int Count => _states.Count;
        public int Version { get; private set; }

        public bool TryGet(StableId buildingId, out BuildingConditionState state) =>
            _states.TryGetValue(buildingId, out state);

        internal BuildingConditionState GetOrAdd(StableId buildingId)
        {
            if (_states.TryGetValue(buildingId, out var existing))
                return existing;
            var created = new BuildingConditionState(buildingId);
            _states.Add(buildingId, created);
            Version = checked(Version + 1);
            return created;
        }

        internal void RemoveMissing(ISet<StableId> liveBuildingIds)
        {
            var removed = new List<StableId>();
            foreach (var pair in _states)
            {
                if (!liveBuildingIds.Contains(pair.Key))
                    removed.Add(pair.Key);
            }
            for (var index = 0; index < removed.Count; index++)
                _states.Remove(removed[index]);
            if (removed.Count > 0)
                Version = checked(Version + 1);
        }

        internal void Touch() => Version = checked(Version + 1);

        internal void Restore(IEnumerable<BuildingConditionState> states)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            _states.Clear();
            foreach (var state in states)
            {
                if (state == null || _states.ContainsKey(state.BuildingId))
                    throw new ArgumentException("Condition states require unique building IDs.", nameof(states));
                _states.Add(state.BuildingId, state);
            }
            Version = _states.Count > 0 ? 1 : 0;
        }
    }

    /// <summary>
    /// Monthly delayed condition transitions plus the effective-capacity resolver
    /// consumed by Population in the same scheduler period.
    /// </summary>
    public sealed class BuildingConditionSystem : IMonthlySimulationSystem, IBuildingCapacityResolver
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly BuildingStateCollection _buildings;
        private readonly IBuildingConditionProfileResolver _profiles;
        private readonly IBuildingConditionEnvironmentResolver _environment;
        private readonly BuildingConditionParameters _parameters;

        public BuildingConditionSystem(
            BuildingStateCollection buildings,
            IBuildingConditionProfileResolver profiles,
            IBuildingConditionEnvironmentResolver environment,
            BuildingConditionParameters parameters = null)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _parameters = parameters ?? BuildingConditionParameters.Default;
            State = new BuildingConditionStateCollection();
        }

        public BuildingConditionStateCollection State { get; }
        public BuildingConditionParameters Parameters => _parameters;
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int Version { get; private set; }

        public void RunMonth(SimulationDate month)
        {
            if (HasProcessedMonth && month.CompareTo(LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Condition processing requires a strictly increasing month.");

            var live = new SortedSet<StableId>();
            var buildings = _buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                live.Add(building.Id);
                if (!_profiles.TryGetConditionProfile(building.DefinitionId, out var profile))
                    throw new InvalidOperationException($"Building {building.Id} has no condition profile.");
                var state = State.GetOrAdd(building.Id);
                ProcessBuilding(state, building, profile, _environment.Resolve(building));
            }
            State.RemoveMissing(live);
            State.Touch();
            HasProcessedMonth = true;
            LastProcessedMonth = month;
            Version = checked(Version + 1);
        }

        public bool TryRequestRedevelopment(StableId buildingId, out RedevelopmentBlockReason reason)
        {
            if (!State.TryGet(buildingId, out var state))
            {
                reason = RedevelopmentBlockReason.NotAbandoned;
                return false;
            }
            if (state.Stage == BuildingConditionStage.Redeveloping)
            {
                reason = RedevelopmentBlockReason.AlreadyRedeveloping;
                return false;
            }
            if (!state.IsRedevelopmentEligible || state.Stage != BuildingConditionStage.Abandoned)
            {
                reason = state.RedevelopmentBlockReason;
                return false;
            }
            state.BeginRedevelopment(_parameters.RedevelopmentDurationMonths);
            State.Touch();
            Version = checked(Version + 1);
            reason = RedevelopmentBlockReason.None;
            return true;
        }

        public int ResolveHousingCapacity(BuildingState building, int definitionCapacity)
        {
            return ApplyCapacity(building, definitionCapacity);
        }

        public int ResolveJobCapacity(BuildingState building, int definitionCapacity)
        {
            return ApplyCapacity(building, definitionCapacity);
        }

        public BuildingConditionRuntimeSnapshotDto ToDto()
        {
            var source = State.States;
            var rows = new BuildingConditionStateDto[source.Count];
            for (var index = 0; index < rows.Length; index++)
                rows[index] = source[index].ToDto();
            return new BuildingConditionRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                HasProcessedMonth = HasProcessedMonth,
                LastProcessedYear = HasProcessedMonth ? LastProcessedMonth.Year : 0,
                LastProcessedMonth = HasProcessedMonth ? LastProcessedMonth.Month : 0,
                States = rows
            };
        }

        public void RestoreFromDto(BuildingConditionRuntimeSnapshotDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            if (dto.SchemaVersion != 1)
                throw new InvalidOperationException("Unsupported building-condition snapshot schema version.");
            var rows = dto.States ?? Array.Empty<BuildingConditionStateDto>();
            var states = new List<BuildingConditionState>(rows.Length);
            ulong previous = 0;
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index] ?? throw new ArgumentException("Condition rows cannot contain null.", nameof(dto));
                if (row.BuildingId == 0 || row.BuildingId <= previous)
                    throw new ArgumentException("Condition rows must be stable-ID ordered and unique.", nameof(dto));
                states.Add(BuildingConditionState.FromDto(row));
                previous = row.BuildingId;
            }
            State.Restore(states);
            HasProcessedMonth = dto.HasProcessedMonth;
            LastProcessedMonth = dto.HasProcessedMonth
                ? new SimulationDate(dto.LastProcessedYear, dto.LastProcessedMonth)
                : default(SimulationDate);
            Version = dto.HasProcessedMonth || states.Count > 0 ? 1 : 0;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Year : 0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Month : 0);
            var states = State.States;
            AddInt(ref hash, states.Count);
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                AddUlong(ref hash, state.BuildingId.Value);
                AddByte(ref hash, (byte)state.Stage);
                AddInt(ref hash, state.AgeMonths);
                AddInt(ref hash, state.AdverseMonths);
                AddInt(ref hash, state.RecoveryMonths);
                AddInt(ref hash, state.RedevelopmentMonthsRemaining);
                AddInt(ref hash, state.LastAdverseScore);
                AddByte(ref hash, (byte)state.TopReason);
                AddByte(ref hash, state.IsRedevelopmentEligible ? (byte)1 : (byte)0);
                AddByte(ref hash, (byte)state.RedevelopmentBlockReason);
                AddInt(ref hash, state.Factors.Count);
                for (var factorIndex = 0; factorIndex < state.Factors.Count; factorIndex++)
                {
                    AddByte(ref hash, (byte)state.Factors[factorIndex].Reason);
                    AddInt(ref hash, state.Factors[factorIndex].Contribution);
                }
            }
            return hash;
        }

        private void ProcessBuilding(
            BuildingConditionState state,
            BuildingState building,
            BuildingConditionProfile profile,
            BuildingConditionEnvironment environment)
        {
            var ignoreOccupancy = state.Stage == BuildingConditionStage.Abandoned ||
                                  state.Stage == BuildingConditionStage.Redeveloping ||
                                  state.AgeMonths < _parameters.NewBuildingGraceMonths;
            var factors = EvaluateFactors(building, profile, environment, ignoreOccupancy, out var score);
            var topReason = factors.Count == 0 ? BuildingConditionReason.None : factors[0].Reason;
            var adverse = score >= _parameters.StressScoreThreshold;
            var severe = score >= _parameters.SevereScoreThreshold;
            var recoveredEnvironment = score < _parameters.RecoveryScoreThreshold;

            var stage = state.Stage;
            var age = checked(state.AgeMonths + 1);
            var adverseMonths = state.AdverseMonths;
            var recoveryMonths = state.RecoveryMonths;
            var remaining = state.RedevelopmentMonthsRemaining;
            var eligible = false;
            var block = RedevelopmentBlockReason.NotAbandoned;

            switch (stage)
            {
                case BuildingConditionStage.Healthy:
                    recoveryMonths = 0;
                    adverseMonths = adverse ? checked(adverseMonths + 1) : 0;
                    if (adverseMonths >= _parameters.MonthsToStress)
                        stage = BuildingConditionStage.Stressed;
                    break;

                case BuildingConditionStage.Stressed:
                    if (adverse)
                    {
                        adverseMonths = checked(adverseMonths + 1);
                        recoveryMonths = 0;
                        if (adverseMonths >= _parameters.MonthsToDecline)
                            stage = BuildingConditionStage.Declining;
                    }
                    else
                    {
                        recoveryMonths = checked(recoveryMonths + 1);
                        if (recoveryMonths >= _parameters.RecoveryMonthsPerStep)
                        {
                            stage = BuildingConditionStage.Healthy;
                            adverseMonths = 0;
                            recoveryMonths = 0;
                        }
                    }
                    break;

                case BuildingConditionStage.Declining:
                    if (adverse || severe)
                    {
                        adverseMonths = checked(adverseMonths + 1);
                        recoveryMonths = 0;
                        if (adverseMonths >= _parameters.MonthsToAbandon)
                        {
                            stage = BuildingConditionStage.Abandoned;
                            adverseMonths = 0;
                            recoveryMonths = 0;
                        }
                    }
                    else
                    {
                        recoveryMonths = checked(recoveryMonths + 1);
                        if (recoveryMonths >= _parameters.RecoveryMonthsPerStep)
                        {
                            stage = BuildingConditionStage.Stressed;
                            adverseMonths = 0;
                            recoveryMonths = 0;
                        }
                    }
                    break;

                case BuildingConditionStage.Abandoned:
                    adverseMonths = 0;
                    block = ResolveRedevelopmentBlock(environment, recoveredEnvironment);
                    if (block == RedevelopmentBlockReason.RecoveryDelay)
                    {
                        recoveryMonths = checked(recoveryMonths + 1);
                        if (recoveryMonths >= _parameters.RedevelopmentRecoveryMonths)
                        {
                            eligible = true;
                            block = RedevelopmentBlockReason.None;
                        }
                    }
                    else
                    {
                        recoveryMonths = 0;
                    }
                    break;

                case BuildingConditionStage.Redeveloping:
                    adverseMonths = 0;
                    recoveryMonths = 0;
                    block = ResolveRedevelopmentBlock(environment, recoveredEnvironment);
                    if (block == RedevelopmentBlockReason.RecoveryDelay)
                        block = RedevelopmentBlockReason.None;
                    if (block == RedevelopmentBlockReason.None)
                    {
                        remaining = Math.Max(0, remaining - 1);
                        if (remaining == 0)
                        {
                            stage = BuildingConditionStage.Healthy;
                            age = 0;
                            score = 0;
                            topReason = BuildingConditionReason.None;
                            factors = Array.AsReadOnly(Array.Empty<BuildingConditionFactor>());
                            block = RedevelopmentBlockReason.NotAbandoned;
                        }
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (stage == BuildingConditionStage.Abandoned && block == RedevelopmentBlockReason.NotAbandoned)
                block = ResolveRedevelopmentBlock(environment, recoveredEnvironment);

            state.ApplyMonth(
                stage,
                age,
                adverseMonths,
                recoveryMonths,
                remaining,
                score,
                topReason,
                eligible,
                block,
                factors);
        }

        private IReadOnlyList<BuildingConditionFactor> EvaluateFactors(
            BuildingState building,
            BuildingConditionProfile profile,
            BuildingConditionEnvironment environment,
            bool ignoreOccupancy,
            out int score)
        {
            var factors = new List<BuildingConditionFactor>();
            AddFactor(factors, BuildingConditionReason.NoRoadAccess, environment.HasRoadAccess ? 0 : 500);
            AddFactor(factors, BuildingConditionReason.NoPower, environment.HasPower ? 0 : 500);
            AddFactor(factors, BuildingConditionReason.Pollution, Math.Max(0, environment.Pollution - 200));
            AddFactor(factors, BuildingConditionReason.Crime, Math.Max(0, environment.Crime - 200));
            AddFactor(factors, BuildingConditionReason.Noise, Math.Max(0, environment.Noise - 250));
            AddFactor(factors, BuildingConditionReason.LowLandValue, Math.Max(0, 400 - environment.LandValue));
            if (!ignoreOccupancy)
            {
                var capacity = Math.Max(1, profile.RelevantCapacity);
                var current = profile.Use == BuildingConditionUse.Residential
                    ? building.Occupancy
                    : building.FilledJobs;
                var occupancyPermille = Math.Min(1000, checked(current * 1000 / capacity));
                AddFactor(factors, BuildingConditionReason.LowOccupancy, Math.Max(0, 500 - occupancyPermille));
            }

            factors.Sort((left, right) =>
            {
                var comparison = right.Contribution.CompareTo(left.Contribution);
                return comparison != 0 ? comparison : left.Reason.CompareTo(right.Reason);
            });
            long total = 0;
            for (var index = 0; index < factors.Count; index++)
                total += factors[index].Contribution;
            score = total >= 2000 ? 2000 : (int)total;
            return factors.AsReadOnly();
        }

        private RedevelopmentBlockReason ResolveRedevelopmentBlock(
            BuildingConditionEnvironment environment,
            bool recoveredEnvironment)
        {
            if (!environment.HasRoadAccess)
                return RedevelopmentBlockReason.NoRoadAccess;
            if (!environment.HasPower)
                return RedevelopmentBlockReason.NoPower;
            if (!recoveredEnvironment)
                return RedevelopmentBlockReason.EnvironmentNotRecovered;
            return RedevelopmentBlockReason.RecoveryDelay;
        }

        private int ApplyCapacity(BuildingState building, int definitionCapacity)
        {
            if (building == null)
                throw new ArgumentNullException(nameof(building));
            if (definitionCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(definitionCapacity));
            if (!State.TryGet(building.Id, out var state))
                return definitionCapacity;

            int permille;
            switch (state.Stage)
            {
                case BuildingConditionStage.Healthy:
                    permille = 1000;
                    break;
                case BuildingConditionStage.Stressed:
                    permille = _parameters.StressedCapacityPermille;
                    break;
                case BuildingConditionStage.Declining:
                    permille = _parameters.DecliningCapacityPermille;
                    break;
                case BuildingConditionStage.Abandoned:
                case BuildingConditionStage.Redeveloping:
                    permille = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return checked(definitionCapacity * permille / 1000);
        }

        private static void AddFactor(
            ICollection<BuildingConditionFactor> factors,
            BuildingConditionReason reason,
            int contribution)
        {
            if (contribution > 0)
                factors.Add(new BuildingConditionFactor(reason, contribution));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime64;
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }
    }
}
