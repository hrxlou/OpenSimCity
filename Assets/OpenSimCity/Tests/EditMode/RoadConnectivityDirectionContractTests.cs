using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadConnectivityDirectionContractTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1);

        [Test]
        public void DirectionOnlyReplacementUpdatesGraphResultAndPublishesExactDirtyCell()
        {
            var roads = new RoadNetworkState(4, 1);
            Assert.That(
                roads.TryApplyPlacement(
                    new[]
                    {
                        new CellCoord(0, 0),
                        new CellCoord(1, 0),
                        new CellCoord(2, 0),
                        new CellCoord(3, 0)
                    },
                    RoadDefinitionId),
                Is.True);

            var graph = new RoadConnectivityGraph(
                roads,
                new[]
                {
                    new ExternalConnectionState(
                        new StableId(10),
                        ExternalConnectionKind.Road,
                        new CellCoord(0, 0),
                        true)
                });
            var readModel = new RoadAccessibilityReadModel(graph);
            var target = new CellCoord(2, 0);
            var versionBefore = graph.GraphVersion;

            Assert.That(graph.Get(target).TravelDirection, Is.EqualTo(RoadTravelDirection.Bidirectional));
            Assert.That(
                roads.TryApplyReplacement(
                    new[] { target },
                    RoadDefinitionId,
                    new[] { RoadTravelDirection.East }),
                Is.True);

            Assert.That(graph.RefreshIfNeeded(), Is.True);
            Assert.That(graph.GraphVersion, Is.EqualTo(versionBefore + 1));
            Assert.That(graph.Get(target).TravelDirection, Is.EqualTo(RoadTravelDirection.East));
            Assert.That(graph.DirtyCells, Is.EqualTo(new[] { target }));
            Assert.That(graph.DirtyRegion.Minimum, Is.EqualTo(target));
            Assert.That(graph.DirtyRegion.Maximum, Is.EqualTo(target));

            Assert.That(readModel.RefreshIfNeeded(), Is.True);
            Assert.That(readModel.DirtyCells, Is.EqualTo(new[] { target }));
            Assert.That(graph.RefreshIfNeeded(), Is.False);
        }
    }
}
