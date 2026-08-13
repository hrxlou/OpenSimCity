using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;

namespace OpenSimCity.Simulation.Conditions
{
    public enum BuildingConditionStage : byte
    {
        Healthy,
        Stressed,
        Declining,
        Abandoned,
        Redeveloping
    }

    public enum BuildingConditionReason : byte
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

    public enum RedevelopmentBlockReason : byte
    {
        None,
        NotAbandoned,
        NoRoadAccess,
        NoPower,
        EnvironmentNotRecovered,
        RecoveryDelay,
        AlreadyRedeveloping
    }

    public enum BuildingConditionUse : byte
    {
        Residential,
        Employment
    }

    [Serializable]
    public readonly struct BuildingConditionProfile
    {
        public BuildingConditionProfile(
            BuildingConditionUse use,
            int housingCapacity,
            int jobCapacity)
        {
            if (housingCapacity < 0 || jobCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(housingCapacity));
            if (use == BuildingConditionUse.Residential && housingCapacity <= 0)
                throw new ArgumentException("Residential condition profiles require housing capacity.");
            if (use == BuildingConditionUse.Employment && jobCapacity <= 0)
                throw new ArgumentException("Employment condition profiles require job capacity.");

            Use = use;
            HousingCapacity = housingCapacity;
            JobCapacity = jobCapacity;
        }

        public BuildingConditionUse Use { get; }
        public int HousingCapacity { get; }
        public int JobCapacity { get; }
        public int RelevantCapacity => Use == BuildingConditionUse.Residential
            ? HousingCapacity
            : JobCapacity;
    }

    public interface IBuildingConditionProfileResolver
    {
        bool TryGetConditionProfile(StableId definitionId, out BuildingConditionProfile profile);
    }

    [Serializable]
    public readonly struct BuildingConditionEnvironment
    {
        public BuildingConditionEnvironment(
            int landValue,
            int pollution,
            int noise,
            int crime,
            bool hasRoadAccess,
            bool hasPower)
        {
            ValidateField(landValue, nameof(landValue));
            ValidateField(pollution, nameof(pollution));
            ValidateField(noise, nameof(noise));
            ValidateField(crime, nameof(crime));
            LandValue = landValue;
            Pollution = pollution;
            Noise = noise;
            Crime = crime;
            HasRoadAccess = hasRoadAccess;
            HasPower = hasPower;
        }

        public int LandValue { get; }
        public int Pollution { get; }
        public int Noise { get; }
        public int Crime { get; }
        public bool HasRoadAccess { get; }
        public bool HasPower { get; }

        private static void ValidateField(int value, string name)
        {
            if (value < 0 || value > 1000)
                throw new ArgumentOutOfRangeException(name, "Condition fields must be 0..1000.");
        }
    }

    public interface IBuildingConditionEnvironmentResolver
    {
        BuildingConditionEnvironment Resolve(BuildingState building);
    }

    [Serializable]
    public readonly struct BuildingConditionFactor : IEquatable<BuildingConditionFactor>
    {
        public BuildingConditionFactor(BuildingConditionReason reason, int contribution)
        {
            if (reason == BuildingConditionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (contribution <= 0)
                throw new ArgumentOutOfRangeException(nameof(contribution));
            Reason = reason;
            Contribution = contribution;
        }

        public BuildingConditionReason Reason { get; }
        public int Contribution { get; }

        public bool Equals(BuildingConditionFactor other) =>
            Reason == other.Reason && Contribution == other.Contribution;
        public override bool Equals(object obj) => obj is BuildingConditionFactor other && Equals(other);
        public override int GetHashCode() => ((int)Reason * 397) ^ Contribution;
    }

    [Serializable]
    public sealed class BuildingConditionParameters
    {
        public BuildingConditionParameters(
            int newBuildingGraceMonths = 2,
            int monthsToStress = 2,
            int monthsToDecline = 4,
            int monthsToAbandon = 7,
            int recoveryMonthsPerStep = 2,
            int redevelopmentRecoveryMonths = 2,
            int redevelopmentDurationMonths = 3,
            int stressScoreThreshold = 250,
            int severeScoreThreshold = 650,
            int recoveryScoreThreshold = 180,
            int stressedCapacityPermille = 800,
            int decliningCapacityPermille = 500)
        {
            if (newBuildingGraceMonths < 0 || monthsToStress < 1 ||
                monthsToDecline <= monthsToStress || monthsToAbandon <= monthsToDecline ||
                recoveryMonthsPerStep < 1 || redevelopmentRecoveryMonths < 1 ||
                redevelopmentDurationMonths < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(newBuildingGraceMonths));
            }
            if (stressScoreThreshold < 1 || severeScoreThreshold <= stressScoreThreshold ||
                recoveryScoreThreshold < 0 || recoveryScoreThreshold >= stressScoreThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(stressScoreThreshold));
            }
            if (stressedCapacityPermille < 1 || stressedCapacityPermille > 1000 ||
                decliningCapacityPermille < 0 || decliningCapacityPermille >= stressedCapacityPermille)
            {
                throw new ArgumentOutOfRangeException(nameof(stressedCapacityPermille));
            }

            NewBuildingGraceMonths = newBuildingGraceMonths;
            MonthsToStress = monthsToStress;
            MonthsToDecline = monthsToDecline;
            MonthsToAbandon = monthsToAbandon;
            RecoveryMonthsPerStep = recoveryMonthsPerStep;
            RedevelopmentRecoveryMonths = redevelopmentRecoveryMonths;
            RedevelopmentDurationMonths = redevelopmentDurationMonths;
            StressScoreThreshold = stressScoreThreshold;
            SevereScoreThreshold = severeScoreThreshold;
            RecoveryScoreThreshold = recoveryScoreThreshold;
            StressedCapacityPermille = stressedCapacityPermille;
            DecliningCapacityPermille = decliningCapacityPermille;
        }

        public int NewBuildingGraceMonths { get; }
        public int MonthsToStress { get; }
        public int MonthsToDecline { get; }
        public int MonthsToAbandon { get; }
        public int RecoveryMonthsPerStep { get; }
        public int RedevelopmentRecoveryMonths { get; }
        public int RedevelopmentDurationMonths { get; }
        public int StressScoreThreshold { get; }
        public int SevereScoreThreshold { get; }
        public int RecoveryScoreThreshold { get; }
        public int StressedCapacityPermille { get; }
        public int DecliningCapacityPermille { get; }
        public static BuildingConditionParameters Default => new BuildingConditionParameters();
    }

    [Serializable]
    public sealed class BuildingConditionFactorDto
    {
        public int Reason;
        public int Contribution;
    }

    [Serializable]
    public sealed class BuildingConditionStateDto
    {
        public ulong BuildingId;
        public int Stage;
        public int AgeMonths;
        public int AdverseMonths;
        public int RecoveryMonths;
        public int RedevelopmentMonthsRemaining;
        public int LastAdverseScore;
        public int TopReason;
        public bool IsRedevelopmentEligible;
        public int RedevelopmentBlockReason;
        public BuildingConditionFactorDto[] Factors = Array.Empty<BuildingConditionFactorDto>();
    }

    [Serializable]
    public sealed class BuildingConditionRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public bool HasProcessedMonth;
        public int LastProcessedYear;
        public int LastProcessedMonth;
        public BuildingConditionStateDto[] States = Array.Empty<BuildingConditionStateDto>();
    }
}
