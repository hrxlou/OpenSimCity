using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class GrowthDevelopmentTests
    {
        private static readonly StableId RoadDefinitionId = new StableId(1);
        private static readonly StableId GateId = new StableId(10);
        private static readonly StableId UtilityDefinitionId = new StableId(0xC100_0000_0000_0001UL);
        private static readonly StableId PlantId = new StableId(0xD100_0000_0000_0001UL);
        private static readonly StableId ResidentialDefId = new StableId(0xB100_0000_0000_0001UL);
        private static readonly StableId CommercialDefId = new StableId(0xB100_0000_0000_0002UL);
        private static readonly StableId IndustrialDefId = new StableId(0xB100_0000_0000_0003UL);

        [Test]
        public void GrowthIsBlockedSeparatelyForRoadPowerAndDemand()
        {
            // Reachable, powered, demanded parcel next to the road; an unreachable parcel;
            // a reachable-but-unpowered parcel. Only the fully satisfied parcel develops.
            var fixture = Fixture.Create(20, 4);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 8);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0)); // reachable
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 3)); // no frontage -> road ineligible
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(6, 0)); // reachable
            fixture.Refresh();

            fixture.Demand.Set(EconomicSector.Residential, 20);

            var reachable = fixture.ParcelIdAt(new CellCoord(2, 0));
            var isolated = fixture.ParcelIdAt(new CellCoord(2, 3));

            Assert.That(fixture.System.TryGetFeasibility(reachable, out var reachableFeasibility), Is.True);
            Assert.That(reachableFeasibility.IsFeasible, Is.True);
            Assert.That(fixture.System.TryGetFeasibility(isolated, out var isolatedFeasibility), Is.True);
            Assert.That(isolatedFeasibility.BlockReason, Is.EqualTo(DevelopmentBlockReason.RoadIneligible));

            // No demand -> NoSectorDemand for the reachable parcel.
            fixture.Demand.Set(EconomicSector.Residential, 0);
            Assert.That(fixture.System.TryGetFeasibility(reachable, out var noDemand), Is.True);
            Assert.That(noDemand.BlockReason, Is.EqualTo(DevelopmentBlockReason.NoSectorDemand));

            // Demand but no power producer -> NoPowerCapacity.
            fixture.Demand.Set(EconomicSector.Residential, 20);
            fixture.RemovePlant();
            fixture.Refresh();
            Assert.That(fixture.System.TryGetFeasibility(reachable, out var noPower), Is.True);
            Assert.That(noPower.BlockReason, Is.EqualTo(DevelopmentBlockReason.NoPowerCapacity));

            fixture.System.RunMonth(new SimulationDate(1, 1));
            Assert.That(fixture.System.State.Count, Is.Zero, "nothing develops without power");
        }

        [Test]
        public void AccessibilityWeightsSelectionButNeverGatesDevelopmentOrDefinition()
        {
            var parameters = new GrowthDevelopmentParameters(
                maxDevelopmentsPerMonth: 4,
                constructionMonths: 1,
                baseSuitabilityWeight: 8,
                residentialAccessibilityWeight: 2);
            var fixture = Fixture.Create(24, 4, parameters);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 20);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));   // distance 2 -> Near
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(18, 0));  // distance 18 -> Far
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 20);

            var nearId = fixture.ParcelIdAt(new CellCoord(2, 0));
            var farId = fixture.ParcelIdAt(new CellCoord(18, 0));

            Assert.That(fixture.System.TryGetFeasibility(nearId, out var near), Is.True);
            Assert.That(fixture.System.TryGetFeasibility(farId, out var far), Is.True);
            Assert.That(near.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Near));
            Assert.That(far.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Far));
            // Weak per-use weight: closer parcel scores higher, farther parcel stays positive.
            Assert.That(near.SuitabilityFactor, Is.EqualTo(8 + 2 * 3));
            Assert.That(far.SuitabilityFactor, Is.EqualTo(8 + 2 * 1));
            Assert.That(far.SuitabilityFactor, Is.GreaterThan(0));
            Assert.That(near.SuitabilityFactor, Is.GreaterThan(far.SuitabilityFactor));

            fixture.System.RunMonth(new SimulationDate(1, 1));

            // Accessibility alone must not exclude the far parcel: both develop when the cap allows.
            var developedParcels = fixture.System.State.Sites.Select(site => site.ParcelId).ToArray();
            Assert.That(developedParcels, Does.Contain(nearId));
            Assert.That(developedParcels, Does.Contain(farId));
            // Definition selection is independent of the accessibility band.
            Assert.That(
                fixture.System.State.Sites.Select(site => site.DefinitionId).Distinct().Single(),
                Is.EqualTo(ResidentialDefId));
        }

        [Test]
        public void PerUseAccessibilityWeightDiffersBySector()
        {
            var parameters = new GrowthDevelopmentParameters(
                baseSuitabilityWeight: 8,
                residentialAccessibilityWeight: 2,
                commercialAccessibilityWeight: 3,
                industrialAccessibilityWeight: 1);
            var fixture = Fixture.Create(12, 6, parameters);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 8);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));
            fixture.Zone(ZoneType.CommercialLow, new CellCoord(4, 0));
            fixture.Zone(ZoneType.IndustrialLow, new CellCoord(6, 0));
            fixture.Refresh();

            // Same Near band (distance 2/4/6 -> Near/Near/Medium), so isolate band by using distance 2 each.
            fixture.System.TryGetFeasibility(fixture.ParcelIdAt(new CellCoord(2, 0)), out var residential);
            Assert.That(residential.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Near));
            Assert.That(residential.SuitabilityFactor, Is.EqualTo(8 + 2 * 3));

            fixture.System.TryGetFeasibility(fixture.ParcelIdAt(new CellCoord(4, 0)), out var commercial);
            Assert.That(commercial.DistanceBand, Is.EqualTo(ExternalGateDistanceBand.Near));
            Assert.That(commercial.SuitabilityFactor, Is.EqualTo(8 + 3 * 3));
        }

        [Test]
        public void HighDemandWithNoFeasibleParcelIsReportedSeparatelyFromDemand()
        {
            var fixture = Fixture.Create(12, 4);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 8);
            // No power plant: parcels are reachable and demanded but not powered.
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(6, 0));
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 25);

            fixture.System.RunMonth(new SimulationDate(1, 1));

            var residential = fixture.Summary(EconomicSector.Residential);
            Assert.That(residential.SmoothedDemand, Is.EqualTo(25));
            Assert.That(residential.FeasibleParcelCount, Is.Zero);
            Assert.That(residential.HasUnmetDemand, Is.True);
            Assert.That(fixture.System.State.Count, Is.Zero);

            // With power added, the same demand now finds feasible parcels.
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Refresh();
            fixture.System.RunMonth(new SimulationDate(1, 2));
            var afterPower = fixture.Summary(EconomicSector.Residential);
            Assert.That(afterPower.FeasibleParcelCount, Is.GreaterThan(0));
            Assert.That(afterPower.HasUnmetDemand, Is.False);
        }

        [Test]
        public void MonthlyCapLimitsStartsAndConstructionTimeDelaysBuildings()
        {
            var parameters = new GrowthDevelopmentParameters(
                maxDevelopmentsPerMonth: 2,
                constructionMonths: 2);
            var fixture = Fixture.Create(16, 4, parameters);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 12);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(4, 0));
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(6, 0));
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(8, 0));
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(10, 0));
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 30);

            fixture.System.RunMonth(new SimulationDate(1, 1));
            Assert.That(fixture.System.State.Count, Is.EqualTo(2), "monthly cap limits starts");
            Assert.That(fixture.Buildings.Count, Is.Zero, "nothing is built the month it starts");

            fixture.System.RunMonth(new SimulationDate(1, 2));
            Assert.That(fixture.Buildings.Count, Is.Zero, "still under construction after one month");

            fixture.System.RunMonth(new SimulationDate(1, 3));
            Assert.That(fixture.Buildings.Count, Is.EqualTo(2), "first cohort completes after two months");
        }

        [Test]
        public void ConstructionStartProgressAndCompletionEventsAreSeparateFromBuildingCreation()
        {
            var parameters = new GrowthDevelopmentParameters(
                maxDevelopmentsPerMonth: 1,
                constructionMonths: 2);
            var fixture = Fixture.Create(12, 4, parameters);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 8);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 30);

            fixture.System.RunMonth(new SimulationDate(1, 1));
            Assert.That(fixture.System.LastEvents.Select(e => e.Kind),
                Is.EqualTo(new[] { GrowthEventKind.ConstructionStarted }));
            Assert.That(fixture.Buildings.Count, Is.Zero);

            fixture.System.RunMonth(new SimulationDate(1, 2));
            Assert.That(fixture.System.LastEvents.Select(e => e.Kind),
                Is.EqualTo(new[] { GrowthEventKind.ConstructionProgressed }));
            Assert.That(fixture.Buildings.Count, Is.Zero);

            fixture.System.RunMonth(new SimulationDate(1, 3));
            Assert.That(fixture.System.LastEvents.Select(e => e.Kind),
                Is.EqualTo(new[] { GrowthEventKind.ConstructionCompleted }));
            Assert.That(fixture.Buildings.Count, Is.EqualTo(1));
            var built = fixture.Buildings.Buildings.Single();
            Assert.That(built.DefinitionId, Is.EqualTo(ResidentialDefId));
            Assert.That(built.Anchor, Is.EqualTo(new CellCoord(2, 0)));
        }

        [Test]
        public void SameSeedAndCommandStreamReproduceTheMonthlyBuildingList()
        {
            string RunEightMonths(ulong seed)
            {
                var fixture = BuildMixedCity(seed);
                for (var month = 1; month <= 8; month++)
                    fixture.System.RunMonth(new SimulationDate(1, month));
                return JsonUtility.ToJson(fixture.Buildings.ToDto()) + "|" +
                       JsonUtility.ToJson(fixture.System.State.ToDto());
            }

            Assert.That(RunEightMonths(1234), Is.EqualTo(RunEightMonths(1234)));
        }

        [Test]
        public void SimulationResultIsIdenticalWhetherOrNotAReadModelObservesIt()
        {
            var withoutView = BuildMixedCity(777);
            var withView = BuildMixedCity(777);
            var readModel = new GrowthDevelopmentReadModel(withView.System);

            for (var month = 1; month <= 6; month++)
            {
                withoutView.System.RunMonth(new SimulationDate(1, month));
                withView.System.RunMonth(new SimulationDate(1, month));
                // A view refreshing every month must not perturb the simulation.
                readModel.RefreshIfNeeded();
            }

            Assert.That(
                JsonUtility.ToJson(withView.Buildings.ToDto()),
                Is.EqualTo(JsonUtility.ToJson(withoutView.Buildings.ToDto())));
            Assert.That(
                JsonUtility.ToJson(withView.System.State.ToDto()),
                Is.EqualTo(JsonUtility.ToJson(withoutView.System.State.ToDto())));
        }

        [Test]
        public void DevelopmentStateAndSnapshotRoundTripThroughSerializableDto()
        {
            var fixture = BuildMixedCity(42);
            for (var month = 1; month <= 4; month++)
                fixture.System.RunMonth(new SimulationDate(1, month));

            var stateJson = JsonUtility.ToJson(fixture.System.State.ToDto());
            var restoredState = DevelopmentState.FromDto(JsonUtility.FromJson<DevelopmentStateDto>(stateJson));
            Assert.That(restoredState.Count, Is.EqualTo(fixture.System.State.Count));
            Assert.That(
                restoredState.Sites.Select(site => site.BuildingId),
                Is.EqualTo(fixture.System.State.Sites.Select(site => site.BuildingId)));

            var readModel = new GrowthDevelopmentReadModel(fixture.System);
            var snapshotJson = JsonUtility.ToJson(readModel.Snapshot);
            var restoredSnapshot = JsonUtility.FromJson<GrowthDevelopmentSnapshotDto>(snapshotJson);
            Assert.That(restoredSnapshot.Date, Is.EqualTo(new SimulationDate(1, 4)));
            Assert.That(restoredSnapshot.Sites.Length, Is.EqualTo(fixture.System.State.Count));
        }

        [Test]
        public void CompletedBuildingIsNotRedeveloped()
        {
            var parameters = new GrowthDevelopmentParameters(
                maxDevelopmentsPerMonth: 4,
                constructionMonths: 1);
            var fixture = Fixture.Create(12, 4, parameters);
            fixture.PlaceRoadRow(y: 1, fromX: 0, toX: 8);
            fixture.PlacePlant(new CellCoord(0, 0), production: 100);
            fixture.Zone(ZoneType.ResidentialLow, new CellCoord(2, 0));
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 30);

            fixture.System.RunMonth(new SimulationDate(1, 1)); // start
            fixture.System.RunMonth(new SimulationDate(1, 2)); // complete
            Assert.That(fixture.Buildings.Count, Is.EqualTo(1));

            fixture.System.RunMonth(new SimulationDate(1, 3)); // must not build a second time
            Assert.That(fixture.Buildings.Count, Is.EqualTo(1));
            Assert.That(fixture.System.State.Count, Is.Zero);

            var parcelId = fixture.ParcelIdAt(new CellCoord(2, 0));
            Assert.That(fixture.System.TryGetFeasibility(parcelId, out var feasibility), Is.True);
            Assert.That(feasibility.BlockReason, Is.EqualTo(DevelopmentBlockReason.AlreadyDeveloped));
        }

        private static Fixture BuildMixedCity(ulong seed)
        {
            var parameters = new GrowthDevelopmentParameters(
                maxDevelopmentsPerMonth: 3,
                constructionMonths: 2);
            var fixture = Fixture.Create(20, 8, parameters, seed);
            fixture.PlaceRoadRow(y: 2, fromX: 0, toX: 16);
            fixture.PlacePlant(new CellCoord(0, 1), production: 200);
            for (var x = 1; x <= 15; x += 2)
                fixture.Zone(ZoneType.ResidentialLow, new CellCoord(x, 1));
            for (var x = 1; x <= 15; x += 2)
                fixture.Zone(ZoneType.CommercialLow, new CellCoord(x, 3));
            fixture.Refresh();
            fixture.Demand.Set(EconomicSector.Residential, 30);
            fixture.Demand.Set(EconomicSector.Commercial, 20);
            return fixture;
        }

        private sealed class Fixture
        {
            private readonly RoadNetworkState _roads;
            private readonly ZoneNetworkState _zones;
            private readonly RoadConnectivityGraph _graph;
            private readonly PowerProducerCollection _producers;

            private Fixture(
                RoadNetworkState roads,
                ZoneNetworkState zones,
                RoadConnectivityGraph graph,
                ParcelNetworkState parcels,
                PowerProducerCollection producers,
                PowerNetwork power,
                BuildingStateCollection buildings,
                StubDemand demand,
                GrowthDevelopmentSystem system)
            {
                _roads = roads;
                _zones = zones;
                _graph = graph;
                _producers = producers;
                Parcels = parcels;
                Power = power;
                Buildings = buildings;
                Demand = demand;
                System = system;
            }

            public ParcelNetworkState Parcels { get; }
            public PowerNetwork Power { get; }
            public BuildingStateCollection Buildings { get; }
            public StubDemand Demand { get; }
            public GrowthDevelopmentSystem System { get; }

            public static Fixture Create(
                int width,
                int height,
                GrowthDevelopmentParameters parameters = null,
                ulong seed = 0)
            {
                var roads = new RoadNetworkState(width, height);
                var zones = new ZoneNetworkState(width, height);
                var gate = new ExternalConnectionState(GateId, ExternalConnectionKind.Road, new CellCoord(0, 1), true);
                var graph = new RoadConnectivityGraph(roads, new[] { gate });
                var parcels = new ParcelNetworkState(zones, graph);
                var catalog = new BuildingDefinitionCatalog(CreateDefinitions());
                var utilities = new UtilityDefinitionCatalog(new[]
                {
                    new UtilityDefinitionData(UtilityDefinitionId, 1000, "utility.power.plant.placeholder")
                });
                var producers = new PowerProducerCollection();
                var buildings = new BuildingStateCollection();
                var power = new PowerNetwork(graph, buildings, producers, catalog, utilities);
                var demand = new StubDemand();
                var system = new GrowthDevelopmentSystem(
                    buildings, parcels, power, demand, catalog,
                    parameters ?? GrowthDevelopmentParameters.Default, seed);
                return new Fixture(roads, zones, graph, parcels, producers, power, buildings, demand, system);
            }

            public void PlaceRoadRow(int y, int fromX, int toX)
            {
                var cells = new List<CellCoord>();
                for (var x = fromX; x <= toX; x++)
                    cells.Add(new CellCoord(x, y));
                Assert.That(_roads.TryApplyPlacement(cells.ToArray(), RoadDefinitionId), Is.True);
            }

            public void Zone(ZoneType type, CellCoord cell)
            {
                Assert.That(_zones.TryApplyZone(new[] { cell }, type), Is.True);
            }

            public void PlacePlant(CellCoord anchor, int production)
            {
                _producers.Add(new PowerProducerState(PlantId, UtilityDefinitionId, anchor, production));
            }

            public void RemovePlant()
            {
                _producers.Remove(PlantId);
            }

            public void Refresh()
            {
                _graph.RefreshIfNeeded();
                Parcels.RefreshIfNeeded();
                Power.RefreshIfNeeded();
            }

            public StableId ParcelIdAt(CellCoord cell)
            {
                Assert.That(Parcels.TryGetAtCell(cell, out var parcel), Is.True, $"no parcel at {cell}");
                return parcel.Id;
            }

            public DevelopmentSectorSummary Summary(EconomicSector sector)
            {
                return System.SectorSummaries.Single(summary => summary.Sector == sector);
            }
        }

        private static IEnumerable<BuildingDefinitionData> CreateDefinitions()
        {
            return new[]
            {
                new BuildingDefinitionData(ResidentialDefId, ZoneType.ResidentialLow,
                    new BuildingFootprint(1, 1), 4, 0, "building.residential.low.placeholder", 2),
                new BuildingDefinitionData(CommercialDefId, ZoneType.CommercialLow,
                    new BuildingFootprint(1, 1), 0, 4, "building.commercial.low.placeholder", 3),
                new BuildingDefinitionData(IndustrialDefId, ZoneType.IndustrialLow,
                    new BuildingFootprint(1, 1), 0, 4, "building.industrial.low.placeholder", 3)
            };
        }

        private sealed class StubDemand : IRciDemandProvider
        {
            private readonly Dictionary<EconomicSector, int> _demand = new Dictionary<EconomicSector, int>();

            public void Set(EconomicSector sector, int value) => _demand[sector] = value;

            public int SmoothedDemand(EconomicSector sector) =>
                _demand.TryGetValue(sector, out var value) ? value : 0;
        }
    }
}
