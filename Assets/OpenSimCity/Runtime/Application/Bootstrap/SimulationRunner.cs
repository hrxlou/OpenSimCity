using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Small application facade that wires the fixed clock to the ordered command
    /// queue. Gameplay and views submit commands to the queue; only handlers mutate
    /// simulation state.
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly ICommandDispatcher _dispatcher;
        private readonly IApplicationTickSystem[] _tickSystems;

        public SimulationRunner(
            SimulationClock clock,
            CommandQueue commandQueue,
            ICommandDispatcher dispatcher)
            : this(clock, commandQueue, dispatcher, Array.Empty<IApplicationTickSystem>())
        {
        }

        public SimulationRunner(
            SimulationClock clock,
            CommandQueue commandQueue,
            ICommandDispatcher dispatcher,
            IEnumerable<IApplicationTickSystem> tickSystems)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            CommandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            if (tickSystems == null)
                throw new ArgumentNullException(nameof(tickSystems));

            var orderedSystems = new List<IApplicationTickSystem>();
            foreach (var system in tickSystems)
            {
                if (system == null)
                    throw new ArgumentException("A tick system cannot be null.", nameof(tickSystems));
                orderedSystems.Add(system);
            }
            _tickSystems = orderedSystems.ToArray();

            if (Clock.CurrentTick != CommandQueue.NextTick)
            {
                throw new ArgumentException(
                    "The clock and command queue must start at the same simulation tick.");
            }
        }

        public SimulationClock Clock { get; }

        public CommandQueue CommandQueue { get; }

        public event Action<CommandTickResult> TickCompleted;

        public int Advance(double realDeltaSeconds)
        {
            return Clock.Advance(realDeltaSeconds, ProcessTick);
        }

        private void ProcessTick(SimulationTick tick)
        {
            var result = CommandQueue.ExecuteTick(tick, _dispatcher);
            for (var index = 0; index < _tickSystems.Length; index++)
                _tickSystems[index].ProcessTick(tick);
            if (TickCompleted != null)
                TickCompleted(result);
        }
    }

    /// <summary>
    /// Application-owned, registration-ordered system seam. SimulationRunner invokes
    /// these systems after commands for the same tick have completed.
    /// </summary>
    public interface IApplicationTickSystem
    {
        void ProcessTick(SimulationTick tick);
    }
}
