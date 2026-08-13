using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Services
{
    /// <summary>
    /// Converts unmet service rows into deterministic response events and a bounded
    /// display-only route sample. Observing or reconfiguring this projection cannot
    /// write PublicServiceState.
    /// </summary>
    public sealed class ServiceResponseProjection
    {
        public const int DefaultMaximumSamples = 16;
        public const int MaximumAllowedSamples = 64;
        private const ulong EventNamespace = 0xF730000000000000UL;
        private const ulong SampleNamespace = 0xF731000000000000UL;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly PublicServiceRuntimeModule _module;
        private readonly ServiceResponseRouteFactory _routes;
        private int _readVersion = -1;
        private int _roadVersion = -1;
        private int _configurationVersion;
        private int _observedConfigurationVersion = -1;
        private int _maximumSamples = DefaultMaximumSamples;
        private IReadOnlyList<ServiceResponseEventData> _events =
            Array.AsReadOnly(Array.Empty<ServiceResponseEventData>());
        private IReadOnlyList<ServiceResponseSampleData> _samples =
            Array.AsReadOnly(Array.Empty<ServiceResponseSampleData>());

        public ServiceResponseProjection(PublicServiceRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _routes = new ServiceResponseRouteFactory(module);
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public int MaximumSamples => _maximumSamples;
        public IReadOnlyList<ServiceResponseEventData> Events
        {
            get { RefreshIfNeeded(); return _events; }
        }
        public IReadOnlyList<ServiceResponseSampleData> Samples
        {
            get { RefreshIfNeeded(); return _samples; }
        }
        public ulong SourceSimulationChecksum
        {
            get { RefreshIfNeeded(); return _module.System.State.ComputeChecksum(); }
        }

        public bool SetMaximumSamples(int value)
        {
            if (value < 0 || value > MaximumAllowedSamples)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_maximumSamples == value)
                return false;
            _maximumSamples = value;
            _configurationVersion = checked(_configurationVersion + 1);
            return true;
        }

        public bool RefreshIfNeeded()
        {
            _module.ReadModel.RefreshIfNeeded();
            var readVersion = _module.ReadModel.Snapshot.Version;
            var roadVersion = _module.Runtime.Roads.Version;
            if (_readVersion == readVersion && _roadVersion == roadVersion &&
                _observedConfigurationVersion == _configurationVersion)
                return false;
            Rebuild();
            _readVersion = readVersion;
            _roadVersion = roadVersion;
            _observedConfigurationVersion = _configurationVersion;
            Version = checked(Version + 1);
            return true;
        }

        private void Rebuild()
        {
            var snapshot = _module.ReadModel.Snapshot;
            var facilities = new Dictionary<StableId, ServiceFacilityReadModel>();
            for (var index = 0; index < snapshot.Facilities.Count; index++)
                facilities[snapshot.Facilities[index].FacilityId] = snapshot.Facilities[index];

            var events = new List<ServiceResponseEventData>();
            var samples = new List<ServiceResponseSampleData>();
            for (var index = 0; index < snapshot.Coverage.Count; index++)
            {
                var row = snapshot.Coverage[index];
                var unmet = row.Requested - row.Served;
                if (unmet <= 0)
                    continue;

                var eventId = BuildId(EventNamespace, row.BuildingId.Value, (byte)row.Kind);
                ServiceResponseRouteReason routeReason;
                OpenSimCity.Application.Queries.VehicleSamplePathData path = null;
                if (!row.FacilityId.IsValid || !facilities.TryGetValue(row.FacilityId, out var facility))
                {
                    routeReason = row.Kind == ServiceKindView.School
                        ? ServiceResponseRouteReason.NonVehicleService
                        : ServiceResponseRouteReason.NoAssignedFacility;
                }
                else
                {
                    routeReason = _routes.TryBuild(row, facility, out path);
                }

                events.Add(new ServiceResponseEventData(
                    eventId,
                    row.BuildingId,
                    row.Kind,
                    row.FacilityId,
                    row.Cell,
                    row.Requested,
                    row.Served,
                    row.FailureReason,
                    routeReason));

                if (routeReason != ServiceResponseRouteReason.None ||
                    samples.Count >= _maximumSamples)
                    continue;

                var sampleId = BuildId(SampleNamespace, eventId.Value, 0);
                samples.Add(new ServiceResponseSampleData(
                    sampleId,
                    eventId,
                    row.Kind,
                    unmet,
                    PhaseOffset(sampleId),
                    path));
            }

            events.Sort();
            _events = events.AsReadOnly();
            _samples = samples.AsReadOnly();
        }

        private static StableId BuildId(ulong nameSpace, ulong value, byte kind)
        {
            var hash = FnvOffset;
            AddUlong(ref hash, value);
            AddByte(ref hash, kind);
            return new StableId(nameSpace | (hash & 0x0000FFFFFFFFFFFFUL));
        }

        private static int PhaseOffset(StableId id)
        {
            var hash = FnvOffset;
            AddUlong(ref hash, id.Value);
            return (int)(hash % 1000UL);
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }
    }
}
