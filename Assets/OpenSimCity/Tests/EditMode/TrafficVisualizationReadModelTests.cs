using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TrafficVisualizationReadModelTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1UL);
        private static readonly StableId ResidentialDefinitionId = new StableId(2UL);
        private static readonly StableId CommercialDefinitionId = new StableId(3UL);

        [Test]
        public void VehicleSampleCountsDoNotChangeSimulationChecksum()
        {
            var fixture = CreateLargeCommuteFixture();
            fixture.Run(new SimulationDate(1, 1));
            var routingModel = new TrafficRoutingReadModel(fixture.Routing);
            var visualization = new TrafficVisualizationReadModel(routingModel);
            var expectedSimulationChecksum = fixture.Routing.State.ComputeChecksum();
            var expectedRoutingVersion = fixture.Routing.State.Version;

            AssertSampleLimit(visualization, fixture.Routing, 0, 0, expectedSimulationChecksum);
            AssertSampleLimit(visualization, fixture.Routing, 100, 100, expectedSimulationChecksum);
            var hundredChecksum = visualization.VisualizationChecksum;
            AssertSampleLimit(visualization, fixture.Routing, 500, 500, expectedSimulationChecksum);

            Assert.That(visualization.VehicleSamples.Sum(row => row.RepresentedTripAmount), Is.EqualTo(500));
            Assert.That(visualization.VehicleSamples.Select(row => row.SampleId).Distinct().Count(), Is.EqualTo(500));
            Assert.That(visualization.VehicleSamples.All(row => row.RepresentedTripAmount == 1), Is.True);
            Assert.That(visualization.VisualizationChecksum, Is.Not.EqualTo(hundredChecksum));
            Assert.That(fixture.Routing.State.Version, Is.EqualTo(expectedRoutingVersion));
            Assert.That(fixture.Routing.State.ComputeChecksum(), Is.EqualTo(expectedSimulationChecksum));
        }

        [Test]
        public void SamplingAndBottleneckOrderingAreDeterministic()
        {
            var first = CreateLargeCommuteFixture();
            var second = CreateLargeCommuteFixture();
            var month = new SimulationDate(2, 4);
            first.Run(month);
            second.Run(month);

            var firstModel = new TrafficVisualizationReadModel(new TrafficRoutingReadModel(first.Routing));
            var secondModel = new TrafficVisualizationReadModel(new TrafficRoutingReadModel(second.Routing));
            firstModel.SetMaximumVehicleSamples(500);
            secondModel.SetMaximumVehicleSamples(500);
            firstModel.RefreshIfNeeded();
            secondModel.RefreshIfNeeded();

            Assert.That(first.Routing.State.ComputeChecksum(), Is.EqualTo(second.Routing.State.ComputeChecksum()));
            Assert.That(firstModel.VehicleSamples.Select(row => row.SampleId),
                Is.EqualTo(secondModel.VehicleSamples.Select(row => row.SampleId)));
            Assert.That(firstModel.VehicleSamples.Select(row => row.PhaseOffsetPermille),
                Is.EqualTo(secondModel.VehicleSamples.Select(row => row.PhaseOffsetPermille)));
            Assert.That(firstModel.Bottlenecks.Select(row => row.Edge),
                Is.EqualTo(secondModel.Bottlenecks.Select(row => row.Edge)));
            Assert.That(firstModel.VisualizationChecksum, Is.EqualTo(secondModel.VisualizationChecksum));
        }

        [Test]
        public void DataMapRowsMatchRoutingQueryAndExposeAffectedOdDelay()
        {
            var fixture = CreateImprovementFixture();
            fixture.Run(new SimulationDate(1, 1));
            var routingModel = new TrafficRoutingReadModel(fixture.Routing);
            var visualization = new TrafficVisualizationReadModel(routingModel);

            Assert.That(visualization.Edges, Has.Count.EqualTo(routingModel.Edges.Count));
            for (var index = 0; index < visualization.Edges.Count; index++)
            {
                var view = visualization.Edges[index];
                var source = routingModel.Edges[index];
                Assert.That(view.Edge.A, Is.EqualTo(source.Edge.A));
                Assert.That(view.Edge.B, Is.EqualTo(source.Edge.B));
                Assert.That(view.Volume, Is.EqualTo(source.Volume));
                Assert.That(view.Capacity, Is.EqualTo(source.Capacity));
                Assert.That(view.VolumeCapacityPermille, Is.EqualTo(source.VolumeCapacityPermille));
                Assert.That(view.CongestedTravelSeconds, Is.EqualTo(source.CongestedTravelSeconds));
            }

            Assert.That(visualization.Bottlenecks.First().VolumeCapacityPermille, Is.EqualTo(2000));
            Assert.That(visualization.OdImpacts, Has.Count.EqualTo(1));
            Assert.That(visualization.OdImpacts[0].Amount, Is.EqualTo(4));
            Assert.That(visualization.OdImpacts[0].AverageFreeFlowTravelSeconds, Is.EqualTo(480));
            Assert.That(visualization.OdImpacts[0].AverageCongestedTravelSeconds, Is.EqualTo(960));
            Assert.That(visualization.OdImpacts[0].AverageDelaySeconds, Is.EqualTo(480));
        }

        [Test]
        public void AlternateConnectionReducesBottleneckAndOdTravelTime()
        {
            var fixture = CreateImprovementFixture();
            fixture.Run(new SimulationDate(1, 1));
            var visualization = new TrafficVisualizationReadModel(
                new TrafficRoutingReadModel(fixture.Routing));
            var beforeSimulationChecksum = fixture.Routing.State.ComputeChecksum();
            var beforeTravelSeconds = visualization.OdImpacts[0].AverageCongestedTravelSeconds;
            var beforeBottleneck = visualization.Bottlenecks[0].VolumeCapacityPermille;

            Assert.That(fixture.Roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(2, 0),
                new CellCoord(3, 0),
                new CellCoord(4, 0)
            }, RoadDefinitionId), Is.True);
            Assert.That(fixture.Routing.RefreshIfNeeded(), Is.True);
            Assert.That(visualization.RefreshIfNeeded(), Is.True);

            Assert.That(fixture.Routing.State.ComputeChecksum(), Is.Not.EqualTo(beforeSimulationChecksum));
            Assert.That(visualization.OdImpacts[0].AverageCongestedTravelSeconds, Is.LessThan(beforeTravelSeconds));
            Assert.That(visualization.OdImpacts[0].AverageDelaySeconds, Is.Zero);
            Assert.That(visualization.Bottlenecks[0].VolumeCapacityPermille, Is.LessThan(beforeBottleneck));
            Assert.That(visualization.Bottlenecks[0].VolumeCapacityPermille, Is.EqualTo(1000));
            Assert.That(visualization.OdImpacts[0].RouteCount, Is.EqualTo(2));
        }

        [Test]
        public void VehiclePathInterpolationUsesCongestedSegmentTiming()
        {
            var path = new VehicleSamplePathData(
                new[]
                {
                    new CellCoord(0, 0),
                    new CellCoord(1, 0),
                    new CellCoord(2, 0)
                },
                new[] { 100, 300 });

            var firstHalf = path.Evaluate(125);
            var secondSegmentStart = path.Evaluate(250);
            var secondHalf = path.Evaluate(625);

            Assert.That(path.TotalTravelSeconds, Is.EqualTo(400));
            Assert.That(firstHalf.From, Is.EqualTo(new CellCoord(0, 0)));
            Assert.That(firstHalf.To, Is.EqualTo(new CellCoord(1, 0)));
            Assert.That(firstHalf.SegmentProgressPermille, Is.EqualTo(500));
            Assert.That(secondSegmentStart.From, Is.EqualTo(new CellCoord(1, 0)));
            Assert.That(secondSegmentStart.SegmentProgressPermille, Is.Zero);
            Assert.That(secondHalf.From, Is.EqualTo(new CellCoord(1, 0)));
            Assert.That(secondHalf.To, Is.EqualTo(new CellCoord(2, 0)));
            Assert.That(secondHalf.SegmentProgressPermille, Is.EqualTo(500));
        }

        [Test]
        public void VehicleSampleCountBenchmarkKeepsSimulationChecksumStable()
        {
            var fixture = CreateLargeCommuteFixture();
            fixture.Run(new SimulationDate(3, 7));
            var visualization = new TrafficVisualizationReadModel(
                new TrafficRoutingReadModel(fixture.Routing));
            var checksum = fixture.Routing.State.ComputeChecksum();

            foreach (var count in new[] { 0, 100, 500 })
            {
                var stopwatch = Stopwatch.StartNew();
                visualization.SetMaximumVehicleSamples(count);
                visualization.RefreshIfNeeded();
                _ = visualization.VisualizationChecksum;
                stopwatch.Stop();
                Assert.That(fixture.Routing.State.ComputeChecksum(), Is.EqualTo(checksum));
                Assert.That(visualization.VehicleSamples, Has.Count.EqualTo(count));
                TestContext.Progress.WriteLine(
                    $"P5-03 sample baseline count={count}: {stopwatch.Elapsed.TotalMilliseconds:0.000} ms, " +
                    $"simulationChecksum={checksum}, visualizationChecksum={visualization.VisualizationChecksum}");
            }
        }

        private static void AssertSampleLimit(
            TrafficVisualizationReadModel visualization,
            TrafficRoutingSystem routing,
            int limit,
            int expectedSamples,
            ulong expectedSimulationChecksum)
        {
            visualization.SetMaximumVehicleSamples(limit);
            visualization.RefreshIfNeeded();
            Assert.That(visualization.VehicleSamples, Has.Count.EqualTo(expectedSamples));
            Assert.That(visualization.SourceSimulationChecksum, Is.EqualTo(expectedSimulationChecksum));
            Assert.That(routing.State.ComputeChecksum(), Is.EqualTo(expectedSimulationChecksum));
        }

        private static RoutingFixture CreateLargeCommuteFixture()
        {
            const int roadLength = 64;
            var roads = new RoadNetworkState(roadLength, 1);
            var roadCells = new CellCoord[roadLength];
            for (var x = 0; x < roadLength; x++)
                roadCells[x] = new CellCoord(x, 0);
            Assert.That(roads.TryApplyPlacement(roadCells, RoadDefinitionId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());

            var buildings = new BuildingStateCollection();
            var accessRows = new Dictionary<StableId, TripEndpoint>();
            for (var index = 0; index < 125; index++)
            {
                var residentialId = new StableId((ulong)(1_000 + index));
                buildings.Add(new BuildingState(
                    residentialId,
                    ResidentialDefinitionId,
                    new StableId((ulong)(10_000 + index)),
                    CellCoord.Zero,
                    occupancy: 4));
                accessRows.Add(residentialId, BuildingEndpoint(residentialId, CellCoord.Zero));

                var commercialId = new StableId((ulong)(2_000 + index));
                buildings.Add(new BuildingState(
                    commercialId,
                    CommercialDefinitionId,
                    new StableId((ulong)(20_000 + index)),
                    new CellCoord(roadLength - 1, 0),
                    filledJobs: 4));
                accessRows.Add(
                    commercialId,
                    BuildingEndpoint(commercialId, new CellCoord(roadLength - 1, 0)));
            }

            var profiles = Profiles();
            var economy = new UrbanEconomySystem(buildings, profiles);
            var demand = new TripDemandSystem(
                buildings,
                profiles,
                new FakeAccessResolver(accessRows),
                economy);
            var routing = new TrafficRoutingSystem(
                roads,
                graph,
                Definitions(capacity: 250, speed: 60),
                demand,
                new TrafficRoutingParameters(maximumRouteAlternatives: 1));
            return new RoutingFixture(roads, economy, demand, routing);
        }

        private static RoutingFixture CreateImprovementFixture()
        {
            var roads = new RoadNetworkState(5, 3);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 1),
                new CellCoord(1, 1),
                new CellCoord(2, 1),
                new CellCoord(3, 1),
                new CellCoord(4, 1)
            }, RoadDefinitionId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());

            var residentialId = new StableId(101UL);
            var commercialId = new StableId(102UL);
            var buildings = new BuildingStateCollection();
            buildings.Add(new BuildingState(
                residentialId,
                ResidentialDefinitionId,
                new StableId(201UL),
                new CellCoord(0, 1),
                occupancy: 4));
            buildings.Add(new BuildingState(
                commercialId,
                CommercialDefinitionId,
                new StableId(202UL),
                new CellCoord(4, 1),
                filledJobs: 4));
            var accessRows = new Dictionary<StableId, TripEndpoint>
            {
                [residentialId] = BuildingEndpoint(residentialId, new CellCoord(0, 1)),
                [commercialId] = BuildingEndpoint(commercialId, new CellCoord(4, 1))
            };
            var profiles = Profiles();
            var economy = new UrbanEconomySystem(buildings, profiles);
            var demand = new TripDemandSystem(
                buildings,
                profiles,
                new FakeAccessResolver(accessRows),
                economy);
            var routing = new TrafficRoutingSystem(
                roads,
                graph,
                Definitions(capacity: 2, speed: 30),
                demand,
                new TrafficRoutingParameters(maximumRouteAlternatives: 3));
            return new RoutingFixture(roads, economy, demand, routing);
        }

        private static FakeProfiles Profiles()
        {
            return new FakeProfiles(
                new UrbanEconomyProfile(ResidentialDefinitionId, EconomicSector.Residential, 4, 0),
                new UrbanEconomyProfile(CommercialDefinitionId, EconomicSector.Commercial, 0, 4));
        }

        private static RoadDefinitionCatalog Definitions(int capacity, int speed)
        {
            return new RoadDefinitionCatalog(new[]
            {
                new RoadDefinition(
                    RoadDefinitionId,
                    buildCost: 0,
                    presentationKey: "test.road",
                    monthlyMaintenanceCost: 0,
                    capacityPerMonth: capacity,
                    freeFlowSpeedCellsPerHour: speed)
            });
        }

        private static TripEndpoint BuildingEndpoint(StableId id, CellCoord roadCell)
        {
            return new TripEndpoint(TripEndpointKind.Building, id, roadCell, componentId: 0);
        }

        private sealed class RoutingFixture
        {
            public RoutingFixture(
                RoadNetworkState roads,
                UrbanEconomySystem economy,
                TripDemandSystem demand,
                TrafficRoutingSystem routing)
            {
                Roads = roads;
                Economy = economy;
                Demand = demand;
                Routing = routing;
            }

            public RoadNetworkState Roads { get; }
            public UrbanEconomySystem Economy { get; }
            public TripDemandSystem Demand { get; }
            public TrafficRoutingSystem Routing { get; }

            public void Run(SimulationDate month)
            {
                Economy.RunMonth(month);
                Demand.RunMonth(month);
                Routing.RunMonth(month);
            }
        }

        private sealed class FakeProfiles : IUrbanEconomyProfileResolver
        {
            private readonly Dictionary<StableId, UrbanEconomyProfile> _profiles =
                new Dictionary<StableId, UrbanEconomyProfile>();

            public FakeProfiles(params UrbanEconomyProfile[] profiles)
            {
                for (var index = 0; index < profiles.Length; index++)
                    _profiles.Add(profiles[index].DefinitionId, profiles[index]);
            }

            public bool TryGetUrbanEconomyProfile(
                StableId definitionId,
                out UrbanEconomyProfile profile)
            {
                return _profiles.TryGetValue(definitionId, out profile);
            }
        }

        private sealed class FakeAccessResolver : ITripAccessResolver
        {
            private readonly Dictionary<StableId, TripEndpoint> _endpoints;
            private readonly IReadOnlyList<TripEndpoint> _gates =
                Array.AsReadOnly(Array.Empty<TripEndpoint>());

            public FakeAccessResolver(Dictionary<StableId, TripEndpoint> endpoints)
            {
                _endpoints = endpoints;
            }

            public TripEndpoint ResolveBuilding(BuildingState building)
            {
                return _endpoints[building.Id];
            }

            public IReadOnlyList<TripEndpoint> GetEnabledExternalRoadGates()
            {
                return _gates;
            }
        }
    }
}
