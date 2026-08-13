using System;
using System.Collections.Generic;
using OpenSimCity.Application.Transit;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Queries
{
    public enum TransitModeChoiceReasonView : byte
    {
        None,
        NoMatchedCommute,
        NoEnabledLine,
        BudgetDisabled,
        BudgetUnaffordable,
        OriginOutsideWalkingRange,
        DestinationOutsideWalkingRange,
        DirectionMismatch,
        BrokenLine,
        TooIndirect,
        CapacityExhausted
    }

    public static class TransitReasonProjection
    {
        public static TransitModeChoiceReasonView ToView(TransitModeChoiceReason reason)
        {
            return (TransitModeChoiceReasonView)(byte)reason;
        }
    }

    [Serializable]
    public readonly struct TransitStopReadModel
    {
        public TransitStopReadModel(TransitStopState source)
        {
            Id = source.Id.Value;
            RoadCell = source.RoadCell;
            Name = source.Name;
        }
        public StableId Id { get; }
        public CellCoord RoadCell { get; }
        public string Name { get; }
    }

    [Serializable]
    public sealed class TransitLineReadModel
    {
        public TransitLineReadModel(TransitLineState source)
        {
            Id = source.Id.Value;
            Name = source.Name;
            ServiceUnits = source.ServiceUnits;
            IsEnabled = source.IsEnabled;
            var ids = new StableId[source.StopIds.Count];
            for (var index = 0; index < ids.Length; index++)
                ids[index] = source.StopIds[index].Value;
            StopIds = Array.AsReadOnly(ids);
        }
        public StableId Id { get; }
        public string Name { get; }
        public int ServiceUnits { get; }
        public bool IsEnabled { get; }
        public IReadOnlyList<StableId> StopIds { get; }
    }

    [Serializable]
    public readonly struct TransitOdReadModel
    {
        public TransitOdReadModel(TransitModeChoiceRow source)
        {
            OriginId = source.Key.OriginId;
            DestinationId = source.Key.DestinationId;
            LineId = source.HasLine ? source.LineId.Value : StableId.Invalid;
            RequestedTrips = source.RequestedTrips;
            BusTrips = source.BusTrips;
            PrivateVehicleTrips = source.PrivateVehicleTrips;
            CarCostSeconds = source.CarGeneralizedCostSeconds;
            BusCostSeconds = source.BusGeneralizedCostSeconds;
            Reason = TransitReasonProjection.ToView(source.Reason);
        }
        public StableId OriginId { get; }
        public StableId DestinationId { get; }
        public StableId LineId { get; }
        public int RequestedTrips { get; }
        public int BusTrips { get; }
        public int PrivateVehicleTrips { get; }
        public int CarCostSeconds { get; }
        public int BusCostSeconds { get; }
        public TransitModeChoiceReasonView Reason { get; }
    }

    [Serializable]
    public sealed class TransitReadModelSnapshot
    {
        public int Version;
        public int ActiveBudgetPermille;
        public bool HasPendingBudget;
        public int PendingBudgetPermille;
        public int RequestedCommuteTrips;
        public int BusTrips;
        public int PrivateVehicleTrips;
        public int BaselineRoutedTrips;
        public int TransitAwareRoutedTrips;
        public int LastProjectedOperatingExpense;
        public int LastPaidOperatingExpense;
        public ulong NetworkChecksum;
        public ulong ModeChoiceChecksum;
        public ulong PrivateRoutingChecksum;
        public IReadOnlyList<TransitStopReadModel> Stops = Array.AsReadOnly(Array.Empty<TransitStopReadModel>());
        public IReadOnlyList<TransitLineReadModel> Lines = Array.AsReadOnly(Array.Empty<TransitLineReadModel>());
        public IReadOnlyList<TransitOdReadModel> OdRows = Array.AsReadOnly(Array.Empty<TransitOdReadModel>());
    }

    /// <summary>Presentation-safe projection of persistent transit and derived mode choice.</summary>
    public sealed class TransitReadModel
    {
        private readonly TransitRuntimeModule _module;
        private int _networkVersion = -1;
        private int _modeVersion = -1;
        private int _routingVersion = -1;

        public TransitReadModel(TransitRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Snapshot = new TransitReadModelSnapshot();
        }

        public TransitReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            var networkVersion = _module.Network.Version;
            var modeVersion = _module.ModeChoice.State.Version;
            var routingVersion = _module.PrivateVehicleRouting.State.Version;
            if (_networkVersion == networkVersion && _modeVersion == modeVersion && _routingVersion == routingVersion)
                return false;

            var stopsSource = _module.Network.Stops;
            var stops = new TransitStopReadModel[stopsSource.Count];
            for (var index = 0; index < stops.Length; index++)
                stops[index] = new TransitStopReadModel(stopsSource[index]);

            var linesSource = _module.Network.Lines;
            var lines = new TransitLineReadModel[linesSource.Count];
            for (var index = 0; index < lines.Length; index++)
                lines[index] = new TransitLineReadModel(linesSource[index]);

            var odSource = _module.ModeChoice.State.Rows;
            var od = new TransitOdReadModel[odSource.Count];
            for (var index = 0; index < od.Length; index++)
                od[index] = new TransitOdReadModel(odSource[index]);

            Snapshot = new TransitReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                ActiveBudgetPermille = _module.Network.ActiveBudgetPermille,
                HasPendingBudget = _module.Network.HasPendingBudget,
                PendingBudgetPermille = _module.Network.PendingBudgetPermille,
                RequestedCommuteTrips = _module.ModeChoice.State.RequestedCommuteTrips,
                BusTrips = _module.ModeChoice.State.BusTrips,
                PrivateVehicleTrips = _module.ModeChoice.State.PrivateVehicleTrips,
                BaselineRoutedTrips = _module.Runtime.TrafficRoutingSystem.State.RoutedAmount,
                TransitAwareRoutedTrips = _module.PrivateVehicleRouting.State.RoutedAmount,
                LastProjectedOperatingExpense = _module.Network.LastProjectedOperatingExpense,
                LastPaidOperatingExpense = _module.Network.LastPaidOperatingExpense,
                NetworkChecksum = _module.Network.ComputeChecksum(),
                ModeChoiceChecksum = _module.ModeChoice.State.ComputeChecksum(),
                PrivateRoutingChecksum = _module.PrivateVehicleRouting.State.ComputeChecksum(),
                Stops = Array.AsReadOnly(stops),
                Lines = Array.AsReadOnly(lines),
                OdRows = Array.AsReadOnly(od)
            };
            _networkVersion = networkVersion;
            _modeVersion = modeVersion;
            _routingVersion = routingVersion;
            return true;
        }
    }
}
