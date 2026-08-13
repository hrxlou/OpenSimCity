using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Population;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Owns the monthly run order. Registered pre-core systems run first in stable
    /// registration order, followed by the immutable core systems supplied at
    /// construction. Simulation systems never schedule themselves.
    /// </summary>
    public sealed class MonthlySimulationScheduler : IApplicationTickSystem
    {
        public const int DefaultTicksPerMonth = 300;

        private readonly IMonthlySimulationSystem[] _monthlySystems;
        private readonly List<IMonthlySimulationSystem> _preCoreSystems =
            new List<IMonthlySimulationSystem>();
        private readonly int _ticksPerMonth;
        private SimulationDate _nextMonth;
        private SimulationTick _nextRunTick;

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
            _nextRunTick = new SimulationTick(ticksPerMonth - 1L);
        }

        public int TicksPerMonth => _ticksPerMonth;
        public int CompletedMonths { get; private set; }
        public SimulationDate NextMonth => _nextMonth;
        public SimulationTick NextRunTick => _nextRunTick;
        public IReadOnlyList<IMonthlySimulationSystem> PreCoreSystems => _preCoreSystems.AsReadOnly();

        public event Action<SimulationDate> MonthCompleted;

        /// <summary>
        /// Registers a monthly capability before Population and the core finance
        /// composition. Duplicate references are ignored. Registration order is the
        /// deterministic tie-breaker between capability systems.
        /// </summary>
        public void RegisterPreCoreSystem(IMonthlySimulationSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            for (var index = 0; index < _preCoreSystems.Count; index++)
            {
                if (ReferenceEquals(_preCoreSystems[index], system))
                    return;
            }
            _preCoreSystems.Add(system);
        }

        public void ProcessTick(SimulationTick tick)
        {
            if (tick < _nextRunTick)
                return;
            if (tick > _nextRunTick)
            {
                throw new InvalidOperationException(
                    $"Monthly scheduler missed its next run tick {_nextRunTick}; received {tick}.");
            }

            var completedMonth = _nextMonth;
            for (var index = 0; index < _preCoreSystems.Count; index++)
                _preCoreSystems[index].RunMonth(completedMonth);
            for (var index = 0; index < _monthlySystems.Length; index++)
                _monthlySystems[index].RunMonth(completedMonth);
            _nextMonth = _nextMonth.AddMonths(1);
            CompletedMonths = checked(CompletedMonths + 1);
            _nextRunTick = AddTicks(tick, _ticksPerMonth);
            MonthCompleted?.Invoke(completedMonth);
        }

        internal void RestoreState(
            int completedMonths,
            SimulationDate nextMonth,
            SimulationTick nextRunTick)
        {
            if (completedMonths < 0)
                throw new ArgumentOutOfRangeException(nameof(completedMonths));
            if (nextRunTick.Value % _ticksPerMonth != _ticksPerMonth - 1L)
            {
                throw new ArgumentException(
                    "The saved scheduler tick does not align with the monthly cadence.",
                    nameof(nextRunTick));
            }

            CompletedMonths = completedMonths;
            _nextMonth = nextMonth;
            _nextRunTick = nextRunTick;
        }

        private static SimulationTick AddTicks(SimulationTick tick, int count)
        {
            if (tick.Value > long.MaxValue - count)
                throw new OverflowException("The monthly scheduler cannot advance past Int64.MaxValue.");
            return new SimulationTick(tick.Value + count);
        }
    }
}
