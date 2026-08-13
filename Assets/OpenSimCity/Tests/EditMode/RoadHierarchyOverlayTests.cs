using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadHierarchyOverlayTests
    {
        [Test]
        public void HierarchyOverlayUsesTierColorAndDirectionPatternFromReadModel()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = 10_000
            });
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Small,
                new CellCoord(0, 0),
                new CellCoord(1, 0))));
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Street,
                new CellCoord(3, 1),
                new CellCoord(5, 1))));
            AssertApplied(runtime.Execute(new UpgradeRoadCommand(
                RoadUpgradeDefinitionIds.Avenue,
                RoadDirectionMode.Forward,
                new CellCoord(3, 1),
                new CellCoord(5, 1))));

            var snapshot = new PlacementOverlayModel(runtime).Build(PlacementOverlayKind.RoadHierarchy);

            Assert.That(snapshot.Title, Is.EqualTo("도로 등급·방향"));
            Assert.That(snapshot.Cells, Has.Count.EqualTo(5));
            Assert.That(snapshot.Cells.Select(row => row.Cell), Is.EqualTo(new[]
            {
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(3, 1),
                new CellCoord(4, 1),
                new CellCoord(5, 1)
            }));

            var small = snapshot.Cells[0];
            Assert.That(small.Category, Is.EqualTo(PlacementOverlayCategory.RoadSmall));
            Assert.That(small.Pattern, Is.EqualTo(OverlayPattern.Ring));
            Assert.That(small.Label, Is.EqualTo("↔"));

            var avenue = snapshot.Cells[2];
            Assert.That(avenue.Category, Is.EqualTo(PlacementOverlayCategory.RoadAvenue));
            Assert.That(avenue.Pattern, Is.EqualTo(OverlayPattern.Hatch));
            Assert.That(avenue.Label, Is.EqualTo("→"));

            Assert.That(snapshot.Legend.Select(entry => entry.Category), Is.EquivalentTo(new[]
            {
                PlacementOverlayCategory.RoadSmall,
                PlacementOverlayCategory.RoadStreet,
                PlacementOverlayCategory.RoadAvenue,
                PlacementOverlayCategory.RoadBidirectional,
                PlacementOverlayCategory.RoadOneWay
            }));
            Assert.That(snapshot.Legend.All(entry =>
                !string.IsNullOrWhiteSpace(entry.Label) ||
                entry.Pattern != OverlayPattern.Solid), Is.True);
        }

        [Test]
        public void HierarchyOverlayBuildDoesNotChangeSimulationChecksum()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = 10_000
            });
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Street,
                new CellCoord(0, 2),
                new CellCoord(4, 2))));
            var roadChecksum = runtime.Roads.ComputeChecksum();
            var trafficChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();

            var model = new PlacementOverlayModel(runtime);
            var first = model.BuildRoadHierarchy();
            var second = model.BuildRoadHierarchy();

            Assert.That(first.Cells, Is.EqualTo(second.Cells));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksum));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(trafficChecksum));
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
