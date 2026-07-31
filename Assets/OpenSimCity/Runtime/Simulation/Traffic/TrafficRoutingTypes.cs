using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Traffic
{
    public enum TrafficRouteFailureReason : byte
    {
        None,
        OriginRoadMissing,
        DestinationRoadMissing,
        DifferentRoadComponent,
        NoPath
    }

    /// <summary>
    /// Canonical undirected cardinal road edge. Endpoints are ordered by the
    /// project's CellCoord ordering so dictionary or insertion order can never
    /// change edge aggregation output.
    /// </summary>
    [Serializable]
    public readonly struct RoadEdgeKey : IEquatable<RoadEdgeKey>, IComparable<RoadEdgeKey>
    {
        public RoadEdgeKey(CellCoord first, CellCoord second)
        {
            if (first.ManhattanDistanceTo(second) != 1)
                throw new ArgumentException("A road edge must connect cardinally adjacent cells.");

            if (first.CompareTo(second) <= 0)
            {
                A = first;
                B = second;
            }
            else
            {
                A = second;
                B = first;
            }
        }

        public CellCoord A { get; }
        public CellCoord B { get; }

        public int CompareTo(RoadEdgeKey other)
        {
            var comparison = A.CompareTo(other.A);
            return comparison != 0 ? comparison : B.CompareTo(other.B);
        }

        public bool Equals(RoadEdgeKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is RoadEdgeKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (A.GetHashCode() * 397) ^ B.GetHashCode();
            }
        }

        public override string ToString() => $"{A}->{B}";
        public static bool operator ==(RoadEdgeKey left, RoadEdgeKey right) => left.Equals(right);
        public static bool operator !=(RoadEdgeKey left, RoadEdgeKey right) => !left.Equals(right);
        public static bool operator <(RoadEdgeKey left, RoadEdgeKey right) => left.CompareTo(right) < 0;
        public static bool operator >(RoadEdgeKey left, RoadEdgeKey right) => left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Immutable deterministic road path. Travel time is stored in integer seconds
    /// and contains no presentation or physical-vehicle state.
    /// </summary>
    [Serializable]
    public sealed class TrafficRoute
    {
        private readonly CellCoord[] _cells;
        private readonly IReadOnlyList<CellCoord> _readOnlyCells;

        public TrafficRoute(IReadOnlyList<CellCoord> cells, int freeFlowTravelSeconds)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            if (cells.Count == 0)
                throw new ArgumentException("A traffic route must contain at least one road cell.", nameof(cells));
            if (freeFlowTravelSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(freeFlowTravelSeconds));

            _cells = new CellCoord[cells.Count];
            for (var index = 0; index < cells.Count; index++)
            {
                _cells[index] = cells[index];
                if (index > 0 && _cells[index - 1].ManhattanDistanceTo(_cells[index]) != 1)
                    throw new ArgumentException("Traffic route cells must be cardinally contiguous.", nameof(cells));
            }

            _readOnlyCells = Array.AsReadOnly(_cells);
            FreeFlowTravelSeconds = freeFlowTravelSeconds;
        }

        public IReadOnlyList<CellCoord> Cells => _readOnlyCells;
        public CellCoord Origin => _cells[0];
        public CellCoord Destination => _cells[_cells.Length - 1];
        public int EdgeCount => _cells.Length - 1;
        public int FreeFlowTravelSeconds { get; }

        public RoadEdgeKey GetEdge(int edgeIndex)
        {
            if ((uint)edgeIndex >= (uint)EdgeCount)
                throw new ArgumentOutOfRangeException(nameof(edgeIndex));
            return new RoadEdgeKey(_cells[edgeIndex], _cells[edgeIndex + 1]);
        }

        public bool Contains(CellCoord cell)
        {
            for (var index = 0; index < _cells.Length; index++)
            {
                if (_cells[index] == cell)
                    return true;
            }
            return false;
        }
    }

    [Serializable]
    public sealed class TrafficRouteSet
    {
        private readonly TrafficRoute[] _routes;
        private readonly IReadOnlyList<TrafficRoute> _readOnlyRoutes;

        public TrafficRouteSet(
            IReadOnlyList<TrafficRoute> routes,
            TrafficRouteFailureReason failureReason)
        {
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));
            if (routes.Count > 0 && failureReason != TrafficRouteFailureReason.None)
                throw new ArgumentException("A successful route set cannot have a failure reason.", nameof(failureReason));
            if (routes.Count == 0 && failureReason == TrafficRouteFailureReason.None)
                throw new ArgumentException("An empty route set requires a failure reason.", nameof(failureReason));

            _routes = new TrafficRoute[routes.Count];
            for (var index = 0; index < routes.Count; index++)
                _routes[index] = routes[index] ?? throw new ArgumentException("A route cannot be null.", nameof(routes));
            _readOnlyRoutes = Array.AsReadOnly(_routes);
            FailureReason = failureReason;
        }

        public IReadOnlyList<TrafficRoute> Routes => _readOnlyRoutes;
        public TrafficRouteFailureReason FailureReason { get; }
        public bool IsReachable => _routes.Length > 0;
    }

    [Serializable]
    public readonly struct TrafficRouteAssignment
    {
        public TrafficRouteAssignment(
            TripDemandKind kind,
            TripEndpoint origin,
            TripEndpoint destination,
            int routeIndex,
            int amount,
            TrafficRoute route,
            int congestedTravelSeconds)
        {
            if (routeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(routeIndex));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            if (congestedTravelSeconds < route.FreeFlowTravelSeconds)
                throw new ArgumentOutOfRangeException(nameof(congestedTravelSeconds));

            Kind = kind;
            Origin = origin;
            Destination = destination;
            RouteIndex = routeIndex;
            Amount = amount;
            Route = route;
            CongestedTravelSeconds = congestedTravelSeconds;
        }

        public TripDemandKind Kind { get; }
        public TripEndpoint Origin { get; }
        public TripEndpoint Destination { get; }
        public int RouteIndex { get; }
        public int Amount { get; }
        public TrafficRoute Route { get; }
        public int FreeFlowTravelSeconds => Route.FreeFlowTravelSeconds;
        public int CongestedTravelSeconds { get; }
    }

    [Serializable]
    public readonly struct TrafficUnroutedDemand
    {
        public TrafficUnroutedDemand(
            TripDemandKind kind,
            TripEndpoint origin,
            TripEndpoint destination,
            int amount,
            TrafficRouteFailureReason reason)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (reason == TrafficRouteFailureReason.None)
                throw new ArgumentException("Unrouted demand requires an explicit reason.", nameof(reason));

            Kind = kind;
            Origin = origin;
            Destination = destination;
            Amount = amount;
            Reason = reason;
        }

        public TripDemandKind Kind { get; }
        public TripEndpoint Origin { get; }
        public TripEndpoint Destination { get; }
        public int Amount { get; }
        public TrafficRouteFailureReason Reason { get; }
    }

    [Serializable]
    public readonly struct TrafficEdgeState
    {
        public TrafficEdgeState(
            RoadEdgeKey edge,
            int volume,
            int capacity,
            int freeFlowSpeedCellsPerHour,
            int effectiveSpeedCellsPerHour,
            int volumeCapacityPermille,
            int freeFlowTravelSeconds,
            int congestedTravelSeconds)
        {
            if (volume < 0)
                throw new ArgumentOutOfRangeException(nameof(volume));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (freeFlowSpeedCellsPerHour <= 0 || effectiveSpeedCellsPerHour <= 0)
                throw new ArgumentOutOfRangeException(nameof(freeFlowSpeedCellsPerHour));
            if (effectiveSpeedCellsPerHour > freeFlowSpeedCellsPerHour)
                throw new ArgumentOutOfRangeException(nameof(effectiveSpeedCellsPerHour));
            if (volumeCapacityPermille < 0)
                throw new ArgumentOutOfRangeException(nameof(volumeCapacityPermille));
            if (freeFlowTravelSeconds <= 0 || congestedTravelSeconds < freeFlowTravelSeconds)
                throw new ArgumentOutOfRangeException(nameof(congestedTravelSeconds));

            Edge = edge;
            Volume = volume;
            Capacity = capacity;
            FreeFlowSpeedCellsPerHour = freeFlowSpeedCellsPerHour;
            EffectiveSpeedCellsPerHour = effectiveSpeedCellsPerHour;
            VolumeCapacityPermille = volumeCapacityPermille;
            FreeFlowTravelSeconds = freeFlowTravelSeconds;
            CongestedTravelSeconds = congestedTravelSeconds;
        }

        public RoadEdgeKey Edge { get; }
        public int Volume { get; }
        public int Capacity { get; }
        public int FreeFlowSpeedCellsPerHour { get; }
        public int EffectiveSpeedCellsPerHour { get; }
        public int VolumeCapacityPermille { get; }
        public int FreeFlowTravelSeconds { get; }
        public int CongestedTravelSeconds { get; }
        public bool IsCongested => Volume > Capacity;
    }

    [Serializable]
    public sealed class TrafficRoutingParameters
    {
        public TrafficRoutingParameters(int maximumRouteAlternatives = 3)
        {
            if (maximumRouteAlternatives < 1 || maximumRouteAlternatives > 3)
                throw new ArgumentOutOfRangeException(nameof(maximumRouteAlternatives),
                    "P5-02 supports one to three representative routes per OD pair.");
            MaximumRouteAlternatives = maximumRouteAlternatives;
        }

        public int MaximumRouteAlternatives { get; }
        public static TrafficRoutingParameters Default => new TrafficRoutingParameters();
    }

    /// <summary>
    /// Rebuildable aggregate routing result. Source versions are diagnostics and are
    /// deliberately excluded from the checksum because restore may rebuild caches
    /// through a different number of internal version increments.
    /// </summary>
    [Serializable]
    public sealed class TrafficRoutingState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private IReadOnlyList<TrafficRouteAssignment> _assignments =
            Array.AsReadOnly(Array.Empty<TrafficRouteAssignment>());
        private IReadOnlyList<TrafficUnroutedDemand> _unrouted =
            Array.AsReadOnly(Array.Empty<TrafficUnroutedDemand>());
        private IReadOnlyList<TrafficEdgeState> _edges =
            Array.AsReadOnly(Array.Empty<TrafficEdgeState>());

        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int Version { get; private set; }
        public int SourceRoadVersion { get; private set; } = -1;
        public int SourceDemandVersion { get; private set; } = -1;
        public int InputMatchedAmount { get; private set; }
        public int RoutedAmount { get; private set; }
        public int UnroutedAmount { get; private set; }
        public IReadOnlyList<TrafficRouteAssignment> Assignments => _assignments;
        public IReadOnlyList<TrafficUnroutedDemand> Unrouted => _unrouted;
        public IReadOnlyList<TrafficEdgeState> Edges => _edges;

        internal void Set(
            SimulationDate month,
            int sourceRoadVersion,
            int sourceDemandVersion,
            TrafficRouteAssignment[] assignments,
            TrafficUnroutedDemand[] unrouted,
            TrafficEdgeState[] edges)
        {
            if (sourceRoadVersion < 0 || sourceDemandVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRoadVersion));
            assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
            unrouted = unrouted ?? throw new ArgumentNullException(nameof(unrouted));
            edges = edges ?? throw new ArgumentNullException(nameof(edges));

            var routed = 0;
            for (var index = 0; index < assignments.Length; index++)
                routed = checked(routed + assignments[index].Amount);
            var missing = 0;
            for (var index = 0; index < unrouted.Length; index++)
                missing = checked(missing + unrouted[index].Amount);

            _assignments = Array.AsReadOnly(assignments);
            _unrouted = Array.AsReadOnly(unrouted);
            _edges = Array.AsReadOnly(edges);
            LastProcessedMonth = month;
            SourceRoadVersion = sourceRoadVersion;
            SourceDemandVersion = sourceDemandVersion;
            RoutedAmount = routed;
            UnroutedAmount = missing;
            InputMatchedAmount = checked(routed + missing);
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, HasProcessedMonth ? 1 : 0);
            if (HasProcessedMonth)
            {
                AddInt(ref hash, LastProcessedMonth.Year);
                AddInt(ref hash, LastProcessedMonth.Month);
            }
            AddInt(ref hash, InputMatchedAmount);
            AddInt(ref hash, RoutedAmount);
            AddInt(ref hash, UnroutedAmount);

            for (var index = 0; index < _assignments.Count; index++)
            {
                var row = _assignments[index];
                AddByte(ref hash, (byte)row.Kind);
                AddEndpoint(ref hash, row.Origin);
                AddEndpoint(ref hash, row.Destination);
                AddInt(ref hash, row.RouteIndex);
                AddInt(ref hash, row.Amount);
                AddInt(ref hash, row.FreeFlowTravelSeconds);
                AddInt(ref hash, row.CongestedTravelSeconds);
                var cells = row.Route.Cells;
                AddInt(ref hash, cells.Count);
                for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    AddInt(ref hash, cells[cellIndex].X);
                    AddInt(ref hash, cells[cellIndex].Y);
                }
            }

            for (var index = 0; index < _unrouted.Count; index++)
            {
                var row = _unrouted[index];
                AddByte(ref hash, (byte)row.Kind);
                AddEndpoint(ref hash, row.Origin);
                AddEndpoint(ref hash, row.Destination);
                AddInt(ref hash, row.Amount);
                AddByte(ref hash, (byte)row.Reason);
            }

            for (var index = 0; index < _edges.Count; index++)
            {
                var row = _edges[index];
                AddInt(ref hash, row.Edge.A.X);
                AddInt(ref hash, row.Edge.A.Y);
                AddInt(ref hash, row.Edge.B.X);
                AddInt(ref hash, row.Edge.B.Y);
                AddInt(ref hash, row.Volume);
                AddInt(ref hash, row.Capacity);
                AddInt(ref hash, row.FreeFlowSpeedCellsPerHour);
                AddInt(ref hash, row.EffectiveSpeedCellsPerHour);
                AddInt(ref hash, row.VolumeCapacityPermille);
                AddInt(ref hash, row.FreeFlowTravelSeconds);
                AddInt(ref hash, row.CongestedTravelSeconds);
            }

            return hash;
        }

        private static void AddEndpoint(ref ulong hash, TripEndpoint endpoint)
        {
            AddByte(ref hash, (byte)endpoint.Kind);
            AddUlong(ref hash, endpoint.Id.Value);
            AddInt(ref hash, endpoint.RoadCell.X);
            AddInt(ref hash, endpoint.RoadCell.Y);
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
}
