using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TripDemandSystemTests
    {
        private static readonly StableId ResidentialDefinitionId = new StableId(1UL);
        private static readonly StableId CommercialDefinitionId = new StableId(2UL);
        private static readonly StableId IndustrialDefinitionId = new StableId(3UL);
        private static readonly StableId ResidentialBuildingId = new StableId(101UL);
        private static readonly StableId CommercialBuildingId = new StableId(102UL);
        private static readonly StableId IndustrialBuildingId = new StableId(103UL);
        private static readonly StableId GateId = new StableId(201UL);

        [Test]
        public void ConnectedBuildingsGenerateConservedCommuteAndGoodsRows()
        {
            var fixture = CreateConnectedFixture(reverseInsertion: false);
            fixture.Run(new SimulationDate(1, 1));

            Assert.That(fixture.Trips.State.CommuteRequested, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.CommuteMatched, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.CommuteUnmet, Is.Zero);
            Assert.That(fixture.Trips.State.GoodsRequested, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.GoodsMatched, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.GoodsUnmet, Is.Zero);

            Assert.That(fixture.Trips.State.Rows.Count(row =>
                row.Kind == TripDemandKind.Commute && row.Status == TripDemandStatus.Matched), Is.EqualTo(2));
            Assert.That(fixture.Trips.State.Rows.Count(row =>
                row.Kind == TripDemandKind.Goods && row.Origin.Kind == TripEndpointKind.Building), Is.EqualTo(1));
            Assert.That(fixture.Trips.State.Rows.Count(row =>
                row.Kind == TripDemandKind.Goods && row.Origin.Kind == TripEndpointKind.ExternalRoadGate), Is.EqualTo(1));
            Assert.That(fixture.Trips.State.Rows.All(row => row.UnmetReason == TripDemandUnmetReason.None), Is.True);
        }

        [Test]
        public void DisconnectedComponentsKeepDemandAndExplainEveryUnmetCommute()
        {
            var fixture = CreateConnectedFixture(
                reverseInsertion: false,
                residentialComponent: 1,
                employmentComponent: 9,
                gateComponent: 9);
            fixture.Run(new SimulationDate(1, 1));

            Assert.That(fixture.Trips.State.CommuteRequested, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.CommuteMatched, Is.Zero);
            Assert.That(fixture.Trips.State.CommuteUnmet, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.Rows.Where(row => row.Kind == TripDemandKind.Commute)
                .All(row => row.UnmetReason == TripDemandUnmetReason.DifferentRoadComponent), Is.True);

            Assert.That(fixture.Trips.State.GoodsRequested, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.GoodsMatched, Is.EqualTo(4));
        }

        [Test]
        public void MissingOriginAndDestinationRoadAccessUseDistinctReasons()
        {
            var missingOrigin = CreateConnectedFixture(
                reverseInsertion: false,
                residentialComponent: -1,
                employmentComponent: 5,
                gateComponent: 5);
            missingOrigin.Run(new SimulationDate(1, 1));
            Assert.That(missingOrigin.Trips.State.Rows.Where(row => row.Kind == TripDemandKind.Commute)
                .Select(row => row.UnmetReason), Is.EquivalentTo(new[]
                {
                    TripDemandUnmetReason.NoOriginRoadAccess
                }));

            var missingDestination = CreateConnectedFixture(
                reverseInsertion: false,
                residentialComponent: 5,
                employmentComponent: -1,
                gateComponent: 5);
            missingDestination.Run(new SimulationDate(1, 1));
            Assert.That(missingDestination.Trips.State.Rows.Where(row => row.Kind == TripDemandKind.Commute)
                .Select(row => row.UnmetReason), Is.EquivalentTo(new[]
                {
                    TripDemandUnmetReason.NoDestinationRoadAccess
                }));
        }

        [Test]
        public void IndustrialSurplusWithoutRoadGateIsRetainedAsExplicitUnmetExport()
        {
            var buildings = new BuildingStateCollection();
            buildings.Create(
                IndustrialBuildingId,
                IndustrialDefinitionId,
                new StableId(303UL),
                new CellCoord(2, 2),
                filledJobs: 3);
            var profiles = new FakeProfiles(
                new UrbanEconomyProfile(IndustrialDefinitionId, EconomicSector.Industrial, 0, 3));
            var access = new FakeAccessResolver(
                new Dictionary<StableId, TripEndpoint>
                {
                    [IndustrialBuildingId] = BuildingEndpoint(IndustrialBuildingId, 4, 2, 2)
                },
                Array.Empty<TripEndpoint>());
            var economy = new UrbanEconomySystem(buildings, profiles);
            var trips = new TripDemandSystem(buildings, profiles, access, economy);

            economy.RunMonth(new SimulationDate(1, 1));
            trips.RunMonth(new SimulationDate(1, 1));

            Assert.That(economy.Market.GoodsSurplus, Is.EqualTo(3));
            Assert.That(trips.State.GoodsRequested, Is.EqualTo(3));
            Assert.That(trips.State.GoodsMatched, Is.Zero);
            Assert.That(trips.State.Rows, Has.Count.EqualTo(1));
            Assert.That(trips.State.Rows[0].UnmetReason,
                Is.EqualTo(TripDemandUnmetReason.NoEnabledExternalRoadGate));
        }

        [Test]
        public void EmploymentChangeRecomputesNextMonthWithoutCitizenAgents()
        {
            var fixture = CreateConnectedFixture(reverseInsertion: false);
            fixture.Run(new SimulationDate(1, 1));
            Assert.That(fixture.Trips.State.CommuteMatched, Is.EqualTo(4));

            fixture.Buildings.Replace(
                fixture.Buildings.Get(CommercialBuildingId).WithCounts(0, 0));
            fixture.Buildings.Replace(
                fixture.Buildings.Get(IndustrialBuildingId).WithCounts(0, 1));
            fixture.Run(new SimulationDate(1, 2));

            Assert.That(fixture.Trips.State.CommuteRequested, Is.EqualTo(4));
            Assert.That(fixture.Trips.State.CommuteMatched, Is.EqualTo(1));
            Assert.That(fixture.Trips.State.CommuteUnmet, Is.EqualTo(3));
            Assert.That(fixture.Trips.State.Rows.Any(row =>
                row.Kind == TripDemandKind.Commute &&
                row.UnmetReason == TripDemandUnmetReason.NoDestinationCapacity &&
                row.Amount == 3), Is.True);
        }

        [Test]
        public void StableIdsMakeInsertionOrderAndReadObservationChecksumIndependent()
        {
            var forward = CreateConnectedFixture(reverseInsertion: false);
            var reverse = CreateConnectedFixture(reverseInsertion: true);
            var month = new SimulationDate(2, 4);
            forward.Run(month);
            reverse.Run(month);

            var beforeObservation = reverse.Trips.State.ComputeChecksum();
            var model = new TripDemandReadModel(reverse.Trips, chunkSize: 2);
            _ = model.Rows.Count;
            _ = model.CommuteMatched;
            _ = model.GoodsMatched;
            _ = model.Checksum;

            Assert.That(reverse.Trips.State.ComputeChecksum(), Is.EqualTo(beforeObservation));
            Assert.That(reverse.Trips.State.ComputeChecksum(), Is.EqualTo(forward.Trips.State.ComputeChecksum()));
            Assert.That(model.Rows.Select(row => row.Amount),
                Is.EqualTo(forward.Trips.State.Rows.Select(row => row.Amount)));
            Assert.That(model.Rows.Any(row => row.OriginChunk != ChunkCoord.Zero), Is.True);
        }

        [Test]
        public void LargeAggregateFixtureIsDeterministicAndDoesNotReferenceUnityPresentation()
        {
            var forward = CreateLargeFixture(reverseInsertion: false);
            var reverse = CreateLargeFixture(reverseInsertion: true);
            var month = new SimulationDate(7, 8);
            forward.Run(month);
            reverse.Run(month);

            Assert.That(forward.Trips.State.CommuteRequested, Is.EqualTo(800));
            Assert.That(forward.Trips.State.CommuteMatched, Is.EqualTo(400));
            Assert.That(forward.Trips.State.GoodsRequested, Is.EqualTo(800));
            Assert.That(forward.Trips.State.GoodsMatched, Is.EqualTo(400));
            Assert.That(forward.Trips.State.ComputeChecksum(), Is.EqualTo(reverse.Trips.State.ComputeChecksum()));

            var assemblyReferences = typeof(TripDemandSystem).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine"));
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        [Test]
        public void TripDemandRejectsOutOfOrderOrMissingEconomyPass()
        {
            var fixture = CreateConnectedFixture(reverseInsertion: false);
            var month = new SimulationDate(1, 1);

            Assert.That(() => fixture.Trips.RunMonth(month), Throws.InvalidOperationException);
            fixture.Run(month);
            Assert.That(() => fixture.Trips.RunMonth(month), Throws.InvalidOperationException);
        }

        [Test]
        public void RuntimeSnapshotRebuildsIdenticalOdStateWithoutSchemaField()
        {
            var runtime = NewCityRuntimeFactory.Create(RuntimeOptions());
            ApplyPublicGrowthLayout(runtime);
            for (var month = 0; month < 6; month++)
                RunRuntimeMonth(runtime, month);

            Assert.That(runtime.TripDemandSystem.State.HasProcessedMonth, Is.True);
            Assert.That(runtime.TripDemandSystem.State.CommuteRequested, Is.GreaterThan(0));
            Assert.That(runtime.TripDemandSystem.State.GoodsRequested, Is.GreaterThan(0));
            var expectedChecksum = runtime.TripDemandSystem.State.ComputeChecksum();
            var expectedRows = runtime.TripDemandSystem.State.Rows.Count;

            var json = JsonUtility.ToJson(CitySnapshotSerializer.Capture(runtime));
            Assert.That(json, Does.Not.Contain("TripDemand"));
            var dto = JsonUtility.FromJson<CitySnapshotDto>(json);
            var restored = CitySnapshotSerializer.Restore(dto, RuntimeOptions());

            Assert.That(restored.TripDemandSystem.State.HasProcessedMonth, Is.True);
            Assert.That(restored.TripDemandSystem.State.Rows.Count, Is.EqualTo(expectedRows));
            Assert.That(restored.TripDemandSystem.State.ComputeChecksum(), Is.EqualTo(expectedChecksum));
            Assert.That(restored.TripDemandModel.Checksum, Is.EqualTo(expectedChecksum));
        }

        private static Fixture CreateConnectedFixture(
            bool reverseInsertion,
            int residentialComponent = 7,
            int employmentComponent = 7,
            int gateComponent = 7)
        {
            var buildings = new BuildingStateCollection();
            var states = new[]
            {
                new BuildingState(
                    ResidentialBuildingId,
                    ResidentialDefinitionId,
                    new StableId(301UL),
                    new CellCoord(2, 2),
                    occupancy: 4),
                new BuildingState(
                    CommercialBuildingId,
                    CommercialDefinitionId,
                    new StableId(302UL),
                    new CellCoord(4, 2),
                    filledJobs: 2),
                new BuildingState(
                    IndustrialBuildingId,
                    IndustrialDefinitionId,
                    new StableId(303UL),
                    new CellCoord(6, 2),
                    filledJobs: 2)
            };
            if (reverseInsertion)
                Array.Reverse(states);
            for (var index = 0; index < states.Length; index++)
                buildings.Add(states[index]);

            var profiles = new FakeProfiles(
                new UrbanEconomyProfile(ResidentialDefinitionId, EconomicSector.Residential, 4, 0),
                new UrbanEconomyProfile(CommercialDefinitionId, EconomicSector.Commercial, 0, 4),
                new UrbanEconomyProfile(IndustrialDefinitionId, EconomicSector.Industrial, 0, 2));
            var access = new FakeAccessResolver(
                new Dictionary<StableId, TripEndpoint>
                {
                    [ResidentialBuildingId] = BuildingEndpoint(ResidentialBuildingId, residentialComponent, 2, 1),
                    [CommercialBuildingId] = BuildingEndpoint(CommercialBuildingId, employmentComponent, 4, 1),
                    [IndustrialBuildingId] = BuildingEndpoint(IndustrialBuildingId, employmentComponent, 6, 1)
                },
                new[] { GateEndpoint(GateId, gateComponent, 0, 1) });
            var economy = new UrbanEconomySystem(buildings, profiles);
            var trips = new TripDemandSystem(buildings, profiles, access, economy);
            return new Fixture(buildings, economy, trips);
        }

        private static Fixture CreateLargeFixture(bool reverseInsertion)
        {
            var buildings = new BuildingStateCollection();
            var states = new List<BuildingState>();
            var accessByBuilding = new Dictionary<StableId, TripEndpoint>();
            for (var index = 0; index < 200; index++)
            {
                var id = new StableId((ulong)(1_000 + index));
                states.Add(new BuildingState(
                    id,
                    ResidentialDefinitionId,
                    new StableId((ulong)(10_000 + index)),
                    new CellCoord(index % 20, 2 + index / 20),
                    occupancy: 4));
                accessByBuilding.Add(id, BuildingEndpoint(id, 3, index % 20, 1));
            }
            for (var index = 0; index < 100; index++)
            {
                var id = new StableId((ulong)(2_000 + index));
                states.Add(new BuildingState(
                    id,
                    CommercialDefinitionId,
                    new StableId((ulong)(20_000 + index)),
                    new CellCoord(index % 20, 20 + index / 20),
                    filledJobs: 2));
                accessByBuilding.Add(id, BuildingEndpoint(id, 3, index % 20, 19));
            }
            for (var index = 0; index < 100; index++)
            {
                var id = new StableId((ulong)(3_000 + index));
                states.Add(new BuildingState(
                    id,
                    IndustrialDefinitionId,
                    new StableId((ulong)(30_000 + index)),
                    new CellCoord(index % 20, 30 + index / 20),
                    filledJobs: 2));
                accessByBuilding.Add(id, BuildingEndpoint(id, 3, index % 20, 29));
            }

            if (reverseInsertion)
                states.Reverse();
            for (var index = 0; index < states.Count; index++)
                buildings.Add(states[index]);

            var profiles = new FakeProfiles(
                new UrbanEconomyProfile(ResidentialDefinitionId, EconomicSector.Residential, 4, 0),
                new UrbanEconomyProfile(CommercialDefinitionId, EconomicSector.Commercial, 0, 4),
                new UrbanEconomyProfile(IndustrialDefinitionId, EconomicSector.Industrial, 0, 2));
            var access = new FakeAccessResolver(
                accessByBuilding,
                new[] { GateEndpoint(GateId, 3, 0, 0) });
            var economy = new UrbanEconomySystem(buildings, profiles);
            var trips = new TripDemandSystem(buildings, profiles, access, economy);
            return new Fixture(buildings, economy, trips);
        }

        private static TripEndpoint BuildingEndpoint(StableId id, int component, int x, int y)
        {
            return new TripEndpoint(
                TripEndpointKind.Building,
                id,
                new CellCoord(x, y),
                component);
        }

        private static TripEndpoint GateEndpoint(StableId id, int component, int x, int y)
        {
            return new TripEndpoint(
                TripEndpointKind.ExternalRoadGate,
                id,
                new CellCoord(x, y),
                component);
        }

        private static CityRuntimeOptions RuntimeOptions()
        {
            return new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x501UL,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1),
                InitialFunds = 100_000
            };
        }

        private static void ApplyPublicGrowthLayout(CityRuntime runtime)
        {
            var roadY = runtime.Height / 2;
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(15, roadY))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(2, roadY - 2),
                new CellCoord(3, roadY - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.CommercialLow,
                new CellCoord(6, roadY - 2),
                new CellCoord(7, roadY - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new CellCoord(10, roadY - 2),
                new CellCoord(11, roadY - 1))));
            var powerCell = new CellCoord(15, roadY - 1);
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(powerCell),
                runtime.DefaultPowerPlantDefinitionId,
                powerCell)));
        }

        private static void RunRuntimeMonth(CityRuntime runtime, int monthIndex)
        {
            Assert.That(runtime.Clock.CurrentTick.Value,
                Is.EqualTo((long)monthIndex * runtime.MonthlyScheduler.TicksPerMonth));
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                Assert.That(runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds), Is.EqualTo(1));
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }

        private sealed class Fixture
        {
            public Fixture(
                BuildingStateCollection buildings,
                UrbanEconomySystem economy,
                TripDemandSystem trips)
            {
                Buildings = buildings;
                Economy = economy;
                Trips = trips;
            }

            public BuildingStateCollection Buildings { get; }
            public UrbanEconomySystem Economy { get; }
            public TripDemandSystem Trips { get; }

            public void Run(SimulationDate month)
            {
                Economy.RunMonth(month);
                Trips.RunMonth(month);
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
            private readonly IReadOnlyDictionary<StableId, TripEndpoint> _buildingAccess;
            private readonly IReadOnlyList<TripEndpoint> _gates;

            public FakeAccessResolver(
                IReadOnlyDictionary<StableId, TripEndpoint> buildingAccess,
                IEnumerable<TripEndpoint> gates)
            {
                _buildingAccess = buildingAccess;
                _gates = gates.OrderBy(gate => gate.Id).ToArray();
            }

            public TripEndpoint ResolveBuilding(BuildingState building)
            {
                return _buildingAccess[building.Id];
            }

            public IReadOnlyList<TripEndpoint> GetEnabledExternalRoadGates()
            {
                return _gates;
            }
        }
    }
}
