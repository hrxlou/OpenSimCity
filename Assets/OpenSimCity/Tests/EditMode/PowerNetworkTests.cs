using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PowerNetworkTests
    {
        private static readonly StableId RoadId = new StableId(1);
        private static readonly StableId BuildingDefinitionId = new StableId(100);
        private static readonly StableId PlantDefinitionId = new StableId(200);

        [Test]
        public void CapacityIsAllocatedAtomicallyInStableBuildingIdOrder()
        {
            var fixture = CreateFixture(8);
            fixture.Buildings.Create(new StableId(30), BuildingDefinitionId, new StableId(300), new CellCoord(6, 2));
            fixture.Buildings.Create(new StableId(10), BuildingDefinitionId, new StableId(100), new CellCoord(2, 2));
            fixture.Buildings.Create(new StableId(20), BuildingDefinitionId, new StableId(200), new CellCoord(4, 2));
            fixture.Producers.Add(new PowerProducerState(new StableId(1), PlantDefinitionId, new CellCoord(0, 0), 8));

            Assert.That(fixture.Power.GetBuilding(new StableId(10)).IsPowered, Is.True);
            Assert.That(fixture.Power.GetBuilding(new StableId(20)).IsPowered, Is.True);
            var rejected = fixture.Power.GetBuilding(new StableId(30));
            Assert.That(rejected.IsPowered, Is.False);
            Assert.That(rejected.Allocated, Is.Zero);
            Assert.That(rejected.FailureReason, Is.EqualTo(PowerSupplyFailureReason.InsufficientCapacity));
            Assert.That(fixture.Power.Buildings.Select(result => result.BuildingId.Value),
                Is.EqualTo(new ulong[] { 10, 20, 30 }));
            Assert.That(fixture.Power.Components, Has.Count.EqualTo(1));
            Assert.That(fixture.Power.Components[0].Capacity, Is.EqualTo(8));
            Assert.That(fixture.Power.Components[0].Demand, Is.EqualTo(12));
            Assert.That(fixture.Power.Components[0].Allocated, Is.EqualTo(8));
            Assert.That(fixture.Power.Components[0].IsOverloaded, Is.True);
        }

        [Test]
        public void RoadSplitAndProducerRemovalInvalidateResultsWithoutStaleSupply()
        {
            var fixture = CreateFixture(12);
            fixture.Buildings.Create(new StableId(10), BuildingDefinitionId, new StableId(100), new CellCoord(2, 2));
            fixture.Buildings.Create(new StableId(20), BuildingDefinitionId, new StableId(200), new CellCoord(6, 2));
            fixture.Producers.Add(new PowerProducerState(new StableId(1), PlantDefinitionId, new CellCoord(0, 0), 12));
            Assert.That(fixture.Power.Buildings.All(result => result.IsPowered), Is.True);
            var version = fixture.Power.NetworkVersion;

            Assert.That(fixture.Roads.TryDemolishRoad(new CellCoord(4, 1)), Is.True);
            Assert.That(fixture.Power.GetBuilding(new StableId(10)).IsPowered, Is.True);
            Assert.That(fixture.Power.GetBuilding(new StableId(20)).FailureReason,
                Is.EqualTo(PowerSupplyFailureReason.NoPowerProducer));
            Assert.That(fixture.Power.NetworkVersion, Is.GreaterThan(version));
            Assert.That(fixture.Power.DirtyBuildingIds.Select(id => id.Value), Is.EqualTo(new ulong[] { 20 }));

            Assert.That(fixture.Producers.Remove(new StableId(1)), Is.True);
            Assert.That(fixture.Power.GetBuilding(new StableId(10)).FailureReason,
                Is.EqualTo(PowerSupplyFailureReason.NoPowerProducer));
            Assert.That(fixture.Power.DirtyBuildingIds.Select(id => id.Value), Is.EqualTo(new ulong[] { 10 }));
        }

        [Test]
        public void EligibilityDistinguishesNoRoadNoProducerAndCapacityShortage()
        {
            var fixture = CreateFixture(3);
            var profile = new BuildingPowerProfile(BuildingDefinitionId, new BuildingFootprint(1, 1), 4);
            var noRoad = fixture.Power.QueryEligibility(new CellCoord(2, 3), profile);
            Assert.That(noRoad.FailureReason, Is.EqualTo(PowerSupplyFailureReason.NoRoadConnection));

            var noProducer = fixture.Power.QueryEligibility(new CellCoord(2, 2), profile);
            Assert.That(noProducer.FailureReason, Is.EqualTo(PowerSupplyFailureReason.NoPowerProducer));

            fixture.Producers.Add(new PowerProducerState(new StableId(1), PlantDefinitionId, new CellCoord(0, 0), 3));
            var shortage = fixture.Power.QueryEligibility(new CellCoord(2, 2), profile);
            Assert.That(shortage.IsEligible, Is.False);
            Assert.That(shortage.FailureReason, Is.EqualTo(PowerSupplyFailureReason.InsufficientCapacity));
            Assert.That(shortage.AvailableCapacity, Is.EqualTo(3));
        }

        [Test]
        public void PlacementRemovalCommandsAndOverlayUseTheSamePowerQuery()
        {
            var fixture = CreateFixture(8);
            fixture.Buildings.Create(new StableId(10), BuildingDefinitionId, new StableId(100), new CellCoord(2, 2));
            var handler = new PowerCommandHandler(fixture.Producers, fixture.UtilityDefinitions, fixture.Roads);
            var place = new PlacePowerProducerCommand(new StableId(5), PlantDefinitionId, new CellCoord(0, 0));
            Assert.That(handler.Preview(place).IsValid, Is.True);
            Assert.That(handler.Execute(new CommandEnvelope(new CommandStamp(SimulationTick.Zero, CommandSequence.Zero), place)).IsApplied, Is.True);

            var readModel = new PowerReadModel(fixture.Power);
            var overlay = new PlacementOverlayModel(fixture.RoadReadModel, fixture.ZoneReadModel,
                fixture.ParcelReadModel, readModel).BuildPower();
            var query = fixture.Power.GetBuilding(new StableId(10));
            var overlayCell = overlay.Cells.Single(cell => cell.Cell == query.Cell);
            Assert.That(overlayCell.Category, Is.EqualTo(PlacementOverlayCategory.BuildingPowered));

            var remove = new RemovePowerProducerCommand(new StableId(5));
            Assert.That(handler.Execute(new CommandEnvelope(new CommandStamp(SimulationTick.Zero, new CommandSequence(1)), remove)).IsApplied, Is.True);
            Assert.That(fixture.Power.GetBuilding(new StableId(10)).FailureReason,
                Is.EqualTo(PowerSupplyFailureReason.NoPowerProducer));
            overlay = new PlacementOverlayModel(fixture.RoadReadModel, fixture.ZoneReadModel,
                fixture.ParcelReadModel, readModel).BuildPower();
            Assert.That(overlay.Cells.Single(cell => cell.Cell == query.Cell).Category,
                Is.EqualTo(PlacementOverlayCategory.BuildingUnpowered));
        }

        [Test]
        public void FixtureChecksumDoesNotDependOnInsertionOrder()
        {
            var first = CreateFixture(8);
            var second = CreateFixture(8);
            AddBuildings(first, new ulong[] { 30, 10, 20 });
            AddBuildings(second, new ulong[] { 20, 30, 10 });
            first.Producers.Add(new PowerProducerState(new StableId(2), PlantDefinitionId, new CellCoord(7, 0), 8));
            first.Producers.Add(new PowerProducerState(new StableId(1), PlantDefinitionId, new CellCoord(0, 0), 8));
            second.Producers.Add(new PowerProducerState(new StableId(1), PlantDefinitionId, new CellCoord(0, 0), 8));
            second.Producers.Add(new PowerProducerState(new StableId(2), PlantDefinitionId, new CellCoord(7, 0), 8));
            var checksum = first.Power.ComputeChecksum();
            TestContext.WriteLine($"P3-02 fixture checksum: {checksum:X16}");
            Assert.That(checksum, Is.EqualTo(0x58F76E9B2E77A98EUL));
            Assert.That(checksum, Is.EqualTo(second.Power.ComputeChecksum()));
        }

        [Test]
        public void ProducerStateDtoRoundTripPreservesStableOrderingAndRuntimeProduction()
        {
            var collection = new PowerProducerCollection();
            collection.Add(new PowerProducerState(new StableId(20), PlantDefinitionId, new CellCoord(2, 3), 7));
            collection.Add(new PowerProducerState(new StableId(10), PlantDefinitionId, new CellCoord(1, 3), 5));
            var restored = PowerProducerCollection.FromDto(collection.ToDto());
            Assert.That(restored.Producers.Select(value => value.Id.Value), Is.EqualTo(new ulong[] { 10, 20 }));
            Assert.That(restored.Producers.Select(value => value.CurrentProduction), Is.EqualTo(new[] { 5, 7 }));
        }

        [Test]
        public void DevelopmentQueryRequiresBothRoadEligibilityAndPower()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions { Width = 8, Height = 4 });
            Assert.That(runtime.Execute(new ZoneAreaCommand(ZoneType.ResidentialLow,
                new CellCoord(1, 3), new CellCoord(1, 3))).IsApplied, Is.True);
            Assert.That(runtime.ParcelModel.TryGetAtCell(new CellCoord(1, 3), out var parcel), Is.True);
            var buildingDefinitionId = runtime.BuildingDefinitions.Definitions[0].Id;
            var withoutRoad = runtime.DevelopmentPowerEligibility.Query(parcel.Id, buildingDefinitionId);
            Assert.That(withoutRoad.FailureReason, Is.EqualTo(DevelopmentPowerEligibilityFailureReason.RoadIneligible));

            Assert.That(runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 2), new CellCoord(2, 2))).IsApplied, Is.True);
            var withoutPower = runtime.DevelopmentPowerEligibility.Query(parcel.Id, buildingDefinitionId);
            Assert.That(withoutPower.FailureReason, Is.EqualTo(DevelopmentPowerEligibilityFailureReason.NoPowerProducer));

            var anchor = new CellCoord(0, 1);
            Assert.That(runtime.Execute(new PlacePowerProducerCommand(runtime.PowerProducerIdFor(anchor),
                runtime.DefaultPowerPlantDefinitionId, anchor)).IsApplied, Is.True);
            Assert.That(runtime.DevelopmentPowerEligibility.Query(parcel.Id, buildingDefinitionId).IsEligible, Is.True);
        }

        private static void AddBuildings(Fixture fixture, ulong[] ids)
        {
            foreach (var id in ids)
                fixture.Buildings.Create(new StableId(id), BuildingDefinitionId, new StableId(1000 + id),
                    new CellCoord((int)(id / 10) + 1, 2));
        }

        private static Fixture CreateFixture(int capacity)
        {
            var roads = new RoadNetworkState(8, 4);
            var cells = Enumerable.Range(0, 8).Select(x => new CellCoord(x, 1)).ToArray();
            Assert.That(roads.TryApplyPlacement(cells, RoadId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var buildings = new BuildingStateCollection();
            var buildingDefinitions = new BuildingDefinitionCatalog(new[]
            {
                new BuildingDefinitionData(BuildingDefinitionId, ZoneType.ResidentialLow,
                    new BuildingFootprint(1, 1), 4, 0, "building.test", 4)
            });
            var producers = new PowerProducerCollection();
            var utilities = new UtilityDefinitionCatalog(new[]
            {
                new UtilityDefinitionData(PlantDefinitionId, capacity, "utility.test")
            });
            var power = new PowerNetwork(graph, buildings, producers, buildingDefinitions, utilities);
            var zones = new ZoneNetworkState(8, 4);
            var parcels = new OpenSimCity.Simulation.Parcels.ParcelNetworkState(zones, graph);
            return new Fixture(roads, buildings, producers, utilities, power,
                new RoadAccessibilityReadModel(graph), new ZoneReadModel(zones), new ParcelReadModel(parcels));
        }

        private sealed class Fixture
        {
            public Fixture(RoadNetworkState roads, BuildingStateCollection buildings,
                PowerProducerCollection producers, UtilityDefinitionCatalog utilityDefinitions,
                PowerNetwork power, RoadAccessibilityReadModel roadReadModel,
                ZoneReadModel zoneReadModel, ParcelReadModel parcelReadModel)
            { Roads = roads; Buildings = buildings; Producers = producers; UtilityDefinitions = utilityDefinitions;
                Power = power; RoadReadModel = roadReadModel; ZoneReadModel = zoneReadModel; ParcelReadModel = parcelReadModel; }
            public RoadNetworkState Roads { get; }
            public BuildingStateCollection Buildings { get; }
            public PowerProducerCollection Producers { get; }
            public UtilityDefinitionCatalog UtilityDefinitions { get; }
            public PowerNetwork Power { get; }
            public RoadAccessibilityReadModel RoadReadModel { get; }
            public ZoneReadModel ZoneReadModel { get; }
            public ParcelReadModel ParcelReadModel { get; }
        }
    }
}
