using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Deterministic presentation sampling for aggregate bus line results. Changing
    /// the sample limit rebuilds only this query and cannot alter mode share.
    /// </summary>
    public sealed class TransitVehicleSamplingReadModel
    {
        public const int DefaultMaximumSamples = 24;
        public const int MaximumAllowedSamples = 200;
        private const ulong SampleNamespace = 0x7600_0000_0000_0000UL;
        private const ulong PayloadMask = 0x00FF_FFFF_FFFF_FFFFUL;

        private readonly TransitOverlayReadModel _overlay;
        private int _sourceVersion = -1;
        private int _configVersion;
        private int _observedConfigVersion = -1;
        private int _maximumSamples = DefaultMaximumSamples;
        private IReadOnlyList<VehicleSampleData> _samples =
            Array.AsReadOnly(Array.Empty<VehicleSampleData>());

        public TransitVehicleSamplingReadModel(TransitOverlayReadModel overlay)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public int MaximumSamples => _maximumSamples;
        public IReadOnlyList<VehicleSampleData> Samples { get { RefreshIfNeeded(); return _samples; } }

        public void SetMaximumSamples(int value)
        {
            if (value < 0 || value > MaximumAllowedSamples)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_maximumSamples == value)
                return;
            _maximumSamples = value;
            _configVersion = checked(_configVersion + 1);
        }

        public bool RefreshIfNeeded()
        {
            _overlay.RefreshIfNeeded();
            if (_sourceVersion == _overlay.Version && _observedConfigVersion == _configVersion)
                return false;

            var routes = new List<TransitOverlayLineData>();
            long totalTrips = 0;
            var source = _overlay.Lines;
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index].BusTrips <= 0 || source[index].Cells.Count == 0)
                    continue;
                routes.Add(source[index]);
                totalTrips += source[index].BusTrips;
            }

            var desired = (int)Math.Min(_maximumSamples, Math.Min(totalTrips, int.MaxValue));
            var assigned = new int[routes.Count];
            for (var slot = 0; slot < desired; slot++)
            {
                var best = -1;
                long bestScore = long.MinValue;
                for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
                {
                    if (assigned[routeIndex] >= routes[routeIndex].BusTrips)
                        continue;
                    var score = (long)routes[routeIndex].BusTrips * 1_000_000L / (assigned[routeIndex] + 1L);
                    if (score > bestScore ||
                        (score == bestScore && best >= 0 && routes[routeIndex].LineId.CompareTo(routes[best].LineId) < 0))
                    {
                        best = routeIndex;
                        bestScore = score;
                    }
                }
                if (best < 0)
                    break;
                assigned[best]++;
            }

            var samples = new List<VehicleSampleData>();
            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                var count = assigned[routeIndex];
                if (count <= 0)
                    continue;
                var durations = new int[Math.Max(0, route.Cells.Count - 1)];
                for (var index = 0; index < durations.Length; index++)
                    durations[index] = 1;
                var path = new VehicleSamplePathData(route.Cells, durations);
                var representedBase = route.BusTrips / count;
                var representedRemainder = route.BusTrips % count;
                for (var sampleIndex = 0; sampleIndex < count; sampleIndex++)
                {
                    var represented = representedBase + (sampleIndex < representedRemainder ? 1 : 0);
                    var payload = (route.LineId.Value ^
                                   ((ulong)(sampleIndex + 1) * 0x9E37_79B9_7F4A_7C15UL)) & PayloadMask;
                    if (payload == 0UL) payload = (ulong)(sampleIndex + 1);
                    var sampleId = new StableId(SampleNamespace | payload);
                    var phase = (int)((route.LineId.Value + (ulong)sampleIndex * 997UL) % 1000UL);
                    samples.Add(new VehicleSampleData(
                        sampleId,
                        TrafficFlowKind.Commute,
                        represented,
                        phase,
                        path));
                }
            }

            _samples = samples.AsReadOnly();
            _sourceVersion = _overlay.Version;
            _observedConfigVersion = _configVersion;
            Version = checked(Version + 1);
            return true;
        }
    }
}
