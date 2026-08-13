using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Specialization;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Specialization
{
    /// <summary>
    /// Builds the P7-04 environment from current authoritative city queries.
    /// It never changes specialization or base-city state while being observed.
    /// </summary>
    public sealed class CitySpecializationEnvironment : ISpecializationEnvironmentProvider
    {
        private readonly CityRuntime _runtime;
        private readonly PublicServiceRuntimeModule _services;
        private readonly SpatialFieldRuntimeModule _spatial;

        public CitySpecializationEnvironment(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _services = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            _spatial = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
        }

        public SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month)
        {
            _runtime.Power.RefreshIfNeeded();
            _services.ReadModel.RefreshIfNeeded();
            _spatial.RefreshIfNeeded();

            var industrialIds = new HashSet<StableId>();
            var buildingCount = 0;
            var jobs = 0;
            var poweredJobs = 0;
            long pollution = 0;
            var buildings = _runtime.Buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                var definition = _runtime.BuildingDefinitions.Get(building.DefinitionId);
                if (definition.ZoneType != ZoneType.IndustrialLow) continue;
                industrialIds.Add(building.Id);
                buildingCount++;
                jobs = checked(jobs + building.FilledJobs);
                if (_runtime.Power.GetBuilding(building.Id).IsPowered)
                    poweredJobs = checked(poweredJobs + building.FilledJobs);
                pollution += _spatial.Fields.Get(building.Anchor).Pollution;
            }

            var marketConnected = HasExternalRoad();
            var demand = _runtime.TripDemandSystem.State;
            var goodsTransport = !marketConnected
                ? 0
                : demand.GoodsRequested <= 0
                    ? 1000
                    : demand.GoodsMatched * 1000 / demand.GoodsRequested;

            var coverageTotal = 0;
            var coverageCount = 0;
            var coverage = _services.ReadModel.Snapshot.Coverage;
            for (var index = 0; index < coverage.Count; index++)
            {
                if (!industrialIds.Contains(coverage[index].BuildingId)) continue;
                coverageTotal = checked(coverageTotal + coverage[index].CoveragePermille);
                coverageCount++;
            }

            return new SpecializationEnvironmentSnapshot(
                buildingCount,
                jobs,
                poweredJobs,
                marketConnected,
                goodsTransport,
                buildingCount == 0 ? 0 : Math.Max(0, Math.Min(1000, (int)(pollution / buildingCount))),
                coverageCount == 0 ? 0 : coverageTotal / coverageCount);
        }

        private bool HasExternalRoad()
        {
            _runtime.RoadGraph.RefreshIfNeeded();
            for (var index = 0; index < _runtime.ExternalConnections.Count; index++)
            {
                var connection = _runtime.ExternalConnections[index];
                if (!connection.IsEnabled || connection.Kind != ExternalConnectionKind.Road) continue;
                if (_runtime.RoadGraph.TryGet(connection.Cell, out var result) && result.HasRoad)
                    return true;
            }
            return false;
        }
    }
}
