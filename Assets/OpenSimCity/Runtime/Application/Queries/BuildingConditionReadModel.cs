using System;
using System.Collections.Generic;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Conditions;

namespace OpenSimCity.Application.Queries
{
    public enum BuildingConditionStageView : byte
    {
        Healthy,
        Stressed,
        Declining,
        Abandoned,
        Redeveloping
    }

    public enum BuildingConditionReasonView : byte
    {
        None,
        NoRoadAccess,
        NoPower,
        Pollution,
        Crime,
        Noise,
        LowLandValue,
        LowOccupancy
    }

    public enum RedevelopmentBlockReasonView : byte
    {
        None,
        NotAbandoned,
        NoRoadAccess,
        NoPower,
        EnvironmentNotRecovered,
        RecoveryDelay,
        AlreadyRedeveloping
    }

    [Serializable]
    public readonly struct BuildingConditionFactorReadModel
    {
        public BuildingConditionFactorReadModel(BuildingConditionFactor factor)
        {
            Reason = (BuildingConditionReasonView)(byte)factor.Reason;
            Contribution = factor.Contribution;
        }
        public BuildingConditionReasonView Reason { get; }
        public int Contribution { get; }
    }

    [Serializable]
    public sealed class BuildingConditionBuildingReadModel
    {
        public StableId BuildingId;
        public StableId DefinitionId;
        public StableId ParcelId;
        public CellCoord Anchor;
        public int FootprintWidth;
        public int FootprintHeight;
        public BuildingConditionStageView Stage;
        public int AgeMonths;
        public int AdverseMonths;
        public int RecoveryMonths;
        public int RedevelopmentMonthsRemaining;
        public int AdverseScore;
        public BuildingConditionReasonView TopReason;
        public bool IsRedevelopmentEligible;
        public RedevelopmentBlockReasonView RedevelopmentBlockReason;
        public int EffectiveHousingCapacity;
        public int EffectiveJobCapacity;
        public int Occupancy;
        public int FilledJobs;
        public IReadOnlyList<BuildingConditionFactorReadModel> Factors =
            Array.AsReadOnly(Array.Empty<BuildingConditionFactorReadModel>());
    }

    [Serializable]
    public sealed class BuildingConditionReadModelSnapshot
    {
        public int Version;
        public bool HasProcessedMonth;
        public int ProcessedYear;
        public int ProcessedMonth;
        public int HealthyCount;
        public int StressedCount;
        public int DecliningCount;
        public int AbandonedCount;
        public int RedevelopingCount;
        public ulong Checksum;
        public IReadOnlyList<BuildingConditionBuildingReadModel> Buildings =
            Array.AsReadOnly(Array.Empty<BuildingConditionBuildingReadModel>());
    }

    public sealed class BuildingConditionReadModel
    {
        private readonly BuildingConditionRuntimeModule _module;
        private int _conditionVersion = -1;
        private int _buildingVersion = -1;

        public BuildingConditionReadModel(BuildingConditionRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Snapshot = new BuildingConditionReadModelSnapshot();
        }

        public BuildingConditionReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            var conditionVersion = _module.System.Version;
            var buildingVersion = _module.Runtime.Buildings.Version;
            if (_conditionVersion == conditionVersion && _buildingVersion == buildingVersion)
                return false;

            var states = _module.System.State.States;
            var rows = new List<BuildingConditionBuildingReadModel>(states.Count);
            var healthy = 0;
            var stressed = 0;
            var declining = 0;
            var abandoned = 0;
            var redeveloping = 0;
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                if (!_module.Runtime.Buildings.TryGet(state.BuildingId, out var building))
                    continue;
                var definition = _module.Runtime.BuildingDefinitions.Get(building.DefinitionId);
                var factors = new BuildingConditionFactorReadModel[state.Factors.Count];
                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++)
                    factors[factorIndex] = new BuildingConditionFactorReadModel(state.Factors[factorIndex]);
                var effectiveHousing = _module.System.ResolveHousingCapacity(
                    building,
                    definition.OccupancyCapacity);
                var effectiveJobs = _module.System.ResolveJobCapacity(
                    building,
                    definition.JobCapacity);
                rows.Add(new BuildingConditionBuildingReadModel
                {
                    BuildingId = building.Id,
                    DefinitionId = building.DefinitionId,
                    ParcelId = building.ParcelId,
                    Anchor = building.Anchor,
                    FootprintWidth = building.Footprint.Width,
                    FootprintHeight = building.Footprint.Height,
                    Stage = (BuildingConditionStageView)(byte)state.Stage,
                    AgeMonths = state.AgeMonths,
                    AdverseMonths = state.AdverseMonths,
                    RecoveryMonths = state.RecoveryMonths,
                    RedevelopmentMonthsRemaining = state.RedevelopmentMonthsRemaining,
                    AdverseScore = state.LastAdverseScore,
                    TopReason = (BuildingConditionReasonView)(byte)state.TopReason,
                    IsRedevelopmentEligible = state.IsRedevelopmentEligible,
                    RedevelopmentBlockReason =
                        (RedevelopmentBlockReasonView)(byte)state.RedevelopmentBlockReason,
                    EffectiveHousingCapacity = effectiveHousing,
                    EffectiveJobCapacity = effectiveJobs,
                    Occupancy = building.Occupancy,
                    FilledJobs = building.FilledJobs,
                    Factors = Array.AsReadOnly(factors)
                });
                switch (state.Stage)
                {
                    case BuildingConditionStage.Healthy: healthy++; break;
                    case BuildingConditionStage.Stressed: stressed++; break;
                    case BuildingConditionStage.Declining: declining++; break;
                    case BuildingConditionStage.Abandoned: abandoned++; break;
                    case BuildingConditionStage.Redeveloping: redeveloping++; break;
                }
            }

            Snapshot = new BuildingConditionReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                HasProcessedMonth = _module.System.HasProcessedMonth,
                ProcessedYear = _module.System.HasProcessedMonth ? _module.System.LastProcessedMonth.Year : 0,
                ProcessedMonth = _module.System.HasProcessedMonth ? _module.System.LastProcessedMonth.Month : 0,
                HealthyCount = healthy,
                StressedCount = stressed,
                DecliningCount = declining,
                AbandonedCount = abandoned,
                RedevelopingCount = redeveloping,
                Checksum = _module.System.ComputeChecksum(),
                Buildings = rows.AsReadOnly()
            };
            _conditionVersion = conditionVersion;
            _buildingVersion = buildingVersion;
            return true;
        }

        public bool TryGet(StableId buildingId, out BuildingConditionBuildingReadModel row)
        {
            RefreshIfNeeded();
            var source = Snapshot.Buildings;
            var low = 0;
            var high = source.Count - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var comparison = source[middle].BuildingId.CompareTo(buildingId);
                if (comparison == 0)
                {
                    row = source[middle];
                    return true;
                }
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            row = null;
            return false;
        }
    }
}
