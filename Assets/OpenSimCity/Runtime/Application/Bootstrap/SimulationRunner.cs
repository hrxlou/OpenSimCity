using System;
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

        public SimulationRunner(
            SimulationClock clock,
            CommandQueue commandQueue,
            ICommandDispatcher dispatcher)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            CommandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

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
            if (TickCompleted != null)
                TickCompleted(result);
        }
    }
}
