using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadCommandTests
    {
        [Test]
        public void StraightDragChoosesDominantAxisAndBuildsContiguousCells()
        {
            var horizontal = StraightRoadPathCalculator.Calculate(
                new CellCoord(1, 2),
                new CellCoord(5, 4));
            var vertical = StraightRoadPathCalculator.Calculate(
                new CellCoord(4, 5),
                new CellCoord(3, 1));

            Assert.That(horizontal.Axis, Is.EqualTo(RoadPathAxis.Horizontal));
            Assert.That(horizontal.RequestedEnd, Is.EqualTo(new CellCoord(5, 4)));
            Assert.That(horizontal.End, Is.EqualTo(new CellCoord(5, 2)));
            Assert.That(horizontal.Cells, Is.EqualTo(new[]
            {
                new CellCoord(1, 2),
                new CellCoord(2, 2),
                new CellCoord(3, 2),
                new CellCoord(4, 2),
                new CellCoord(5, 2)
            }));

            Assert.That(vertical.Axis, Is.EqualTo(RoadPathAxis.Vertical));
            Assert.That(vertical.Cells, Is.EqualTo(new[]
            {
                new CellCoord(4, 5),
                new CellCoord(4, 4),
                new CellCoord(4, 3),
                new CellCoord(4, 2),
                new CellCoord(4, 1)
            }));
        }

        [Test]
        public void PreviewReturnsCostAndCellsWithoutChangingNetworkOrFunds()
        {
            var handler = CreateHandler(100);
            var command = new PlaceRoadCommand(
                new StableId(1),
                new CellCoord(1, 1),
                new CellCoord(3, 1));
            var before = handler.RoadNetwork.DefinitionIds.ToArray();
            var versionBefore = handler.RoadNetwork.Version;

            var preview = handler.Preview(command);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.IsApplied, Is.False);
            Assert.That(preview.Cost, Is.EqualTo(30));
            Assert.That(preview.FundsBefore, Is.EqualTo(100));
            Assert.That(preview.FundsAfter, Is.EqualTo(100));
            Assert.That(preview.ChangedCells, Is.EqualTo(command.Cells));
            Assert.That(handler.Budget.Funds, Is.EqualTo(100));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(versionBefore));
            Assert.That(handler.RoadNetwork.DefinitionIds.ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void CommitUsesTheSameValidationAndAppliesOneAtomicBatch()
        {
            var handler = CreateHandler(100);
            var command = new PlaceRoadCommand(
                new StableId(1),
                new CellCoord(1, 1),
                new CellCoord(3, 1));

            var preview = handler.Preview(command);
            var result = handler.Execute(command);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(RoadCommandFailureReason.None));
            Assert.That(result.Cost, Is.EqualTo(preview.Cost));
            Assert.That(result.ChangedCells, Is.EqualTo(preview.ChangedCells));
            Assert.That(result.FundsBefore, Is.EqualTo(preview.FundsBefore));
            Assert.That(result.FundsAfter, Is.EqualTo(70));
            Assert.That(handler.Budget.Funds, Is.EqualTo(70));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(1));
            Assert.That(handler.RoadNetwork.GetRoadDefinitionId(new CellCoord(1, 1)), Is.EqualTo(new StableId(1)));
            Assert.That(handler.RoadNetwork.GetRoadDefinitionId(new CellCoord(2, 1)), Is.EqualTo(new StableId(1)));
            Assert.That(handler.RoadNetwork.GetRoadDefinitionId(new CellCoord(3, 1)), Is.EqualTo(new StableId(1)));
        }

        [Test]
        public void InsufficientFundsRejectsPreviewAndCommitWithoutMutation()
        {
            var handler = CreateHandler(20);
            var command = new PlaceRoadCommand(
                new StableId(1),
                new CellCoord(0, 0),
                new CellCoord(2, 0));

            var preview = handler.Preview(command);
            var result = handler.Execute(command);

            Assert.That(preview.FailureReason, Is.EqualTo(RoadCommandFailureReason.InsufficientFunds));
            Assert.That(preview.Cost, Is.EqualTo(30));
            Assert.That(result.FailureReason, Is.EqualTo(preview.FailureReason));
            Assert.That(result.Cost, Is.EqualTo(preview.Cost));
            Assert.That(result.IsApplied, Is.False);
            Assert.That(handler.Budget.Funds, Is.EqualTo(20));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(0));
            Assert.That(handler.RoadNetwork.Cells.Any(cell => cell.IsOccupied), Is.False);
        }

        [Test]
        public void DuplicateRoadCellsAreRejectedBeforeAnyCellChanges()
        {
            var handler = CreateHandler(100);
            var command = new PlaceRoadCommand(
                new StableId(1),
                new[]
                {
                    new CellCoord(1, 1),
                    new CellCoord(2, 1),
                    new CellCoord(2, 1)
                });

            var result = handler.Execute(command);

            Assert.That(result.FailureReason, Is.EqualTo(RoadCommandFailureReason.DuplicateCell));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(0));
            Assert.That(handler.Budget.Funds, Is.EqualTo(100));
        }

        [Test]
        public void OutOfBoundsRoadCellsAreRejectedWithoutPartialPlacement()
        {
            var handler = CreateHandler(100, width: 4, height: 4);
            var command = new PlaceRoadCommand(
                new StableId(1),
                new CellCoord(2, 3),
                new CellCoord(5, 3));

            var result = handler.Execute(command);

            Assert.That(result.FailureReason, Is.EqualTo(RoadCommandFailureReason.OutOfBounds));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(0));
            Assert.That(handler.Budget.Funds, Is.EqualTo(100));
            Assert.That(handler.RoadNetwork.Cells.Any(cell => cell.IsOccupied), Is.False);
        }

        [Test]
        public void ExistingRoadIsRejectedAsASeparateFailureReason()
        {
            var handler = CreateHandler(100);
            var first = new PlaceRoadCommand(new StableId(1), new CellCoord(1, 1), new CellCoord(2, 1));
            handler.Execute(first);

            var duplicate = new PlaceRoadCommand(new StableId(1), new CellCoord(2, 1), new CellCoord(3, 1));
            var result = handler.Execute(duplicate);

            Assert.That(result.FailureReason, Is.EqualTo(RoadCommandFailureReason.RoadAlreadyExists));
            Assert.That(handler.Budget.Funds, Is.EqualTo(80));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(1));
            Assert.That(handler.RoadNetwork.GetRoadDefinitionId(new CellCoord(3, 1)), Is.EqualTo(StableId.Invalid));
        }

        [Test]
        public void PartialDemolitionRemovesOnlySelectedOccupiedCells()
        {
            var handler = CreateHandler(100);
            handler.Execute(new PlaceRoadCommand(new StableId(1), new CellCoord(1, 2), new CellCoord(5, 2)));

            var result = handler.Execute(new DemolishRoadCommand(new CellCoord(2, 2), new CellCoord(4, 2)));

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.Operation, Is.EqualTo(RoadCommandOperation.Demolish));
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(result.ChangedCells, Is.EqualTo(new[]
            {
                new CellCoord(2, 2),
                new CellCoord(3, 2),
                new CellCoord(4, 2)
            }));
            Assert.That(handler.RoadNetwork.IsOccupied(new CellCoord(1, 2)), Is.True);
            Assert.That(handler.RoadNetwork.IsOccupied(new CellCoord(2, 2)), Is.False);
            Assert.That(handler.RoadNetwork.IsOccupied(new CellCoord(3, 2)), Is.False);
            Assert.That(handler.RoadNetwork.IsOccupied(new CellCoord(4, 2)), Is.False);
            Assert.That(handler.RoadNetwork.IsOccupied(new CellCoord(5, 2)), Is.True);
            Assert.That(handler.Budget.Funds, Is.EqualTo(50));
            Assert.That(handler.RoadNetwork.Version, Is.EqualTo(2));
        }

        [Test]
        public void DispatcherPreservesRoadResultDtoAndCommandStamp()
        {
            var handler = CreateHandler(100);
            var dispatcher = new CommandDispatcher(new[] { handler });
            var stamp = new CommandStamp(new SimulationTick(4), new CommandSequence(7));
            var command = new PlaceRoadCommand(new StableId(1), new CellCoord(0, 0), new CellCoord(1, 0));

            var result = dispatcher.Dispatch(new CommandEnvelope(stamp, command));

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.Stamp, Is.EqualTo(stamp));
            Assert.That(result.HasDetails, Is.True);
            Assert.That(result.Details, Is.TypeOf<RoadCommandResult>());
            Assert.That(((RoadCommandResult)result.Details).Cost, Is.EqualTo(20));
        }

        [Test]
        public void DispatcherPreservesRejectedRoadResultDto()
        {
            var handler = CreateHandler(10);
            var dispatcher = new CommandDispatcher(new[] { handler });
            var command = new PlaceRoadCommand(new StableId(1), new CellCoord(0, 0), new CellCoord(1, 0));

            var result = dispatcher.Dispatch(new CommandEnvelope(Stamp(0, 0), command));

            Assert.That(result.Status, Is.EqualTo(CommandExecutionStatus.Rejected));
            Assert.That(result.HasDetails, Is.True);
            var details = result.Details as RoadCommandResult;
            Assert.That(details, Is.Not.Null);
            Assert.That(details.FailureReason, Is.EqualTo(RoadCommandFailureReason.InsufficientFunds));
            Assert.That(details.Cost, Is.EqualTo(20));
        }

        [Test]
        public void OrderedCommandStreamProducesTheSameRoadFixture()
        {
            var first = CreateHandler(200);
            var second = CreateHandler(200);
            var firstQueue = new CommandQueue();
            var secondQueue = new CommandQueue();
            var firstDispatcher = new CommandDispatcher(new[] { first });
            var secondDispatcher = new CommandDispatcher(new[] { second });
            var firstCommand = new PlaceRoadCommand(new StableId(1), new CellCoord(1, 1), new CellCoord(3, 1));
            var secondCommand = new PlaceRoadCommand(new StableId(1), new CellCoord(1, 3), new CellCoord(3, 3));

            firstQueue.Enqueue(Stamp(0, 1), secondCommand);
            firstQueue.Enqueue(Stamp(0, 0), firstCommand);
            secondQueue.Enqueue(Stamp(0, 0), firstCommand);
            secondQueue.Enqueue(Stamp(0, 1), secondCommand);

            firstQueue.ExecuteTick(SimulationTick.Zero, firstDispatcher);
            secondQueue.ExecuteTick(SimulationTick.Zero, secondDispatcher);

            Assert.That(first.RoadNetwork.DefinitionIds.ToArray(), Is.EqualTo(second.RoadNetwork.DefinitionIds.ToArray()));
            Assert.That(first.Budget.Funds, Is.EqualTo(second.Budget.Funds));
            Assert.That(first.RoadNetwork.Version, Is.EqualTo(second.RoadNetwork.Version));
        }

        private static RoadCommandHandler CreateHandler(int funds, int width = 8, int height = 8)
        {
            return new RoadCommandHandler(
                new RoadNetworkState(width, height),
                new[] { new RoadDefinition(new StableId(1), 10, "road.basic") },
                funds);
        }

        private static CommandStamp Stamp(long tick, ulong sequence)
        {
            return new CommandStamp(new SimulationTick(tick), new CommandSequence(sequence));
        }
    }
}
