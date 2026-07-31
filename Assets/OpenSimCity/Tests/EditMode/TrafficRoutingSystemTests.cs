using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
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
    public sealed class TrafficRoutingSystemTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1UL);
        private static readonly StableId ResidentialDefinitionId = new StableId(2UL);
        private static readonly StableId CommercialDefinitionId = new StableId(3UL);
        private static readonly StableId ResidentialBuildingId = new StableId(101UL);
        private static readonly StableId CommercialBuildingId = new StableId(102UL);

        [Test]
        public void EqualCostAlternativesUseCompleteCellSequenceTieBreak()
        {
            var roads = new RoadNetworkState(3, 3);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 1),
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(2, 0),
                new CellCoord(2, 1),
                new CellCoord(0, 2),
                new CellCoord(1, 2),
                new CellCoord(2, 2)
            }, RoadDefinitionId), Is.True);

            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                Definitions(capacity: 100, speed: 30),
                new TrafficRoutingParameters(maximumRouteAlternatives: 3));

            var first = planner.GetRoutes(new CellCoord(0, 1), new CellCoord(2, 1));
            var second = planner.GetRoutes(new CellCoord(0, 1), new CellCoord(2, 1));

            Assert.That(first.IsReachable, Is.True);
            Assert.That(first.Routes, Has.Count.EqualTo(2));
            Assert.That(first.Routes[0].Cells, Is.EqualTo(new[]
            {
                new CellCoord(0, 1),
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(2, 0),
                new CellCoord(2, 1)
            }));
            Assert.That(first.Routes[1].Cells, Is.EqualTo(new[]
            {
                new CellCoord(0, 1),
                new CellCoord(0, 2),
                new CellCoord(1, 2),
                new CellCoord(2, 2),
                new CellCoord(2, 1)
            }));
            Assert.That(first.Routes[0].FreeFlowTravelSeconds,
                Is.EqualTo(first.Routes[1].FreeFlowTravelSeconds));
            Assert.That(second, Is.SameAs(first));
            Assert.That(planner.CacheMissCount, Is.EqualTo(1));
            Assert.That(planner.CacheHitCount, Is.EqualTo(1));
        }

        [Test]
        public void RoadVersionInvalidatesAffectedComponentAndRetainsUnrelatedCache()
        {
            var roads = new RoadNetworkState(8, 4);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(5, 3), new CellCoord(6, 3), new CellCoord(7, 3)
            }, RoadDefinitionId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                Definitions(capacity: 100, speed: 30),
                new TrafficRoutingParameters(maximumRouteAlternatives: 1));

            var firstComponent = planner.GetRoutes(new CellCoord(0, 0), new CellCoord(2, 0));
            var secondComponent = planner.GetRoutes(new CellCoord(5, 3), new CellCoord(7, 3));
            Assert.That(firstComponent.IsReachable, Is.True);
            Assert.That(secondComponent.IsReachable, Is.True);
            Assert.That(planner.CacheEntryCount, Is.EqualTo(2));

            Assert.That(roads.TryPlaceRoad(new CellCoord(1, 1), RoadDefinitionId), Is.True);
            var retained = planner.GetRoutes(new CellCoord(5, 3), new CellCoord(7, 3));

            Assert.That(retained, Is.SameAs(secondComponent));
            Assert.That(planner.LastInvalidatedEntryCount, Is.EqualTo(1));
            Assert.That(planner.LastRetainedEntryCount, Is.EqualTo(1));
            Assert.That(planner.CacheHitCount, Is.EqualTo(1));

            var rebuilt = planner.GetRoutes(new CellCoord(0, 0), new CellCoord(2, 0));
            Assert.That(rebuilt.IsReachable, Is.True);
            Assert.That(planner.CacheMissCount, Is.EqualTo(3));
        }

        [Test]
        public void MatchedDemandIsConservedIntoEdgeVolumeAndCongestion()
        {
            var fixture = CreateStraightRoutingFixture(capacity: 2, speed: 30);
            fixture.Run(new SimulationDate(1, 1));

            Assert.That(fixture.Demand.State.CommuteMatched, Is.EqualTo(4));
            Assert.That(fixture.Routing.State.InputMatchedAmount, Is.EqualTo(4));
            Assert.That(fixture.Routing.State.RoutedAmount, Is.EqualTo(4));
            Assert.That(fixture.Routing.State.UnroutedAmount, Is.Zero);
            Assert.That(fixture.Routing.State.Assignments, Has.Count.EqualTo(1));
            Assert.That(fixture.Routing.State.Assignments[0].Amount, Is.EqualTo(4));
            Assert.That(fixture.Routing.State.Assignments[0].FreeFlowTravelSeconds, Is.EqualTo(240));
            Assert.That(fixture.Routing.State.Assignments[0].CongestedTravelSeconds, Is.EqualTo(480));

            Assert.That(fixture.Routing.State.Edges, Has.Count.EqualTo(2));
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.Volume == 4), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.Capacity == 2), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.VolumeCapacityPermille == 2000), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.FreeFlowSpeedCellsPerHour == 30), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.EffectiveSpeedCellsPerHour == 15), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.FreeFlowTravelSeconds == 120), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.CongestedTravelSeconds == 240), Is.True);
        }

        [Test]
        public void CapacityBoundaryDoesNotReduceEffectiveSpeed()
        {
            var fixture = CreateStraightRoutingFixture(capacity: 4, speed: 30);
            fixture.Run(new SimulationDate(1, 1));

            Assert.That(fixture.Routing.State.Edges.All(edge => edge.Volume == edge.Capacity), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge => edge.VolumeCapacityPermille == 1000), Is.True);
            Assert.That(fixture.Routing.State.Edges.All(edge =>
                edge.EffectiveSpeedCellsPerHour == edge.FreeFlowSpeedCellsPerHour), Is.True);
            Assert.That(fixture.Routing.State.Assignments[0].CongestedTravelSeconds,
                Is.EqualTo(fixture.Routing.State.Assignments[0].FreeFlowTravelSeconds));
        }

        [Test]
        public void RoadRemovalRefreshesRoutingWithoutStalePath()
        {
            var fixture = CreateStraightRoutingFixture(capacity: 100, speed: 30);
            fixture.Run(new SimulationDate(1, 1));
            var firstChecksum = fixture.Routing.State.ComputeChecksum();
            Assert.That(fixture.Routing.State.RoutedAmount, Is.EqualTo(4));

            Assert.That(fixture.Roads.TryDemolishRoad(new CellCoord(1, 0)), Is.True);
            Assert.That(fixture.Routing.RefreshIfNeeded(), Is.True);

            Assert.That(fixture.Routing.State.RoutedAmount, Is.Zero);
            Assert.That(fixture.Routing.State.UnroutedAmount, Is.EqualTo(4));
            Assert.That(fixture.Routing.State.Assignments, Is.Empty);
            Assert.That(fixture.Routing.State.Edges, Is.Empty);
            Assert.That(fixture.Routing.State.Unrouted, Has.Count.EqualTo(1));
            Assert.That(fixture.Routing.State.Unrouted[0].Reason,
                Is.EqualTo(TrafficRouteFailureReason.DifferentRoadComponent));
            Assert.That(fixture.Routing.State.ComputeChecksum(), Is.Not.EqualTo(firstChecksum));
        }

        [Test]
        public void LargeGridRouteIsDeterministicAndSimulationStaysUnityFree()
        {
            const int size = 128;
            var roads = new RoadNetworkState(size, size);
            var cells = new List<CellCoord>(size * size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    cells.Add(new CellCoord(x, y));
            }
            Assert.That(roads.TryApplyPlacement(cells, RoadDefinitionId), Is.True);

            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                Definitions(capacity: 1000, speed: 60),
                new TrafficRoutingParameters(maximumRouteAlternatives: 1));
            var stopwatch = Stopwatch.StartNew();
            var first = planner.GetRoutes(CellCoord.Zero, new CellCoord(size - 1, size - 1));
            stopwatch.Stop();
            var second = planner.GetRoutes(CellCoord.Zero, new CellCoord(size - 1, size - 1));

            Assert.That(first.IsReachable, Is.True);
            Assert.That(first.Routes, Has.Count.EqualTo(1));
            Assert.That(first.Routes[0].EdgeCount, Is.EqualTo((size - 1) * 2));
            Assert.That(second, Is.SameAs(first));
            Assert.That(planner.CacheHitCount, Is.EqualTo(1));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(30_000));
            TestContext.Progress.WriteLine(
                $"P5-02 128x128 deterministic route baseline: {stopwatch.ElapsedMilliseconds} ms, " +
                $"edges={first.Routes[0].EdgeCount}, cacheEntries={planner.CacheEntryCount}");

            var assemblyReferences = typeof(TrafficRoutingSystem).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine"));
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        [Test]
        public void RoadDefinitionDefaultsPreserveExistingConstructionCallers()
        {
            var definition = new RoadDefinition(RoadDefinitionId, buildCost: 10);
            Assert.That(definition.CapacityPerMonth, Is.EqualTo(RoadDefinition.DefaultCapacityPerMonth));
            Assert.That(definition.FreeFlowSpeedCellsPerHour,
                Is.EqualTo(RoadDefinition.DefaultFreeFlowSpeedCellsPerHour));
        }

        private static RoutingFixture CreateStraightRoutingFixture(int capacity, int speed)
        {
            var roads = new RoadNetworkState(3, 1);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 0),
                new CellCoord(1, 0),
                new CellCoord(2, 0)
            }, RoadDefinitionId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());

            var buildings = new BuildingStateCollection();
            buildings.Add(new BuildingState(
                ResidentialBuildingId,
                ResidentialDefinitionId,
                new StableId(201UL),
                new CellCoord(0, 0),
                occupancy: 4));
            buildings.Add(new BuildingState(
                CommercialBuildingId,
                CommercialDefinitionId,
                new StableId(202UL),
                new CellCoord(2, 0),
                filledJobs: 4));

            var profiles = new FakeProfiles(
                new UrbanEconomyProfile(ResidentialDefinitionId, EconomicSector.Residential, 4, 0),
                new UrbanEconomyProfile(CommercialDefinitionId, EconomicSector.Commercial, 0, 4));
            var access = new FakeAccessResolver(new Dictionary<StableId, TripEndpoint>
            {
                [ResidentialBuildingId] = BuildingEndpoint(ResidentialBuildingId, new CellCoord(0, 0), 0),
                [CommercialBuildingId] = BuildingEndpoint(CommercialBuildingId, new CellCoord(2, 0), 0)
            });
            var economy = new UrbanEconomySystem(buildings, profiles);
            var demand = new TripDemandSystem(buildings, profiles, access, economy);
            var routing = new TrafficRoutingSystem(
                roads,
                graph,
                Definitions(capacity, speed),
                demand,
                new TrafficRoutingParameters(maximumRouteAlternatives: 1));
            return new RoutingFixture(roads, economy, demand, routing);
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

        private static TripEndpoint BuildingEndpoint(StableId id, CellCoord roadCell, int component)
        {
            return new TripEndpoint(TripEndpointKind.Building, id, roadCell, component);
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
