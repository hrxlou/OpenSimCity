using System;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Services
{
    public enum ServiceResponseRouteReason : byte
    {
        None,
        NonVehicleService,
        NoAssignedFacility,
        NoRoadEndpoint,
        RouteUnavailable
    }

    [Serializable]
    public readonly struct ServiceResponseEventData : IComparable<ServiceResponseEventData>
    {
        public ServiceResponseEventData(StableId id, StableId buildingId, ServiceKindView kind, StableId facilityId, CellCoord cell, int requested, int served, ServiceFailureReasonView failure, ServiceResponseRouteReason routeReason)
        {
            Id = id;
            BuildingId = buildingId;
            Kind = kind;
            FacilityId = facilityId;
            Cell = cell;
            Requested = requested;
            Served = served;
            FailureReason = failure;
            RouteReason = routeReason;
        }

        public StableId Id { get; }
        public StableId BuildingId { get; }
        public ServiceKindView Kind { get; }
        public StableId FacilityId { get; }
        public CellCoord Cell { get; }
        public int Requested { get; }
        public int Served { get; }
        public int Unmet => Math.Max(0, Requested - Served);
        public ServiceFailureReasonView FailureReason { get; }
        public ServiceResponseRouteReason RouteReason { get; }
        public bool HasRoute => RouteReason == ServiceResponseRouteReason.None;

        public int CompareTo(ServiceResponseEventData other)
        {
            var comparison = BuildingId.CompareTo(other.BuildingId);
            return comparison != 0 ? comparison : Kind.CompareTo(other.Kind);
        }
    }

    [Serializable]
    public readonly struct ServiceResponseSampleData
    {
        public ServiceResponseSampleData(StableId id, StableId eventId, ServiceKindView kind, int amount, int phaseOffsetPermille, VehicleSamplePathData path)
        {
            Id = id;
            EventId = eventId;
            Kind = kind;
            RepresentedDemand = amount;
            PhaseOffsetPermille = phaseOffsetPermille;
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public StableId Id { get; }
        public StableId EventId { get; }
        public ServiceKindView Kind { get; }
        public int RepresentedDemand { get; }
        public int PhaseOffsetPermille { get; }
        public VehicleSamplePathData Path { get; }
    }
}
