using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Versioned application-facing projection for route, bottleneck and overlay
    /// consumers. Simulation rows are immutable, so observation never mutates or
    /// reorders routing state. Missing derived state after restore is rebuilt from
    /// current road and trip-demand sources on first access.
    /// </summary>
    public sealed class TrafficRoutingReadModel
    {
        private readonly TrafficRoutingSystem _system;
        private int _sourceVersion = -1;

        public TrafficRoutingReadModel(TrafficRoutingSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            RefreshIfNeeded();
        }

        public int Version { get; private set; }

        public bool HasProcessedMonth
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.HasProcessedMonth;
            }
        }

        public SimulationDate LastProcessedMonth
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.LastProcessedMonth;
            }
        }

        public int InputMatchedAmount
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.InputMatchedAmount;
            }
        }

        public int RoutedAmount
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.RoutedAmount;
            }
        }

        public int UnroutedAmount
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.UnroutedAmount;
            }
        }

        public IReadOnlyList<TrafficRouteAssignment> Assignments
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.Assignments;
            }
        }

        public IReadOnlyList<TrafficUnroutedDemand> Unrouted
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.Unrouted;
            }
        }

        public IReadOnlyList<TrafficEdgeState> Edges
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.Edges;
            }
        }

        public ulong Checksum
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.ComputeChecksum();
            }
        }

        public bool RefreshIfNeeded()
        {
            _system.RefreshIfNeeded();
            var sourceVersion = _system.State.Version;
            if (_sourceVersion == sourceVersion)
                return false;

            _sourceVersion = sourceVersion;
            Version = checked(Version + 1);
            return true;
        }
    }
}
