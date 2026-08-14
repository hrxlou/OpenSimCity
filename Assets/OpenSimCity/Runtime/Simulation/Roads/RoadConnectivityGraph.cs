using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;

namespace OpenSimCity.Simulation.Roads
{
    /// <summary>
    /// Rebuildable cardinal road graph and deterministic external-gate distance
    /// cache. The graph observes RoadNetworkState.Version and refreshes lazily
    /// before every public read.
    /// </summary>
    public sealed class RoadConnectivityGraph
    {
        public const int UnreachableDistance = -1;
        public const int NearDistanceMaximum = 4;
        public const int MediumDistanceMaximum = 12;

        private const int NoComponent = -1;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly RoadNetworkState _roadNetwork;
        private readonly ExternalConnectionState[] _externalConnections;
        private readonly RoadConnectivityResult[] _results;
        private readonly IReadOnlyList<RoadConnectivityResult> _readOnlyResults;
        private readonly RoadConnectivityResult[] _previousResults;
        private readonly bool[] _occupied;
        private readonly RoadConnectionMask[] _connectionMasks;
        private readonly int[] _componentIds;
        private readonly int[] _distances;
        private readonly StableId[] _nearestGateIds;
        private readonly Queue<int> _componentQueue;
        private readonly DistanceQueue _distanceQueue;
        private readonly List<CellCoord> _changedCells;
        private IReadOnlyList<CellCoord> _readOnlyDirtyCells = Array.AsReadOnly(Array.Empty<CellCoord>());
        private int _sourceVersion = -1;
        private int _graphVersion;
        private RoadGraphDirtyRegion _dirtyRegion;

        public RoadConnectivityGraph(
            RoadNetworkState roadNetwork,
            IEnumerable<ExternalConnectionState> externalConnections)
        {
            _roadNetwork = roadNetwork ?? throw new ArgumentNullException(nameof(roadNetwork));
            _externalConnections = CopyAndSortConnections(externalConnections);

            var cellCount = _roadNetwork.CellCount;
            _results = new RoadConnectivityResult[cellCount];
            _readOnlyResults = Array.AsReadOnly(_results);
            _previousResults = new RoadConnectivityResult[cellCount];
            _occupied = new bool[cellCount];
            _connectionMasks = new RoadConnectionMask[cellCount];
            _componentIds = new int[cellCount];
            _distances = new int[cellCount];
            _nearestGateIds = new StableId[cellCount];
            _componentQueue = new Queue<int>();
            _distanceQueue = new DistanceQueue();
            _changedCells = new List<CellCoord>();

            Rebuild();
        }

        public int Width => _roadNetwork.Width;

        public int Height => _roadNetwork.Height;

        public int GraphVersion
        {
            get
            {
                RefreshIfNeeded();
                return _graphVersion;
            }
        }

        public IReadOnlyList<RoadConnectivityResult> Cells
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyResults;
            }
        }

        /// <summary>
        /// Exact row-major cells whose public connectivity result changed in
        /// the most recent rebuild. This is intentionally independent of the
        /// bounding DirtyRegion, which may contain unchanged cells.
        /// </summary>
        public IReadOnlyList<CellCoord> DirtyCells
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyDirtyCells;
            }
        }

        public RoadGraphDirtyRegion DirtyRegion
        {
            get
            {
                RefreshIfNeeded();
                return _dirtyRegion;
            }
        }

        public bool RefreshIfNeeded()
        {
            if (_sourceVersion == _roadNetwork.Version)
                return false;

            Rebuild();
            return true;
        }

        public bool TryGet(CellCoord cell, out RoadConnectivityResult result)
        {
            RefreshIfNeeded();
            if (!_roadNetwork.TryGetIndex(cell, out var index))
            {
                result = default;
                return false;
            }

            result = _results[index];
            return true;
        }

        public RoadConnectivityResult Get(CellCoord cell)
        {
            RefreshIfNeeded();
            return _results[_roadNetwork.GetIndex(cell)];
        }

        public bool IsReachableFromExternalGate(CellCoord cell)
        {
            return Get(cell).IsReachable;
        }

        public int NetworkDistanceToExternalGate(CellCoord cell)
        {
            return Get(cell).NetworkDistanceToExternalGate;
        }

        /// <summary>
        /// Cross-runtime FNV-1a checksum over the public row-major graph query.
        /// The checksum is verification evidence, not persisted simulation state.
        /// </summary>
        public ulong ComputeChecksum()
        {
            RefreshIfNeeded();
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, Width);
            AddInt(ref hash, Height);
            AddInt(ref hash, _graphVersion);

            for (var index = 0; index < _results.Length; index++)
            {
                var result = _results[index];
                AddInt(ref hash, result.Cell.X);
                AddInt(ref hash, result.Cell.Y);
                AddByte(ref hash, (byte)result.ConnectionMask);
                AddInt(ref hash, result.ComponentId);
                AddInt(ref hash, result.NetworkDistanceToExternalGate);
                AddUlong(ref hash, result.NearestExternalGateId.Value);
                AddByte(ref hash, (byte)result.DistanceBand);
                AddByte(ref hash, (byte)result.UnreachableReason);
            }

            return hash;
        }

        private void Rebuild()
        {
            Array.Copy(_results, _previousResults, _results.Length);
            var wasInitialized = _sourceVersion >= 0;

            var occupied = BuildOccupancy();
            var connectionMasks = BuildConnectionMasks(occupied);
            var componentIds = BuildComponents(occupied);
            for (var index = 0; index < _distances.Length; index++)
            {
                _distances[index] = UnreachableDistance;
                _nearestGateIds[index] = StableId.Invalid;
            }

            var activeGateCount = CalculateExternalGateDistances(
                occupied,
                connectionMasks,
                _distances,
                _nearestGateIds);

            for (var index = 0; index < _results.Length; index++)
            {
                var roadCell = _roadNetwork.Cells[index];
                var cell = roadCell.Coordinate;
                if (!occupied[index])
                {
                    _results[index] = new RoadConnectivityResult(
                        cell,
                        RoadConnectionMask.None,
                        NoComponent,
                        UnreachableDistance,
                        StableId.Invalid,
                        ExternalGateDistanceBand.Unreachable,
                        RoadUnreachableReason.NoRoad);
                    continue;
                }

                var unreachableReason = RoadUnreachableReason.None;
                if (_distances[index] == UnreachableDistance)
                {
                    unreachableReason = activeGateCount == 0
                        ? RoadUnreachableReason.NoEnabledExternalRoadGate
                        : RoadUnreachableReason.DisconnectedFromExternalRoadGate;
                }

                _results[index] = new RoadConnectivityResult(
                    cell,
                    connectionMasks[index],
                    componentIds[index],
                    _distances[index],
                    _nearestGateIds[index],
                    GetDistanceBand(_distances[index]),
                    unreachableReason,
                    roadCell.TravelDirection);
            }

            _sourceVersion = _roadNetwork.Version;
            _graphVersion = _sourceVersion;
            UpdateDirtyCells(_previousResults, wasInitialized);
        }

        private bool[] BuildOccupancy()
        {
            for (var index = 0; index < _occupied.Length; index++)
                _occupied[index] = _roadNetwork.Cells[index].IsOccupied;
            return _occupied;
        }

        private RoadConnectionMask[] BuildConnectionMasks(bool[] occupied)
        {
            for (var index = 0; index < occupied.Length; index++)
            {
                if (!occupied[index])
                {
                    _connectionMasks[index] = RoadConnectionMask.None;
                    continue;
                }

                var cell = _roadNetwork.Cells[index].Coordinate;
                var mask = RoadConnectionMask.None;
                if (IsOccupied(occupied, cell.X, cell.Y + 1))
                    mask |= RoadConnectionMask.North;
                if (IsOccupied(occupied, cell.X + 1, cell.Y))
                    mask |= RoadConnectionMask.East;
                if (IsOccupied(occupied, cell.X, cell.Y - 1))
                    mask |= RoadConnectionMask.South;
                if (IsOccupied(occupied, cell.X - 1, cell.Y))
                    mask |= RoadConnectionMask.West;
                _connectionMasks[index] = mask;
            }

            return _connectionMasks;
        }

        private int[] BuildComponents(bool[] occupied)
        {
            for (var index = 0; index < _componentIds.Length; index++)
                _componentIds[index] = NoComponent;

            _componentQueue.Clear();
            for (var rootIndex = 0; rootIndex < occupied.Length; rootIndex++)
            {
                if (!occupied[rootIndex] || _componentIds[rootIndex] != NoComponent)
                    continue;

                // The minimum row-major member is deterministic and keeps an
                // unrelated component's label stable across local edits.
                _componentIds[rootIndex] = rootIndex;
                _componentQueue.Enqueue(rootIndex);
                while (_componentQueue.Count > 0)
                {
                    var current = _componentQueue.Dequeue();
                    var x = current % Width;
                    var y = current / Width;
                    if (y > 0)
                        TryVisitComponentNeighbor(current - Width, rootIndex, occupied);
                    if (x > 0)
                        TryVisitComponentNeighbor(current - 1, rootIndex, occupied);
                    if (x + 1 < Width)
                        TryVisitComponentNeighbor(current + 1, rootIndex, occupied);
                    if (y + 1 < Height)
                        TryVisitComponentNeighbor(current + Width, rootIndex, occupied);
                }
            }

            return _componentIds;
        }

        private void TryVisitComponentNeighbor(int neighbor, int componentId, bool[] occupied)
        {
            if (!occupied[neighbor] || _componentIds[neighbor] != NoComponent)
                return;

            _componentIds[neighbor] = componentId;
            _componentQueue.Enqueue(neighbor);
        }

        private int CalculateExternalGateDistances(
            bool[] occupied,
            RoadConnectionMask[] connectionMasks,
            int[] distances,
            StableId[] nearestGateIds)
        {
            _distanceQueue.Clear();
            var activeGateCount = 0;
            for (var index = 0; index < _externalConnections.Length; index++)
            {
                var gate = _externalConnections[index];
                if (!IsActiveRoadGate(gate, occupied, out var cellIndex))
                    continue;

                activeGateCount++;
                if (distances[cellIndex] == 0 && nearestGateIds[cellIndex] <= gate.Id)
                    continue;

                distances[cellIndex] = 0;
                nearestGateIds[cellIndex] = gate.Id;
                _distanceQueue.Enqueue(new DistanceQueueEntry(0, gate.Id, cellIndex));
            }

            while (_distanceQueue.Count > 0)
            {
                var current = _distanceQueue.Dequeue();
                if (distances[current.CellIndex] != current.Distance ||
                    nearestGateIds[current.CellIndex] != current.GateId)
                {
                    continue;
                }

                var mask = connectionMasks[current.CellIndex];
                if ((mask & RoadConnectionMask.South) != 0)
                {
                    TryRelaxDistance(
                        current,
                        current.CellIndex - Width,
                        distances,
                        nearestGateIds);
                }
                if ((mask & RoadConnectionMask.West) != 0)
                {
                    TryRelaxDistance(
                        current,
                        current.CellIndex - 1,
                        distances,
                        nearestGateIds);
                }
                if ((mask & RoadConnectionMask.East) != 0)
                {
                    TryRelaxDistance(
                        current,
                        current.CellIndex + 1,
                        distances,
                        nearestGateIds);
                }
                if ((mask & RoadConnectionMask.North) != 0)
                {
                    TryRelaxDistance(
                        current,
                        current.CellIndex + Width,
                        distances,
                        nearestGateIds);
                }
            }

            return activeGateCount;
        }

        private void TryRelaxDistance(
            DistanceQueueEntry current,
            int neighbor,
            int[] distances,
            StableId[] nearestGateIds)
        {
            var candidateDistance = checked(current.Distance + 1);
            var existingDistance = distances[neighbor];
            var existingGateId = nearestGateIds[neighbor];
            var isBetter = existingDistance == UnreachableDistance ||
                           candidateDistance < existingDistance ||
                           (candidateDistance == existingDistance && current.GateId < existingGateId);
            if (!isBetter)
                return;

            distances[neighbor] = candidateDistance;
            nearestGateIds[neighbor] = current.GateId;
            _distanceQueue.Enqueue(new DistanceQueueEntry(
                candidateDistance,
                current.GateId,
                neighbor));
        }

        private bool IsActiveRoadGate(
            ExternalConnectionState gate,
            bool[] occupied,
            out int cellIndex)
        {
            cellIndex = -1;
            if (!gate.IsEnabled || gate.Kind != ExternalConnectionKind.Road)
                return false;
            if (!_roadNetwork.TryGetIndex(gate.Cell, out cellIndex) || !occupied[cellIndex])
                return false;

            return gate.Cell.X == 0 || gate.Cell.X == Width - 1 ||
                   gate.Cell.Y == 0 || gate.Cell.Y == Height - 1;
        }

        private void UpdateDirtyCells(RoadConnectivityResult[] previous, bool wasInitialized)
        {
            _changedCells.Clear();
            for (var index = 0; index < _results.Length; index++)
            {
                if (!wasInitialized || previous[index] != _results[index])
                    _changedCells.Add(_results[index].Cell);
            }

            var cells = _changedCells.ToArray();
            _readOnlyDirtyCells = Array.AsReadOnly(cells);
            _dirtyRegion = RoadGraphDirtyRegion.FromSortedCells(cells);
        }

        private bool IsOccupied(bool[] occupied, int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return false;
            return occupied[y * Width + x];
        }

        private static ExternalGateDistanceBand GetDistanceBand(int distance)
        {
            if (distance == UnreachableDistance)
                return ExternalGateDistanceBand.Unreachable;
            if (distance == 0)
                return ExternalGateDistanceBand.Gate;
            if (distance <= NearDistanceMaximum)
                return ExternalGateDistanceBand.Near;
            return distance <= MediumDistanceMaximum
                ? ExternalGateDistanceBand.Medium
                : ExternalGateDistanceBand.Far;
        }

        private static ExternalConnectionState[] CopyAndSortConnections(
            IEnumerable<ExternalConnectionState> externalConnections)
        {
            if (externalConnections == null)
                throw new ArgumentNullException(nameof(externalConnections));

            var connections = new List<ExternalConnectionState>(externalConnections);
            connections.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (var index = 1; index < connections.Count; index++)
            {
                if (connections[index - 1].Id == connections[index].Id)
                {
                    throw new ArgumentException(
                        $"External connection ID {connections[index].Id} is duplicated.",
                        nameof(externalConnections));
                }
            }

            return connections.ToArray();
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

        private readonly struct DistanceQueueEntry : IComparable<DistanceQueueEntry>
        {
            public DistanceQueueEntry(int distance, StableId gateId, int cellIndex)
            {
                Distance = distance;
                GateId = gateId;
                CellIndex = cellIndex;
            }

            public int Distance { get; }

            public StableId GateId { get; }

            public int CellIndex { get; }

            public int CompareTo(DistanceQueueEntry other)
            {
                var comparison = Distance.CompareTo(other.Distance);
                if (comparison != 0)
                    return comparison;
                comparison = GateId.CompareTo(other.GateId);
                return comparison != 0 ? comparison : CellIndex.CompareTo(other.CellIndex);
            }
        }

        /// <summary>
        /// Small binary min-heap with an explicit total ordering. It avoids
        /// runtime-specific collection ordering in the graph result.
        /// </summary>
        private sealed class DistanceQueue
        {
            private readonly List<DistanceQueueEntry> _entries = new List<DistanceQueueEntry>();

            public int Count => _entries.Count;

            public void Clear()
            {
                _entries.Clear();
            }

            public void Enqueue(DistanceQueueEntry entry)
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

            public DistanceQueueEntry Dequeue()
            {
                if (_entries.Count == 0)
                    throw new InvalidOperationException("Cannot dequeue an empty distance queue.");

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
                    var child = right < _entries.Count && _entries[right].CompareTo(_entries[left]) < 0
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
