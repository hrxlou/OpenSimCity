using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Simulation.Traffic
{
    /// <summary>
    /// Unity-independent deterministic Dijkstra routing with a road-version cache.
    /// Equal-cost queue entries use row-major cell index, and path alternatives are
    /// ordered by integer travel time then their complete CellCoord sequence.
    /// </summary>
    public sealed class DeterministicRoutePlanner
    {
        private readonly RoadNetworkState _roads;
        private readonly RoadConnectivityGraph _graph;
        private readonly RoadDefinitionCatalog _definitions;
        private readonly TrafficRoutingParameters _parameters;
        private readonly Dictionary<RouteCacheKey, RouteCacheEntry> _cache =
            new Dictionary<RouteCacheKey, RouteCacheEntry>();
        private int _cacheRoadVersion;

        public DeterministicRoutePlanner(
            RoadNetworkState roads,
            RoadConnectivityGraph graph,
            RoadDefinitionCatalog definitions,
            TrafficRoutingParameters parameters = null)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _parameters = parameters ?? TrafficRoutingParameters.Default;
            _graph.RefreshIfNeeded();
            _cacheRoadVersion = _roads.Version;
        }

        public int CacheEntryCount => _cache.Count;
        public int CacheHitCount { get; private set; }
        public int CacheMissCount { get; private set; }
        public int LastInvalidatedEntryCount { get; private set; }
        public int LastRetainedEntryCount { get; private set; }

        public TrafficRouteSet GetRoutes(CellCoord origin, CellCoord destination)
        {
            RefreshCacheForRoadVersion();

            var validation = ValidateEndpoints(origin, destination, out var componentId);
            if (validation != TrafficRouteFailureReason.None)
                return Failure(validation);

            var key = new RouteCacheKey(origin, destination);
            if (_cache.TryGetValue(key, out var cached))
            {
                CacheHitCount = checked(CacheHitCount + 1);
                return cached.RouteSet;
            }

            CacheMissCount = checked(CacheMissCount + 1);
            var result = FindRepresentativeRoutes(origin, destination);
            if (result.IsReachable)
                _cache.Add(key, new RouteCacheEntry(componentId, result));
            return result;
        }

        public void ClearCache()
        {
            _cache.Clear();
            _cacheRoadVersion = _roads.Version;
            LastInvalidatedEntryCount = 0;
            LastRetainedEntryCount = 0;
        }

        private void RefreshCacheForRoadVersion()
        {
            if (_cacheRoadVersion == _roads.Version)
                return;

            _graph.RefreshIfNeeded();
            var dirtyCells = _graph.DirtyCells;
            var dirtySet = new HashSet<CellCoord>();
            var affectedComponents = new HashSet<int>();
            for (var index = 0; index < dirtyCells.Count; index++)
            {
                var cell = dirtyCells[index];
                dirtySet.Add(cell);
                var result = _graph.Get(cell);
                if (result.HasRoad)
                    affectedComponents.Add(result.ComponentId);
            }

            var keys = new List<RouteCacheKey>(_cache.Keys);
            keys.Sort();
            var invalidated = 0;
            var retained = 0;
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                var entry = _cache[key];
                var remove = dirtyCells.Count == 0 ||
                             ShouldInvalidate(key, entry, dirtySet, affectedComponents);
                if (remove)
                {
                    _cache.Remove(key);
                    invalidated++;
                }
                else
                {
                    retained++;
                }
            }

            LastInvalidatedEntryCount = invalidated;
            LastRetainedEntryCount = retained;
            _cacheRoadVersion = _roads.Version;
        }

        private bool ShouldInvalidate(
            RouteCacheKey key,
            RouteCacheEntry entry,
            ISet<CellCoord> dirtyCells,
            ISet<int> affectedComponents)
        {
            if (!_roads.IsOccupied(key.Origin) || !_roads.IsOccupied(key.Destination))
                return true;

            var originResult = _graph.Get(key.Origin);
            var destinationResult = _graph.Get(key.Destination);
            if (!originResult.HasRoad || !destinationResult.HasRoad ||
                originResult.ComponentId != destinationResult.ComponentId ||
                originResult.ComponentId != entry.ComponentId ||
                affectedComponents.Contains(originResult.ComponentId))
            {
                return true;
            }

            var routes = entry.RouteSet.Routes;
            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var cells = routes[routeIndex].Cells;
                for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    if (dirtyCells.Contains(cells[cellIndex]))
                        return true;
                }
            }

            return false;
        }

        private TrafficRouteFailureReason ValidateEndpoints(
            CellCoord origin,
            CellCoord destination,
            out int componentId)
        {
            componentId = -1;
            if (!_roads.IsInBounds(origin) || !_roads.IsOccupied(origin))
                return TrafficRouteFailureReason.OriginRoadMissing;
            if (!_roads.IsInBounds(destination) || !_roads.IsOccupied(destination))
                return TrafficRouteFailureReason.DestinationRoadMissing;

            var originResult = _graph.Get(origin);
            var destinationResult = _graph.Get(destination);
            if (!originResult.HasRoad)
                return TrafficRouteFailureReason.OriginRoadMissing;
            if (!destinationResult.HasRoad)
                return TrafficRouteFailureReason.DestinationRoadMissing;
            if (originResult.ComponentId != destinationResult.ComponentId)
                return TrafficRouteFailureReason.DifferentRoadComponent;

            componentId = originResult.ComponentId;
            return TrafficRouteFailureReason.None;
        }

        private TrafficRouteSet FindRepresentativeRoutes(CellCoord origin, CellCoord destination)
        {
            var first = FindShortestRoute(origin, destination, null);
            if (first == null)
                return Failure(TrafficRouteFailureReason.NoPath);

            var accepted = new List<TrafficRoute> { first };
            var candidates = new List<TrafficRoute>();
            while (accepted.Count < _parameters.MaximumRouteAlternatives)
            {
                var source = accepted[accepted.Count - 1];
                for (var edgeIndex = 0; edgeIndex < source.EdgeCount; edgeIndex++)
                {
                    var banned = new HashSet<RoadEdgeKey> { source.GetEdge(edgeIndex) };
                    var candidate = FindShortestRoute(origin, destination, banned);
                    if (candidate == null || ContainsRoute(accepted, candidate) || ContainsRoute(candidates, candidate))
                        continue;
                    candidates.Add(candidate);
                }

                if (candidates.Count == 0)
                    break;

                candidates.Sort(CompareRoutes);
                accepted.Add(candidates[0]);
                candidates.RemoveAt(0);
            }

            accepted.Sort(CompareRoutes);
            return new TrafficRouteSet(accepted, TrafficRouteFailureReason.None);
        }

        private TrafficRoute FindShortestRoute(
            CellCoord origin,
            CellCoord destination,
            ISet<RoadEdgeKey> bannedEdges)
        {
            var originIndex = _roads.GetIndex(origin);
            var destinationIndex = _roads.GetIndex(destination);
            if (originIndex == destinationIndex)
                return new TrafficRoute(new[] { origin }, 0);

            var distances = new long[_roads.CellCount];
            var predecessors = new int[_roads.CellCount];
            var closed = new bool[_roads.CellCount];
            for (var index = 0; index < distances.Length; index++)
            {
                distances[index] = long.MaxValue;
                predecessors[index] = -1;
            }

            var queue = new RouteQueue();
            distances[originIndex] = 0;
            queue.Enqueue(new RouteQueueEntry(0, originIndex));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (closed[current.CellIndex] || distances[current.CellIndex] != current.Distance)
                    continue;
                closed[current.CellIndex] = true;
                if (current.CellIndex == destinationIndex)
                    break;

                VisitOccupiedNeighborsInStableOrder(current.CellIndex, neighborIndex =>
                {
                    if (closed[neighborIndex])
                        return;

                    var currentCell = _roads.Cells[current.CellIndex].Coordinate;
                    var neighborCell = _roads.Cells[neighborIndex].Coordinate;
                    var edge = new RoadEdgeKey(currentCell, neighborCell);
                    if (bannedEdges != null && bannedEdges.Contains(edge))
                        return;

                    var candidateDistance = checked(current.Distance + EdgeTravelSeconds(edge));
                    var existingDistance = distances[neighborIndex];
                    var existingPredecessor = predecessors[neighborIndex];
                    var isBetter = candidateDistance < existingDistance ||
                                   (candidateDistance == existingDistance &&
                                    (existingPredecessor < 0 || current.CellIndex < existingPredecessor));
                    if (!isBetter)
                        return;

                    distances[neighborIndex] = candidateDistance;
                    predecessors[neighborIndex] = current.CellIndex;
                    queue.Enqueue(new RouteQueueEntry(candidateDistance, neighborIndex));
                });
            }

            if (distances[destinationIndex] == long.MaxValue)
                return null;

            var reverse = new List<CellCoord>();
            var cursor = destinationIndex;
            while (cursor >= 0)
            {
                reverse.Add(_roads.Cells[cursor].Coordinate);
                if (cursor == originIndex)
                    break;
                cursor = predecessors[cursor];
            }
            if (reverse[reverse.Count - 1] != origin)
                return null;

            reverse.Reverse();
            return new TrafficRoute(reverse, ToInt(distances[destinationIndex]));
        }

        private void VisitOccupiedNeighborsInStableOrder(int index, Action<int> visitor)
        {
            var x = index % _roads.Width;
            var y = index / _roads.Width;
            if (y > 0)
                VisitIfOccupied(index - _roads.Width, visitor);
            if (x > 0)
                VisitIfOccupied(index - 1, visitor);
            if (x + 1 < _roads.Width)
                VisitIfOccupied(index + 1, visitor);
            if (y + 1 < _roads.Height)
                VisitIfOccupied(index + _roads.Width, visitor);
        }

        private void VisitIfOccupied(int index, Action<int> visitor)
        {
            if (_roads.Cells[index].IsOccupied)
                visitor(index);
        }

        public int EdgeCapacity(RoadEdgeKey edge)
        {
            var first = _definitions.Get(_roads.GetRoadDefinitionId(edge.A));
            var second = _definitions.Get(_roads.GetRoadDefinitionId(edge.B));
            return Math.Min(first.CapacityPerMonth, second.CapacityPerMonth);
        }

        public int EdgeFreeFlowSpeed(RoadEdgeKey edge)
        {
            var first = _definitions.Get(_roads.GetRoadDefinitionId(edge.A));
            var second = _definitions.Get(_roads.GetRoadDefinitionId(edge.B));
            return Math.Min(first.FreeFlowSpeedCellsPerHour, second.FreeFlowSpeedCellsPerHour);
        }

        public int EdgeTravelSeconds(RoadEdgeKey edge)
        {
            var speed = EdgeFreeFlowSpeed(edge);
            return checked((3600 + speed - 1) / speed);
        }

        private static TrafficRouteSet Failure(TrafficRouteFailureReason reason)
        {
            return new TrafficRouteSet(Array.Empty<TrafficRoute>(), reason);
        }

        private static int CompareRoutes(TrafficRoute left, TrafficRoute right)
        {
            var comparison = left.FreeFlowTravelSeconds.CompareTo(right.FreeFlowTravelSeconds);
            if (comparison != 0)
                return comparison;

            var leftCells = left.Cells;
            var rightCells = right.Cells;
            var common = Math.Min(leftCells.Count, rightCells.Count);
            for (var index = 0; index < common; index++)
            {
                comparison = leftCells[index].CompareTo(rightCells[index]);
                if (comparison != 0)
                    return comparison;
            }
            return leftCells.Count.CompareTo(rightCells.Count);
        }

        private static bool ContainsRoute(IReadOnlyList<TrafficRoute> routes, TrafficRoute candidate)
        {
            for (var index = 0; index < routes.Count; index++)
            {
                if (RoutesEqual(routes[index], candidate))
                    return true;
            }
            return false;
        }

        private static bool RoutesEqual(TrafficRoute left, TrafficRoute right)
        {
            var leftCells = left.Cells;
            var rightCells = right.Cells;
            if (leftCells.Count != rightCells.Count)
                return false;
            for (var index = 0; index < leftCells.Count; index++)
            {
                if (leftCells[index] != rightCells[index])
                    return false;
            }
            return true;
        }

        private static int ToInt(long value)
        {
            if (value < 0 || value > int.MaxValue)
                throw new OverflowException("Route travel time exceeded Int32 range.");
            return (int)value;
        }

        private readonly struct RouteCacheKey : IEquatable<RouteCacheKey>, IComparable<RouteCacheKey>
        {
            public RouteCacheKey(CellCoord origin, CellCoord destination)
            {
                Origin = origin;
                Destination = destination;
            }

            public CellCoord Origin { get; }
            public CellCoord Destination { get; }

            public int CompareTo(RouteCacheKey other)
            {
                var comparison = Origin.CompareTo(other.Origin);
                return comparison != 0 ? comparison : Destination.CompareTo(other.Destination);
            }

            public bool Equals(RouteCacheKey other) =>
                Origin == other.Origin && Destination == other.Destination;
            public override bool Equals(object obj) => obj is RouteCacheKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (Origin.GetHashCode() * 397) ^ Destination.GetHashCode();
                }
            }
        }

        private sealed class RouteCacheEntry
        {
            public RouteCacheEntry(int componentId, TrafficRouteSet routeSet)
            {
                ComponentId = componentId;
                RouteSet = routeSet;
            }

            public int ComponentId { get; }
            public TrafficRouteSet RouteSet { get; }
        }

        private readonly struct RouteQueueEntry : IComparable<RouteQueueEntry>
        {
            public RouteQueueEntry(long distance, int cellIndex)
            {
                Distance = distance;
                CellIndex = cellIndex;
            }

            public long Distance { get; }
            public int CellIndex { get; }

            public int CompareTo(RouteQueueEntry other)
            {
                var comparison = Distance.CompareTo(other.Distance);
                return comparison != 0 ? comparison : CellIndex.CompareTo(other.CellIndex);
            }
        }

        private sealed class RouteQueue
        {
            private readonly List<RouteQueueEntry> _entries = new List<RouteQueueEntry>();
            public int Count => _entries.Count;

            public void Enqueue(RouteQueueEntry entry)
            {
                _entries.Add(entry);
                var index = _entries.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (_entries[parent].CompareTo(entry) <= 0)
                        break;
                    _entries[index] = _entries[parent];
                    index = parent;
                }
                _entries[index] = entry;
            }

            public RouteQueueEntry Dequeue()
            {
                if (_entries.Count == 0)
                    throw new InvalidOperationException("The route queue is empty.");

                var result = _entries[0];
                var lastIndex = _entries.Count - 1;
                var last = _entries[lastIndex];
                _entries.RemoveAt(lastIndex);
                if (_entries.Count == 0)
                    return result;

                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= _entries.Count)
                        break;
                    var right = left + 1;
                    var child = right < _entries.Count &&
                                _entries[right].CompareTo(_entries[left]) < 0
                        ? right
                        : left;
                    if (_entries[child].CompareTo(last) >= 0)
                        break;
                    _entries[index] = _entries[child];
                    index = child;
                }
                _entries[index] = last;
                return result;
            }
        }
    }
}
