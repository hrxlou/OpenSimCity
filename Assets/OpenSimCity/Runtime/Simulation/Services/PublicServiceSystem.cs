using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Simulation.Services
{
    public enum ServiceKind : byte
    {
        Police,
        Fire,
        School,
        Healthcare,
        Waste
    }

    public enum ServiceCoverageStrategy : byte
    {
        SpatialDistance,
        RoadTravelTime
    }

    public enum ServiceCoverageFailureReason : byte
    {
        None,
        NoFacility,
        NoRoadAccess,
        DifferentRoadComponent,
        OutOfRange,
        CapacityExceeded,
        BudgetTooLow
    }

    [Serializable]
    public sealed class ServiceDefinition
    {
        public ServiceDefinition(
            StableId id,
            string name,
            ServiceKind kind,
            ServiceCoverageStrategy coverageStrategy,
            int maximumCost,
            int capacity,
            int constructionCost,
            int monthlyOperatingCost)
        {
            if (!id.IsValid) throw new ArgumentException("Service definition ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Service name is required.", nameof(name));
            if (maximumCost <= 0 || capacity <= 0 || constructionCost < 0 || monthlyOperatingCost < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCost));
            Id = id;
            Name = name.Trim();
            Kind = kind;
            CoverageStrategy = coverageStrategy;
            MaximumCost = maximumCost;
            Capacity = capacity;
            ConstructionCost = constructionCost;
            MonthlyOperatingCost = monthlyOperatingCost;
        }

        public StableId Id { get; }
        public string Name { get; }
        public ServiceKind Kind { get; }
        public ServiceCoverageStrategy CoverageStrategy { get; }
        public int MaximumCost { get; }
        public int Capacity { get; }
        public int ConstructionCost { get; }
        public int MonthlyOperatingCost { get; }
    }

    public static class ServiceDefinitionIds
    {
        private const ulong Namespace = 0xF701_0000_0000_0000UL;
        public static readonly StableId Police = new StableId(Namespace | 0x01UL);
        public static readonly StableId Fire = new StableId(Namespace | 0x02UL);
        public static readonly StableId School = new StableId(Namespace | 0x03UL);
        public static readonly StableId Healthcare = new StableId(Namespace | 0x04UL);
        public static readonly StableId Waste = new StableId(Namespace | 0x05UL);
    }

    public sealed class ServiceDefinitionCatalog
    {
        private readonly SortedDictionary<StableId, ServiceDefinition> _byId =
            new SortedDictionary<StableId, ServiceDefinition>();

        public ServiceDefinitionCatalog(IEnumerable<ServiceDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions)
            {
                if (definition == null || _byId.ContainsKey(definition.Id))
                    throw new ArgumentException("Service definitions require unique IDs.", nameof(definitions));
                _byId.Add(definition.Id, definition);
            }
            if (_byId.Count == 0) throw new ArgumentException("At least one service definition is required.", nameof(definitions));
        }

        public IReadOnlyList<ServiceDefinition> Definitions =>
            new List<ServiceDefinition>(_byId.Values).AsReadOnly();

        public ServiceDefinition Get(StableId id)
        {
            if (_byId.TryGetValue(id, out var definition)) return definition;
            throw new KeyNotFoundException($"Unknown service definition {id.Value}.");
        }

        public bool TryGet(StableId id, out ServiceDefinition definition) => _byId.TryGetValue(id, out definition);

        public static ServiceDefinitionCatalog CreateDefault()
        {
            return new ServiceDefinitionCatalog(new[]
            {
                new ServiceDefinition(ServiceDefinitionIds.Police, "Police", ServiceKind.Police,
                    ServiceCoverageStrategy.RoadTravelTime, 1_200, 180, 1_500, 120),
                new ServiceDefinition(ServiceDefinitionIds.Fire, "Fire", ServiceKind.Fire,
                    ServiceCoverageStrategy.RoadTravelTime, 1_000, 150, 1_700, 140),
                new ServiceDefinition(ServiceDefinitionIds.School, "School", ServiceKind.School,
                    ServiceCoverageStrategy.SpatialDistance, 8, 240, 1_300, 100),
                new ServiceDefinition(ServiceDefinitionIds.Healthcare, "Healthcare", ServiceKind.Healthcare,
                    ServiceCoverageStrategy.RoadTravelTime, 1_400, 200, 1_900, 150),
                new ServiceDefinition(ServiceDefinitionIds.Waste, "Waste", ServiceKind.Waste,
                    ServiceCoverageStrategy.RoadTravelTime, 1_600, 300, 1_600, 130)
            });
        }
    }

    [Serializable]
    public sealed class ServiceFacilityState
    {
        public ServiceFacilityState(StableId id, StableId definitionId, CellCoord cell)
        {
            if (!id.IsValid || !definitionId.IsValid)
                throw new ArgumentException("Service facility and definition IDs are required.");
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
            ActiveBudgetPermille = 1000;
        }

        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
        public int ActiveBudgetPermille { get; private set; }
        public bool HasPendingBudget { get; private set; }
        public int PendingBudgetPermille { get; private set; }
        public SimulationDate PendingEffectiveMonth { get; private set; }
        public int Version { get; private set; }

        public void ScheduleBudget(int budgetPermille, SimulationDate effectiveMonth)
        {
            if (budgetPermille < 0 || budgetPermille > 1500)
                throw new ArgumentOutOfRangeException(nameof(budgetPermille));
            PendingBudgetPermille = budgetPermille;
            PendingEffectiveMonth = effectiveMonth;
            HasPendingBudget = true;
            Version = checked(Version + 1);
        }

        public bool ApplyBudget(SimulationDate month)
        {
            if (!HasPendingBudget || month.CompareTo(PendingEffectiveMonth) < 0)
                return false;
            ActiveBudgetPermille = PendingBudgetPermille;
            PendingBudgetPermille = 0;
            PendingEffectiveMonth = default;
            HasPendingBudget = false;
            Version = checked(Version + 1);
            return true;
        }

        internal void RestoreBudget(
            int activeBudgetPermille,
            bool hasPending,
            int pendingBudgetPermille,
            SimulationDate pendingEffectiveMonth)
        {
            if (activeBudgetPermille < 0 || activeBudgetPermille > 1500 ||
                pendingBudgetPermille < 0 || pendingBudgetPermille > 1500)
                throw new ArgumentOutOfRangeException(nameof(activeBudgetPermille));
            ActiveBudgetPermille = activeBudgetPermille;
            HasPendingBudget = hasPending;
            PendingBudgetPermille = hasPending ? pendingBudgetPermille : 0;
            PendingEffectiveMonth = hasPending ? pendingEffectiveMonth : default;
            Version = 1;
        }
    }

    public sealed class ServiceFacilityCollection
    {
        private readonly SortedDictionary<StableId, ServiceFacilityState> _byId =
            new SortedDictionary<StableId, ServiceFacilityState>();

        public IReadOnlyList<ServiceFacilityState> Facilities =>
            new List<ServiceFacilityState>(_byId.Values).AsReadOnly();
        public int Count => _byId.Count;
        public int Version { get; private set; }

        public bool TryGet(StableId id, out ServiceFacilityState facility) => _byId.TryGetValue(id, out facility);

        public bool Add(ServiceFacilityState facility)
        {
            if (facility == null) throw new ArgumentNullException(nameof(facility));
            if (_byId.ContainsKey(facility.Id)) return false;
            _byId.Add(facility.Id, facility);
            Version = checked(Version + 1);
            return true;
        }

        public bool Remove(StableId id)
        {
            if (!_byId.Remove(id)) return false;
            Version = checked(Version + 1);
            return true;
        }

        internal void Touch() => Version = checked(Version + 1);

        internal void ClearAndRestore(IEnumerable<ServiceFacilityState> facilities)
        {
            _byId.Clear();
            foreach (var facility in facilities)
            {
                if (facility == null || _byId.ContainsKey(facility.Id))
                    throw new ArgumentException("Service facilities must be unique.", nameof(facilities));
                _byId.Add(facility.Id, facility);
            }
            Version = _byId.Count > 0 ? 1 : 0;
        }
    }

    [Serializable]
    public readonly struct ServiceDemandNode : IComparable<ServiceDemandNode>
    {
        public ServiceDemandNode(
            StableId buildingId,
            CellCoord cell,
            CellCoord roadCell,
            int roadComponentId,
            int amount)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Service demand requires building ID.", nameof(buildingId));
            if (roadComponentId < -1 || amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            BuildingId = buildingId;
            Cell = cell;
            RoadCell = roadCell;
            RoadComponentId = roadComponentId;
            Amount = amount;
        }

        public StableId BuildingId { get; }
        public CellCoord Cell { get; }
        public CellCoord RoadCell { get; }
        public int RoadComponentId { get; }
        public int Amount { get; }
        public bool HasRoadAccess => RoadComponentId >= 0;
        public int CompareTo(ServiceDemandNode other) => BuildingId.CompareTo(other.BuildingId);
    }

    [Serializable]
    public readonly struct ServiceCoverageCostResult
    {
        private ServiceCoverageCostResult(bool isReachable, int cost, ServiceCoverageFailureReason reason)
        {
            IsReachable = isReachable;
            Cost = cost;
            FailureReason = reason;
        }

        public bool IsReachable { get; }
        public int Cost { get; }
        public ServiceCoverageFailureReason FailureReason { get; }
        public static ServiceCoverageCostResult Reachable(int cost) =>
            new ServiceCoverageCostResult(true, cost, ServiceCoverageFailureReason.None);
        public static ServiceCoverageCostResult Unreachable(ServiceCoverageFailureReason reason) =>
            new ServiceCoverageCostResult(false, int.MaxValue, reason);
    }

    public interface IServiceDemandProvider
    {
        IReadOnlyList<ServiceDemandNode> GetDemand(ServiceKind kind);
    }

    public interface IServiceCoverageCostResolver
    {
        ServiceCoverageCostResult Resolve(
            ServiceFacilityState facility,
            ServiceDemandNode demand,
            ServiceCoverageStrategy strategy);
    }

    [Serializable]
    public readonly struct ServiceCoverageRow : IComparable<ServiceCoverageRow>
    {
        public ServiceCoverageRow(
            StableId buildingId,
            ServiceKind kind,
            StableId facilityId,
            int requested,
            int served,
            int coveragePermille,
            int travelCost,
            ServiceCoverageFailureReason failureReason)
        {
            BuildingId = buildingId;
            Kind = kind;
            FacilityId = facilityId;
            Requested = requested;
            Served = served;
            CoveragePermille = coveragePermille;
            TravelCost = travelCost;
            FailureReason = failureReason;
        }

        public StableId BuildingId { get; }
        public ServiceKind Kind { get; }
        public StableId FacilityId { get; }
        public int Requested { get; }
        public int Served { get; }
        public int CoveragePermille { get; }
        public int TravelCost { get; }
        public ServiceCoverageFailureReason FailureReason { get; }
        public int CompareTo(ServiceCoverageRow other)
        {
            var comparison = BuildingId.CompareTo(other.BuildingId);
            return comparison != 0 ? comparison : Kind.CompareTo(other.Kind);
        }
    }

    [Serializable]
    public readonly struct ServiceFacilityUsageRow : IComparable<ServiceFacilityUsageRow>
    {
        public ServiceFacilityUsageRow(
            StableId facilityId,
            ServiceKind kind,
            int effectiveBudgetPermille,
            int effectiveCapacity,
            int usage,
            int effectiveMaximumCost,
            int monthlyCost)
        {
            FacilityId = facilityId;
            Kind = kind;
            EffectiveBudgetPermille = effectiveBudgetPermille;
            EffectiveCapacity = effectiveCapacity;
            Usage = usage;
            EffectiveMaximumCost = effectiveMaximumCost;
            MonthlyCost = monthlyCost;
        }

        public StableId FacilityId { get; }
        public ServiceKind Kind { get; }
        public int EffectiveBudgetPermille { get; }
        public int EffectiveCapacity { get; }
        public int Usage { get; }
        public int EffectiveMaximumCost { get; }
        public int MonthlyCost { get; }
        public int CompareTo(ServiceFacilityUsageRow other) => FacilityId.CompareTo(other.FacilityId);
    }

    public sealed class PublicServiceState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private IReadOnlyList<ServiceCoverageRow> _coverage =
            Array.AsReadOnly(Array.Empty<ServiceCoverageRow>());
        private IReadOnlyList<ServiceFacilityUsageRow> _usage =
            Array.AsReadOnly(Array.Empty<ServiceFacilityUsageRow>());

        public IReadOnlyList<ServiceCoverageRow> Coverage => _coverage;
        public IReadOnlyList<ServiceFacilityUsageRow> FacilityUsage => _usage;
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int RequestedTotal { get; private set; }
        public int ServedTotal { get; private set; }
        public int MonthlyOperatingCost { get; private set; }
        public int Version { get; private set; }

        internal void Set(
            SimulationDate month,
            IList<ServiceCoverageRow> coverage,
            IList<ServiceFacilityUsageRow> usage)
        {
            var coverageRows = new ServiceCoverageRow[coverage.Count];
            for (var index = 0; index < coverageRows.Length; index++) coverageRows[index] = coverage[index];
            Array.Sort(coverageRows);
            var usageRows = new ServiceFacilityUsageRow[usage.Count];
            for (var index = 0; index < usageRows.Length; index++) usageRows[index] = usage[index];
            Array.Sort(usageRows);
            _coverage = Array.AsReadOnly(coverageRows);
            _usage = Array.AsReadOnly(usageRows);

            var requested = 0;
            var served = 0;
            var cost = 0;
            for (var index = 0; index < coverageRows.Length; index++)
            {
                requested = checked(requested + coverageRows[index].Requested);
                served = checked(served + coverageRows[index].Served);
            }
            for (var index = 0; index < usageRows.Length; index++)
                cost = checked(cost + usageRows[index].MonthlyCost);
            RequestedTotal = requested;
            ServedTotal = served;
            MonthlyOperatingCost = cost;
            HasProcessedMonth = true;
            LastProcessedMonth = month;
            Version = checked(Version + 1);
        }

        internal void ClearDerived()
        {
            _coverage = Array.AsReadOnly(Array.Empty<ServiceCoverageRow>());
            _usage = Array.AsReadOnly(Array.Empty<ServiceFacilityUsageRow>());
            RequestedTotal = 0;
            ServedTotal = 0;
            MonthlyOperatingCost = 0;
            HasProcessedMonth = false;
            LastProcessedMonth = default;
            Version = 0;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Year : 0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Month : 0);
            AddInt(ref hash, _coverage.Count);
            for (var index = 0; index < _coverage.Count; index++)
            {
                var row = _coverage[index];
                AddUlong(ref hash, row.BuildingId.Value);
                AddByte(ref hash, (byte)row.Kind);
                AddUlong(ref hash, row.FacilityId.Value);
                AddInt(ref hash, row.Requested);
                AddInt(ref hash, row.Served);
                AddInt(ref hash, row.CoveragePermille);
                AddInt(ref hash, row.TravelCost);
                AddByte(ref hash, (byte)row.FailureReason);
            }
            AddInt(ref hash, _usage.Count);
            for (var index = 0; index < _usage.Count; index++)
            {
                var row = _usage[index];
                AddUlong(ref hash, row.FacilityId.Value);
                AddByte(ref hash, (byte)row.Kind);
                AddInt(ref hash, row.EffectiveBudgetPermille);
                AddInt(ref hash, row.EffectiveCapacity);
                AddInt(ref hash, row.Usage);
                AddInt(ref hash, row.EffectiveMaximumCost);
                AddInt(ref hash, row.MonthlyCost);
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

    /// <summary>
    /// Aggregate monthly public-service allocation. Demand, distance and routing
    /// enter through pure ports; no vehicle, view or UI state participates.
    /// </summary>
    public sealed class PublicServiceSystem : IMonthlySimulationSystem
    {
        private readonly ServiceFacilityCollection _facilities;
        private readonly ServiceDefinitionCatalog _definitions;
        private readonly IServiceDemandProvider _demand;
        private readonly IServiceCoverageCostResolver _coverageResolver;

        public PublicServiceSystem(
            ServiceFacilityCollection facilities,
            ServiceDefinitionCatalog definitions,
            IServiceDemandProvider demand,
            IServiceCoverageCostResolver coverageResolver)
        {
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _demand = demand ?? throw new ArgumentNullException(nameof(demand));
            _coverageResolver = coverageResolver ?? throw new ArgumentNullException(nameof(coverageResolver));
            State = new PublicServiceState();
        }

        public PublicServiceState State { get; }
        public int AvailableFundingPermille { get; set; } = 1000;

        public void RunMonth(SimulationDate month)
        {
            if (AvailableFundingPermille < 0 || AvailableFundingPermille > 1000)
                throw new InvalidOperationException("Available service funding must be 0..1000 permille.");
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Service processing requires a strictly increasing month.");

            var facilities = _facilities.Facilities;
            for (var index = 0; index < facilities.Count; index++)
            {
                if (facilities[index].ApplyBudget(month))
                    _facilities.Touch();
            }

            var remainingCapacity = new SortedDictionary<StableId, int>();
            var usage = new SortedDictionary<StableId, int>();
            var effectiveBudget = new SortedDictionary<StableId, int>();
            var effectiveRange = new SortedDictionary<StableId, int>();
            var monthlyCost = new SortedDictionary<StableId, int>();
            for (var index = 0; index < facilities.Count; index++)
            {
                var facility = facilities[index];
                var definition = _definitions.Get(facility.DefinitionId);
                var budget = checked(facility.ActiveBudgetPermille * AvailableFundingPermille / 1000);
                var capacity = checked(definition.Capacity * budget / 1000);
                var rangeScale = Math.Max(250, budget);
                var range = checked(definition.MaximumCost * rangeScale / 1000);
                var cost = checked(definition.MonthlyOperatingCost * budget / 1000);
                effectiveBudget[facility.Id] = budget;
                remainingCapacity[facility.Id] = capacity;
                usage[facility.Id] = 0;
                effectiveRange[facility.Id] = range;
                monthlyCost[facility.Id] = cost;
            }

            var coverageRows = new List<ServiceCoverageRow>();
            foreach (ServiceKind kind in Enum.GetValues(typeof(ServiceKind)))
            {
                var demandRows = new List<ServiceDemandNode>(_demand.GetDemand(kind));
                demandRows.Sort();
                for (var demandIndex = 0; demandIndex < demandRows.Count; demandIndex++)
                {
                    var node = demandRows[demandIndex];
                    ServiceFacilityState best = null;
                    var bestCost = int.MaxValue;
                    var sawFacility = false;
                    var sawBudget = false;
                    var sawReachable = false;
                    var sawInRange = false;
                    var strongestFailure = ServiceCoverageFailureReason.NoFacility;

                    for (var facilityIndex = 0; facilityIndex < facilities.Count; facilityIndex++)
                    {
                        var facility = facilities[facilityIndex];
                        var definition = _definitions.Get(facility.DefinitionId);
                        if (definition.Kind != kind) continue;
                        sawFacility = true;
                        if (effectiveBudget[facility.Id] <= 0)
                        {
                            strongestFailure = ServiceCoverageFailureReason.BudgetTooLow;
                            continue;
                        }
                        sawBudget = true;
                        var cost = _coverageResolver.Resolve(facility, node, definition.CoverageStrategy);
                        if (!cost.IsReachable)
                        {
                            if (cost.FailureReason == ServiceCoverageFailureReason.NoRoadAccess ||
                                cost.FailureReason == ServiceCoverageFailureReason.DifferentRoadComponent)
                                strongestFailure = cost.FailureReason;
                            continue;
                        }
                        sawReachable = true;
                        if (cost.Cost > effectiveRange[facility.Id])
                        {
                            strongestFailure = ServiceCoverageFailureReason.OutOfRange;
                            continue;
                        }
                        sawInRange = true;
                        if (remainingCapacity[facility.Id] <= 0)
                        {
                            strongestFailure = ServiceCoverageFailureReason.CapacityExceeded;
                            continue;
                        }
                        if (cost.Cost < bestCost ||
                            (cost.Cost == bestCost && (best == null || facility.Id.CompareTo(best.Id) < 0)))
                        {
                            best = facility;
                            bestCost = cost.Cost;
                        }
                    }

                    if (best == null)
                    {
                        if (!sawFacility) strongestFailure = ServiceCoverageFailureReason.NoFacility;
                        else if (!sawBudget) strongestFailure = ServiceCoverageFailureReason.BudgetTooLow;
                        else if (sawReachable && !sawInRange) strongestFailure = ServiceCoverageFailureReason.OutOfRange;
                        else if (sawInRange) strongestFailure = ServiceCoverageFailureReason.CapacityExceeded;
                        coverageRows.Add(new ServiceCoverageRow(
                            node.BuildingId, kind, StableId.Invalid, node.Amount, 0, 0,
                            int.MaxValue, strongestFailure));
                        continue;
                    }

                    var served = Math.Min(node.Amount, remainingCapacity[best.Id]);
                    remainingCapacity[best.Id] -= served;
                    usage[best.Id] = checked(usage[best.Id] + served);
                    coverageRows.Add(new ServiceCoverageRow(
                        node.BuildingId,
                        kind,
                        best.Id,
                        node.Amount,
                        served,
                        checked(served * 1000 / node.Amount),
                        bestCost,
                        served == node.Amount
                            ? ServiceCoverageFailureReason.None
                            : ServiceCoverageFailureReason.CapacityExceeded));
                }
            }

            var usageRows = new List<ServiceFacilityUsageRow>();
            for (var index = 0; index < facilities.Count; index++)
            {
                var facility = facilities[index];
                var definition = _definitions.Get(facility.DefinitionId);
                usageRows.Add(new ServiceFacilityUsageRow(
                    facility.Id,
                    definition.Kind,
                    effectiveBudget[facility.Id],
                    checked(definition.Capacity * effectiveBudget[facility.Id] / 1000),
                    usage[facility.Id],
                    effectiveRange[facility.Id],
                    monthlyCost[facility.Id]));
            }
            State.Set(month, coverageRows, usageRows);
        }
    }

    [Serializable]
    public sealed class ServiceFacilityStateDto
    {
        public ulong Id;
        public ulong DefinitionId;
        public int CellX;
        public int CellY;
        public int ActiveBudgetPermille;
        public bool HasPendingBudget;
        public int PendingBudgetPermille;
        public int PendingEffectiveYear;
        public int PendingEffectiveMonth;
    }

    [Serializable]
    public sealed class PublicServiceRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public bool HasProcessedMonth;
        public int LastProcessedYear;
        public int LastProcessedMonth;
        public ServiceFacilityStateDto[] Facilities = Array.Empty<ServiceFacilityStateDto>();
    }
}
