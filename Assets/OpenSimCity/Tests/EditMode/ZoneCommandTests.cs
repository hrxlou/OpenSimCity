using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ZoneCommandTests
    {
        [Test]
        public void ZoneTypeAndDefinitionIdsUseStableLowDensityContract()
        {
            var catalog = ZoneDefinitionCatalog.CreateDefault();

            Assert.That((byte)ZoneType.None, Is.EqualTo(0));
            Assert.That((byte)ZoneType.ResidentialLow, Is.EqualTo(1));
            Assert.That((byte)ZoneType.CommercialLow, Is.EqualTo(2));
            Assert.That((byte)ZoneType.IndustrialLow, Is.EqualTo(3));
            Assert.That(catalog.Get(ZoneType.ResidentialLow).Id, Is.EqualTo(ZoneDefinitionIds.ResidentialLow));
            Assert.That(catalog.Get(ZoneType.CommercialLow).Id, Is.EqualTo(ZoneDefinitionIds.CommercialLow));
            Assert.That(catalog.Get(ZoneType.IndustrialLow).Id, Is.EqualTo(ZoneDefinitionIds.IndustrialLow));
            Assert.That(catalog.Get(ZoneType.ResidentialLow).PaintCost, Is.EqualTo(0));
        }

        [Test]
        public void BrushNormalizesDuplicatesAndSortsCellsInRowMajorOrder()
        {
            var brush = ZoneBrush.FromCells(new[]
            {
                new CellCoord(3, 2),
                new CellCoord(2, 1),
                new CellCoord(3, 2),
                new CellCoord(1, 1)
            });

            Assert.That(brush.Cells, Is.EqualTo(new[]
            {
                new CellCoord(1, 1),
                new CellCoord(2, 1),
                new CellCoord(3, 2)
            }));

            var area = ZoneBrushArea.Calculate(new CellCoord(2, 2), new CellCoord(1, 1));
            Assert.That(area.Cells, Is.EqualTo(new[]
            {
                new CellCoord(1, 1),
                new CellCoord(2, 1),
                new CellCoord(1, 2),
                new CellCoord(2, 2)
            }));
        }

        [Test]
        public void PreviewReturnsProspectiveChangedCellsWithoutMutation()
        {
            var handler = CreateHandler();
            var command = new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new[]
                {
                    new CellCoord(3, 1),
                    new CellCoord(1, 1),
                    new CellCoord(2, 1),
                    new CellCoord(2, 1)
                });

            var preview = handler.Preview(command);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.IsApplied, Is.False);
            Assert.That(preview.StateChanged, Is.False);
            Assert.That(preview.Cost, Is.EqualTo(0));
            Assert.That(preview.RequestedCells, Is.EqualTo(new[]
            {
                new CellCoord(1, 1),
                new CellCoord(2, 1),
                new CellCoord(3, 1)
            }));
            Assert.That(preview.ChangedCells, Is.EqualTo(preview.RequestedCells));
            Assert.That(preview.RejectedCells, Is.Empty);
            Assert.That(handler.Zones.Version, Is.EqualTo(0));
            Assert.That(handler.Zones.Cells.Any(cell => cell.IsZoned), Is.False);
        }

        [Test]
        public void PaintCommitAppliesOneAtomicFreeBatch()
        {
            var handler = CreateHandler();
            var command = ZoneAreaCommand.FromArea(
                ZoneType.CommercialLow,
                new CellCoord(1, 1),
                new CellCoord(2, 2));

            var preview = handler.Preview(command);
            var result = handler.Execute(command);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.StateChanged, Is.True);
            Assert.That(result.ChangedCells, Is.EqualTo(preview.ChangedCells));
            Assert.That(result.FundsBefore, Is.EqualTo(0));
            Assert.That(result.FundsAfter, Is.EqualTo(0));
            Assert.That(handler.Zones.Version, Is.EqualTo(1));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(1, 1)), Is.EqualTo(ZoneType.CommercialLow));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(2, 2)), Is.EqualTo(ZoneType.CommercialLow));
        }

        [Test]
        public void RoadCellsRejectPaintWithoutPartialZoneMutation()
        {
            var handler = CreateHandler();
            var roadCell = new CellCoord(2, 2);
            Assert.That(handler.RoadNetwork.TryPlaceRoad(roadCell, new StableId(10)), Is.True);

            var result = handler.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new[] { new CellCoord(1, 2), roadCell, new CellCoord(3, 2) }));

            Assert.That(result.FailureReason, Is.EqualTo(ZoneCommandFailureReason.RoadOccupied));
            Assert.That(result.RejectedCells, Is.EqualTo(new[] { roadCell }));
            Assert.That(handler.Zones.Version, Is.EqualTo(0));
            Assert.That(handler.Zones.Cells.Any(cell => cell.IsZoned), Is.False);
        }

        [Test]
        public void OutOfBoundsBrushRejectsWithoutPartialZoneMutation()
        {
            var handler = CreateHandler(width: 4, height: 4);
            var result = handler.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new[] { new CellCoord(2, 3), new CellCoord(4, 3) }));

            Assert.That(result.FailureReason, Is.EqualTo(ZoneCommandFailureReason.OutOfBounds));
            Assert.That(result.RejectedCells, Is.EqualTo(new[] { new CellCoord(4, 3) }));
            Assert.That(handler.Zones.Version, Is.EqualTo(0));
            Assert.That(handler.Zones.Cells.Any(cell => cell.IsZoned), Is.False);
        }

        [Test]
        public void OtherZoneCannotBeOverwrittenByTheSameAtomicBrush()
        {
            var handler = CreateHandler();
            Assert.That(handler.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(1, 1),
                new CellCoord(1, 1))).IsApplied, Is.True);

            var result = handler.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new[] { new CellCoord(1, 1), new CellCoord(2, 1) }));

            Assert.That(result.FailureReason, Is.EqualTo(ZoneCommandFailureReason.DifferentZone));
            Assert.That(result.RejectedCells, Is.EqualTo(new[] { new CellCoord(1, 1) }));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(1, 1)), Is.EqualTo(ZoneType.ResidentialLow));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(2, 1)), Is.EqualTo(ZoneType.None));
            Assert.That(handler.Zones.Version, Is.EqualTo(1));
        }

        [Test]
        public void SameZoneReapplyAndClearAreIdempotentAndShareChangedCells()
        {
            var handler = CreateHandler();
            var command = new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(1, 1),
                new CellCoord(2, 1));
            Assert.That(handler.Execute(command).IsApplied, Is.True);

            var reapplyPreview = handler.Preview(command);
            var reapply = handler.Execute(command);
            Assert.That(reapplyPreview.IsValid, Is.True);
            Assert.That(reapplyPreview.ChangedCells, Is.Empty);
            Assert.That(reapply.ChangedCells, Is.EqualTo(reapplyPreview.ChangedCells));
            Assert.That(handler.Zones.Version, Is.EqualTo(1));

            var clear = new ClearZoneCommand(new[]
            {
                new CellCoord(2, 1),
                new CellCoord(1, 1),
                new CellCoord(3, 1)
            });
            var clearPreview = handler.Preview(clear);
            var clearResult = handler.Execute(clear);

            Assert.That(clearPreview.ChangedCells, Is.EqualTo(new[]
            {
                new CellCoord(1, 1),
                new CellCoord(2, 1)
            }));
            Assert.That(clearResult.ChangedCells, Is.EqualTo(clearPreview.ChangedCells));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(1, 1)), Is.EqualTo(ZoneType.None));
            Assert.That(handler.Zones.GetZoneType(new CellCoord(2, 1)), Is.EqualTo(ZoneType.None));
            Assert.That(handler.Zones.Version, Is.EqualTo(2));
        }

        [Test]
        public void ZoneReadModelRefreshesExactChangedCellsInCommandOrder()
        {
            var handler = CreateHandler();
            var readModel = new ZoneReadModel(handler.Zones);
            var initialVersion = readModel.Version;

            var command = ZoneAreaCommand.FromArea(
                ZoneType.IndustrialLow,
                new CellCoord(2, 1),
                new CellCoord(1, 2));
            var result = handler.Execute(command);

            Assert.That(readModel.RefreshIfNeeded(), Is.True);
            Assert.That(readModel.Version, Is.EqualTo(initialVersion + 1));
            Assert.That(readModel.ChangedCells, Is.EqualTo(result.ChangedCells));
            Assert.That(readModel.Get(new CellCoord(1, 1)).ZoneType, Is.EqualTo(ZoneType.IndustrialLow));

            Assert.That(readModel.RefreshIfNeeded(), Is.False);
            Assert.That(readModel.ChangedCells, Is.EqualTo(result.ChangedCells));
        }

        [Test]
        public void OrderedCommandStreamProducesTheSameZoneFixture()
        {
            var first = CreateHandler(8, 8);
            var second = CreateHandler(8, 8);
            var firstQueue = new CommandQueue();
            var secondQueue = new CommandQueue();
            var firstDispatcher = new CommandDispatcher(new[] { first });
            var secondDispatcher = new CommandDispatcher(new[] { second });
            var firstCommand = ZoneAreaCommand.FromArea(
                ZoneType.ResidentialLow,
                new CellCoord(1, 1),
                new CellCoord(2, 2));
            var secondCommand = ZoneAreaCommand.FromPath(
                ZoneType.CommercialLow,
                new CellCoord(4, 1),
                new CellCoord(4, 3));

            firstQueue.Enqueue(Stamp(0, 1), secondCommand);
            firstQueue.Enqueue(Stamp(0, 0), firstCommand);
            secondQueue.Enqueue(Stamp(0, 0), firstCommand);
            secondQueue.Enqueue(Stamp(0, 1), secondCommand);

            firstQueue.ExecuteTick(SimulationTick.Zero, firstDispatcher);
            secondQueue.ExecuteTick(SimulationTick.Zero, secondDispatcher);

            Assert.That(first.Zones.ZoneTypes.ToArray(), Is.EqualTo(second.Zones.ZoneTypes.ToArray()));
            Assert.That(first.Zones.DefinitionIds.ToArray(), Is.EqualTo(second.Zones.DefinitionIds.ToArray()));
            Assert.That(first.Zones.Version, Is.EqualTo(second.Zones.Version));
        }

        private static ZoneCommandHandler CreateHandler(int width = 8, int height = 8)
        {
            return new ZoneCommandHandler(
                new ZoneNetworkState(width, height),
                new RoadNetworkState(width, height));
        }

        private static CommandStamp Stamp(long tick, ulong sequence)
        {
            return new CommandStamp(new SimulationTick(tick), new CommandSequence(sequence));
        }
    }
}
