using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Traffic
{
    /// <summary>
    /// Converts matched aggregate OD rows into representative route assignments,
    /// conserved edge volume and deterministic integer congestion. The complete
    /// result is derived from road and trip-demand source state and is not persisted.
    /// </summary>
    public sealed class TrafficRoutingSystem
    {
        private readonly RoadNetworkState _roads;
        private readonly TripDemandSystem _tripDemand;
        private readonly DeterministicRoutePlanner _planner;
        private ITrafficRoutePolicy _routePolicy = IdentityTrafficRoutePolicy.Instance;
        private int _appliedRoutePolicyVersion = -1;

        public TrafficRoutingSystem(
            RoadNetworkState roads,
            RoadConnectivityGraph graph,
            RoadDefinitionCatalog roadDefinitions,
            TripDemandSystem tripDemand,
            TrafficRoutingParameters parameters = null)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _tripDemand = tripDemand ?? throw new ArgumentNullException(nameof(tripDemand));
            _planner = new DeterministicRoutePlanner(
                roads,
                graph ?? throw new ArgumentNullException(nameof(graph)),
                roadDefinitions ?? throw new ArgumentNullException(nameof(roadDefinitions)),
                parameters);
            State = new TrafficRoutingState();
        }

        public TrafficRoutingState State { get; }
        public DeterministicRoutePlanner Planner => _planner;
        public ITrafficRoutePolicy RoutePolicy
        {
            get => _routePolicy;
            set => _routePolicy = value ?? IdentityTrafficRoutePolicy.Instance;
        }

        public void RunMonth(SimulationDate month)
        {
            if (!_tripDemand.State.HasProcessedMonth || _tripDemand.State.LastProcessedMonth != month)
                throw new InvalidOperationException("Traffic routing requires trip demand from the same month.");
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Monthly traffic routing requires a strictly increasing month.");
            Rebuild(month);
        }

        public bool RefreshIfNeeded()
        {
            if (!_tripDemand.State.HasProcessedMonth)
                return false;
            if (State.HasProcessedMonth &&
                State.SourceRoadVersion == _roads.Version &&
                State.SourceDemandVersion == _tripDemand.State.Version &&
                _appliedRoutePolicyVersion == _routePolicy.Version)
                return false;
            Rebuild(_tripDemand.State.LastProcessedMonth);
            return true;
        }

        public void RebuildFromCurrentState()
        {
            if (_tripDemand.State.HasProcessedMonth)
                Rebuild(_tripDemand.State.LastProcessedMonth);
        }

        private void Rebuild(SimulationDate month)
        {
            var drafts = new List<AssignmentDraft>();
            var unrouted = new List<TrafficUnroutedDemand>();
            var volumes = new SortedDictionary<RoadEdgeKey, int>();
            var demandRows = _tripDemand.State.Rows;

            for (var demandIndex = 0; demandIndex < demandRows.Count; demandIndex++)
            {
                var demand = demandRows[demandIndex];
                if (demand.Status != TripDemandStatus.Matched)
                    continue;

                var routeSet = _planner.GetRoutes(demand.Origin.RoadCell, demand.Destination.RoadCell);
                if (!routeSet.IsReachable)
                {
                    unrouted.Add(new TrafficUnroutedDemand(
                        demand.Kind,
                        demand.Origin,
                        demand.Destination,
                        demand.Amount,
                        routeSet.FailureReason));
                    continue;
                }

                var preferred = PreferredRoutes(demand, routeSet.Routes);
                var routeCount = Math.Min(preferred.Count, demand.Amount);
                var baseAmount = demand.Amount / routeCount;
                var remainder = demand.Amount % routeCount;
                for (var routeIndex = 0; routeIndex < routeCount; routeIndex++)
                {
                    var amount = baseAmount + (routeIndex < remainder ? 1 : 0);
                    var routeChoice = preferred[routeIndex];
                    var route = routeChoice.Route;
                    drafts.Add(new AssignmentDraft(
                        demand.Kind,
                        demand.Origin,
                        demand.Destination,
                        routeChoice.SourceIndex,
                        amount,
                        route));

                    for (var edgeIndex = 0; edgeIndex < route.EdgeCount; edgeIndex++)
                    {
                        var edge = route.GetEdge(edgeIndex);
                        volumes.TryGetValue(edge, out var current);
                        volumes[edge] = checked(current + amount);
                    }
                }
            }

            var edgeRows = new TrafficEdgeState[volumes.Count];
            var edgeLookup = new Dictionary<RoadEdgeKey, TrafficEdgeState>();
            var rowIndex = 0;
            foreach (var pair in volumes)
            {
                var capacity = _planner.EdgeCapacity(pair.Key);
                var freeFlowSpeed = _planner.EdgeFreeFlowSpeed(pair.Key);
                var effectiveSpeed = pair.Value <= capacity
                    ? freeFlowSpeed
                    : Math.Max(1, ToInt((long)freeFlowSpeed * capacity / pair.Value,
                        "Effective traffic speed exceeded Int32 range."));
                var volumeCapacityPermille = ToInt((long)pair.Value * 1000 / capacity,
                    "Traffic volume/capacity ratio exceeded Int32 range.");
                var freeFlowSeconds = checked((3600 + freeFlowSpeed - 1) / freeFlowSpeed);
                var congestedSeconds = checked((3600 + effectiveSpeed - 1) / effectiveSpeed);
                var edgeState = new TrafficEdgeState(
                    pair.Key, pair.Value, capacity, freeFlowSpeed, effectiveSpeed,
                    volumeCapacityPermille, freeFlowSeconds, congestedSeconds);
                edgeRows[rowIndex++] = edgeState;
                edgeLookup.Add(pair.Key, edgeState);
            }

            var assignments = new TrafficRouteAssignment[drafts.Count];
            for (var index = 0; index < drafts.Count; index++)
            {
                var draft = drafts[index];
                long congestedTravelSeconds = 0;
                for (var edgeIndex = 0; edgeIndex < draft.Route.EdgeCount; edgeIndex++)
                    congestedTravelSeconds += edgeLookup[draft.Route.GetEdge(edgeIndex)].CongestedTravelSeconds;
                assignments[index] = new TrafficRouteAssignment(
                    draft.Kind, draft.Origin, draft.Destination, draft.RouteIndex,
                    draft.Amount, draft.Route,
                    ToInt(congestedTravelSeconds, "Route travel time exceeded Int32 range."));
            }

            State.Set(month, _roads.Version, _tripDemand.State.Version,
                assignments, unrouted.ToArray(), edgeRows);
            _appliedRoutePolicyVersion = _routePolicy.Version;
        }

        private List<RouteChoice> PreferredRoutes(TripDemandRow demand, IReadOnlyList<TrafficRoute> routes)
        {
            var result = new List<RouteChoice>();
            var minimum = int.MaxValue;
            for (var index = 0; index < routes.Count; index++)
            {
                var preference = _routePolicy.RoutePreferencePermille(demand.Kind, routes[index]);
                if (preference < 1000)
                    throw new InvalidOperationException("Traffic route policy preference must be at least 1000 permille.");
                if (preference < minimum)
                {
                    minimum = preference;
                    result.Clear();
                    result.Add(new RouteChoice(index, routes[index]));
                }
                else if (preference == minimum)
                {
                    result.Add(new RouteChoice(index, routes[index]));
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("A reachable route set must contain at least one policy-eligible route.");
            return result;
        }

        private static int ToInt(long value, string message)
        {
            if (value < 0 || value > int.MaxValue)
                throw new OverflowException(message);
            return (int)value;
        }

        private readonly struct RouteChoice
        {
            public RouteChoice(int sourceIndex, TrafficRoute route) { SourceIndex = sourceIndex; Route = route; }
            public int SourceIndex { get; }
            public TrafficRoute Route { get; }
        }

        private sealed class AssignmentDraft
        {
            public AssignmentDraft(TripDemandKind kind, TripEndpoint origin, TripEndpoint destination,
                int routeIndex, int amount, TrafficRoute route)
            { Kind = kind; Origin = origin; Destination = destination; RouteIndex = routeIndex; Amount = amount; Route = route; }
            public TripDemandKind Kind { get; }
            public TripEndpoint Origin { get; }
            public TripEndpoint Destination { get; }
            public int RouteIndex { get; }
            public int Amount { get; }
            public TrafficRoute Route { get; }
        }
    }
}
