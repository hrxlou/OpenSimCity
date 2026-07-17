using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ParcelNetworkStateTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1);

        [Test]
        public void SameZoneCellsPartitionIntoDeterministicRectanglesUpToTwoByTwo()
        {
            var fixture = CreateFixture(5, 3);
            Zone(
                fixture.Zones,
                ZoneType.ResidentialLow,
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(0, 1), new CellCoord(1, 1), new CellCoord(2, 1));

            var first = new ParcelNetworkState(fixture.Zones, fixture.Graph);
            var second = new ParcelNetworkState(fixture.Zones, fixture.Graph);

            Assert.That(first.Parcels.Count, Is.EqualTo(2));
            Assert.That(first.TryGetAtCell(new CellCoord(0, 0), out var twoByTwo), Is.True);
            Assert.That(twoByTwo.Anchor, Is.EqualTo(new CellCoord(0, 0)));
            Assert.That(twoByTwo.Width, Is.EqualTo(2));
            Assert.That(twoByTwo.Height, Is.EqualTo(2));
            Assert.That(twoByTwo.Cells, Is.EqualTo(new[]
            {
                new CellCoord(0, 0), new CellCoord(1, 0),
                new CellCoord(0, 1), new CellCoord(1, 1)
            }));

            Assert.That(first.TryGetAtCell(new CellCoord(2, 1), out var oneByTwo), Is.True);
            Assert.That(oneByTwo.Anchor, Is.EqualTo(new CellCoord(2, 0)));
            Assert.That(oneByTwo.Width, Is.EqualTo(1));
            Assert.That(oneByTwo.Height, Is.EqualTo(2));
            Assert.That(
                first.Parcels.Select(parcel => parcel.Id),
                Is.EqualTo(second.Parcels.Select(parcel => parcel.Id)));
        }

        [Test]
        public void DifferentZoneTypesNeverShareAParcel()
        {
            var fixture = CreateFixture(4, 2);
            Zone(fixture.Zones, ZoneType.ResidentialLow, new CellCoord(0, 0), new CellCoord(1, 0));
            Zone(fixture.Zones, ZoneType.CommercialLow, new CellCoord(2, 0), new CellCoord(3, 0));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);

            Assert.That(parcels.Parcels.Count, Is.EqualTo(2));
            Assert.That(parcels.Parcels.All(parcel => parcel.Cells.All(
                cell => fixture.Zones.GetZoneType(cell) == parcel.ZoneType)), Is.True);
        }

        [Test]
        public void NoFrontageAndDisconnectedFrontageExposeDifferentFailureReasons()
        {
            var fixture = CreateFixture(6, 4, Gate(10, new CellCoord(0, 0)));
            Place(
                fixture.Roads,
                new CellCoord(0, 0), new CellCoord(1, 0),
                new CellCoord(4, 2), new CellCoord(5, 2));
            Zone(fixture.Zones, ZoneType.ResidentialLow, new CellCoord(2, 3));
            Zone(fixture.Zones, ZoneType.CommercialLow, new CellCoord(4, 3));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);

            Assert.That(parcels.TryGetAtCell(new CellCoord(2, 3), out var noFrontage), Is.True);
            Assert.That(noFrontage.IsDevelopmentEligible, Is.False);
            Assert.That(noFrontage.FailureReason,
                Is.EqualTo(ParcelDevelopmentFailureReason.NoRoadFrontage));

            Assert.That(parcels.TryGetAtCell(new CellCoord(4, 3), out var disconnected), Is.True);
            Assert.That(disconnected.HasRoadFrontage, Is.True);
            Assert.That(disconnected.IsDevelopmentEligible, Is.False);
            Assert.That(disconnected.FailureReason,
                Is.EqualTo(ParcelDevelopmentFailureReason.NoExternalRoadConnection));
            Assert.That(disconnected.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Unreachable));
        }

        [Test]
        public void MultipleFrontagesUseMinimumReachableDistanceAndExposeFarBand()
        {
            var fixture = CreateFixture(15, 3, Gate(10, new CellCoord(0, 1)));
            Place(
                fixture.Roads,
                Enumerable.Range(0, 15).Select(x => new CellCoord(x, 1)).ToArray());
            Zone(fixture.Zones, ZoneType.IndustrialLow, new CellCoord(13, 0), new CellCoord(14, 0));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);

            Assert.That(parcels.TryGetAtCell(new CellCoord(13, 0), out var parcel), Is.True);
            Assert.That(parcel.Frontages.Count, Is.EqualTo(2));
            Assert.That(parcel.IsDevelopmentEligible, Is.True);
            Assert.That(parcel.FailureReason, Is.EqualTo(ParcelDevelopmentFailureReason.None));
            Assert.That(parcel.NetworkDistanceToExternalGate, Is.EqualTo(13));
            Assert.That(parcel.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Far));
        }

        [Test]
        public void RoadInstallationAndDemolitionRefreshEligibilityAndApplicationQueryInOneRefresh()
        {
            var fixture = CreateFixture(5, 2, Gate(10, new CellCoord(0, 0)));
            Place(
                fixture.Roads,
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(3, 0), new CellCoord(4, 0));
            Zone(fixture.Zones, ZoneType.CommercialLow, new CellCoord(4, 1));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);
            var query = new ParcelReadModel(parcels);
            Assert.That(query.TryGetAtCell(new CellCoord(4, 1), out var before), Is.True);
            Assert.That(before.IsDevelopmentEligible, Is.True);

            Assert.That(fixture.Roads.TryDemolishRoad(new CellCoord(2, 0)), Is.True);
            Assert.That(query.RefreshIfNeeded(), Is.True);

            Assert.That(query.TryGetAtCell(new CellCoord(4, 1), out var after), Is.True);
            Assert.That(after.IsDevelopmentEligible, Is.False);
            Assert.That(after.FailureReason,
                Is.EqualTo(ParcelDevelopmentFailureReason.NoExternalRoadConnection));
            Assert.That(after.NetworkDistanceToExternalGate,
                Is.EqualTo(RoadConnectivityGraph.UnreachableDistance));
            Assert.That(query.ChangedParcelIds, Does.Contain(after.Id));
            Assert.That(query.DirtyCells, Is.EqualTo(new[] { new CellCoord(4, 1) }));

            Assert.That(fixture.Roads.TryPlaceRoad(new CellCoord(2, 0), RoadDefinitionId), Is.True);
            Assert.That(query.RefreshIfNeeded(), Is.True);
            Assert.That(query.TryGetAtCell(new CellCoord(4, 1), out var restored), Is.True);
            Assert.That(restored.IsDevelopmentEligible, Is.True);
            Assert.That(restored.NetworkDistanceToExternalGate, Is.EqualTo(4));
        }

        [Test]
        public void ParcelBesideIntersectionKeepsEachFrontageDirectionDeterministically()
        {
            var fixture = CreateFixture(5, 5, Gate(10, new CellCoord(0, 2)));
            Place(
                fixture.Roads,
                new CellCoord(0, 2), new CellCoord(1, 2), new CellCoord(2, 2),
                new CellCoord(3, 2), new CellCoord(4, 2), new CellCoord(2, 0),
                new CellCoord(2, 1), new CellCoord(2, 3), new CellCoord(2, 4));
            Zone(fixture.Zones, ZoneType.ResidentialLow, new CellCoord(3, 3));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);

            Assert.That(parcels.TryGetAtCell(new CellCoord(3, 3), out var parcel), Is.True);
            Assert.That(
                parcel.Frontages.Select(frontage => frontage.Direction),
                Is.EqualTo(new[]
                {
                    ParcelFrontageDirection.South,
                    ParcelFrontageDirection.West
                }));
            Assert.That(parcel.NetworkDistanceToExternalGate, Is.EqualTo(3));
            Assert.That(parcel.IsDevelopmentEligible, Is.True);
        }

        [Test]
        public void ZoneClearKeepsUnaffectedParcelIdentityOutsideExactDirtyCells()
        {
            var fixture = CreateFixture(8, 3);
            Zone(
                fixture.Zones,
                ZoneType.ResidentialLow,
                new CellCoord(0, 0), new CellCoord(1, 0),
                new CellCoord(0, 1), new CellCoord(1, 1));
            Zone(
                fixture.Zones,
                ZoneType.IndustrialLow,
                new CellCoord(6, 0), new CellCoord(7, 0),
                new CellCoord(6, 1), new CellCoord(7, 1));
            var parcels = new ParcelNetworkState(fixture.Zones, fixture.Graph);
            Assert.That(parcels.TryGetAtCell(new CellCoord(6, 0), out var unaffectedBefore), Is.True);
            var unaffectedId = unaffectedBefore.Id;

            Assert.That(fixture.Zones.TryApplyClear(new[]
            {
                new CellCoord(0, 0), new CellCoord(1, 0),
                new CellCoord(0, 1), new CellCoord(1, 1)
            }), Is.True);
            Assert.That(parcels.RefreshIfNeeded(), Is.True);

            Assert.That(parcels.TryGetAtCell(new CellCoord(0, 0), out _), Is.False);
            Assert.That(parcels.TryGetAtCell(new CellCoord(6, 0), out var unaffectedAfter), Is.True);
            Assert.That(unaffectedAfter.Id, Is.EqualTo(unaffectedId));
            Assert.That(ReferenceEquals(unaffectedBefore, unaffectedAfter), Is.True);
            Assert.That(parcels.ChangedParcelIds.Contains(unaffectedId), Is.False);
            Assert.That(parcels.DirtyCells, Is.EqualTo(new[]
            {
                new CellCoord(0, 0), new CellCoord(1, 0),
                new CellCoord(0, 1), new CellCoord(1, 1)
            }));
        }

        private static Fixture CreateFixture(
            int width,
            int height,
            params ExternalConnectionState[] gates)
        {
            var roads = new RoadNetworkState(width, height);
            return new Fixture(
                roads,
                new ZoneNetworkState(width, height),
                new RoadConnectivityGraph(roads, gates));
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

        private static void Zone(
            ZoneNetworkState zones,
            ZoneType type,
            params CellCoord[] cells)
        {
            Assert.That(zones.TryApplyZone(cells, type), Is.True);
        }

        private sealed class Fixture
        {
            public Fixture(
                RoadNetworkState roads,
                ZoneNetworkState zones,
                RoadConnectivityGraph graph)
            {
                Roads = roads;
                Zones = zones;
                Graph = graph;
            }

            public RoadNetworkState Roads { get; }

            public ZoneNetworkState Zones { get; }

            public RoadConnectivityGraph Graph { get; }
        }
    }
}
