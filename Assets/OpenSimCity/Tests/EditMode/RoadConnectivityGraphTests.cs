using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadConnectivityGraphTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1);

        [Test]
        public void IntersectionAndDeadEndsExposeCardinalMasksAndOneComponent()
        {
            var roads = new RoadNetworkState(5, 5);
            Place(
                roads,
                new CellCoord(0, 2), new CellCoord(1, 2), new CellCoord(2, 2),
                new CellCoord(3, 2), new CellCoord(4, 2), new CellCoord(2, 0),
                new CellCoord(2, 1), new CellCoord(2, 3), new CellCoord(2, 4));
            var graph = CreateGraph(roads, Gate(10, new CellCoord(0, 2)));

            var center = graph.Get(new CellCoord(2, 2));
            var westDeadEnd = graph.Get(new CellCoord(0, 2));
            var northDeadEnd = graph.Get(new CellCoord(2, 4));

            Assert.That(
                center.ConnectionMask,
                Is.EqualTo(
                    RoadConnectionMask.North |
                    RoadConnectionMask.East |
                    RoadConnectionMask.South |
                    RoadConnectionMask.West));
            Assert.That(westDeadEnd.ConnectionMask, Is.EqualTo(RoadConnectionMask.East));
            Assert.That(northDeadEnd.ConnectionMask, Is.EqualTo(RoadConnectionMask.South));
            Assert.That(graph.Cells.Where(cell => cell.HasRoad).Select(cell => cell.ComponentId).Distinct(),
                Is.EqualTo(new[] { 2 }));
            Assert.That(center.NetworkDistanceToExternalGate, Is.EqualTo(2));
            Assert.That(northDeadEnd.NetworkDistanceToExternalGate, Is.EqualTo(4));
        }

        [Test]
        public void BoundaryRoadGateMakesOnlyItsConnectedComponentReachable()
        {
            var roads = new RoadNetworkState(6, 4);
            Place(
                roads,
                new CellCoord(0, 1), new CellCoord(1, 1), new CellCoord(2, 1),
                new CellCoord(4, 2), new CellCoord(5, 2));
            var graph = CreateGraph(roads, Gate(100, new CellCoord(0, 1)));

            var connected = graph.Get(new CellCoord(2, 1));
            var isolated = graph.Get(new CellCoord(4, 2));
            var empty = graph.Get(new CellCoord(3, 1));

            Assert.That(connected.IsReachable, Is.True);
            Assert.That(connected.NetworkDistanceToExternalGate, Is.EqualTo(2));
            Assert.That(connected.NearestExternalGateId, Is.EqualTo(new StableId(100)));
            Assert.That(isolated.IsReachable, Is.False);
            Assert.That(isolated.UnreachableReason,
                Is.EqualTo(RoadUnreachableReason.DisconnectedFromExternalRoadGate));
            Assert.That(isolated.NetworkDistanceToExternalGate,
                Is.EqualTo(RoadConnectivityGraph.UnreachableDistance));
            Assert.That(empty.UnreachableReason, Is.EqualTo(RoadUnreachableReason.NoRoad));
        }

        [Test]
        public void DemolitionSplitsComponentAndRefreshesVersionAndExactDirtyCellsOnce()
        {
            var roads = new RoadNetworkState(5, 3);
            Place(
                roads,
                new CellCoord(0, 1), new CellCoord(1, 1), new CellCoord(2, 1),
                new CellCoord(3, 1), new CellCoord(4, 1));
            var graph = CreateGraph(roads, Gate(10, new CellCoord(0, 1)));
            var versionBefore = graph.GraphVersion;

            Assert.That(roads.TryDemolishRoad(new CellCoord(2, 1)), Is.True);
            Assert.That(graph.RefreshIfNeeded(), Is.True);

            Assert.That(graph.GraphVersion, Is.EqualTo(versionBefore + 1));
            Assert.That(graph.RefreshIfNeeded(), Is.False);
            Assert.That(graph.Get(new CellCoord(4, 1)).UnreachableReason,
                Is.EqualTo(RoadUnreachableReason.DisconnectedFromExternalRoadGate));
            Assert.That(graph.Get(new CellCoord(0, 1)).ComponentId, Is.EqualTo(5));
            Assert.That(graph.Get(new CellCoord(3, 1)).ComponentId, Is.EqualTo(8));
            Assert.That(graph.DirtyCells, Is.EqualTo(new[]
            {
                new CellCoord(1, 1),
                new CellCoord(2, 1),
                new CellCoord(3, 1),
                new CellCoord(4, 1)
            }));
            Assert.That(graph.DirtyRegion.Minimum, Is.EqualTo(new CellCoord(1, 1)));
            Assert.That(graph.DirtyRegion.Maximum, Is.EqualTo(new CellCoord(4, 1)));

            Assert.That(roads.TryPlaceRoad(new CellCoord(2, 1), RoadDefinitionId), Is.True);
            Assert.That(graph.Get(new CellCoord(4, 1)).IsReachable, Is.True);
            Assert.That(graph.GraphVersion, Is.EqualTo(versionBefore + 2));
        }

        [Test]
        public void SeveralRoadCellsInOneBatchAdvanceGraphVersionOnlyOnce()
        {
            var roads = new RoadNetworkState(6, 2);
            var graph = CreateGraph(roads, Gate(10, new CellCoord(0, 0)));

            Place(
                roads,
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(3, 0), new CellCoord(4, 0));

            Assert.That(graph.GraphVersion, Is.EqualTo(1));
            Assert.That(graph.DirtyCells, Is.EqualTo(new[]
            {
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(2, 0),
                new CellCoord(3, 0),
                new CellCoord(4, 0)
            }));
        }

        [Test]
        public void EqualDistanceUsesSmallestStableGateIdRegardlessOfInputOrder()
        {
            var roads = new RoadNetworkState(15, 1);
            Place(roads, Enumerable.Range(0, 15).Select(x => new CellCoord(x, 0)).ToArray());
            var high = Gate(200, new CellCoord(0, 0));
            var low = Gate(100, new CellCoord(14, 0));

            var first = CreateGraph(roads, high, low);
            var second = CreateGraph(roads, low, high);
            var midpoint = first.Get(new CellCoord(7, 0));

            Assert.That(midpoint.NetworkDistanceToExternalGate, Is.EqualTo(7));
            Assert.That(midpoint.NearestExternalGateId, Is.EqualTo(new StableId(100)));
            Assert.That(midpoint.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Medium));
            Assert.That(first.Cells, Is.EqualTo(second.Cells));
            Assert.That(first.ComputeChecksum(), Is.EqualTo(second.ComputeChecksum()));
        }

        [Test]
        public void DistanceBandsAndUnreachableSentinelAreFixedPublicContract()
        {
            var roads = new RoadNetworkState(15, 1);
            Place(roads, Enumerable.Range(0, 15).Select(x => new CellCoord(x, 0)).ToArray());
            var graph = CreateGraph(roads, Gate(10, new CellCoord(0, 0)));

            Assert.That(graph.Get(new CellCoord(0, 0)).DistanceBand,
                Is.EqualTo(ExternalGateDistanceBand.Gate));
            Assert.That(graph.Get(new CellCoord(4, 0)).DistanceBand,
                Is.EqualTo(ExternalGateDistanceBand.Near));
            Assert.That(graph.Get(new CellCoord(5, 0)).DistanceBand,
                Is.EqualTo(ExternalGateDistanceBand.Medium));
            Assert.That(graph.Get(new CellCoord(12, 0)).DistanceBand,
                Is.EqualTo(ExternalGateDistanceBand.Medium));
            Assert.That(graph.Get(new CellCoord(13, 0)).DistanceBand,
                Is.EqualTo(ExternalGateDistanceBand.Far));
            Assert.That(graph.Get(new CellCoord(14, 0)).NetworkDistanceToExternalGate, Is.EqualTo(14));
        }

        [Test]
        public void DisabledNonRoadInteriorAndMissingRoadGatesDoNotActivateExternalReachability()
        {
            var roads = new RoadNetworkState(5, 3);
            Place(
                roads,
                new CellCoord(0, 1), new CellCoord(1, 1), new CellCoord(2, 1),
                new CellCoord(3, 1), new CellCoord(4, 1));
            var graph = CreateGraph(
                roads,
                new ExternalConnectionState(
                    new StableId(1), ExternalConnectionKind.Road, new CellCoord(0, 1), false),
                new ExternalConnectionState(
                    new StableId(2), ExternalConnectionKind.Rail, new CellCoord(4, 1), true),
                Gate(3, new CellCoord(2, 1)),
                Gate(4, new CellCoord(0, 2)));

            var result = graph.Get(new CellCoord(2, 1));

            Assert.That(result.IsReachable, Is.False);
            Assert.That(result.NetworkDistanceToExternalGate,
                Is.EqualTo(RoadConnectivityGraph.UnreachableDistance));
            Assert.That(result.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Unreachable));
            Assert.That(result.UnreachableReason,
                Is.EqualTo(RoadUnreachableReason.NoEnabledExternalRoadGate));
        }

        [Test]
        public void FixedFixtureProducesStableRowMajorChecksum()
        {
            var roads = new RoadNetworkState(6, 4);
            Place(
                roads,
                new CellCoord(0, 1), new CellCoord(1, 1), new CellCoord(2, 1),
                new CellCoord(2, 2), new CellCoord(3, 2), new CellCoord(4, 2),
                new CellCoord(5, 2), new CellCoord(0, 3), new CellCoord(1, 3));
            var graph = CreateGraph(
                roads,
                Gate(90, new CellCoord(5, 2)),
                Gate(30, new CellCoord(0, 1)));

            Assert.That(graph.ComputeChecksum(), Is.EqualTo(0x437e8c870287f6dbUL));
        }

        [Test]
        public void ApplicationReadModelMatchesGraphAndRefreshesTheSameDirtySet()
        {
            var roads = new RoadNetworkState(5, 2);
            Place(
                roads,
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(3, 0), new CellCoord(4, 0));
            var graph = CreateGraph(roads, Gate(10, new CellCoord(0, 0)));
            var readModel = new RoadAccessibilityReadModel(graph);

            var graphResult = graph.Get(new CellCoord(4, 0));
            var overlayResult = readModel.Get(new CellCoord(4, 0));

            Assert.That((byte)overlayResult.ConnectionMask, Is.EqualTo((byte)graphResult.ConnectionMask));
            Assert.That(overlayResult.ComponentId, Is.EqualTo(graphResult.ComponentId));
            Assert.That(overlayResult.IsReachable, Is.EqualTo(graphResult.IsReachable));
            Assert.That(overlayResult.NetworkDistanceToExternalGate,
                Is.EqualTo(graphResult.NetworkDistanceToExternalGate));
            Assert.That((byte)overlayResult.DistanceBand, Is.EqualTo((byte)graphResult.DistanceBand));
            Assert.That((byte)overlayResult.UnavailableReason,
                Is.EqualTo((byte)graphResult.UnreachableReason));

            Assert.That(roads.TryDemolishRoad(new CellCoord(2, 0)), Is.True);
            Assert.That(readModel.RefreshIfNeeded(), Is.True);
            Assert.That(readModel.GraphVersion, Is.EqualTo(graph.GraphVersion));
            Assert.That(readModel.DirtyCells, Is.EqualTo(graph.DirtyCells));
            Assert.That(readModel.Get(new CellCoord(4, 0)).UnavailableReason,
                Is.EqualTo(RoadAccessibilityUnavailableReason.DisconnectedFromExternalRoadGate));
        }

        private static RoadConnectivityGraph CreateGraph(
            RoadNetworkState roads,
            params ExternalConnectionState[] gates)
        {
            return new RoadConnectivityGraph(roads, gates);
        }

        private static ExternalConnectionState Gate(ulong id, CellCoord cell)
        {
            return new ExternalConnectionState(
                new StableId(id),
                ExternalConnectionKind.Road,
                cell,
                true);
        }

        private static void Place(RoadNetworkState roads, params CellCoord[] cells)
        {
            Assert.That(roads.TryApplyPlacement(cells, RoadDefinitionId), Is.True);
        }
    }
}
