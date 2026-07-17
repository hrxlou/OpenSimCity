using System;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Employment;
using OpenSimCity.Simulation.Migration;
using OpenSimCity.Simulation.Population;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Versioned aggregate snapshot for inspectors and future HUD consumers. It
    /// contains no Unity or presentation objects and never exposes simulation arrays.
    /// </summary>
    public sealed class PopulationEmploymentReadModel
    {
        private readonly PopulationEmploymentMigrationSystem _system;
        private int _sourceVersion = -1;
        private PopulationEmploymentSnapshotDto _snapshot;

        public PopulationEmploymentReadModel(PopulationEmploymentMigrationSystem system)
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

        public PopulationEmploymentSnapshotDto Snapshot
        {
            get
            {
                RefreshIfNeeded();
                if (_snapshot == null)
                    throw new InvalidOperationException("No monthly population snapshot has been produced yet.");
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

            _snapshot = new PopulationEmploymentSnapshotDto
            {
                Year = _system.LastProcessedMonth.Year,
                Month = _system.LastProcessedMonth.Month,
                Population = _system.Population.ToDto(),
                Employment = _system.Employment.ToDto(),
                Migration = _system.Migration.ToDto()
            };
            return true;
        }
    }

    [Serializable]
    public sealed class PopulationEmploymentSnapshotDto
    {
        public int Year;
        public int Month;
        public PopulationStateDto Population;
        public EmploymentStateDto Employment;
        public MigrationStateDto Migration;

        public SimulationDate Date => new SimulationDate(Year, Month);
    }
}
