using System;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Versioned aggregate snapshot of the market and RCI demand for inspectors and
    /// future HUD consumers. It contains no Unity or presentation objects and never
    /// exposes simulation arrays; the serialized snapshot is the monthly checksum
    /// used to compare cross-platform runs.
    /// </summary>
    public sealed class UrbanEconomyReadModel
    {
        private readonly UrbanEconomySystem _system;
        private int _sourceVersion = -1;
        private UrbanEconomySnapshotDto _snapshot;

        public UrbanEconomyReadModel(UrbanEconomySystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public int Version
        {
            get
            {
                RefreshIfNeeded();
                return _sourceVersion;
            }
        }

        public bool HasSnapshot
        {
            get
            {
                RefreshIfNeeded();
                return _snapshot != null;
            }
        }

        public UrbanEconomySnapshotDto Snapshot
        {
            get
            {
                RefreshIfNeeded();
                if (_snapshot == null)
                    throw new InvalidOperationException("No monthly economy snapshot has been produced yet.");
                return _snapshot;
            }
        }

        public bool RefreshIfNeeded()
        {
            if (_sourceVersion == _system.Version)
                return false;

            _sourceVersion = _system.Version;
            if (!_system.HasProcessedMonth)
            {
                _snapshot = null;
                return true;
            }

            _snapshot = new UrbanEconomySnapshotDto
            {
                Year = _system.LastProcessedMonth.Year,
                Month = _system.LastProcessedMonth.Month,
                Market = _system.Market.ToDto(),
                Rci = _system.Rci.ToDto()
            };
            return true;
        }
    }

    [Serializable]
    public sealed class UrbanEconomySnapshotDto
    {
        public int Year;
        public int Month;
        public MarketStateDto Market;
        public RciDemandStateDto Rci;

        public SimulationDate Date => new SimulationDate(Year, Month);
    }
}
