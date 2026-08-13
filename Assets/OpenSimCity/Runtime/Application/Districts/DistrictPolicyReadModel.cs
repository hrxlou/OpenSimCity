using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.Transit;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictPolicyViewData
    {
        public DistrictPolicyKind Kind { get; set; }
        public bool Enabled { get; set; }
        public bool Effective { get; set; }
        public bool HasPending { get; set; }
        public bool PendingEnabled { get; set; }
        public int PendingYear { get; set; }
        public int PendingMonth { get; set; }
        public int MonthlyCost { get; set; }
        public int InputPermille { get; set; }
        public int LastProjectedCost { get; set; }
        public int LastPaidCost { get; set; }
        public DistrictPolicyOperatingReason Reason { get; set; }
    }

    public sealed class DistrictStatsViewData
    {
        public StableId DistrictId { get; set; }
        public string Name { get; set; }
        public int Population { get; set; }
        public int Jobs { get; set; }
        public int RoutedTrips { get; set; }
        public int TrafficNoisePermille { get; set; }
        public int BusTrips { get; set; }
        public int PrivateVehicleTrips { get; set; }
        public int ServiceCoveragePermille { get; set; }
        public int Pollution { get; set; }
        public int PolicyCost { get; set; }
        public IReadOnlyList<DistrictPolicyViewData> Policies { get; set; }
    }

    public sealed class DistrictPolicyReadModelSnapshot
    {
        public int Version { get; set; }
        public bool HasProcessedMonth { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public IReadOnlyList<DistrictStatsViewData> Districts { get; set; } = Array.AsReadOnly(Array.Empty<DistrictStatsViewData>());
    }

    public sealed class DistrictPolicyReadModel
    {
        private readonly DistrictPolicyRuntimeModule _module;
        private readonly PublicServiceRuntimeModule _services;
        private readonly SpatialFieldRuntimeModule _spatial;
        private int _policyVersion = -1;
        private int _districtVersion = -1;
        private int _buildingVersion = -1;
        private int _trafficVersion = -1;
        private int _transitVersion = -1;
        private int _spatialVersion = -1;

        public DistrictPolicyReadModel(DistrictPolicyRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _services = PublicServiceRuntimeRegistry.GetOrCreate(module.Runtime);
            _spatial = SpatialFieldRuntimeRegistry.GetOrCreate(module.Runtime);
            Snapshot = new DistrictPolicyReadModelSnapshot();
        }

        public DistrictPolicyReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            _module.PruneMissingDistricts();
            _module.Districts.ReadModel.RefreshIfNeeded();
            _services.ReadModel.RefreshIfNeeded();
            _spatial.RefreshIfNeeded();
            var trafficVersion = _module.Runtime.TrafficRoutingSystem.State.Version;
            var transitVersion = TransitRuntimeRegistry.TryGet(_module.Runtime, out var transit) ? transit.ModeChoice.State.Version : -1;
            if (_policyVersion == _module.Policies.Version && _districtVersion == _module.Districts.Districts.Version &&
                _buildingVersion == _module.Runtime.Buildings.Version && _trafficVersion == trafficVersion &&
                _transitVersion == transitVersion && _spatialVersion == _spatial.Fields.FieldVersion) return false;

            var rows = new List<DistrictStatsViewData>();
            var districts = _module.Districts.ReadModel.Snapshot.Districts;
            for (var index = 0; index < districts.Count; index++) rows.Add(BuildDistrict(districts[index], transit));
            Snapshot = new DistrictPolicyReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                HasProcessedMonth = _module.HasProcessedMonth,
                Year = _module.HasProcessedMonth ? _module.LastProcessedMonth.Year : 0,
                Month = _module.HasProcessedMonth ? _module.LastProcessedMonth.Month : 0,
                Districts = rows.AsReadOnly()
            };
            _policyVersion = _module.Policies.Version;
            _districtVersion = _module.Districts.Districts.Version;
            _buildingVersion = _module.Runtime.Buildings.Version;
            _trafficVersion = trafficVersion;
            _transitVersion = transitVersion;
            _spatialVersion = _spatial.Fields.FieldVersion;
            return true;
        }

        private DistrictStatsViewData BuildDistrict(DistrictViewData district, TransitRuntimeModule transit)
        {
            var cells = new HashSet<CellCoord>(district.Cells);
            var buildingIds = new HashSet<StableId>();
            var population = 0; var jobs = 0; long pollution = 0;
            for (var cellIndex = 0; cellIndex < district.Cells.Count; cellIndex++) pollution += _spatial.Fields.Get(district.Cells[cellIndex]).Pollution;
            var buildings = _module.Runtime.Buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (!cells.Contains(building.Anchor)) continue;
                buildingIds.Add(building.Id); population = checked(population + building.Occupancy); jobs = checked(jobs + building.FilledJobs);
            }

            var routed = 0;
            var assignments = _module.Runtime.TrafficRoutingSystem.State.Assignments;
            for (var index = 0; index < assignments.Count; index++)
            {
                var touches = false;
                for (var cellIndex = 0; cellIndex < assignments[index].Route.Cells.Count && !touches; cellIndex++)
                    touches = cells.Contains(assignments[index].Route.Cells[cellIndex]);
                if (touches) routed = checked(routed + assignments[index].Amount);
            }

            long noiseTotal = 0; var noiseEdges = 0;
            var edges = _module.Runtime.TrafficRoutingSystem.State.Edges;
            for (var index = 0; index < edges.Count; index++)
            {
                if (!cells.Contains(edges[index].Edge.A) && !cells.Contains(edges[index].Edge.B)) continue;
                noiseTotal += Math.Min(2000, edges[index].VolumeCapacityPermille); noiseEdges++;
            }

            var bus = 0; var privateVehicles = 0;
            if (transit != null)
            {
                var modeRows = transit.ModeChoice.State.Rows;
                for (var index = 0; index < modeRows.Count; index++)
                {
                    var mode = modeRows[index];
                    if (!buildingIds.Contains(mode.Key.OriginId) && !buildingIds.Contains(mode.Key.DestinationId)) continue;
                    bus = checked(bus + mode.BusTrips); privateVehicles = checked(privateVehicles + mode.PrivateVehicleTrips);
                }
            }

            var coverageTotal = 0; var coverageCount = 0;
            var coverage = _services.ReadModel.Snapshot.Coverage;
            for (var index = 0; index < coverage.Count; index++)
            {
                if (!buildingIds.Contains(coverage[index].BuildingId)) continue;
                coverageTotal = checked(coverageTotal + coverage[index].CoveragePermille); coverageCount++;
            }

            var policyRows = new List<DistrictPolicyViewData>(); var policyCost = 0;
            for (var kindIndex = 0; kindIndex < 2; kindIndex++)
            {
                var kind = (DistrictPolicyKind)kindIndex;
                _module.Policies.TryGet(district.DistrictId, kind, out var state);
                if (state != null) policyCost = checked(policyCost + state.LastPaidCost);
                policyRows.Add(new DistrictPolicyViewData
                {
                    Kind = kind,
                    Enabled = state != null && state.IsEnabled,
                    Effective = state != null && state.IsFundedAndEffective,
                    HasPending = state != null && state.HasPending,
                    PendingEnabled = state != null && state.PendingEnabled,
                    PendingYear = state != null && state.HasPending ? state.PendingEffectiveMonth.Year : 0,
                    PendingMonth = state != null && state.HasPending ? state.PendingEffectiveMonth.Month : 0,
                    MonthlyCost = _module.Parameters.MonthlyCost(kind),
                    InputPermille = kind == DistrictPolicyKind.HeavyVehicleRestriction ? _module.Parameters.HeavyVehiclePenaltyPermille : _module.Parameters.TransitBusCostPermille,
                    LastProjectedCost = state?.LastProjectedCost ?? 0,
                    LastPaidCost = state?.LastPaidCost ?? 0,
                    Reason = state?.LastReason ?? DistrictPolicyOperatingReason.Disabled
                });
            }

            return new DistrictStatsViewData
            {
                DistrictId = district.DistrictId,
                Name = district.Name,
                Population = population,
                Jobs = jobs,
                RoutedTrips = routed,
                TrafficNoisePermille = noiseEdges == 0 ? 0 : (int)(noiseTotal / noiseEdges),
                BusTrips = bus,
                PrivateVehicleTrips = privateVehicles,
                ServiceCoveragePermille = coverageCount == 0 ? 0 : coverageTotal / coverageCount,
                Pollution = district.Cells.Count == 0 ? 0 : (int)Math.Min(int.MaxValue, pollution / district.Cells.Count),
                PolicyCost = policyCost,
                Policies = policyRows.AsReadOnly()
            };
        }
    }
}
