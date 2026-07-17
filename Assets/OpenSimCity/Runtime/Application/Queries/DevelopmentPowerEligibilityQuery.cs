using System;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.Queries
{
    public enum DevelopmentPowerEligibilityFailureReason : byte
    {
        None,
        UnknownParcel,
        UnknownBuildingDefinition,
        RoadIneligible,
        NoRoadPowerConnection,
        NoPowerProducer,
        InsufficientCapacity
    }

    public readonly struct DevelopmentPowerEligibilityResult
    {
        public DevelopmentPowerEligibilityResult(bool isEligible,
            DevelopmentPowerEligibilityFailureReason failureReason,
            ParcelDevelopmentFailureReason parcelFailureReason,
            PowerEligibilityResult power)
        { IsEligible = isEligible; FailureReason = failureReason; ParcelFailureReason = parcelFailureReason; Power = power; }
        public bool IsEligible { get; }
        public DevelopmentPowerEligibilityFailureReason FailureReason { get; }
        public ParcelDevelopmentFailureReason ParcelFailureReason { get; }
        public PowerEligibilityResult Power { get; }
    }

    /// <summary>Growth-facing query requiring both P2 road eligibility and P3 power capacity.</summary>
    public sealed class DevelopmentPowerEligibilityQuery
    {
        private readonly ParcelReadModel _parcels;
        private readonly BuildingDefinitionCatalog _definitions;
        private readonly PowerNetwork _power;
        public DevelopmentPowerEligibilityQuery(ParcelReadModel parcels, BuildingDefinitionCatalog definitions, PowerNetwork power)
        { _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels)); _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions)); _power = power ?? throw new ArgumentNullException(nameof(power)); }

        public DevelopmentPowerEligibilityResult Query(StableId parcelId, StableId buildingDefinitionId)
        {
            if (!_parcels.TryGet(parcelId, out var parcel))
                return Failure(DevelopmentPowerEligibilityFailureReason.UnknownParcel);
            if (!_definitions.TryGetPowerProfile(buildingDefinitionId, out var profile))
                return Failure(DevelopmentPowerEligibilityFailureReason.UnknownBuildingDefinition);
            if (!parcel.IsDevelopmentEligible)
                return new DevelopmentPowerEligibilityResult(false,
                    DevelopmentPowerEligibilityFailureReason.RoadIneligible, parcel.FailureReason, default);
            var power = _power.QueryEligibility(parcel.Anchor, profile);
            var reason = Map(power.FailureReason);
            return new DevelopmentPowerEligibilityResult(power.IsEligible, reason,
                ParcelDevelopmentFailureReason.None, power);
        }

        private static DevelopmentPowerEligibilityResult Failure(DevelopmentPowerEligibilityFailureReason reason) =>
            new DevelopmentPowerEligibilityResult(false, reason, ParcelDevelopmentFailureReason.None, default);
        private static DevelopmentPowerEligibilityFailureReason Map(PowerSupplyFailureReason reason)
        {
            switch (reason)
            {
                case PowerSupplyFailureReason.None: return DevelopmentPowerEligibilityFailureReason.None;
                case PowerSupplyFailureReason.NoRoadConnection: return DevelopmentPowerEligibilityFailureReason.NoRoadPowerConnection;
                case PowerSupplyFailureReason.NoPowerProducer: return DevelopmentPowerEligibilityFailureReason.NoPowerProducer;
                default: return DevelopmentPowerEligibilityFailureReason.InsufficientCapacity;
            }
        }
    }
}
