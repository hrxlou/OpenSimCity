using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Simulation.Transit
{
    public sealed class TransitModeChoiceState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private TransitModeChoiceRow[] _rows = Array.Empty<TransitModeChoiceRow>();
        private IReadOnlyList<TransitModeChoiceRow> _readOnlyRows = Array.AsReadOnly(Array.Empty<TransitModeChoiceRow>());
        private readonly Dictionary<TransitOdKey, TransitModeChoiceRow> _lookup = new Dictionary<TransitOdKey, TransitModeChoiceRow>();
        public IReadOnlyList<TransitModeChoiceRow> Rows => _readOnlyRows;
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int RequestedCommuteTrips { get; private set; }
        public int BusTrips { get; private set; }
        public int PrivateVehicleTrips { get; private set; }
        public int SourceNetworkVersion { get; private set; }
        public int SourceRoadVersion { get; private set; }
        public int SourceDemandVersion { get; private set; }
        public int EffectiveBudgetPermille { get; private set; }
        public bool BudgetAffordable { get; private set; }
        public int Version { get; private set; }

        internal void Set(SimulationDate month, IList<TransitModeChoiceRow> rows, int sourceNetworkVersion,
            int sourceRoadVersion, int sourceDemandVersion, int effectiveBudgetPermille, bool budgetAffordable)
        {
            _rows = new TransitModeChoiceRow[rows.Count];
            for (var index = 0; index < rows.Count; index++) _rows[index] = rows[index];
            Array.Sort(_rows, (left, right) => left.Key.CompareTo(right.Key));
            _readOnlyRows = Array.AsReadOnly(_rows); _lookup.Clear();
            var requested = 0; var bus = 0; var privateVehicles = 0;
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                if (_lookup.ContainsKey(row.Key)) throw new InvalidOperationException("Transit mode-choice OD keys must be unique.");
                _lookup.Add(row.Key, row);
                requested = checked(requested + row.RequestedTrips);
                bus = checked(bus + row.BusTrips);
                privateVehicles = checked(privateVehicles + row.PrivateVehicleTrips);
            }
            RequestedCommuteTrips = requested; BusTrips = bus; PrivateVehicleTrips = privateVehicles;
            LastProcessedMonth = month; HasProcessedMonth = true;
            SourceNetworkVersion = sourceNetworkVersion; SourceRoadVersion = sourceRoadVersion; SourceDemandVersion = sourceDemandVersion;
            EffectiveBudgetPermille = effectiveBudgetPermille; BudgetAffordable = budgetAffordable;
            Version = checked(Version + 1);
        }

        public int GetPrivateVehicleAmount(TripDemandRow demand)
        {
            if (demand.Kind != TripDemandKind.Commute || demand.Status != TripDemandStatus.Matched) return demand.Amount;
            return _lookup.TryGetValue(new TransitOdKey(demand), out var row) ? row.PrivateVehicleTrips : demand.Amount;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Year : 0); AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Month : 0);
            AddInt(ref hash, EffectiveBudgetPermille); AddByte(ref hash, BudgetAffordable ? (byte)1 : (byte)0);
            AddInt(ref hash, RequestedCommuteTrips); AddInt(ref hash, BusTrips); AddInt(ref hash, PrivateVehicleTrips); AddInt(ref hash, _rows.Length);
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index]; AddByte(ref hash, (byte)row.Key.Kind); AddUlong(ref hash, row.Key.OriginId.Value); AddUlong(ref hash, row.Key.DestinationId.Value);
                AddUlong(ref hash, row.HasLine ? row.LineId.RawValue : 0UL); AddInt(ref hash, row.RequestedTrips); AddInt(ref hash, row.BusTrips); AddInt(ref hash, row.PrivateVehicleTrips);
                AddInt(ref hash, row.CarGeneralizedCostSeconds); AddInt(ref hash, row.BusGeneralizedCostSeconds); AddByte(ref hash, (byte)row.Reason);
            }
            return hash;
        }
        private static void AddByte(ref ulong hash, byte value) { hash ^= value; hash *= FnvPrime64; }
        private static void AddInt(ref ulong hash, int value) { unchecked { AddByte(ref hash, (byte)value); AddByte(ref hash, (byte)(value >> 8)); AddByte(ref hash, (byte)(value >> 16)); AddByte(ref hash, (byte)(value >> 24)); } }
        private static void AddUlong(ref ulong hash, ulong value) { for (var shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(value >> shift)); }
    }

    public sealed class TransitModeChoiceSystem
    {
        private readonly TransitNetworkState _network;
        private readonly RoadNetworkState _roads;
        private readonly TripDemandSystem _tripDemand;
        private readonly DeterministicRoutePlanner _planner;
        private readonly TransitModeChoiceParameters _parameters;
        private ITransitGeneralizedCostPolicy _costPolicy = IdentityTransitGeneralizedCostPolicy.Instance;
        private int _appliedCostPolicyVersion = -1;

        public TransitModeChoiceSystem(TransitNetworkState network, RoadNetworkState roads, RoadConnectivityGraph graph,
            RoadDefinitionCatalog roadDefinitions, TripDemandSystem tripDemand, TransitModeChoiceParameters parameters = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _tripDemand = tripDemand ?? throw new ArgumentNullException(nameof(tripDemand));
            _planner = new DeterministicRoutePlanner(roads, graph ?? throw new ArgumentNullException(nameof(graph)), roadDefinitions ?? throw new ArgumentNullException(nameof(roadDefinitions)));
            _parameters = parameters ?? TransitModeChoiceParameters.Default;
            State = new TransitModeChoiceState();
        }

        public TransitModeChoiceState State { get; }
        public TransitModeChoiceParameters Parameters => _parameters;
        public ITransitGeneralizedCostPolicy CostPolicy
        {
            get => _costPolicy;
            set => _costPolicy = value ?? IdentityTransitGeneralizedCostPolicy.Instance;
        }

        public void RunMonth(SimulationDate month, int effectiveBudgetPermille, bool budgetAffordable)
        {
            if (!_tripDemand.State.HasProcessedMonth || _tripDemand.State.LastProcessedMonth != month)
                throw new InvalidOperationException("Transit mode choice requires trip demand from the same month.");
            if (effectiveBudgetPermille < 0 || effectiveBudgetPermille > 1000) throw new ArgumentOutOfRangeException(nameof(effectiveBudgetPermille));
            Rebuild(month, effectiveBudgetPermille, budgetAffordable);
        }

        public bool RefreshIfNeeded(int effectiveBudgetPermille, bool budgetAffordable)
        {
            if (!_tripDemand.State.HasProcessedMonth) return false;
            if (State.HasProcessedMonth && State.SourceNetworkVersion == _network.Version && State.SourceRoadVersion == _roads.Version &&
                State.SourceDemandVersion == _tripDemand.State.Version && State.EffectiveBudgetPermille == effectiveBudgetPermille &&
                State.BudgetAffordable == budgetAffordable && _appliedCostPolicyVersion == _costPolicy.Version) return false;
            Rebuild(_tripDemand.State.LastProcessedMonth, effectiveBudgetPermille, budgetAffordable);
            return true;
        }

        public int GetPrivateVehicleAmount(TripDemandRow demand) => State.GetPrivateVehicleAmount(demand);

        private void Rebuild(SimulationDate month, int effectiveBudgetPermille, bool budgetAffordable)
        {
            var rows = new List<TransitModeChoiceRow>();
            var remainingCapacity = BuildLineCapacity(effectiveBudgetPermille);
            var demands = _tripDemand.State.Rows;
            for (var demandIndex = 0; demandIndex < demands.Count; demandIndex++)
            {
                var demand = demands[demandIndex];
                if (demand.Kind != TripDemandKind.Commute || demand.Status != TripDemandStatus.Matched) continue;
                var carCost = CarCostSeconds(demand);
                if (!budgetAffordable) { rows.Add(Blocked(demand, carCost, TransitModeChoiceReason.BudgetUnaffordable)); continue; }
                if (effectiveBudgetPermille == 0) { rows.Add(Blocked(demand, carCost, TransitModeChoiceReason.BudgetDisabled)); continue; }
                if (!TryFindBestLine(demand, month, effectiveBudgetPermille, out var candidate, out var reason)) { rows.Add(Blocked(demand, carCost, reason)); continue; }
                var ratioPermille = checked(candidate.GeneralizedCostSeconds * 1000 / Math.Max(1, carCost));
                if (ratioPermille > _parameters.MaximumGeneralizedCostPermille)
                {
                    rows.Add(new TransitModeChoiceRow(new TransitOdKey(demand), candidate.Line.Id, demand.Amount, 0, demand.Amount,
                        carCost, candidate.GeneralizedCostSeconds, TransitModeChoiceReason.TooIndirect));
                    continue;
                }
                var sharePermille = ModeSharePermille(carCost, candidate.GeneralizedCostSeconds);
                var desired = checked(demand.Amount * sharePermille / 1000);
                if (sharePermille > 0 && desired == 0) desired = 1;
                var available = remainingCapacity[candidate.Line.Id];
                var busTrips = Math.Min(desired, available); remainingCapacity[candidate.Line.Id] = available - busTrips;
                var blockReason = busTrips > 0 ? TransitModeChoiceReason.None : TransitModeChoiceReason.CapacityExhausted;
                rows.Add(new TransitModeChoiceRow(new TransitOdKey(demand), candidate.Line.Id, demand.Amount, busTrips,
                    demand.Amount - busTrips, carCost, candidate.GeneralizedCostSeconds, blockReason));
            }
            State.Set(month, rows, _network.Version, _roads.Version, _tripDemand.State.Version, effectiveBudgetPermille, budgetAffordable);
            _appliedCostPolicyVersion = _costPolicy.Version;
        }

        private Dictionary<TransitLineId, int> BuildLineCapacity(int budgetPermille)
        {
            var result = new Dictionary<TransitLineId, int>();
            var lines = _network.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var capacity = line.IsEnabled ? checked(line.ServiceUnits * _parameters.CapacityPerServiceUnit * budgetPermille / 1000) : 0;
                result.Add(line.Id, capacity);
            }
            return result;
        }

        private int CarCostSeconds(TripDemandRow demand)
        {
            var routes = _planner.GetRoutes(demand.Origin.RoadCell, demand.Destination.RoadCell);
            return routes.IsReachable && routes.Routes.Count > 0 ? Math.Max(1, routes.Routes[0].FreeFlowTravelSeconds) : 1;
        }

        private int ModeSharePermille(int carCost, int busCost)
        {
            if (busCost <= carCost) return _parameters.MaximumModeSharePermille;
            var ratio = checked(busCost * 1000 / Math.Max(1, carCost));
            if (ratio >= _parameters.MaximumGeneralizedCostPermille) return 0;
            return checked(_parameters.MaximumModeSharePermille * (_parameters.MaximumGeneralizedCostPermille - ratio) /
                (_parameters.MaximumGeneralizedCostPermille - 1000));
        }

        private bool TryFindBestLine(TripDemandRow demand, SimulationDate month, int budgetPermille, out Candidate best, out TransitModeChoiceReason reason)
        {
            best = default; reason = TransitModeChoiceReason.NoEnabledLine;
            var hasEnabled = false; var sawOrigin = false; var sawDestination = false; var sawReverseDestination = false; var sawBroken = false; var hasBest = false;
            var lines = _network.Lines;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex]; if (!line.IsEnabled) continue; hasEnabled = true;
                for (var originIndex = 0; originIndex < line.StopIds.Count - 1; originIndex++)
                {
                    var originStop = _network.TryGetStop(line.StopIds[originIndex], out var origin) ? origin : null;
                    if (originStop == null || originStop.RoadCell.ManhattanDistanceTo(demand.Origin.RoadCell) > _parameters.MaximumWalkingCells) continue;
                    sawOrigin = true;
                    for (var destinationIndex = 0; destinationIndex < line.StopIds.Count; destinationIndex++)
                    {
                        if (!_network.TryGetStop(line.StopIds[destinationIndex], out var destination) ||
                            destination.RoadCell.ManhattanDistanceTo(demand.Destination.RoadCell) > _parameters.MaximumWalkingCells) continue;
                        sawDestination = true;
                        if (destinationIndex <= originIndex) { sawReverseDestination = true; continue; }
                        if (!TryLineTravelSeconds(line, originIndex, destinationIndex, out var travelSeconds)) { sawBroken = true; continue; }
                        var walkingCells = originStop.RoadCell.ManhattanDistanceTo(demand.Origin.RoadCell) + destination.RoadCell.ManhattanDistanceTo(demand.Destination.RoadCell);
                        var waitSeconds = checked(_parameters.BaseWaitSeconds * 1000 / Math.Max(1, budgetPermille));
                        var baseGeneralized = checked(walkingCells * _parameters.WalkingSecondsPerCell + waitSeconds + travelSeconds +
                            (destinationIndex - originIndex) * _parameters.StopDwellSeconds);
                        var generalized = _costPolicy.AdjustBusCostSeconds(new TransitOdKey(demand), month, baseGeneralized);
                        if (generalized <= 0) throw new InvalidOperationException("Transit cost policy must return a positive generalized cost.");
                        var candidate = new Candidate(line, generalized, originIndex, destinationIndex);
                        if (!hasBest || candidate.CompareTo(best) < 0) { best = candidate; hasBest = true; }
                    }
                }
            }
            if (hasBest) { reason = TransitModeChoiceReason.None; return true; }
            if (!hasEnabled) reason = TransitModeChoiceReason.NoEnabledLine;
            else if (!sawOrigin) reason = TransitModeChoiceReason.OriginOutsideWalkingRange;
            else if (!sawDestination) reason = TransitModeChoiceReason.DestinationOutsideWalkingRange;
            else if (sawBroken) reason = TransitModeChoiceReason.BrokenLine;
            else if (sawReverseDestination) reason = TransitModeChoiceReason.DirectionMismatch;
            return false;
        }

        private bool TryLineTravelSeconds(TransitLineState line, int originStopIndex, int destinationStopIndex, out int seconds)
        {
            long total = 0;
            for (var index = originStopIndex; index < destinationStopIndex; index++)
            {
                if (!_network.TryGetStop(line.StopIds[index], out var from) || !_network.TryGetStop(line.StopIds[index + 1], out var to)) { seconds = 0; return false; }
                var routeSet = _planner.GetRoutes(from.RoadCell, to.RoadCell);
                if (!routeSet.IsReachable || routeSet.Routes.Count == 0) { seconds = 0; return false; }
                total += routeSet.Routes[0].FreeFlowTravelSeconds;
            }
            if (total > int.MaxValue) throw new OverflowException("Transit line travel time exceeded Int32 range.");
            seconds = (int)total; return true;
        }

        private static TransitModeChoiceRow Blocked(TripDemandRow demand, int carCost, TransitModeChoiceReason reason) =>
            new TransitModeChoiceRow(new TransitOdKey(demand), default, demand.Amount, 0, demand.Amount, carCost, 0, reason);

        private readonly struct Candidate : IComparable<Candidate>
        {
            public Candidate(TransitLineState line, int generalizedCostSeconds, int originIndex, int destinationIndex)
            { Line = line; GeneralizedCostSeconds = generalizedCostSeconds; OriginIndex = originIndex; DestinationIndex = destinationIndex; }
            public TransitLineState Line { get; }
            public int GeneralizedCostSeconds { get; }
            public int OriginIndex { get; }
            public int DestinationIndex { get; }
            public int CompareTo(Candidate other)
            {
                var comparison = GeneralizedCostSeconds.CompareTo(other.GeneralizedCostSeconds);
                if (comparison != 0) return comparison;
                comparison = Line.Id.CompareTo(other.Line.Id); if (comparison != 0) return comparison;
                comparison = OriginIndex.CompareTo(other.OriginIndex); return comparison != 0 ? comparison : DestinationIndex.CompareTo(other.DestinationIndex);
            }
        }
    }
}
