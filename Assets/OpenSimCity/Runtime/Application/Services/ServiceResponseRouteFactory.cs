using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Services
{
    internal sealed class ServiceResponseRouteFactory
    {
        private readonly PublicServiceRuntimeModule _module;
        private readonly DeterministicRoutePlanner _planner;

        public ServiceResponseRouteFactory(PublicServiceRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _planner = new DeterministicRoutePlanner(module.Runtime.Roads, module.Runtime.RoadGraph, module.Runtime.RoadDefinitions);
        }

        public ServiceResponseRouteReason TryBuild(ServiceCoverageReadModel row, ServiceFacilityReadModel facility, out VehicleSamplePathData path)
        {
            path = null;
            if (row.Kind == ServiceKindView.School) return ServiceResponseRouteReason.NonVehicleService;
            if (!facility.FacilityId.IsValid) return ServiceResponseRouteReason.NoAssignedFacility;
            if (!_module.Runtime.Buildings.TryGet(row.BuildingId, out var building) || !TryFacilityRoad(facility.Cell, out var origin) || !TryBuildingRoad(building.ParcelId, out var destination)) return ServiceResponseRouteReason.NoRoadEndpoint;
            var routes = _planner.GetRoutes(origin, destination);
            if (!routes.IsReachable || routes.Routes.Count == 0) return ServiceResponseRouteReason.RouteUnavailable;
            path = ToPath(routes.Routes[0]);
            return ServiceResponseRouteReason.None;
        }

        private bool TryFacilityRoad(CellCoord cell, out CellCoord road)
        {
            var candidates = new[] { new CellCoord(cell.X, cell.Y - 1), new CellCoord(cell.X - 1, cell.Y), new CellCoord(cell.X + 1, cell.Y), new CellCoord(cell.X, cell.Y + 1) };
            Array.Sort(candidates);
            for (var index = 0; index < candidates.Length; index++)
            {
                if (_module.Runtime.World.IsInBounds(candidates[index]) && _module.Runtime.Roads.HasRoad(candidates[index])) { road = candidates[index]; return true; }
            }
            road = default; return false;
        }

        private bool TryBuildingRoad(StableId parcelId, out CellCoord road)
        {
            _module.Runtime.Parcels.RefreshIfNeeded();
            if (!_module.Runtime.Parcels.TryGet(parcelId, out var parcel)) { road = default; return false; }
            var candidates = new List<CellCoord>();
            for (var index = 0; index < parcel.Frontages.Count; index++) if (parcel.Frontages[index].IsReachable) candidates.Add(parcel.Frontages[index].RoadCell);
            if (candidates.Count == 0) { road = default; return false; }
            candidates.Sort(); road = candidates[0]; return true;
        }

        private static VehicleSamplePathData ToPath(TrafficRoute route)
        {
            if (route.EdgeCount == 0) return new VehicleSamplePathData(route.Cells, Array.Empty<int>());
            var total = Math.Max(route.EdgeCount, route.FreeFlowTravelSeconds);
            var durations = new int[route.EdgeCount];
            var quotient = total / route.EdgeCount; var remainder = total % route.EdgeCount;
            for (var index = 0; index < durations.Length; index++) durations[index] = quotient + (index < remainder ? 1 : 0);
            return new VehicleSamplePathData(route.Cells, durations);
        }
    }
}
