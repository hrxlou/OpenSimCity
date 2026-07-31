using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Traffic
{
    public enum TripDemandKind : byte
    {
        Commute,
        Goods
    }

    public enum TripEndpointKind : byte
    {
        None,
        Building,
        ExternalRoadGate
    }

    public enum TripDemandStatus : byte
    {
        Matched,
        Unmet
    }

    public enum TripDemandUnmetReason : byte
    {
        None,
        NoOriginRoadAccess,
        NoDestinationRoadAccess,
        DifferentRoadComponent,
        NoDestinationCapacity,
        NoOriginCapacity,
        NoEnabledExternalRoadGate,
        ExternalRoadGateDisconnected
    }

    /// <summary>
    /// Stable identity plus the rebuildable road component used by aggregate OD
    /// matching. ComponentId is never an entity ID and is intentionally omitted
    /// from persistence; P5-02 may later resolve a path between these endpoints.
    /// </summary>
    [Serializable]
    public readonly struct TripEndpoint : IEquatable<TripEndpoint>
    {
        public TripEndpoint(
            TripEndpointKind kind,
            StableId id,
            CellCoord roadCell,
            int componentId)
        {
            if (kind == TripEndpointKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!id.IsValid)
                throw new ArgumentException("A trip endpoint requires a valid stable ID.", nameof(id));
            if (componentId < -1)
                throw new ArgumentOutOfRangeException(nameof(componentId));

            Kind = kind;
            Id = id;
            RoadCell = roadCell;
            ComponentId = componentId;
        }

        public TripEndpointKind Kind { get; }
        public StableId Id { get; }
        public CellCoord RoadCell { get; }
        public int ComponentId { get; }
        public bool HasRoadAccess => Kind != TripEndpointKind.None && Id.IsValid && ComponentId >= 0;

        public bool Equals(TripEndpoint other)
        {
            return Kind == other.Kind &&
                   Id == other.Id &&
                   RoadCell == other.RoadCell &&
                   ComponentId == other.ComponentId;
        }

        public override bool Equals(object obj) => obj is TripEndpoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Id.GetHashCode();
                hash = (hash * 397) ^ RoadCell.GetHashCode();
                return (hash * 397) ^ ComponentId;
            }
        }

        public static bool operator ==(TripEndpoint left, TripEndpoint right) => left.Equals(right);
        public static bool operator !=(TripEndpoint left, TripEndpoint right) => !left.Equals(right);
    }

    /// <summary>
    /// One aggregate origin-destination quantity for a monthly window. A row is
    /// either physically matchable within one road component or explicitly unmet;
    /// it is never a vehicle or an individual citizen shipment.
    /// </summary>
    [Serializable]
    public readonly struct TripDemandRow : IEquatable<TripDemandRow>, IComparable<TripDemandRow>
    {
        public TripDemandRow(
            TripDemandKind kind,
            TripEndpoint origin,
            TripEndpoint destination,
            int amount,
            TripDemandStatus status,
            TripDemandUnmetReason unmetReason)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (origin.Kind == TripEndpointKind.None || !origin.Id.IsValid)
                throw new ArgumentException("A trip row requires an origin identity.", nameof(origin));
            if (status == TripDemandStatus.Matched)
            {
                if (!origin.HasRoadAccess || !destination.HasRoadAccess)
                    throw new ArgumentException("Matched trip rows require road-accessible endpoints.");
                if (origin.ComponentId != destination.ComponentId)
                    throw new ArgumentException("Matched trip endpoints must share one road component.");
                if (unmetReason != TripDemandUnmetReason.None)
                    throw new ArgumentException("Matched trip rows cannot carry an unmet reason.", nameof(unmetReason));
            }
            else if (unmetReason == TripDemandUnmetReason.None)
            {
                throw new ArgumentException("Unmet trip rows require an explicit reason.", nameof(unmetReason));
            }

            Kind = kind;
            Origin = origin;
            Destination = destination;
            Amount = amount;
            Status = status;
            UnmetReason = unmetReason;
        }

        public TripDemandKind Kind { get; }
        public TripEndpoint Origin { get; }
        public TripEndpoint Destination { get; }
        public int Amount { get; }
        public TripDemandStatus Status { get; }
        public TripDemandUnmetReason UnmetReason { get; }

        public int CompareTo(TripDemandRow other)
        {
            var comparison = Kind.CompareTo(other.Kind);
            if (comparison != 0)
                return comparison;
            comparison = Origin.Kind.CompareTo(other.Origin.Kind);
            if (comparison != 0)
                return comparison;
            comparison = Origin.Id.CompareTo(other.Origin.Id);
            if (comparison != 0)
                return comparison;
            comparison = Destination.Kind.CompareTo(other.Destination.Kind);
            if (comparison != 0)
                return comparison;
            comparison = Destination.Id.CompareTo(other.Destination.Id);
            if (comparison != 0)
                return comparison;
            comparison = Status.CompareTo(other.Status);
            if (comparison != 0)
                return comparison;
            comparison = UnmetReason.CompareTo(other.UnmetReason);
            return comparison != 0 ? comparison : Amount.CompareTo(other.Amount);
        }

        public bool Equals(TripDemandRow other)
        {
            return Kind == other.Kind &&
                   Origin == other.Origin &&
                   Destination == other.Destination &&
                   Amount == other.Amount &&
                   Status == other.Status &&
                   UnmetReason == other.UnmetReason;
        }

        public override bool Equals(object obj) => obj is TripDemandRow other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ Destination.GetHashCode();
                hash = (hash * 397) ^ Amount;
                hash = (hash * 397) ^ (int)Status;
                return (hash * 397) ^ (int)UnmetReason;
            }
        }

        public static bool operator ==(TripDemandRow left, TripDemandRow right) => left.Equals(right);
        public static bool operator !=(TripDemandRow left, TripDemandRow right) => !left.Equals(right);
    }

    /// <summary>
    /// Monthly aggregate OD state. It is derived from persistent buildings, market,
    /// parcels and roads, so the save format stores those sources rather than this
    /// path-independent cache. ComputeChecksum excludes Version for restore parity.
    /// </summary>
    public sealed class TripDemandState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private TripDemandRow[] _rows = Array.Empty<TripDemandRow>();
        private IReadOnlyList<TripDemandRow> _readOnlyRows = Array.AsReadOnly(Array.Empty<TripDemandRow>());

        public IReadOnlyList<TripDemandRow> Rows => _readOnlyRows;
        public SimulationDate LastProcessedMonth { get; private set; }
        public bool HasProcessedMonth { get; private set; }
        public int Version { get; private set; }
        public int CommuteRequested { get; private set; }
        public int CommuteMatched { get; private set; }
        public int CommuteUnmet => CommuteRequested - CommuteMatched;
        public int GoodsRequested { get; private set; }
        public int GoodsMatched { get; private set; }
        public int GoodsUnmet => GoodsRequested - GoodsMatched;
        public int TotalRequested => checked(CommuteRequested + GoodsRequested);
        public int TotalMatched => checked(CommuteMatched + GoodsMatched);
        public int TotalUnmet => checked(CommuteUnmet + GoodsUnmet);

        internal void Set(SimulationDate month, IList<TripDemandRow> rows, bool derivedRestore)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            _rows = new TripDemandRow[rows.Count];
            for (var index = 0; index < rows.Count; index++)
                _rows[index] = rows[index];
            Array.Sort(_rows);
            _readOnlyRows = Array.AsReadOnly(_rows);

            var commuteRequested = 0;
            var commuteMatched = 0;
            var goodsRequested = 0;
            var goodsMatched = 0;
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                if (row.Kind == TripDemandKind.Commute)
                {
                    commuteRequested = checked(commuteRequested + row.Amount);
                    if (row.Status == TripDemandStatus.Matched)
                        commuteMatched = checked(commuteMatched + row.Amount);
                }
                else
                {
                    goodsRequested = checked(goodsRequested + row.Amount);
                    if (row.Status == TripDemandStatus.Matched)
                        goodsMatched = checked(goodsMatched + row.Amount);
                }
            }

            CommuteRequested = commuteRequested;
            CommuteMatched = commuteMatched;
            GoodsRequested = goodsRequested;
            GoodsMatched = goodsMatched;
            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = derivedRestore ? 1 : checked(Version + 1);
        }

        internal void ClearDerived()
        {
            _rows = Array.Empty<TripDemandRow>();
            _readOnlyRows = Array.AsReadOnly(_rows);
            LastProcessedMonth = default;
            HasProcessedMonth = false;
            Version = 0;
            CommuteRequested = 0;
            CommuteMatched = 0;
            GoodsRequested = 0;
            GoodsMatched = 0;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, HasProcessedMonth ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Year : 0);
            AddInt(ref hash, HasProcessedMonth ? LastProcessedMonth.Month : 0);
            AddInt(ref hash, CommuteRequested);
            AddInt(ref hash, CommuteMatched);
            AddInt(ref hash, GoodsRequested);
            AddInt(ref hash, GoodsMatched);
            AddInt(ref hash, _rows.Length);
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                AddByte(ref hash, (byte)row.Kind);
                AddEndpoint(ref hash, row.Origin);
                AddEndpoint(ref hash, row.Destination);
                AddInt(ref hash, row.Amount);
                AddByte(ref hash, (byte)row.Status);
                AddByte(ref hash, (byte)row.UnmetReason);
            }

            return hash;
        }

        private static void AddEndpoint(ref ulong hash, TripEndpoint endpoint)
        {
            AddByte(ref hash, (byte)endpoint.Kind);
            AddUlong(ref hash, endpoint.Id.Value);
            AddInt(ref hash, endpoint.RoadCell.X);
            AddInt(ref hash, endpoint.RoadCell.Y);
            AddInt(ref hash, endpoint.ComponentId);
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime64;
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }
    }

    /// <summary>
    /// Pure simulation port separating OD generation from parcel/road lookup. It
    /// also makes matching fixtures independent of any rendered vehicle objects.
    /// </summary>
    public interface ITripAccessResolver
    {
        TripEndpoint ResolveBuilding(BuildingState building);
        IReadOnlyList<TripEndpoint> GetEnabledExternalRoadGates();
    }

    /// <summary>
    /// Rebuildable adapter from persistent parcel and road state to OD endpoints.
    /// The first frontage in the parcel's stable ordering is used; no path is stored.
    /// </summary>
    public sealed class ParcelRoadTripAccessResolver : ITripAccessResolver
    {
        private readonly ParcelNetworkState _parcels;
        private readonly RoadConnectivityGraph _roads;
        private readonly ExternalConnectionState[] _externalConnections;

        public ParcelRoadTripAccessResolver(
            ParcelNetworkState parcels,
            RoadConnectivityGraph roads,
            IEnumerable<ExternalConnectionState> externalConnections)
        {
            _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            if (externalConnections == null)
                throw new ArgumentNullException(nameof(externalConnections));

            var connections = new List<ExternalConnectionState>(externalConnections);
            connections.Sort((left, right) => left.Id.CompareTo(right.Id));
            _externalConnections = connections.ToArray();
        }

        public TripEndpoint ResolveBuilding(BuildingState building)
        {
            if (building == null)
                throw new ArgumentNullException(nameof(building));

            _parcels.RefreshIfNeeded();
            _roads.RefreshIfNeeded();
            if (!_parcels.TryGet(building.ParcelId, out var parcel))
            {
                return new TripEndpoint(
                    TripEndpointKind.Building,
                    building.Id,
                    building.Anchor,
                    -1);
            }

            var frontages = parcel.Frontages;
            for (var index = 0; index < frontages.Count; index++)
            {
                var frontage = frontages[index];
                var connectivity = _roads.Get(frontage.RoadCell);
                if (connectivity.HasRoad)
                {
                    return new TripEndpoint(
                        TripEndpointKind.Building,
                        building.Id,
                        frontage.RoadCell,
                        connectivity.ComponentId);
                }
            }

            return new TripEndpoint(
                TripEndpointKind.Building,
                building.Id,
                building.Anchor,
                -1);
        }

        public IReadOnlyList<TripEndpoint> GetEnabledExternalRoadGates()
        {
            _roads.RefreshIfNeeded();
            var gates = new List<TripEndpoint>();
            for (var index = 0; index < _externalConnections.Length; index++)
            {
                var connection = _externalConnections[index];
                if (!connection.IsEnabled || connection.Kind != ExternalConnectionKind.Road)
                    continue;

                var componentId = -1;
                if (_roads.TryGet(connection.Cell, out var connectivity) && connectivity.HasRoad)
                    componentId = connectivity.ComponentId;
                gates.Add(new TripEndpoint(
                    TripEndpointKind.ExternalRoadGate,
                    connection.Id,
                    connection.Cell,
                    componentId));
            }

            return gates.AsReadOnly();
        }
    }

    /// <summary>
    /// Generates monthly building-level aggregate commute and goods OD rows from
    /// population/employment counts plus the existing single-commodity market.
    /// Matching is stable-ID ordered, integer-only and independent of presentation.
    /// </summary>
    public sealed class TripDemandSystem : IMonthlySimulationSystem
    {
        private readonly BuildingStateCollection _buildings;
        private readonly IUrbanEconomyProfileResolver _profiles;
        private readonly ITripAccessResolver _access;
        private readonly UrbanEconomySystem _economy;

        public TripDemandSystem(
            BuildingStateCollection buildings,
            IUrbanEconomyProfileResolver profiles,
            ITripAccessResolver access,
            UrbanEconomySystem economy)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _access = access ?? throw new ArgumentNullException(nameof(access));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            State = new TripDemandState();
        }

        public TripDemandState State { get; }

        public void RunMonth(SimulationDate month)
        {
            if (!_economy.HasProcessedMonth || _economy.LastProcessedMonth != month)
                throw new InvalidOperationException("Trip demand requires the same month's economy result first.");
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Monthly trip-demand processing requires a strictly increasing month.");

            Build(month, derivedRestore: false);
        }

        /// <summary>
        /// Rebuilds the path-independent OD cache after snapshot restore. Persistent
        /// source states are already validated by the serializer; no bootstrap or
        /// monthly economy mutation is repeated.
        /// </summary>
        public void RebuildFromCurrentState()
        {
            if (!_economy.HasProcessedMonth)
            {
                State.ClearDerived();
                return;
            }

            Build(_economy.LastProcessedMonth, derivedRestore: true);
        }

        private void Build(SimulationDate month, bool derivedRestore)
        {
            var residential = new List<MutableNode>();
            var jobs = new List<MutableNode>();
            var industry = new List<MutableNode>();
            var commerce = new List<MutableNode>();
            BuildNodes(residential, jobs, industry, commerce);

            var rows = new List<TripDemandRow>();
            BuildCommuteRows(residential, jobs, rows);
            BuildGoodsRows(industry, commerce, rows);
            State.Set(month, rows, derivedRestore);
        }

        private void BuildNodes(
            ICollection<MutableNode> residential,
            ICollection<MutableNode> jobs,
            ICollection<MutableNode> industry,
            ICollection<MutableNode> commerce)
        {
            var source = _buildings.Buildings;
            var parameters = _economy.Parameters;
            for (var index = 0; index < source.Count; index++)
            {
                var building = source[index];
                if (!_profiles.TryGetUrbanEconomyProfile(building.DefinitionId, out var profile))
                    throw new InvalidOperationException($"Building {building.Id} has no urban-economy profile.");

                var endpoint = _access.ResolveBuilding(building);
                switch (profile.Sector)
                {
                    case EconomicSector.Residential:
                    {
                        var residents = Math.Min(building.Occupancy, profile.HousingCapacity);
                        if (residents > 0)
                            residential.Add(new MutableNode(endpoint, residents));
                        break;
                    }
                    case EconomicSector.Commercial:
                    {
                        var filledJobs = Math.Min(building.FilledJobs, profile.JobCapacity);
                        if (filledJobs > 0)
                            jobs.Add(new MutableNode(endpoint, filledJobs));
                        var throughput = checked(profile.JobCapacity * parameters.ThroughputPerCommercialJob);
                        if (throughput > 0)
                            commerce.Add(new MutableNode(endpoint, throughput));
                        break;
                    }
                    case EconomicSector.Industrial:
                    {
                        var filledJobs = Math.Min(building.FilledJobs, profile.JobCapacity);
                        if (filledJobs > 0)
                        {
                            jobs.Add(new MutableNode(endpoint, filledJobs));
                            var supply = checked(filledJobs * parameters.GoodsPerIndustrialWorker);
                            if (supply > 0)
                                industry.Add(new MutableNode(endpoint, supply));
                        }
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Building {building.Id} has an unknown economic sector.");
                }
            }
        }

        private static void BuildCommuteRows(
            IEnumerable<MutableNode> residential,
            IList<MutableNode> jobs,
            ICollection<TripDemandRow> rows)
        {
            foreach (var origin in residential)
                MatchLocal(TripDemandKind.Commute, origin.Endpoint, origin.Remaining, jobs, rows);
        }

        private void BuildGoodsRows(
            IList<MutableNode> industry,
            IList<MutableNode> commerce,
            ICollection<TripDemandRow> rows)
        {
            var localRemaining = _economy.Market.GoodsSold;
            for (var index = 0; index < industry.Count && localRemaining > 0; index++)
            {
                var origin = industry[index];
                var amount = Math.Min(origin.Remaining, localRemaining);
                if (amount <= 0)
                    continue;

                MatchLocal(TripDemandKind.Goods, origin.Endpoint, amount, commerce, rows);
                origin.Remaining -= amount;
                localRemaining -= amount;
            }

            if (localRemaining > 0)
            {
                rows.Add(new TripDemandRow(
                    TripDemandKind.Goods,
                    FirstEndpointOrSynthetic(industry, TripEndpointKind.Building, 0xF100_0000_0000_0001UL),
                    default,
                    localRemaining,
                    TripDemandStatus.Unmet,
                    TripDemandUnmetReason.NoOriginCapacity));
            }

            BuildExternalImports(_economy.Market.GoodsShortage, commerce, rows);
            BuildExternalExports(_economy.Market.GoodsSurplus, industry, rows);
        }

        private void BuildExternalImports(
            int requested,
            IList<MutableNode> destinations,
            ICollection<TripDemandRow> rows)
        {
            if (requested <= 0)
                return;

            var gates = _access.GetEnabledExternalRoadGates();
            var remaining = requested;
            for (var index = 0; index < destinations.Count && remaining > 0; index++)
            {
                var destination = destinations[index];
                if (destination.Remaining <= 0)
                    continue;

                var amount = Math.Min(destination.Remaining, remaining);
                var gate = FindGate(gates, destination.Endpoint.ComponentId);
                if (!destination.Endpoint.HasRoadAccess)
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        FirstGateOrSynthetic(gates),
                        destination.Endpoint,
                        amount,
                        TripDemandStatus.Unmet,
                        TripDemandUnmetReason.NoDestinationRoadAccess));
                }
                else if (gate.HasRoadAccess)
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        gate,
                        destination.Endpoint,
                        amount,
                        TripDemandStatus.Matched,
                        TripDemandUnmetReason.None));
                }
                else
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        FirstGateOrSynthetic(gates),
                        destination.Endpoint,
                        amount,
                        TripDemandStatus.Unmet,
                        gates.Count == 0
                            ? TripDemandUnmetReason.NoEnabledExternalRoadGate
                            : TripDemandUnmetReason.ExternalRoadGateDisconnected));
                }

                destination.Remaining -= amount;
                remaining -= amount;
            }

            if (remaining > 0)
            {
                rows.Add(new TripDemandRow(
                    TripDemandKind.Goods,
                    FirstGateOrSynthetic(gates),
                    default,
                    remaining,
                    TripDemandStatus.Unmet,
                    TripDemandUnmetReason.NoDestinationCapacity));
            }
        }

        private void BuildExternalExports(
            int requested,
            IList<MutableNode> origins,
            ICollection<TripDemandRow> rows)
        {
            if (requested <= 0)
                return;

            var gates = _access.GetEnabledExternalRoadGates();
            var remaining = requested;
            for (var index = 0; index < origins.Count && remaining > 0; index++)
            {
                var origin = origins[index];
                if (origin.Remaining <= 0)
                    continue;

                var amount = Math.Min(origin.Remaining, remaining);
                var gate = FindGate(gates, origin.Endpoint.ComponentId);
                if (!origin.Endpoint.HasRoadAccess)
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        origin.Endpoint,
                        FirstGateOrSynthetic(gates),
                        amount,
                        TripDemandStatus.Unmet,
                        TripDemandUnmetReason.NoOriginRoadAccess));
                }
                else if (gate.HasRoadAccess)
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        origin.Endpoint,
                        gate,
                        amount,
                        TripDemandStatus.Matched,
                        TripDemandUnmetReason.None));
                }
                else
                {
                    rows.Add(new TripDemandRow(
                        TripDemandKind.Goods,
                        origin.Endpoint,
                        FirstGateOrSynthetic(gates),
                        amount,
                        TripDemandStatus.Unmet,
                        gates.Count == 0
                            ? TripDemandUnmetReason.NoEnabledExternalRoadGate
                            : TripDemandUnmetReason.ExternalRoadGateDisconnected));
                }

                origin.Remaining -= amount;
                remaining -= amount;
            }

            if (remaining > 0)
            {
                rows.Add(new TripDemandRow(
                    TripDemandKind.Goods,
                    FirstEndpointOrSynthetic(origins, TripEndpointKind.Building, 0xF100_0000_0000_0002UL),
                    FirstGateOrSynthetic(gates),
                    remaining,
                    TripDemandStatus.Unmet,
                    TripDemandUnmetReason.NoOriginCapacity));
            }
        }

        private static void MatchLocal(
            TripDemandKind kind,
            TripEndpoint origin,
            int requested,
            IList<MutableNode> destinations,
            ICollection<TripDemandRow> rows)
        {
            if (requested <= 0)
                return;

            var remaining = requested;
            if (origin.HasRoadAccess)
            {
                for (var index = 0; index < destinations.Count && remaining > 0; index++)
                {
                    var destination = destinations[index];
                    if (destination.Remaining <= 0 ||
                        !destination.Endpoint.HasRoadAccess ||
                        destination.Endpoint.ComponentId != origin.ComponentId)
                    {
                        continue;
                    }

                    var amount = Math.Min(destination.Remaining, remaining);
                    rows.Add(new TripDemandRow(
                        kind,
                        origin,
                        destination.Endpoint,
                        amount,
                        TripDemandStatus.Matched,
                        TripDemandUnmetReason.None));
                    destination.Remaining -= amount;
                    remaining -= amount;
                }
            }

            if (remaining <= 0)
                return;

            var representative = FindRemainingDestination(destinations);
            rows.Add(new TripDemandRow(
                kind,
                origin,
                representative,
                remaining,
                TripDemandStatus.Unmet,
                DetermineLocalUnmetReason(origin, destinations)));
        }

        private static TripDemandUnmetReason DetermineLocalUnmetReason(
            TripEndpoint origin,
            IList<MutableNode> destinations)
        {
            if (!origin.HasRoadAccess)
                return TripDemandUnmetReason.NoOriginRoadAccess;

            var hasRemaining = false;
            var hasAccessibleRemaining = false;
            for (var index = 0; index < destinations.Count; index++)
            {
                var destination = destinations[index];
                if (destination.Remaining <= 0)
                    continue;
                hasRemaining = true;
                if (!destination.Endpoint.HasRoadAccess)
                    continue;
                hasAccessibleRemaining = true;
                if (destination.Endpoint.ComponentId == origin.ComponentId)
                    return TripDemandUnmetReason.NoDestinationCapacity;
            }

            if (!hasRemaining)
                return TripDemandUnmetReason.NoDestinationCapacity;
            return hasAccessibleRemaining
                ? TripDemandUnmetReason.DifferentRoadComponent
                : TripDemandUnmetReason.NoDestinationRoadAccess;
        }

        private static TripEndpoint FindRemainingDestination(IList<MutableNode> destinations)
        {
            for (var index = 0; index < destinations.Count; index++)
            {
                if (destinations[index].Remaining > 0)
                    return destinations[index].Endpoint;
            }

            return default;
        }

        private static TripEndpoint FindGate(IReadOnlyList<TripEndpoint> gates, int componentId)
        {
            if (componentId < 0)
                return default;
            for (var index = 0; index < gates.Count; index++)
            {
                if (gates[index].HasRoadAccess && gates[index].ComponentId == componentId)
                    return gates[index];
            }

            return default;
        }

        private static TripEndpoint FirstGateOrSynthetic(IReadOnlyList<TripEndpoint> gates)
        {
            if (gates.Count > 0)
                return gates[0];
            return new TripEndpoint(
                TripEndpointKind.ExternalRoadGate,
                new StableId(0xF200_0000_0000_0001UL),
                CellCoord.Zero,
                -1);
        }

        private static TripEndpoint FirstEndpointOrSynthetic(
            IList<MutableNode> nodes,
            TripEndpointKind kind,
            ulong syntheticId)
        {
            if (nodes.Count > 0)
                return nodes[0].Endpoint;
            return new TripEndpoint(kind, new StableId(syntheticId), CellCoord.Zero, -1);
        }

        private sealed class MutableNode
        {
            public MutableNode(TripEndpoint endpoint, int remaining)
            {
                if (endpoint.Kind == TripEndpointKind.None || !endpoint.Id.IsValid)
                    throw new ArgumentException("A demand node requires an endpoint identity.", nameof(endpoint));
                if (remaining <= 0)
                    throw new ArgumentOutOfRangeException(nameof(remaining));
                Endpoint = endpoint;
                Remaining = remaining;
            }

            public TripEndpoint Endpoint { get; }
            public int Remaining { get; set; }
        }
    }
}
