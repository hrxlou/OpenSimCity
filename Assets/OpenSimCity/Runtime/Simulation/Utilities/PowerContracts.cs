using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Buildings;

namespace OpenSimCity.Simulation.Utilities
{
    [Serializable]
    public readonly struct BuildingPowerProfile
    {
        public BuildingPowerProfile(StableId definitionId, BuildingFootprint footprint, int demand)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A power profile requires a valid building definition ID.", nameof(definitionId));
            if (demand <= 0)
                throw new ArgumentOutOfRangeException(nameof(demand), "Building power demand must be positive.");
            DefinitionId = definitionId;
            Footprint = footprint;
            Demand = demand;
        }

        public StableId DefinitionId { get; }
        public BuildingFootprint Footprint { get; }
        public int Demand { get; }
    }

    public interface IBuildingPowerProfileResolver
    {
        bool TryGetPowerProfile(StableId definitionId, out BuildingPowerProfile profile);
    }

    [Serializable]
    public readonly struct UtilityPowerProfile
    {
        public UtilityPowerProfile(StableId definitionId, int capacity)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A utility profile requires a valid definition ID.", nameof(definitionId));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Power capacity must be positive.");
            DefinitionId = definitionId;
            Capacity = capacity;
        }

        public StableId DefinitionId { get; }
        public int Capacity { get; }
    }

    public interface IUtilityPowerProfileResolver
    {
        bool TryGetPowerProfile(StableId definitionId, out UtilityPowerProfile profile);
    }

    public enum PowerSupplyFailureReason : byte
    {
        None,
        UnknownBuildingDefinition,
        NoRoadConnection,
        NoPowerProducer,
        InsufficientCapacity
    }

    public enum PowerProducerConnectionReason : byte
    {
        None,
        UnknownUtilityDefinition,
        NoRoadConnection
    }

    [Serializable]
    public readonly struct PowerComponentResult
    {
        public PowerComponentResult(int componentId, int capacity, int demand, int allocated)
        { ComponentId = componentId; Capacity = capacity; Demand = demand; Allocated = allocated; }
        public int ComponentId { get; }
        public int Capacity { get; }
        public int Demand { get; }
        public int Allocated { get; }
        public int RemainingCapacity => Capacity - Allocated;
        public bool IsOverloaded => Demand > Capacity;
    }

    [Serializable]
    public readonly struct BuildingPowerResult : IEquatable<BuildingPowerResult>
    {
        public BuildingPowerResult(StableId buildingId, CellCoord cell, int componentId,
            int demand, int allocated, PowerSupplyFailureReason failureReason)
        {
            BuildingId = buildingId;
            Cell = cell;
            ComponentId = componentId;
            Demand = demand;
            Allocated = allocated;
            FailureReason = failureReason;
        }

        public StableId BuildingId { get; }
        public CellCoord Cell { get; }
        public int ComponentId { get; }
        public int Demand { get; }
        public int Allocated { get; }
        public PowerSupplyFailureReason FailureReason { get; }
        public bool IsPowered => FailureReason == PowerSupplyFailureReason.None && Allocated == Demand;

        public bool Equals(BuildingPowerResult other) => BuildingId == other.BuildingId && Cell == other.Cell &&
            ComponentId == other.ComponentId && Demand == other.Demand && Allocated == other.Allocated &&
            FailureReason == other.FailureReason;
        public override bool Equals(object obj) => obj is BuildingPowerResult other && Equals(other);
        public override int GetHashCode() => BuildingId.GetHashCode();
        public static bool operator ==(BuildingPowerResult left, BuildingPowerResult right) => left.Equals(right);
        public static bool operator !=(BuildingPowerResult left, BuildingPowerResult right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct PowerProducerResult : IEquatable<PowerProducerResult>
    {
        public PowerProducerResult(StableId producerId, CellCoord cell, int componentId, int production,
            PowerProducerConnectionReason connectionReason)
        {
            ProducerId = producerId;
            Cell = cell;
            ComponentId = componentId;
            Production = production;
            ConnectionReason = connectionReason;
        }

        public StableId ProducerId { get; }
        public CellCoord Cell { get; }
        public int ComponentId { get; }
        public int Production { get; }
        public PowerProducerConnectionReason ConnectionReason { get; }
        public bool IsConnected => ConnectionReason == PowerProducerConnectionReason.None;
        public bool Equals(PowerProducerResult other) => ProducerId == other.ProducerId && Cell == other.Cell &&
            ComponentId == other.ComponentId && Production == other.Production && ConnectionReason == other.ConnectionReason;
        public override bool Equals(object obj) => obj is PowerProducerResult other && Equals(other);
        public override int GetHashCode() => ProducerId.GetHashCode();
        public static bool operator ==(PowerProducerResult left, PowerProducerResult right) => left.Equals(right);
        public static bool operator !=(PowerProducerResult left, PowerProducerResult right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct PowerEligibilityResult
    {
        public PowerEligibilityResult(bool isEligible, int componentId, int requiredCapacity,
            int availableCapacity, PowerSupplyFailureReason failureReason)
        {
            IsEligible = isEligible;
            ComponentId = componentId;
            RequiredCapacity = requiredCapacity;
            AvailableCapacity = availableCapacity;
            FailureReason = failureReason;
        }

        public bool IsEligible { get; }
        public int ComponentId { get; }
        public int RequiredCapacity { get; }
        public int AvailableCapacity { get; }
        public PowerSupplyFailureReason FailureReason { get; }
    }
}
