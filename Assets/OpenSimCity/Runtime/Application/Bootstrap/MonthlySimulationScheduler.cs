using System;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Population;

namespace OpenSimCity.Application.Bootstrap
{
    public sealed class MonthlySimulationScheduler : IApplicationTickSystem
    {
        public const int DefaultTicksPerMonth = 300;

        private readonly PopulationEmploymentMigrationSystem _populationSystem;
        private readonly int _ticksPerMonth;
        private SimulationDate _nextMonth;

        public MonthlySimulationScheduler(
            PopulationEmploymentMigrationSystem populationSystem,
            int ticksPerMonth = DefaultTicksPerMonth,
            SimulationDate firstMonth = default(SimulationDate))
        {
            _populationSystem = populationSystem ?? throw new ArgumentNullException(nameof(populationSystem));
            if (ticksPerMonth <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksPerMonth));

            _ticksPerMonth = ticksPerMonth;
            _nextMonth = firstMonth == default(SimulationDate)
                ? new SimulationDate(1, 1)
                : firstMonth;
        }

        public int TicksPerMonth => _ticksPerMonth;
        public int CompletedMonths { get; private set; }
        public SimulationDate NextMonth => _nextMonth;

        public event Action<SimulationDate> MonthCompleted;

        public void ProcessTick(SimulationTick tick)
        {
            if (tick.Value % _ticksPerMonth != _ticksPerMonth - 1L)
                return;

            var completedMonth = _nextMonth;
            _populationSystem.RunMonth(completedMonth);
            _nextMonth = _nextMonth.AddMonths(1);
            CompletedMonths = checked(CompletedMonths + 1);
            if (MonthCompleted != null)
                MonthCompleted(completedMonth);
        }
    }
}
