using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// Why a parcel is or is not a development candidate this month. Reported as a
    /// pure diagnostic so a public query can attribute a stalled parcel to the one
    /// missing requirement (road, power, demand) without inspecting simulation arrays.
    /// </summary>
    public enum DevelopmentBlockReason : byte
    {
        None,
        UnknownParcel,
        AlreadyDeveloped,
        UnderConstruction,
        RoadIneligible,
        NoSectorDemand,
        NoMatchingDefinition,
        NoPowerCapacity
    }

    public enum GrowthEventKind : byte
    {
        ConstructionStarted,
        ConstructionProgressed,
        ConstructionCompleted,
        BuildingDemolished
    }

    /// <summary>
    /// Unity-independent projection of the static building data the growth loop needs
    /// to size a footprint, draw power and pick a sector. It carries stable IDs and
    /// value data only; the Application catalog owns the mapping from definition assets.
    /// </summary>
    public readonly struct DevelopmentBuildingProfile
    {
        public DevelopmentBuildingProfile(
            StableId definitionId,
            EconomicSector sector,
            BuildingFootprint footprint,
            int powerDemand)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A development profile requires a valid definition ID.", nameof(definitionId));
            if (powerDemand <= 0)
                throw new ArgumentOutOfRangeException(nameof(powerDemand), "Building power demand must be positive.");

            DefinitionId = definitionId;
            Sector = sector;
            Footprint = footprint;
            PowerDemand = powerDemand;
        }

        public StableId DefinitionId { get; }

        public EconomicSector Sector { get; }

        public BuildingFootprint Footprint { get; }

        public int PowerDemand { get; }
    }

    /// <summary>
    /// Application-implemented resolver that lists the candidate building definitions
    /// for a sector in deterministic stable-ID order. Prefab existence and view state
    /// never reach this boundary, so definition selection stays a pure simulation input.
    /// </summary>
    public interface IDevelopmentDefinitionResolver
    {
        IReadOnlyList<DevelopmentBuildingProfile> DefinitionsForSector(EconomicSector sector);

        bool TryGetProfile(StableId definitionId, out DevelopmentBuildingProfile profile);
    }

    /// <summary>
    /// Public, per-parcel development feasibility. The accessibility inputs
    /// (<see cref="NetworkDistanceToExternalGate"/>, <see cref="DistanceBand"/>) and the
    /// derived <see cref="SuitabilityFactor"/> are exposed as raw diagnostics; the factor
    /// only weights sampling and never removes an otherwise eligible parcel.
    /// </summary>
    public readonly struct DevelopmentFeasibility : IEquatable<DevelopmentFeasibility>
    {
        public DevelopmentFeasibility(
            StableId parcelId,
            EconomicSector sector,
            bool isFeasible,
            DevelopmentBlockReason blockReason,
            int networkDistanceToExternalGate,
            ExternalGateDistanceBand distanceBand,
            int suitabilityFactor)
        {
            ParcelId = parcelId;
            Sector = sector;
            IsFeasible = isFeasible;
            BlockReason = blockReason;
            NetworkDistanceToExternalGate = networkDistanceToExternalGate;
            DistanceBand = distanceBand;
            SuitabilityFactor = suitabilityFactor;
        }

        public StableId ParcelId { get; }

        public EconomicSector Sector { get; }

        public bool IsFeasible { get; }

        public DevelopmentBlockReason BlockReason { get; }

        public int NetworkDistanceToExternalGate { get; }

        public ExternalGateDistanceBand DistanceBand { get; }

        public int SuitabilityFactor { get; }

        public bool Equals(DevelopmentFeasibility other)
        {
            return ParcelId == other.ParcelId && Sector == other.Sector &&
                   IsFeasible == other.IsFeasible && BlockReason == other.BlockReason &&
                   NetworkDistanceToExternalGate == other.NetworkDistanceToExternalGate &&
                   DistanceBand == other.DistanceBand && SuitabilityFactor == other.SuitabilityFactor;
        }

        public override bool Equals(object obj) => obj is DevelopmentFeasibility other && Equals(other);

        public override int GetHashCode() => ParcelId.GetHashCode();
    }

    /// <summary>
    /// Per-sector development summary for one month. City demand
    /// (<see cref="SmoothedDemand"/>) and per-parcel feasibility
    /// (<see cref="FeasibleParcelCount"/>) are separate values, so
    /// <see cref="HasUnmetDemand"/> distinguishes "no demand" from "demand exists but no
    /// feasible parcel".
    /// </summary>
    public readonly struct DevelopmentSectorSummary
    {
        public DevelopmentSectorSummary(
            EconomicSector sector,
            int smoothedDemand,
            int feasibleParcelCount,
            int startedThisMonth,
            bool hasUnmetDemand)
        {
            Sector = sector;
            SmoothedDemand = smoothedDemand;
            FeasibleParcelCount = feasibleParcelCount;
            StartedThisMonth = startedThisMonth;
            HasUnmetDemand = hasUnmetDemand;
        }

        public EconomicSector Sector { get; }

        public int SmoothedDemand { get; }

        public int FeasibleParcelCount { get; }

        public int StartedThisMonth { get; }

        public bool HasUnmetDemand { get; }

        public DevelopmentSectorSummaryDto ToDto() => new DevelopmentSectorSummaryDto
        {
            Sector = (int)Sector,
            SmoothedDemand = SmoothedDemand,
            FeasibleParcelCount = FeasibleParcelCount,
            StartedThisMonth = StartedThisMonth,
            HasUnmetDemand = HasUnmetDemand
        };
    }

    [Serializable]
    public sealed class DevelopmentSectorSummaryDto
    {
        public int Sector;
        public int SmoothedDemand;
        public int FeasibleParcelCount;
        public int StartedThisMonth;
        public bool HasUnmetDemand;
    }

    /// <summary>Maps low-density zoning to its aggregate economic sector.</summary>
    public static class DevelopmentSectors
    {
        public static bool TryFromZoneType(ZoneType zoneType, out EconomicSector sector)
        {
            switch (zoneType)
            {
                case ZoneType.ResidentialLow:
                    sector = EconomicSector.Residential;
                    return true;
                case ZoneType.CommercialLow:
                    sector = EconomicSector.Commercial;
                    return true;
                case ZoneType.IndustrialLow:
                    sector = EconomicSector.Industrial;
                    return true;
                default:
                    sector = default;
                    return false;
            }
        }
    }
}
