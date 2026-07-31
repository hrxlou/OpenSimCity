using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Queries
{
    public enum TrafficFlowKind : byte
    {
        Commute,
        Goods
    }

    public enum TrafficVisualizationUnroutedReason : byte
    {
        OriginRoadMissing,
        DestinationRoadMissing,
        DifferentRoadComponent,
        NoPath
    }

    [Serializable]
    public readonly struct TrafficEdgeViewKey : IEquatable<TrafficEdgeViewKey>, IComparable<TrafficEdgeViewKey>
    {
        public TrafficEdgeViewKey(CellCoord first, CellCoord second)
        {
            if (first.ManhattanDistanceTo(second) != 1)
                throw new ArgumentException("A traffic edge must connect cardinally adjacent cells.");

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

        public int CompareTo(TrafficEdgeViewKey other)
        {
            var comparison = A.CompareTo(other.A);
            return comparison != 0 ? comparison : B.CompareTo(other.B);
        }

        public bool Equals(TrafficEdgeViewKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is TrafficEdgeViewKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (A.GetHashCode() * 397) ^ B.GetHashCode();
            }
        }

        public static bool operator ==(TrafficEdgeViewKey left, TrafficEdgeViewKey right) => left.Equals(right);
        public static bool operator !=(TrafficEdgeViewKey left, TrafficEdgeViewKey right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct TrafficEdgeViewData
    {
        public TrafficEdgeViewData(
            TrafficEdgeViewKey edge,
            int volume,
            int capacity,
            int freeFlowSpeedCellsPerHour,
            int effectiveSpeedCellsPerHour,
            int volumeCapacityPermille,
            int freeFlowTravelSeconds,
            int congestedTravelSeconds)
        {
            Edge = edge;
            Volume = volume;
            Capacity = capacity;
            FreeFlowSpeedCellsPerHour = freeFlowSpeedCellsPerHour;
            EffectiveSpeedCellsPerHour = effectiveSpeedCellsPerHour;
            VolumeCapacityPermille = volumeCapacityPermille;
            FreeFlowTravelSeconds = freeFlowTravelSeconds;
            CongestedTravelSeconds = congestedTravelSeconds;
        }

        public TrafficEdgeViewKey Edge { get; }
        public int Volume { get; }
        public int Capacity { get; }
        public int FreeFlowSpeedCellsPerHour { get; }
        public int EffectiveSpeedCellsPerHour { get; }
        public int VolumeCapacityPermille { get; }
        public int FreeFlowTravelSeconds { get; }
        public int CongestedTravelSeconds { get; }
        public int DelaySeconds => CongestedTravelSeconds - FreeFlowTravelSeconds;
        public bool IsCongested => Volume > Capacity;
    }

    [Serializable]
    public readonly struct TrafficUnroutedViewData
    {
        public TrafficUnroutedViewData(
            TrafficFlowKind kind,
            StableId originId,
            StableId destinationId,
            CellCoord originRoadCell,
            CellCoord destinationRoadCell,
            int amount,
            TrafficVisualizationUnroutedReason reason)
        {
            Kind = kind;
            OriginId = originId;
            DestinationId = destinationId;
            OriginRoadCell = originRoadCell;
            DestinationRoadCell = destinationRoadCell;
            Amount = amount;
            Reason = reason;
        }

        public TrafficFlowKind Kind { get; }
        public StableId OriginId { get; }
        public StableId DestinationId { get; }
        public CellCoord OriginRoadCell { get; }
        public CellCoord DestinationRoadCell { get; }
        public int Amount { get; }
        public TrafficVisualizationUnroutedReason Reason { get; }
    }

    [Serializable]
    public readonly struct TrafficOdViewData
    {
        public TrafficOdViewData(
            TrafficFlowKind kind,
            StableId originId,
            StableId destinationId,
            CellCoord originRoadCell,
            CellCoord destinationRoadCell,
            int amount,
            int routeCount,
            int averageFreeFlowTravelSeconds,
            int averageCongestedTravelSeconds)
        {
            Kind = kind;
            OriginId = originId;
            DestinationId = destinationId;
            OriginRoadCell = originRoadCell;
            DestinationRoadCell = destinationRoadCell;
            Amount = amount;
            RouteCount = routeCount;
            AverageFreeFlowTravelSeconds = averageFreeFlowTravelSeconds;
            AverageCongestedTravelSeconds = averageCongestedTravelSeconds;
        }

        public TrafficFlowKind Kind { get; }
        public StableId OriginId { get; }
        public StableId DestinationId { get; }
        public CellCoord OriginRoadCell { get; }
        public CellCoord DestinationRoadCell { get; }
        public int Amount { get; }
        public int RouteCount { get; }
        public int AverageFreeFlowTravelSeconds { get; }
        public int AverageCongestedTravelSeconds { get; }
        public int AverageDelaySeconds => AverageCongestedTravelSeconds - AverageFreeFlowTravelSeconds;
    }

    [Serializable]
    public readonly struct VehicleRoutePosition
    {
        public VehicleRoutePosition(CellCoord from, CellCoord to, int segmentProgressPermille)
        {
            From = from;
            To = to;
            SegmentProgressPermille = segmentProgressPermille;
        }

        public CellCoord From { get; }
        public CellCoord To { get; }
        public int SegmentProgressPermille { get; }
    }

    /// <summary>
    /// Immutable route geometry and congestion-weighted segment timing for a sampled
    /// presentation vehicle. It is derived from the aggregate route and edge query.
    /// </summary>
    [Serializable]
    public sealed class VehicleSamplePathData
    {
        private readonly CellCoord[] _cells;
        private readonly int[] _segmentDurations;
        private readonly int[] _cumulativeDurations;
        private readonly IReadOnlyList<CellCoord> _readOnlyCells;
        private readonly IReadOnlyList<int> _readOnlySegmentDurations;

        public VehicleSamplePathData(
            IReadOnlyList<CellCoord> cells,
            IReadOnlyList<int> segmentDurations)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            if (segmentDurations == null)
                throw new ArgumentNullException(nameof(segmentDurations));
            if (cells.Count == 0)
                throw new ArgumentException("A sampled vehicle path requires at least one cell.", nameof(cells));
            if (segmentDurations.Count != cells.Count - 1)
                throw new ArgumentException("There must be one duration for every route segment.", nameof(segmentDurations));

            _cells = new CellCoord[cells.Count];
            _segmentDurations = new int[segmentDurations.Count];
            _cumulativeDurations = new int[segmentDurations.Count + 1];
            for (var index = 0; index < cells.Count; index++)
            {
                _cells[index] = cells[index];
                if (index > 0 && _cells[index - 1].ManhattanDistanceTo(_cells[index]) != 1)
                    throw new ArgumentException("Sampled route cells must be cardinally contiguous.", nameof(cells));
            }

            for (var index = 0; index < segmentDurations.Count; index++)
            {
                if (segmentDurations[index] <= 0)
                    throw new ArgumentOutOfRangeException(nameof(segmentDurations), "Segment duration must be positive.");
                _segmentDurations[index] = segmentDurations[index];
                _cumulativeDurations[index + 1] = checked(_cumulativeDurations[index] + segmentDurations[index]);
            }

            _readOnlyCells = Array.AsReadOnly(_cells);
            _readOnlySegmentDurations = Array.AsReadOnly(_segmentDurations);
        }

        public IReadOnlyList<CellCoord> Cells => _readOnlyCells;
        public IReadOnlyList<int> SegmentDurationsSeconds => _readOnlySegmentDurations;
        public int SegmentCount => _segmentDurations.Length;
        public int TotalTravelSeconds => _cumulativeDurations[_cumulativeDurations.Length - 1];

        public VehicleRoutePosition Evaluate(int routeProgressPermille)
        {
            if (routeProgressPermille < 0 || routeProgressPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(routeProgressPermille));
            if (_cells.Length == 1)
                return new VehicleRoutePosition(_cells[0], _cells[0], 0);

            var target = routeProgressPermille == 1000
                ? TotalTravelSeconds
                : (int)((long)TotalTravelSeconds * routeProgressPermille / 1000);
            var segment = _segmentDurations.Length - 1;
            for (var index = 0; index < _segmentDurations.Length; index++)
            {
                if (target < _cumulativeDurations[index + 1])
                {
                    segment = index;
                    break;
                }
            }

            var elapsed = target - _cumulativeDurations[segment];
            var progress = routeProgressPermille == 1000 && segment == _segmentDurations.Length - 1
                ? 1000
                : (int)((long)elapsed * 1000 / _segmentDurations[segment]);
            return new VehicleRoutePosition(_cells[segment], _cells[segment + 1], progress);
        }
    }

    [Serializable]
    public readonly struct VehicleSampleData
    {
        public VehicleSampleData(
            StableId sampleId,
            TrafficFlowKind kind,
            int representedTripAmount,
            int phaseOffsetPermille,
            VehicleSamplePathData path)
        {
            if (!sampleId.IsValid)
                throw new ArgumentException("A vehicle sample requires a valid ID.", nameof(sampleId));
            if (representedTripAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(representedTripAmount));
            if (phaseOffsetPermille < 0 || phaseOffsetPermille >= 1000)
                throw new ArgumentOutOfRangeException(nameof(phaseOffsetPermille));

            SampleId = sampleId;
            Kind = kind;
            RepresentedTripAmount = representedTripAmount;
            PhaseOffsetPermille = phaseOffsetPermille;
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public StableId SampleId { get; }
        public TrafficFlowKind Kind { get; }
        public int RepresentedTripAmount { get; }
        public int PhaseOffsetPermille { get; }
        public VehicleSamplePathData Path { get; }
    }

    /// <summary>
    /// Presentation-safe traffic projection. Changing the vehicle sample limit only
    /// rebuilds this read model and can never modify aggregate route or edge state.
    /// </summary>
    public sealed class TrafficVisualizationReadModel
    {
        public const int DefaultMaximumVehicleSamples = 100;
        public const int MaximumAllowedVehicleSamples = 500;

        private const ulong SampleIdNamespace = 0x7500_0000_0000_0000UL;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly TrafficRoutingReadModel _routing;
        private int _sourceVersion = -1;
        private int _configurationVersion;
        private int _observedConfigurationVersion = -1;
        private int _maximumVehicleSamples = DefaultMaximumVehicleSamples;
        private IReadOnlyList<TrafficEdgeViewData> _edges =
            Array.AsReadOnly(Array.Empty<TrafficEdgeViewData>());
        private IReadOnlyList<TrafficEdgeViewData> _bottlenecks =
            Array.AsReadOnly(Array.Empty<TrafficEdgeViewData>());
        private IReadOnlyList<TrafficUnroutedViewData> _unrouted =
            Array.AsReadOnly(Array.Empty<TrafficUnroutedViewData>());
        private IReadOnlyList<TrafficOdViewData> _odImpacts =
            Array.AsReadOnly(Array.Empty<TrafficOdViewData>());
        private IReadOnlyList<VehicleSampleData> _vehicleSamples =
            Array.AsReadOnly(Array.Empty<VehicleSampleData>());

        public TrafficVisualizationReadModel(TrafficRoutingReadModel routing)
        {
            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public int MaximumVehicleSamples => _maximumVehicleSamples;
        public IReadOnlyList<TrafficEdgeViewData> Edges
        {
            get
            {
                RefreshIfNeeded();
                return _edges;
            }
        }

        public IReadOnlyList<TrafficEdgeViewData> Bottlenecks
        {
            get
            {
                RefreshIfNeeded();
                return _bottlenecks;
            }
        }

        public IReadOnlyList<TrafficUnroutedViewData> Unrouted
        {
            get
            {
                RefreshIfNeeded();
                return _unrouted;
            }
        }

        public IReadOnlyList<TrafficOdViewData> OdImpacts
        {
            get
            {
                RefreshIfNeeded();
                return _odImpacts;
            }
        }

        public IReadOnlyList<VehicleSampleData> VehicleSamples
        {
            get
            {
                RefreshIfNeeded();
                return _vehicleSamples;
            }
        }

        public ulong SourceSimulationChecksum
        {
            get
            {
                RefreshIfNeeded();
                return _routing.Checksum;
            }
        }

        public ulong VisualizationChecksum
        {
            get
            {
                RefreshIfNeeded();
                return ComputeVisualizationChecksum();
            }
        }

        public bool SetMaximumVehicleSamples(int value)
        {
            if (value < 0 || value > MaximumAllowedVehicleSamples)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_maximumVehicleSamples == value)
                return false;

            _maximumVehicleSamples = value;
            _configurationVersion = checked(_configurationVersion + 1);
            return true;
        }

        public bool RefreshIfNeeded()
        {
            _routing.RefreshIfNeeded();
            if (_sourceVersion == _routing.Version &&
                _observedConfigurationVersion == _configurationVersion)
            {
                return false;
            }

            Rebuild();
            _sourceVersion = _routing.Version;
            _observedConfigurationVersion = _configurationVersion;
            Version = checked(Version + 1);
            return true;
        }

        private void Rebuild()
        {
            var sourceEdges = _routing.Edges;
            var edges = new TrafficEdgeViewData[sourceEdges.Count];
            var edgeLookup = new Dictionary<TrafficEdgeViewKey, TrafficEdgeViewData>();
            for (var index = 0; index < sourceEdges.Count; index++)
            {
                var source = sourceEdges[index];
                var row = new TrafficEdgeViewData(
                    new TrafficEdgeViewKey(source.Edge.A, source.Edge.B),
                    source.Volume,
                    source.Capacity,
                    source.FreeFlowSpeedCellsPerHour,
                    source.EffectiveSpeedCellsPerHour,
                    source.VolumeCapacityPermille,
                    source.FreeFlowTravelSeconds,
                    source.CongestedTravelSeconds);
                edges[index] = row;
                edgeLookup.Add(row.Edge, row);
            }
            _edges = Array.AsReadOnly(edges);

            var bottlenecks = (TrafficEdgeViewData[])edges.Clone();
            Array.Sort(bottlenecks, CompareBottlenecks);
            _bottlenecks = Array.AsReadOnly(bottlenecks);

            var sourceUnrouted = _routing.Unrouted;
            var unrouted = new TrafficUnroutedViewData[sourceUnrouted.Count];
            for (var index = 0; index < sourceUnrouted.Count; index++)
            {
                var source = sourceUnrouted[index];
                unrouted[index] = new TrafficUnroutedViewData(
                    ConvertKind(source.Kind),
                    source.Origin.Id,
                    source.Destination.Id,
                    source.Origin.RoadCell,
                    source.Destination.RoadCell,
                    source.Amount,
                    ConvertReason(source.Reason));
            }
            _unrouted = Array.AsReadOnly(unrouted);

            var assignments = _routing.Assignments;
            _odImpacts = Array.AsReadOnly(BuildOdImpacts(assignments));
            _vehicleSamples = Array.AsReadOnly(BuildVehicleSamples(assignments, edgeLookup));
        }

        private TrafficOdViewData[] BuildOdImpacts(IReadOnlyList<TrafficRouteAssignment> assignments)
        {
            var aggregates = new SortedDictionary<OdKey, OdAggregate>();
            for (var index = 0; index < assignments.Count; index++)
            {
                var row = assignments[index];
                var key = new OdKey(row.Kind, row.Origin.Id, row.Destination.Id);
                if (!aggregates.TryGetValue(key, out var aggregate))
                {
                    aggregate = new OdAggregate(
                        row.Origin.RoadCell,
                        row.Destination.RoadCell);
                }

                aggregate.Amount = checked(aggregate.Amount + row.Amount);
                aggregate.RouteCount = checked(aggregate.RouteCount + 1);
                aggregate.WeightedFreeFlowSeconds = checked(
                    aggregate.WeightedFreeFlowSeconds + (long)row.Amount * row.FreeFlowTravelSeconds);
                aggregate.WeightedCongestedSeconds = checked(
                    aggregate.WeightedCongestedSeconds + (long)row.Amount * row.CongestedTravelSeconds);
                aggregates[key] = aggregate;
            }

            var rows = new TrafficOdViewData[aggregates.Count];
            var rowIndex = 0;
            foreach (var pair in aggregates)
            {
                var aggregate = pair.Value;
                rows[rowIndex++] = new TrafficOdViewData(
                    ConvertKind(pair.Key.Kind),
                    pair.Key.OriginId,
                    pair.Key.DestinationId,
                    aggregate.OriginRoadCell,
                    aggregate.DestinationRoadCell,
                    aggregate.Amount,
                    aggregate.RouteCount,
                    DivideToInt(aggregate.WeightedFreeFlowSeconds, aggregate.Amount),
                    DivideToInt(aggregate.WeightedCongestedSeconds, aggregate.Amount));
            }

            Array.Sort(rows, CompareOdImpacts);
            return rows;
        }

        private VehicleSampleData[] BuildVehicleSamples(
            IReadOnlyList<TrafficRouteAssignment> assignments,
            IReadOnlyDictionary<TrafficEdgeViewKey, TrafficEdgeViewData> edges)
        {
            var totalRouted = 0;
            for (var index = 0; index < assignments.Count; index++)
                totalRouted = checked(totalRouted + assignments[index].Amount);
            var target = Math.Min(_maximumVehicleSamples, totalRouted);
            if (target == 0 || assignments.Count == 0)
                return Array.Empty<VehicleSampleData>();

            var counts = new int[assignments.Count];
            var remainders = new SampleRemainder[assignments.Count];
            var allocated = 0;
            for (var index = 0; index < assignments.Count; index++)
            {
                var scaled = (long)assignments[index].Amount * target;
                counts[index] = (int)(scaled / totalRouted);
                allocated += counts[index];
                remainders[index] = new SampleRemainder(index, scaled % totalRouted);
            }
            Array.Sort(remainders);
            for (var index = 0; index < target - allocated; index++)
                counts[remainders[index].AssignmentIndex]++;

            var paths = new VehicleSamplePathData[assignments.Count];
            var result = new VehicleSampleData[target];
            var sampleIndex = 0;
            for (var assignmentIndex = 0; assignmentIndex < assignments.Count; assignmentIndex++)
            {
                var sampleCount = counts[assignmentIndex];
                if (sampleCount == 0)
                    continue;

                var assignment = assignments[assignmentIndex];
                var path = paths[assignmentIndex] ?? BuildPath(assignment, edges);
                paths[assignmentIndex] = path;
                var baseWeight = assignment.Amount / sampleCount;
                var weightRemainder = assignment.Amount % sampleCount;
                for (var ordinal = 0; ordinal < sampleCount; ordinal++)
                {
                    var weight = baseWeight + (ordinal < weightRemainder ? 1 : 0);
                    var phase = (int)((long)sampleIndex * 1000 / target);
                    result[sampleIndex] = new VehicleSampleData(
                        BuildSampleId(assignment, ordinal),
                        ConvertKind(assignment.Kind),
                        weight,
                        phase,
                        path);
                    sampleIndex++;
                }
            }

            return result;
        }

        private static VehicleSamplePathData BuildPath(
            TrafficRouteAssignment assignment,
            IReadOnlyDictionary<TrafficEdgeViewKey, TrafficEdgeViewData> edges)
        {
            var routeCells = assignment.Route.Cells;
            var durations = new int[Math.Max(0, routeCells.Count - 1)];
            for (var index = 0; index < durations.Length; index++)
            {
                var key = new TrafficEdgeViewKey(routeCells[index], routeCells[index + 1]);
                if (!edges.TryGetValue(key, out var edge))
                    throw new InvalidOperationException($"Route edge {key.A}->{key.B} has no congestion row.");
                durations[index] = edge.CongestedTravelSeconds;
            }
            return new VehicleSamplePathData(routeCells, durations);
        }

        private static StableId BuildSampleId(TrafficRouteAssignment assignment, int ordinal)
        {
            var hash = FnvOffsetBasis64;
            AddByte(ref hash, (byte)assignment.Kind);
            AddUlong(ref hash, assignment.Origin.Id.Value);
            AddUlong(ref hash, assignment.Destination.Id.Value);
            AddInt(ref hash, assignment.RouteIndex);
            AddInt(ref hash, ordinal);
            var value = SampleIdNamespace | (hash & 0x00FF_FFFF_FFFF_FFFFUL);
            if (value == 0)
                value = SampleIdNamespace | 1UL;
            return new StableId(value);
        }

        private ulong ComputeVisualizationChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, _maximumVehicleSamples);
            AddUlong(ref hash, _routing.Checksum);
            for (var index = 0; index < _vehicleSamples.Count; index++)
            {
                var sample = _vehicleSamples[index];
                AddUlong(ref hash, sample.SampleId.Value);
                AddByte(ref hash, (byte)sample.Kind);
                AddInt(ref hash, sample.RepresentedTripAmount);
                AddInt(ref hash, sample.PhaseOffsetPermille);
            }
            return hash;
        }

        private static int CompareBottlenecks(TrafficEdgeViewData left, TrafficEdgeViewData right)
        {
            var comparison = right.VolumeCapacityPermille.CompareTo(left.VolumeCapacityPermille);
            if (comparison != 0)
                return comparison;
            comparison = right.DelaySeconds.CompareTo(left.DelaySeconds);
            return comparison != 0 ? comparison : left.Edge.CompareTo(right.Edge);
        }

        private static int CompareOdImpacts(TrafficOdViewData left, TrafficOdViewData right)
        {
            var comparison = right.AverageDelaySeconds.CompareTo(left.AverageDelaySeconds);
            if (comparison != 0)
                return comparison;
            comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
                return comparison;
            comparison = left.OriginId.CompareTo(right.OriginId);
            return comparison != 0 ? comparison : left.DestinationId.CompareTo(right.DestinationId);
        }

        private static TrafficFlowKind ConvertKind(TripDemandKind kind)
        {
            switch (kind)
            {
                case TripDemandKind.Commute:
                    return TrafficFlowKind.Commute;
                case TripDemandKind.Goods:
                    return TrafficFlowKind.Goods;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static TrafficVisualizationUnroutedReason ConvertReason(TrafficRouteFailureReason reason)
        {
            switch (reason)
            {
                case TrafficRouteFailureReason.OriginRoadMissing:
                    return TrafficVisualizationUnroutedReason.OriginRoadMissing;
                case TrafficRouteFailureReason.DestinationRoadMissing:
                    return TrafficVisualizationUnroutedReason.DestinationRoadMissing;
                case TrafficRouteFailureReason.DifferentRoadComponent:
                    return TrafficVisualizationUnroutedReason.DifferentRoadComponent;
                case TrafficRouteFailureReason.NoPath:
                    return TrafficVisualizationUnroutedReason.NoPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        private static int DivideToInt(long numerator, int denominator)
        {
            if (denominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            var value = numerator / denominator;
            if (value < 0 || value > int.MaxValue)
                throw new OverflowException("Traffic visualization average exceeded Int32 range.");
            return (int)value;
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

        private readonly struct SampleRemainder : IComparable<SampleRemainder>
        {
            public SampleRemainder(int assignmentIndex, long remainder)
            {
                AssignmentIndex = assignmentIndex;
                Remainder = remainder;
            }

            public int AssignmentIndex { get; }
            public long Remainder { get; }

            public int CompareTo(SampleRemainder other)
            {
                var comparison = other.Remainder.CompareTo(Remainder);
                return comparison != 0 ? comparison : AssignmentIndex.CompareTo(other.AssignmentIndex);
            }
        }

        private readonly struct OdKey : IComparable<OdKey>
        {
            public OdKey(TripDemandKind kind, StableId originId, StableId destinationId)
            {
                Kind = kind;
                OriginId = originId;
                DestinationId = destinationId;
            }

            public TripDemandKind Kind { get; }
            public StableId OriginId { get; }
            public StableId DestinationId { get; }

            public int CompareTo(OdKey other)
            {
                var comparison = Kind.CompareTo(other.Kind);
                if (comparison != 0)
                    return comparison;
                comparison = OriginId.CompareTo(other.OriginId);
                return comparison != 0 ? comparison : DestinationId.CompareTo(other.DestinationId);
            }
        }

        private struct OdAggregate
        {
            public OdAggregate(CellCoord originRoadCell, CellCoord destinationRoadCell)
            {
                OriginRoadCell = originRoadCell;
                DestinationRoadCell = destinationRoadCell;
                Amount = 0;
                RouteCount = 0;
                WeightedFreeFlowSeconds = 0;
                WeightedCongestedSeconds = 0;
            }

            public CellCoord OriginRoadCell;
            public CellCoord DestinationRoadCell;
            public int Amount;
            public int RouteCount;
            public long WeightedFreeFlowSeconds;
            public long WeightedCongestedSeconds;
        }
    }
}
