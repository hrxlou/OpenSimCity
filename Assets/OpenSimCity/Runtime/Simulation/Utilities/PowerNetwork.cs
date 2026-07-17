using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Utilities
{
    /// <summary>
    /// Rebuildable road-component power cache. Roads transmit power; producers and building
    /// footprints connect to the lowest row-major cardinally adjacent road cell. Capacity is
    /// allocated atomically to buildings in stable-ID order.
    /// </summary>
    public sealed class PowerNetwork
    {
        public const int NoComponent = -1;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly RoadConnectivityGraph _roads;
        private readonly BuildingStateCollection _buildings;
        private readonly PowerProducerCollection _producers;
        private readonly IBuildingPowerProfileResolver _buildingProfiles;
        private readonly IUtilityPowerProfileResolver _utilityProfiles;
        private readonly List<BuildingPowerResult> _buildingResults = new List<BuildingPowerResult>();
        private readonly List<PowerProducerResult> _producerResults = new List<PowerProducerResult>();
        private readonly List<PowerComponentResult> _componentResults = new List<PowerComponentResult>();
        private readonly Dictionary<StableId, BuildingPowerResult> _byBuilding = new Dictionary<StableId, BuildingPowerResult>();
        private readonly Dictionary<int, int> _capacityByComponent = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _remainingByComponent = new Dictionary<int, int>();
        private IReadOnlyList<StableId> _dirtyBuildingIds = Array.AsReadOnly(Array.Empty<StableId>());
        private int _roadVersion = -1, _buildingVersion = -1, _producerVersion = -1;

        public PowerNetwork(RoadConnectivityGraph roads, BuildingStateCollection buildings,
            PowerProducerCollection producers, IBuildingPowerProfileResolver buildingProfiles,
            IUtilityPowerProfileResolver utilityProfiles)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _producers = producers ?? throw new ArgumentNullException(nameof(producers));
            _buildingProfiles = buildingProfiles ?? throw new ArgumentNullException(nameof(buildingProfiles));
            _utilityProfiles = utilityProfiles ?? throw new ArgumentNullException(nameof(utilityProfiles));
            Rebuild();
        }

        public int NetworkVersion { get; private set; }
        public IReadOnlyList<BuildingPowerResult> Buildings { get { RefreshIfNeeded(); return _buildingResults.AsReadOnly(); } }
        public IReadOnlyList<PowerProducerResult> Producers { get { RefreshIfNeeded(); return _producerResults.AsReadOnly(); } }
        public IReadOnlyList<PowerComponentResult> Components { get { RefreshIfNeeded(); return _componentResults.AsReadOnly(); } }
        public IReadOnlyList<StableId> DirtyBuildingIds { get { RefreshIfNeeded(); return _dirtyBuildingIds; } }

        public bool RefreshIfNeeded()
        {
            var roadVersion = _roads.GraphVersion;
            if (roadVersion == _roadVersion && _buildings.Version == _buildingVersion && _producers.Version == _producerVersion)
                return false;
            Rebuild(); return true;
        }

        public bool TryGetBuilding(StableId id, out BuildingPowerResult result)
        {
            RefreshIfNeeded(); return _byBuilding.TryGetValue(id, out result);
        }

        public BuildingPowerResult GetBuilding(StableId id)
        {
            if (!TryGetBuilding(id, out var result)) throw new KeyNotFoundException($"Building {id} has no power result.");
            return result;
        }

        public PowerEligibilityResult QueryEligibility(CellCoord anchor, BuildingPowerProfile profile)
        {
            RefreshIfNeeded();
            var component = FindConnectionComponent(anchor, profile.Footprint);
            if (component == NoComponent)
                return new PowerEligibilityResult(false, NoComponent, profile.Demand, 0, PowerSupplyFailureReason.NoRoadConnection);
            _remainingByComponent.TryGetValue(component, out var available);
            _capacityByComponent.TryGetValue(component, out var totalCapacity);
            if (totalCapacity <= 0)
                return new PowerEligibilityResult(false, component, profile.Demand, available, PowerSupplyFailureReason.NoPowerProducer);
            if (available < profile.Demand)
                return new PowerEligibilityResult(false, component, profile.Demand, available, PowerSupplyFailureReason.InsufficientCapacity);
            return new PowerEligibilityResult(true, component, profile.Demand, available, PowerSupplyFailureReason.None);
        }

        public ulong ComputeChecksum()
        {
            RefreshIfNeeded(); var hash = FnvOffset;
            foreach (var producer in _producerResults)
            {
                Add(ref hash, producer.ProducerId.Value); Add(ref hash, (ulong)(uint)producer.ComponentId);
                Add(ref hash, (ulong)(uint)producer.Production); Add(ref hash, (byte)producer.ConnectionReason);
            }
            foreach (var building in _buildingResults)
            {
                Add(ref hash, building.BuildingId.Value); Add(ref hash, (ulong)(uint)building.ComponentId);
                Add(ref hash, (ulong)(uint)building.Demand); Add(ref hash, (ulong)(uint)building.Allocated);
                Add(ref hash, (byte)building.FailureReason);
            }
            return hash;
        }

        private void Rebuild()
        {
            var previous = new Dictionary<StableId, BuildingPowerResult>(_byBuilding);
            _buildingResults.Clear(); _producerResults.Clear(); _componentResults.Clear(); _byBuilding.Clear();
            _capacityByComponent.Clear(); _remainingByComponent.Clear();
            var demandByComponent = new Dictionary<int, int>();
            var allocatedByComponent = new Dictionary<int, int>();
            foreach (var producer in _producers.Producers)
            {
                var component = FindConnectionComponent(producer.Anchor, new BuildingFootprint(1, 1));
                var reason = PowerProducerConnectionReason.None;
                var production = 0;
                if (!_utilityProfiles.TryGetPowerProfile(producer.DefinitionId, out var profile))
                    reason = PowerProducerConnectionReason.UnknownUtilityDefinition;
                else if (component == NoComponent)
                    reason = PowerProducerConnectionReason.NoRoadConnection;
                else
                {
                    production = Math.Min(producer.CurrentProduction, profile.Capacity);
                    _remainingByComponent.TryGetValue(component, out var existing);
                    _remainingByComponent[component] = checked(existing + production);
                    _capacityByComponent[component] = _remainingByComponent[component];
                }
                _producerResults.Add(new PowerProducerResult(producer.Id, producer.Anchor, component, production, reason));
            }

            foreach (var building in _buildings.Buildings)
            {
                var component = NoComponent; var demand = 0; var allocated = 0;
                var reason = PowerSupplyFailureReason.None;
                if (!_buildingProfiles.TryGetPowerProfile(building.DefinitionId, out var profile))
                    reason = PowerSupplyFailureReason.UnknownBuildingDefinition;
                else
                {
                    demand = profile.Demand; component = FindConnectionComponent(building.Anchor, profile.Footprint);
                    if (component == NoComponent) reason = PowerSupplyFailureReason.NoRoadConnection;
                    else
                    {
                        demandByComponent.TryGetValue(component, out var componentDemand);
                        demandByComponent[component] = checked(componentDemand + demand);
                        _remainingByComponent.TryGetValue(component, out var remaining);
                        _capacityByComponent.TryGetValue(component, out var totalCapacity);
                        if (totalCapacity <= 0) reason = PowerSupplyFailureReason.NoPowerProducer;
                        else if (remaining < demand) reason = PowerSupplyFailureReason.InsufficientCapacity;
                        else
                        {
                            allocated = demand; _remainingByComponent[component] = remaining - demand;
                            allocatedByComponent.TryGetValue(component, out var componentAllocated);
                            allocatedByComponent[component] = checked(componentAllocated + demand);
                        }
                    }
                }
                var result = new BuildingPowerResult(building.Id, building.Anchor, component, demand, allocated, reason);
                _buildingResults.Add(result); _byBuilding.Add(building.Id, result);
            }

            var componentIds = new List<int>(_capacityByComponent.Keys);
            foreach (var componentId in demandByComponent.Keys)
                if (!componentIds.Contains(componentId)) componentIds.Add(componentId);
            componentIds.Sort();
            foreach (var componentId in componentIds)
            {
                _capacityByComponent.TryGetValue(componentId, out var capacity);
                demandByComponent.TryGetValue(componentId, out var demand);
                allocatedByComponent.TryGetValue(componentId, out var allocated);
                _componentResults.Add(new PowerComponentResult(componentId, capacity, demand, allocated));
            }

            var dirty = new List<StableId>();
            foreach (var result in _buildingResults)
                if (!previous.TryGetValue(result.BuildingId, out var old) || old != result) dirty.Add(result.BuildingId);
            foreach (var old in previous) if (!_byBuilding.ContainsKey(old.Key)) dirty.Add(old.Key);
            dirty.Sort(); _dirtyBuildingIds = dirty.AsReadOnly();
            _roadVersion = _roads.GraphVersion; _buildingVersion = _buildings.Version; _producerVersion = _producers.Version;
            NetworkVersion = checked(NetworkVersion + 1);
        }

        private int FindConnectionComponent(CellCoord anchor, BuildingFootprint footprint)
        {
            var foundIndex = int.MaxValue; var component = NoComponent;
            for (var y = anchor.Y; y < anchor.Y + footprint.Height; y++)
            for (var x = anchor.X; x < anchor.X + footprint.Width; x++)
            {
                TryCandidate(new CellCoord(x, y - 1), ref foundIndex, ref component);
                TryCandidate(new CellCoord(x - 1, y), ref foundIndex, ref component);
                TryCandidate(new CellCoord(x + 1, y), ref foundIndex, ref component);
                TryCandidate(new CellCoord(x, y + 1), ref foundIndex, ref component);
            }
            return component;
        }

        private void TryCandidate(CellCoord cell, ref int foundIndex, ref int component)
        {
            if (!_roads.TryGet(cell, out var road) || !road.HasRoad) return;
            var index = checked(cell.Y * _roads.Width + cell.X);
            if (index >= foundIndex) return;
            foundIndex = index; component = road.ComponentId;
        }

        private static void Add(ref ulong hash, byte value) { hash ^= value; hash *= FnvPrime; }
        private static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) Add(ref hash, (byte)(value >> shift));
        }
    }
}
