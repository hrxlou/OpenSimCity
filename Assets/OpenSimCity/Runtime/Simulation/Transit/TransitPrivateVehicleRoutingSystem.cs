using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Simulation.Transit
{
    /// <summary>
    /// Re-routes only the private-vehicle remainder after bus mode choice. The
    /// original P5 traffic state remains available as a no-transit baseline.
    /// </summary>
    public sealed class TransitPrivateVehicleRoutingSystem
    {
        private readonly RoadNetworkState _roads;
        private readonly TripDemandSystem _tripDemand;
        private readonly TransitModeChoiceSystem _transit;
        private readonly DeterministicRoutePlanner _planner;
        private int _sourceTransitVersion = -1;

        public TransitPrivateVehicleRoutingSystem(
            RoadNetworkState roads,
            RoadConnectivityGraph graph,
            RoadDefinitionCatalog roadDefinitions,
            TripDemandSystem tripDemand,
            TransitModeChoiceSystem transit,
            TrafficRoutingParameters parameters = null)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _tripDemand = tripDemand ?? throw new ArgumentNullException(nameof(tripDemand));
            _transit = transit ?? throw new ArgumentNullException(nameof(transit));
            _planner = new DeterministicRoutePlanner(
                roads,
                graph ?? throw new ArgumentNullException(nameof(graph)),
                roadDefinitions ?? throw new ArgumentNullException(nameof(roadDefinitions)),
                parameters);
            State = new TrafficRoutingState();
        }

        public TrafficRoutingState State { get; }

        public void RunMonth(SimulationDate month)
        {
            if (!_transit.State.HasProcessedMonth || _transit.State.LastProcessedMonth != month)
                throw new InvalidOperationException("Private-vehicle routing requires transit mode choice from the same month.");
            Rebuild(month);
        }

        public bool RefreshIfNeeded()
        {
            if (!_transit.State.HasProcessedMonth)
                return false;
            if (State.HasProcessedMonth &&
                State.SourceRoadVersion == _roads.Version &&
                State.SourceDemandVersion == _tripDemand.State.Version &&
                _sourceTransitVersion == _transit.State.Version)
            {
                return false;
            }
            Rebuild(_transit.State.LastProcessedMonth);
            return true;
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
                var privateAmount = _transit.GetPrivateVehicleAmount(demand);
                if (privateAmount <= 0)
                    continue;

                var routeSet = _planner.GetRoutes(demand.Origin.RoadCell, demand.Destination.RoadCell);
                if (!routeSet.IsReachable)
                {
                    unrouted.Add(new TrafficUnroutedDemand(
                        demand.Kind,
                        demand.Origin,
                        demand.Destination,
                        privateAmount,
                        routeSet.FailureReason));
                    continue;
                }

                var routeCount = Math.Min(routeSet.Routes.Count, privateAmount);
                var baseAmount = privateAmount / routeCount;
                var remainder = privateAmount % routeCount;
                for (var routeIndex = 0; routeIndex < routeCount; routeIndex++)
                {
                    var amount = baseAmount + (routeIndex < remainder ? 1 : 0);
                    var route = routeSet.Routes[routeIndex];
                    drafts.Add(new AssignmentDraft(
                        demand.Kind,
                        demand.Origin,
                        demand.Destination,
                        routeIndex,
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
                    : Math.Max(1, ToInt((long)freeFlowSpeed * capacity / pair.Value));
                var volumeCapacityPermille = ToInt((long)pair.Value * 1000 / capacity);
                var freeFlowSeconds = checked((3600 + freeFlowSpeed - 1) / freeFlowSpeed);
                var congestedSeconds = checked((3600 + effectiveSpeed - 1) / effectiveSpeed);
                var edgeState = new TrafficEdgeState(
                    pair.Key,
                    pair.Value,
                    capacity,
                    freeFlowSpeed,
                    effectiveSpeed,
                    volumeCapacityPermille,
                    freeFlowSeconds,
                    congestedSeconds);
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
                    draft.Kind,
                    draft.Origin,
                    draft.Destination,
                    draft.RouteIndex,
                    draft.Amount,
                    draft.Route,
                    ToInt(congestedTravelSeconds));
            }

            State.Set(
                month,
                _roads.Version,
                _tripDemand.State.Version,
                assignments,
                unrouted.ToArray(),
                edgeRows);
            _sourceTransitVersion = _transit.State.Version;
        }

        private static int ToInt(long value)
        {
            if (value < 0 || value > int.MaxValue)
                throw new OverflowException("Transit-aware traffic value exceeded Int32 range.");
            return (int)value;
        }

        private sealed class AssignmentDraft
        {
            public AssignmentDraft(
                TripDemandKind kind,
                TripEndpoint origin,
                TripEndpoint destination,
                int routeIndex,
                int amount,
                TrafficRoute route)
            {
                Kind = kind;
                Origin = origin;
                Destination = destination;
                RouteIndex = routeIndex;
                Amount = amount;
                Route = route;
            }

            public TripDemandKind Kind { get; }
            public TripEndpoint Origin { get; }
            public TripEndpoint Destination { get; }
            public int RouteIndex { get; }
            public int Amount { get; }
            public TrafficRoute Route { get; }
        }
    }
}
