using System;
using System.Collections.Generic;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Application.Commands
{
    public enum CommandEnqueueStatus
    {
        Accepted,
        NullCommand,
        DuplicateStamp,
        PastTick
    }

    public readonly struct CommandEnqueueResult
    {
        private CommandEnqueueResult(
            CommandEnqueueStatus status,
            CommandStamp stamp,
            string message)
        {
            Status = status;
            Stamp = stamp;
            Message = message ?? string.Empty;
        }

        public CommandEnqueueStatus Status { get; }

        public CommandStamp Stamp { get; }

        public string Message { get; }

        public bool Accepted => Status == CommandEnqueueStatus.Accepted;

        public static CommandEnqueueResult Accept(CommandStamp stamp) =>
            new CommandEnqueueResult(CommandEnqueueStatus.Accepted, stamp, string.Empty);

        public static CommandEnqueueResult Reject(
            CommandEnqueueStatus status,
            CommandStamp stamp,
            string message) =>
            new CommandEnqueueResult(status, stamp, message);
    }

    public readonly struct CommandTickResult
    {
        private readonly IReadOnlyList<CommandExecutionResult> _results;

        internal CommandTickResult(
            SimulationTick tick,
            IReadOnlyList<CommandExecutionResult> results)
        {
            Tick = tick;
            _results = results ?? throw new ArgumentNullException(nameof(results));
        }

        public SimulationTick Tick { get; }

        public IReadOnlyList<CommandExecutionResult> Results => _results;

        public int CommandCount => _results.Count;

        public int AppliedCount => Count(CommandExecutionStatus.Applied);

        public int RejectedCount => Count(CommandExecutionStatus.Rejected);

        public int UnsupportedCount => Count(CommandExecutionStatus.Unsupported);

        public int FailedCount => Count(CommandExecutionStatus.Failed);

        public bool AllApplied => RejectedCount == 0 && UnsupportedCount == 0 && FailedCount == 0;

        private int Count(CommandExecutionStatus status)
        {
            var count = 0;
            for (var index = 0; index < _results.Count; index++)
            {
                if (_results[index].Status == status)
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Stores commands by deterministic stamp. Duplicate stamps are rejected so
    /// two commands can never silently compete for the same ordered position.
    /// </summary>
    public sealed class CommandQueue
    {
        private readonly List<CommandEnvelope> _pending = new List<CommandEnvelope>();
        private readonly HashSet<CommandStamp> _pendingStamps = new HashSet<CommandStamp>();
        private SimulationTick _nextTick;

        public CommandQueue()
            : this(SimulationTick.Zero)
        {
        }

        public CommandQueue(SimulationTick firstTick)
        {
            _nextTick = firstTick;
        }

        /// <summary>
        /// The next tick that must be processed. Tick zero is the first tick by default.
        /// </summary>
        public SimulationTick NextTick => _nextTick;

        public int Count => _pending.Count;

        public CommandEnqueueResult Enqueue(CommandStamp stamp, ICityCommand command)
        {
            if (command == null)
            {
                return CommandEnqueueResult.Reject(
                    CommandEnqueueStatus.NullCommand,
                    stamp,
                    "A command cannot be null.");
            }

            if (stamp.Tick < _nextTick)
            {
                return CommandEnqueueResult.Reject(
                    CommandEnqueueStatus.PastTick,
                    stamp,
                    "A command for an already processed tick cannot be queued.");
            }

            if (!_pendingStamps.Add(stamp))
            {
                return CommandEnqueueResult.Reject(
                    CommandEnqueueStatus.DuplicateStamp,
                    stamp,
                    "A command with the same tick and sequence is already queued.");
            }

            _pending.Add(new CommandEnvelope(stamp, command));
            _pending.Sort(CompareEnvelopes);
            return CommandEnqueueResult.Accept(stamp);
        }

        public CommandEnqueueResult Enqueue(CommandEnvelope command)
        {
            return Enqueue(command.Stamp, command.Command);
        }

        public CommandEnqueueResult Enqueue<TCommand>(CommandEnvelope<TCommand> command)
            where TCommand : ICityCommand
        {
            return Enqueue(command.Stamp, command.Command);
        }

        public bool TryEnqueue(
            CommandStamp stamp,
            ICityCommand command,
            out CommandEnqueueResult result)
        {
            result = Enqueue(stamp, command);
            return result.Accepted;
        }

        public CommandTickResult ExecuteTick(SimulationTick tick, ICommandDispatcher dispatcher)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));
            if (tick != _nextTick)
            {
                throw new InvalidOperationException(
                    $"Command ticks must be processed in order. Expected {_nextTick}, received {tick}.");
            }
            if (!_nextTick.CanAdvance)
                throw new OverflowException("The command queue cannot advance past the maximum simulation tick.");

            var results = new List<CommandExecutionResult>();
            var consumed = 0;
            while (consumed < _pending.Count)
            {
                var command = _pending[consumed];
                if (command.Stamp.Tick > tick)
                    break;
                if (command.Stamp.Tick < tick)
                {
                    throw new InvalidOperationException(
                        $"Command queue contains an unprocessed command for tick {command.Stamp.Tick}.");
                }

                results.Add(dispatcher.Dispatch(command));
                consumed++;
            }

            if (consumed > 0)
            {
                for (var index = 0; index < consumed; index++)
                    _pendingStamps.Remove(_pending[index].Stamp);

                _pending.RemoveRange(0, consumed);
            }

            _nextTick = _nextTick.Next();
            return new CommandTickResult(tick, results.AsReadOnly());
        }

        private static int CompareEnvelopes(CommandEnvelope left, CommandEnvelope right)
        {
            return left.Stamp.CompareTo(right.Stamp);
        }
    }
}
