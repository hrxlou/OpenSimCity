using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Population;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Owns the monthly run order. On each month boundary it drives its registered
    /// monthly systems in registration order for the same completed month, so later
    /// systems (market/RCI) observe the state earlier systems (population/employment)
    /// already produced. Simulation systems never schedule themselves.
    /// </summary>
    public sealed class MonthlySimulationScheduler : IApplicationTickSystem
    {
        public const int DefaultTicksPerMonth = 300;

        private readonly IMonthlySimulationSystem[] _monthlySystems;
        private readonly int _ticksPerMonth;
        private SimulationDate _nextMonth;

        public MonthlySimulationScheduler(
            PopulationEmploymentMigrationSystem populationSystem,
            int ticksPerMonth = DefaultTicksPerMonth,
            SimulationDate firstMonth = default(SimulationDate))
            : this(
                new IMonthlySimulationSystem[] { populationSystem ?? throw new ArgumentNullException(nameof(populationSystem)) },
                ticksPerMonth,
                firstMonth)
        {
        }

        public MonthlySimulationScheduler(
            IReadOnlyList<IMonthlySimulationSystem> monthlySystems,
            int ticksPerMonth = DefaultTicksPerMonth,
            SimulationDate firstMonth = default(SimulationDate))
        {
            if (monthlySystems == null)
                throw new ArgumentNullException(nameof(monthlySystems));
            if (ticksPerMonth <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksPerMonth));

            var ordered = new IMonthlySimulationSystem[monthlySystems.Count];
            for (var index = 0; index < monthlySystems.Count; index++)
            {
                ordered[index] = monthlySystems[index]
                    ?? throw new ArgumentException("A monthly system cannot be null.", nameof(monthlySystems));
            }

            _monthlySystems = ordered;
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
            for (var index = 0; index < _monthlySystems.Length; index++)
                _monthlySystems[index].RunMonth(completedMonth);
            _nextMonth = _nextMonth.AddMonths(1);
            CompletedMonths = checked(CompletedMonths + 1);
            if (MonthCompleted != null)
                MonthCompleted(completedMonth);
        }
    }
}
