using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Simulation.Transit
{
    [Serializable]
    public readonly struct TransitStopId : IEquatable<TransitStopId>, IComparable<TransitStopId>
    {
        public TransitStopId(StableId value)
        {
            if (!value.IsValid)
                throw new ArgumentException("A transit stop ID must be valid.", nameof(value));
            Value = value;
        }

        public TransitStopId(ulong value) : this(new StableId(value)) { }
        public StableId Value { get; }
        public ulong RawValue => Value.Value;
        public bool IsValid => Value.IsValid;
        public int CompareTo(TransitStopId other) => Value.CompareTo(other.Value);
        public bool Equals(TransitStopId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TransitStopId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.Value.ToString();
        public static bool operator ==(TransitStopId left, TransitStopId right) => left.Equals(right);
        public static bool operator !=(TransitStopId left, TransitStopId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct TransitLineId : IEquatable<TransitLineId>, IComparable<TransitLineId>
    {
        public TransitLineId(StableId value)
        {
            if (!value.IsValid)
                throw new ArgumentException("A transit line ID must be valid.", nameof(value));
            Value = value;
        }

        public TransitLineId(ulong value) : this(new StableId(value)) { }
        public StableId Value { get; }
        public ulong RawValue => Value.Value;
        public bool IsValid => Value.IsValid;
        public int CompareTo(TransitLineId other) => Value.CompareTo(other.Value);
        public bool Equals(TransitLineId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TransitLineId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.Value.ToString();
        public static bool operator ==(TransitLineId left, TransitLineId right) => left.Equals(right);
        public static bool operator !=(TransitLineId left, TransitLineId right) => !left.Equals(right);
    }

    [Serializable]
    public sealed class TransitStopState
    {
        public TransitStopState(TransitStopId id, CellCoord roadCell, string name)
        {
            if (!id.IsValid)
                throw new ArgumentException("A transit stop requires a stable ID.", nameof(id));
            Id = id;
            RoadCell = roadCell;
            Name = string.IsNullOrWhiteSpace(name) ? $"Stop {id.RawValue}" : name.Trim();
        }

        public TransitStopId Id { get; }
        public CellCoord RoadCell { get; }
        public string Name { get; }
    }

    [Serializable]
    public sealed class TransitLineState
    {
        private readonly TransitStopId[] _stopIds;
        private readonly IReadOnlyList<TransitStopId> _readOnlyStopIds;

        public TransitLineState(
            TransitLineId id,
            string name,
            IReadOnlyList<TransitStopId> stopIds,
            int serviceUnits,
            bool isEnabled)
        {
            if (!id.IsValid)
                throw new ArgumentException("A transit line requires a stable ID.", nameof(id));
            if (stopIds == null)
                throw new ArgumentNullException(nameof(stopIds));
            if (stopIds.Count < 2)
                throw new ArgumentException("A transit line requires at least two ordered stops.", nameof(stopIds));
            if (serviceUnits < 1 || serviceUnits > 10_000)
                throw new ArgumentOutOfRangeException(nameof(serviceUnits));

            var seen = new HashSet<TransitStopId>();
            _stopIds = new TransitStopId[stopIds.Count];
            for (var index = 0; index < stopIds.Count; index++)
            {
                if (!stopIds[index].IsValid || !seen.Add(stopIds[index]))
                    throw new ArgumentException("Transit line stop IDs must be valid and unique.", nameof(stopIds));
                _stopIds[index] = stopIds[index];
            }

            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? $"Line {id.RawValue}" : name.Trim();
            ServiceUnits = serviceUnits;
            IsEnabled = isEnabled;
            _readOnlyStopIds = Array.AsReadOnly(_stopIds);
        }

        public TransitLineId Id { get; }
        public string Name { get; }
        public IReadOnlyList<TransitStopId> StopIds => _readOnlyStopIds;
        public int ServiceUnits { get; }
        public bool IsEnabled { get; }
    }

    public enum TransitModeChoiceReason : byte
    {
        None,
        NoMatchedCommute,
        NoEnabledLine,
        BudgetDisabled,
        BudgetUnaffordable,
        OriginOutsideWalkingRange,
        DestinationOutsideWalkingRange,
        DirectionMismatch,
        BrokenLine,
        TooIndirect,
        CapacityExhausted
    }

    [Serializable]
    public readonly struct TransitOdKey : IEquatable<TransitOdKey>, IComparable<TransitOdKey>
    {
        public TransitOdKey(TripDemandRow demand)
        {
            Kind = demand.Kind;
            OriginKind = demand.Origin.Kind;
            OriginId = demand.Origin.Id;
            DestinationKind = demand.Destination.Kind;
            DestinationId = demand.Destination.Id;
        }

        public TripDemandKind Kind { get; }
        public TripEndpointKind OriginKind { get; }
        public StableId OriginId { get; }
        public TripEndpointKind DestinationKind { get; }
        public StableId DestinationId { get; }

        public int CompareTo(TransitOdKey other)
        {
            var comparison = Kind.CompareTo(other.Kind);
            if (comparison != 0) return comparison;
            comparison = OriginKind.CompareTo(other.OriginKind);
            if (comparison != 0) return comparison;
            comparison = OriginId.CompareTo(other.OriginId);
            if (comparison != 0) return comparison;
            comparison = DestinationKind.CompareTo(other.DestinationKind);
            return comparison != 0 ? comparison : DestinationId.CompareTo(other.DestinationId);
        }

        public bool Equals(TransitOdKey other) =>
            Kind == other.Kind &&
            OriginKind == other.OriginKind &&
            OriginId == other.OriginId &&
            DestinationKind == other.DestinationKind &&
            DestinationId == other.DestinationId;

        public override bool Equals(object obj) => obj is TransitOdKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (int)OriginKind;
                hash = (hash * 397) ^ OriginId.GetHashCode();
                hash = (hash * 397) ^ (int)DestinationKind;
                return (hash * 397) ^ DestinationId.GetHashCode();
            }
        }
    }

    [Serializable]
    public readonly struct TransitModeChoiceRow
    {
        public TransitModeChoiceRow(
            TransitOdKey key,
            TransitLineId lineId,
            int requestedTrips,
            int busTrips,
            int privateVehicleTrips,
            int carGeneralizedCostSeconds,
            int busGeneralizedCostSeconds,
            TransitModeChoiceReason reason)
        {
            if (requestedTrips <= 0 || busTrips < 0 || privateVehicleTrips < 0)
                throw new ArgumentOutOfRangeException(nameof(requestedTrips));
            if (checked(busTrips + privateVehicleTrips) != requestedTrips)
                throw new ArgumentException("Bus and private-vehicle trips must conserve requested trips.");
            if (carGeneralizedCostSeconds < 0 || busGeneralizedCostSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(carGeneralizedCostSeconds));
            if (busTrips > 0 && (!lineId.IsValid || reason != TransitModeChoiceReason.None))
                throw new ArgumentException("Allocated bus trips require a line and no block reason.");

            Key = key;
            LineId = lineId;
            RequestedTrips = requestedTrips;
            BusTrips = busTrips;
            PrivateVehicleTrips = privateVehicleTrips;
            CarGeneralizedCostSeconds = carGeneralizedCostSeconds;
            BusGeneralizedCostSeconds = busGeneralizedCostSeconds;
            Reason = reason;
        }

        public TransitOdKey Key { get; }
        public TransitLineId LineId { get; }
        public bool HasLine => LineId.IsValid;
        public int RequestedTrips { get; }
        public int BusTrips { get; }
        public int PrivateVehicleTrips { get; }
        public int CarGeneralizedCostSeconds { get; }
        public int BusGeneralizedCostSeconds { get; }
        public TransitModeChoiceReason Reason { get; }
    }

    [Serializable]
    public sealed class TransitModeChoiceParameters
    {
        public TransitModeChoiceParameters(
            int maximumWalkingCells = 3,
            int walkingSecondsPerCell = 90,
            int baseWaitSeconds = 180,
            int stopDwellSeconds = 20,
            int capacityPerServiceUnit = 24,
            int operatingCostPerServiceUnit = 4,
            int stopConstructionCost = 50,
            int lineSetupCost = 100,
            int maximumGeneralizedCostPermille = 2200,
            int maximumModeSharePermille = 650)
        {
            if (maximumWalkingCells < 0 || walkingSecondsPerCell < 0 || baseWaitSeconds < 0 || stopDwellSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWalkingCells));
            if (capacityPerServiceUnit <= 0 || operatingCostPerServiceUnit < 0 || stopConstructionCost < 0 || lineSetupCost < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityPerServiceUnit));
            if (maximumGeneralizedCostPermille <= 1000)
                throw new ArgumentOutOfRangeException(nameof(maximumGeneralizedCostPermille));
            if (maximumModeSharePermille < 0 || maximumModeSharePermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(maximumModeSharePermille));

            MaximumWalkingCells = maximumWalkingCells;
            WalkingSecondsPerCell = walkingSecondsPerCell;
            BaseWaitSeconds = baseWaitSeconds;
            StopDwellSeconds = stopDwellSeconds;
            CapacityPerServiceUnit = capacityPerServiceUnit;
            OperatingCostPerServiceUnit = operatingCostPerServiceUnit;
            StopConstructionCost = stopConstructionCost;
            LineSetupCost = lineSetupCost;
            MaximumGeneralizedCostPermille = maximumGeneralizedCostPermille;
            MaximumModeSharePermille = maximumModeSharePermille;
        }

        public int MaximumWalkingCells { get; }
        public int WalkingSecondsPerCell { get; }
        public int BaseWaitSeconds { get; }
        public int StopDwellSeconds { get; }
        public int CapacityPerServiceUnit { get; }
        public int OperatingCostPerServiceUnit { get; }
        public int StopConstructionCost { get; }
        public int LineSetupCost { get; }
        public int MaximumGeneralizedCostPermille { get; }
        public int MaximumModeSharePermille { get; }
        public static TransitModeChoiceParameters Default => new TransitModeChoiceParameters();
    }

    [Serializable]
    public sealed class TransitStopStateDto
    {
        public ulong Id;
        public int RoadCellX;
        public int RoadCellY;
        public string Name;
    }

    [Serializable]
    public sealed class TransitLineStateDto
    {
        public ulong Id;
        public string Name;
        public ulong[] StopIds = Array.Empty<ulong>();
        public int ServiceUnits;
        public bool IsEnabled;
    }

    [Serializable]
    public sealed class TransitNetworkStateDto
    {
        public int ActiveBudgetPermille;
        public bool HasPendingBudget;
        public int PendingBudgetPermille;
        public int PendingBudgetYear;
        public int PendingBudgetMonth;
        public int AccruedConstructionExpense;
        public bool HasProcessedOperatingPeriod;
        public int LastOperatingYear;
        public int LastOperatingMonth;
        public int LastProjectedOperatingExpense;
        public int LastPaidOperatingExpense;
        public TransitStopStateDto[] Stops = Array.Empty<TransitStopStateDto>();
        public TransitLineStateDto[] Lines = Array.Empty<TransitLineStateDto>();
    }

    [Serializable]
    public sealed class TransitRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public TransitNetworkStateDto Network = new TransitNetworkStateDto();
    }
}
