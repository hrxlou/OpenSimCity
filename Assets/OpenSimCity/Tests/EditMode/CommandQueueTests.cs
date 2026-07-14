using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CommandQueueTests
    {
        [Test]
        public void CommandsAreSortedByTickThenSequenceBeforeExecution()
        {
            var handler = new RecordingCommandHandler();
            var dispatcher = new CommandDispatcher(new[] { handler });
            var queue = new CommandQueue();

            Assert.That(queue.Enqueue(Stamp(1, 4), new TestCommand(14)).Accepted, Is.True);
            Assert.That(queue.Enqueue(Stamp(0, 2), new TestCommand(2)).Accepted, Is.True);
            Assert.That(queue.Enqueue(Stamp(0, 0), new TestCommand(0)).Accepted, Is.True);
            Assert.That(queue.Enqueue(Stamp(0, 1), new TestCommand(1)).Accepted, Is.True);

            var firstTick = queue.ExecuteTick(SimulationTick.Zero, dispatcher);
            Assert.That(firstTick.Results, Has.Count.EqualTo(3));
            Assert.That(handler.ExecutedIds, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(queue.NextTick, Is.EqualTo(new SimulationTick(1)));

            var secondTick = queue.ExecuteTick(new SimulationTick(1), dispatcher);
            Assert.That(secondTick.AppliedCount, Is.EqualTo(1));
            Assert.That(handler.ExecutedIds, Is.EqualTo(new[] { 0, 1, 2, 14 }));
        }

        [Test]
        public void DuplicateAndPastStampsAreRejected()
        {
            var queue = new CommandQueue();
            var stamp = Stamp(0, 7);

            Assert.That(queue.Enqueue(stamp, new TestCommand(1)).Status, Is.EqualTo(CommandEnqueueStatus.Accepted));
            Assert.That(queue.Enqueue(stamp, new TestCommand(2)).Status, Is.EqualTo(CommandEnqueueStatus.DuplicateStamp));

            var dispatcher = new CommandDispatcher(new[] { new RecordingCommandHandler() });
            queue.ExecuteTick(SimulationTick.Zero, dispatcher);

            Assert.That(queue.Enqueue(stamp, new TestCommand(3)).Status, Is.EqualTo(CommandEnqueueStatus.PastTick));
            Assert.That(queue.Enqueue(stamp, null).Status, Is.EqualTo(CommandEnqueueStatus.NullCommand));
        }

        [Test]
        public void HandlerValidationBecomesARejectedExecutionResult()
        {
            var handler = new RecordingCommandHandler();
            var queue = new CommandQueue();
            var dispatcher = new CommandDispatcher(new[] { handler });

            queue.Enqueue(Stamp(0, 0), new TestCommand(-1));
            var result = queue.ExecuteTick(SimulationTick.Zero, dispatcher);

            Assert.That(result.CommandCount, Is.EqualTo(1));
            Assert.That(result.RejectedCount, Is.EqualTo(1));
            Assert.That(result.AllApplied, Is.False);
            Assert.That(handler.ExecutedIds, Is.Empty);
            Assert.That(result.Results[0].Stamp, Is.EqualTo(Stamp(0, 0)));
        }

        [Test]
        public void RunnerAppliesQueuedCommandsOnlyOnConsumedTicks()
        {
            var handler = new RecordingCommandHandler();
            var queue = new CommandQueue();
            var dispatcher = new CommandDispatcher(new[] { handler });
            var runner = new SimulationRunner(new SimulationClock(0.1d, 4), queue, dispatcher);

            queue.Enqueue(Stamp(0, 0), new TestCommand(10));
            queue.Enqueue(Stamp(1, 0), new TestCommand(11));

            Assert.That(runner.Advance(0.09d), Is.EqualTo(0));
            Assert.That(handler.ExecutedIds, Is.Empty);
            Assert.That(runner.Advance(0.11d), Is.EqualTo(2));
            Assert.That(handler.ExecutedIds, Is.EqualTo(new[] { 10, 11 }));
        }

        [Test]
        public void UnsupportedCommandsReturnAStableResultWithoutMutatingQueueOrdering()
        {
            var queue = new CommandQueue();
            var dispatcher = new CommandDispatcher(new[] { new RecordingCommandHandler() });
            queue.Enqueue(Stamp(0, 0), new UnsupportedCommand());

            var result = queue.ExecuteTick(SimulationTick.Zero, dispatcher);

            Assert.That(result.UnsupportedCount, Is.EqualTo(1));
            Assert.That(result.Results[0].Status, Is.EqualTo(CommandExecutionStatus.Unsupported));
            Assert.That(queue.Count, Is.EqualTo(0));
        }

        private static CommandStamp Stamp(long tick, ulong sequence)
        {
            return new CommandStamp(new SimulationTick(tick), new CommandSequence(sequence));
        }

        private sealed class TestCommand : ICityCommand
        {
            public TestCommand(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }

        private sealed class UnsupportedCommand : ICityCommand
        {
        }

        private sealed class RecordingCommandHandler : ICityCommandHandler
        {
            public readonly List<int> ExecutedIds = new List<int>();

            public bool CanHandle(ICityCommand command)
            {
                return command is TestCommand;
            }

            public CommandValidationResult Validate(CommandEnvelope command)
            {
                var typedCommand = (TestCommand)command.Command;
                return typedCommand.Id < 0
                    ? CommandValidationResult.Invalid("Test command id cannot be negative.")
                    : CommandValidationResult.Valid;
            }

            public CommandExecutionResult Execute(CommandEnvelope command)
            {
                var typedCommand = (TestCommand)command.Command;
                ExecutedIds.Add(typedCommand.Id);
                return CommandExecutionResult.Applied();
            }
        }
    }
}
