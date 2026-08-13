using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Simulation.Utilities
{
    public enum WaterSupplyFailureReason : byte
    {
        None,
        NoRoadAccess,
        NoProducer,
        DifferentRoadComponent,
        CapacityExceeded
    }

    [Serializable]
    public sealed class WaterProducerDefinition
    {
        public WaterProducerDefinition(
            StableId id,
            string name,
            int capacity,
            int constructionCost,
            int monthlyMaintenanceCost)
        {
            if (!id.IsValid) throw new ArgumentException("Water definition ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Water definition name is required.", nameof(name));
            if (capacity <= 0 || constructionCost < 0 || monthlyMaintenanceCost < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Id = id;
            Name = name.Trim();
            Capacity = capacity;
            ConstructionCost = constructionCost;
            MonthlyMaintenanceCost = monthlyMaintenanceCost;
        }

        public StableId Id { get; }
        public string Name { get; }
        public int Capacity { get; }
        public int ConstructionCost { get; }
        public int MonthlyMaintenanceCost { get; }
    }

    public static class WaterProducerDefinitionIds
    {
        public static readonly StableId Pump = new StableId(0xF702_0000_0000_0001UL);
    }

    public sealed class WaterProducerDefinitionCatalog
    {
        private readonly SortedDictionary<StableId, WaterProducerDefinition> _byId =
            new SortedDictionary<StableId, WaterProducerDefinition>();

        public WaterProducerDefinitionCatalog(IEnumerable<WaterProducerDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions)
            {
                if (definition == null || _byId.ContainsKey(definition.Id))
                    throw new ArgumentException("Water definitions require unique IDs.", nameof(definitions));
                _byId.Add(definition.Id, definition);
            }
            if (_byId.Count == 0) throw new ArgumentException("At least one water definition is required.", nameof(definitions));
        }

        public IReadOnlyList<WaterProducerDefinition> Definitions =>
            new List<WaterProducerDefinition>(_byId.Values).AsReadOnly();
        public WaterProducerDefinition Get(StableId id)
        {
            if (_byId.TryGetValue(id, out var value)) return value;
            throw new KeyNotFoundException($"Unknown water definition {id.Value}.");
        }
        public bool TryGet(StableId id, out WaterProducerDefinition value) => _byId.TryGetValue(id, out value);
        public static WaterProducerDefinitionCatalog CreateDefault() =>
            new WaterProducerDefinitionCatalog(new[]
            {
                new WaterProducerDefinition(WaterProducerDefinitionIds.Pump, "Water pump", 1_000, 1_200, 90)
            });
    }

    [Serializable]
    public sealed class WaterProducerState
    {
        public WaterProducerState(StableId id, StableId definitionId, CellCoord cell)
        {
            if (!id.IsValid || !definitionId.IsValid)
                throw new ArgumentException("Water producer and definition IDs are required.");
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
        }
        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
    }

    public sealed class WaterProducerCollection
    {
        private readonly SortedDictionary<StableId, WaterProducerState> _byId =
            new SortedDictionary<StableId, WaterProducerState>();
        public IReadOnlyList<WaterProducerState> Producers =>
            new List<WaterProducerState>(_byId.Values).AsReadOnly();
        public int Version { get; private set; }
        public bool TryGet(StableId id, out WaterProducerState value) => _byId.TryGetValue(id, out value);
        public bool Add(WaterProducerState value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (_byId.ContainsKey(value.Id)) return false;
            _byId.Add(value.Id, value);
            Version = checked(Version + 1);
            return true;
        }
        public bool Remove(StableId id)
        {
            if (!_byId.Remove(id)) return false;
            Version = checked(Version + 1);
            return true;
        }
        internal void ClearAndRestore(IEnumerable<WaterProducerState> values)
        {
            _byId.Clear();
            foreach (var value in values)
            {
                if (value == null || _byId.ContainsKey(value.Id))
                    throw new ArgumentException("Water producers require unique IDs.", nameof(values));
                _byId.Add(value.Id, value);
            }
            Version = _byId.Count == 0 ? 0 : 1;
        }
    }

    [Serializable]
    public readonly struct WaterConsumerNode : IComparable<WaterConsumerNode>
    {
        public WaterConsumerNode(
            StableId buildingId,
            CellCoord cell,
            int roadComponentId,
            int demand)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Water consumer building ID is required.", nameof(buildingId));
            if (roadComponentId < -1 || demand <= 0) throw new ArgumentOutOfRangeException(nameof(demand));
            BuildingId = buildingId;
            Cell = cell;
            RoadComponentId = roadComponentId;
            Demand = demand;
        }
        public StableId BuildingId { get; }
        public CellCoord Cell { get; }
        public int RoadComponentId { get; }
        public int Demand { get; }
        public bool HasRoadAccess => RoadComponentId >= 0;
        public int CompareTo(WaterConsumerNode other) => BuildingId.CompareTo(other.BuildingId);
    }

    [Serializable]
    public readonly struct WaterProducerNetworkNode : IComparable<WaterProducerNetworkNode>
    {
        public WaterProducerNetworkNode(
            StableId producerId,
            int roadComponentId,
            int capacity)
        {
            if (!producerId.IsValid || roadComponentId < -1 || capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            ProducerId = producerId;
            RoadComponentId = roadComponentId;
            Capacity = capacity;
        }
        public StableId ProducerId { get; }
        public int RoadComponentId { get; }
        public int Capacity { get; }
        public bool HasRoadAccess => RoadComponentId >= 0;
        public int CompareTo(WaterProducerNetworkNode other) => ProducerId.CompareTo(other.ProducerId);
    }

    public interface IWaterNetworkResolver
    {
        IReadOnlyList<WaterConsumerNode> GetConsumers();
        WaterProducerNetworkNode ResolveProducer(WaterProducerState producer, WaterProducerDefinition definition);
    }

    [Serializable]
    public readonly struct WaterSupplyRow : IComparable<WaterSupplyRow>
    {
        public WaterSupplyRow(
            StableId buildingId,
            int requested,
            int supplied,
            int supplyPermille,
            WaterSupplyFailureReason failureReason)
        {
            BuildingId = buildingId;
            Requested = requested;
            Supplied = supplied;
            SupplyPermille = supplyPermille;
            FailureReason = failureReason;
        }
        public StableId BuildingId { get; }
        public int Requested { get; }
        public int Supplied { get; }
        public int SupplyPermille { get; }
        public WaterSupplyFailureReason FailureReason { get; }
        public int CompareTo(WaterSupplyRow other) => BuildingId.CompareTo(other.BuildingId);
    }

    [Serializable]
    public readonly struct WaterProducerUsageRow : IComparable<WaterProducerUsageRow>
    {
        public WaterProducerUsageRow(StableId producerId, int capacity, int used, int roadComponentId)
        {
            ProducerId = producerId;
            Capacity = capacity;
            Used = used;
            RoadComponentId = roadComponentId;
        }
        public StableId ProducerId { get; }
        public int Capacity { get; }
        public int Used { get; }
        public int RoadComponentId { get; }
        public int CompareTo(WaterProducerUsageRow other) => ProducerId.CompareTo(other.ProducerId);
    }

    public sealed class WaterUtilityState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private IReadOnlyList<WaterSupplyRow> _supply = Array.AsReadOnly(Array.Empty<WaterSupplyRow>());
        private IReadOnlyList<WaterProducerUsageRow> _usage = Array.AsReadOnly(Array.Empty<WaterProducerUsageRow>());
        public IReadOnlyList<WaterSupplyRow> Supply => _supply;
        public IReadOnlyList<WaterProducerUsageRow> ProducerUsage => _usage;
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int RequestedTotal { get; private set; }
        public int SuppliedTotal { get; private set; }
        public int Version { get; private set; }

        internal void Set(
            SimulationDate month,
            IList<WaterSupplyRow> supply,
            IList<WaterProducerUsageRow> usage)
        {
            var supplyRows = new WaterSupplyRow[supply.Count];
            for (var index = 0; index < supplyRows.Length; index++) supplyRows[index] = supply[index];
            Array.Sort(supplyRows);
            var usageRows = new WaterProducerUsageRow[usage.Count];
            for (var index = 0; index < usageRows.Length; index++) usageRows[index] = usage[index];
            Array.Sort(usageRows);
            _supply = Array.AsReadOnly(supplyRows);
            _usage = Array.AsReadOnly(usageRows);
            var requested = 0;
            var supplied = 0;
            for (var index = 0; index < supplyRows.Length; index++)
            {
                requested = checked(requested + supplyRows[index].Requested);
                supplied = checked(supplied + supplyRows[index].Supplied);
            }
            RequestedTotal = requested;
            SuppliedTotal = supplied;
            HasProcessedMonth = true;
            LastProcessedMonth = month;
            Version = checked(Version + 1);
        }

        public bool TryGet(StableId buildingId, out WaterSupplyRow row)
        {
            var low = 0;
            var high = _supply.Count - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var comparison = _supply[middle].BuildingId.CompareTo(buildingId);
                if (comparison == 0) { row = _supply[middle]; return true; }
                if (comparison < 0) low = middle + 1; else high = middle - 1;
            }
            row = default;
            return false;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Year : 0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Month : 0);
            AddInt(ref hash, _supply.Count);
            for (var index = 0; index < _supply.Count; index++)
            {
                var row = _supply[index];
                AddUlong(ref hash, row.BuildingId.Value);
                AddInt(ref hash, row.Requested);
                AddInt(ref hash, row.Supplied);
                AddInt(ref hash, row.SupplyPermille);
                AddByte(ref hash, (byte)row.FailureReason);
            }
            AddInt(ref hash, _usage.Count);
            for (var index = 0; index < _usage.Count; index++)
            {
                var row = _usage[index];
                AddUlong(ref hash, row.ProducerId.Value);
                AddInt(ref hash, row.Capacity);
                AddInt(ref hash, row.Used);
                AddInt(ref hash, row.RoadComponentId);
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

    public sealed class WaterUtilitySystem : IMonthlySimulationSystem
    {
        private readonly WaterProducerCollection _producers;
        private readonly WaterProducerDefinitionCatalog _definitions;
        private readonly IWaterNetworkResolver _resolver;

        public WaterUtilitySystem(
            WaterProducerCollection producers,
            WaterProducerDefinitionCatalog definitions,
            IWaterNetworkResolver resolver)
        {
            _producers = producers ?? throw new ArgumentNullException(nameof(producers));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            State = new WaterUtilityState();
        }

        public WaterUtilityState State { get; }

        public void RunMonth(SimulationDate month)
        {
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Water processing requires strictly increasing months.");

            var producerStates = _producers.Producers;
            var networkProducers = new List<WaterProducerNetworkNode>(producerStates.Count);
            for (var index = 0; index < producerStates.Count; index++)
            {
                networkProducers.Add(_resolver.ResolveProducer(
                    producerStates[index],
                    _definitions.Get(producerStates[index].DefinitionId)));
            }
            networkProducers.Sort();
            var remaining = new SortedDictionary<StableId, int>();
            var used = new SortedDictionary<StableId, int>();
            for (var index = 0; index < networkProducers.Count; index++)
            {
                remaining[networkProducers[index].ProducerId] = networkProducers[index].Capacity;
                used[networkProducers[index].ProducerId] = 0;
            }

            var consumers = new List<WaterConsumerNode>(_resolver.GetConsumers());
            consumers.Sort();
            var supply = new List<WaterSupplyRow>(consumers.Count);
            for (var consumerIndex = 0; consumerIndex < consumers.Count; consumerIndex++)
            {
                var consumer = consumers[consumerIndex];
                if (!consumer.HasRoadAccess)
                {
                    supply.Add(new WaterSupplyRow(
                        consumer.BuildingId, consumer.Demand, 0, 0,
                        WaterSupplyFailureReason.NoRoadAccess));
                    continue;
                }

                var requestedRemaining = consumer.Demand;
                var supplied = 0;
                var hasConnectedProducer = false;
                var hasAnyProducer = networkProducers.Count > 0;
                for (var producerIndex = 0; producerIndex < networkProducers.Count && requestedRemaining > 0; producerIndex++)
                {
                    var producer = networkProducers[producerIndex];
                    if (!producer.HasRoadAccess || producer.RoadComponentId != consumer.RoadComponentId)
                        continue;
                    hasConnectedProducer = true;
                    var amount = Math.Min(requestedRemaining, remaining[producer.ProducerId]);
                    if (amount <= 0) continue;
                    remaining[producer.ProducerId] -= amount;
                    used[producer.ProducerId] = checked(used[producer.ProducerId] + amount);
                    requestedRemaining -= amount;
                    supplied += amount;
                }

                var reason = supplied == consumer.Demand
                    ? WaterSupplyFailureReason.None
                    : !hasAnyProducer
                        ? WaterSupplyFailureReason.NoProducer
                        : !hasConnectedProducer
                            ? WaterSupplyFailureReason.DifferentRoadComponent
                            : WaterSupplyFailureReason.CapacityExceeded;
                supply.Add(new WaterSupplyRow(
                    consumer.BuildingId,
                    consumer.Demand,
                    supplied,
                    checked(supplied * 1000 / consumer.Demand),
                    reason));
            }

            var usage = new List<WaterProducerUsageRow>(networkProducers.Count);
            for (var index = 0; index < networkProducers.Count; index++)
            {
                var producer = networkProducers[index];
                usage.Add(new WaterProducerUsageRow(
                    producer.ProducerId,
                    producer.Capacity,
                    used[producer.ProducerId],
                    producer.RoadComponentId));
            }
            State.Set(month, supply, usage);
        }
    }

    [Serializable]
    public sealed class WaterProducerStateDto
    {
        public ulong Id;
        public ulong DefinitionId;
        public int CellX;
        public int CellY;
    }

    [Serializable]
    public sealed class WaterRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public WaterProducerStateDto[] Producers = Array.Empty<WaterProducerStateDto>();
    }
}
