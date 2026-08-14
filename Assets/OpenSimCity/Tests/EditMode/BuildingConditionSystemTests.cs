using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Conditions;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingConditionSystemTests
    {
        [Test]
        public void AdverseEnvironmentTransitionsWithExplicitMonthlyDelay()
        {
            var fixture = CreateFixture(BadEnvironment());

            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Healthy));
            Assert.That(fixture.State.AdverseMonths, Is.EqualTo(1));

            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Stressed));
            Assert.That(fixture.System.ResolveHousingCapacity(fixture.Building, 10), Is.EqualTo(8));

            fixture.RunMonths(2);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Declining));
            Assert.That(fixture.System.ResolveHousingCapacity(fixture.Building, 10), Is.EqualTo(5));

            fixture.RunMonths(3);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Abandoned));
            Assert.That(fixture.System.ResolveHousingCapacity(fixture.Building, 10), Is.Zero);
            Assert.That(fixture.State.TopReason, Is.EqualTo(BuildingConditionReason.Pollution));
        }

        [Test]
        public void DecliningBuildingRequiresDelayedRecoveryBeforeHealthy()
        {
            var fixture = CreateFixture(BadEnvironment());
            fixture.RunMonths(4);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Declining));

            fixture.Environment.Value = GoodEnvironment();
            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Declining));
            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Stressed));
            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Stressed));
            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Healthy));
        }

        [Test]
        public void AbandonedBuildingRecoversEligibilityThenRedevelopsWithSameStableId()
        {
            var fixture = CreateFixture(BadEnvironment());
            var buildingId = fixture.Building.Id;
            fixture.RunMonths(7);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Abandoned));

            fixture.Environment.Value = GoodEnvironment();
            fixture.RunMonths(1);
            Assert.That(fixture.State.IsRedevelopmentEligible, Is.False);
            Assert.That(fixture.State.RedevelopmentBlockReason,
                Is.EqualTo(RedevelopmentBlockReason.RecoveryDelay));
            fixture.RunMonths(1);
            Assert.That(fixture.State.IsRedevelopmentEligible, Is.True);
            Assert.That(fixture.State.RedevelopmentBlockReason, Is.EqualTo(RedevelopmentBlockReason.None));

            Assert.That(fixture.System.TryRequestRedevelopment(buildingId, out var reason), Is.True);
            Assert.That(reason, Is.EqualTo(RedevelopmentBlockReason.None));
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Redeveloping));
            Assert.That(fixture.State.RedevelopmentMonthsRemaining, Is.EqualTo(3));

            fixture.RunMonths(2);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Redeveloping));
            Assert.That(fixture.State.RedevelopmentMonthsRemaining, Is.EqualTo(1));
            fixture.RunMonths(1);
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Healthy));
            Assert.That(fixture.State.BuildingId, Is.EqualTo(buildingId));
            Assert.That(fixture.Buildings.Buildings.Single().Id, Is.EqualTo(buildingId));
        }

        [Test]
        public void RoadPowerAndFactorTiesHaveStableActionableOrdering()
        {
            var fixture = CreateFixture(new BuildingConditionEnvironment(
                landValue: 500,
                pollution: 0,
                noise: 0,
                crime: 0,
                hasRoadAccess: false,
                hasPower: false));
            fixture.RunMonths(1);

            Assert.That(fixture.State.LastAdverseScore, Is.EqualTo(1000));
            Assert.That(fixture.State.Factors.Select(factor => factor.Reason), Is.EqualTo(new[]
            {
                BuildingConditionReason.NoRoadAccess,
                BuildingConditionReason.NoPower
            }));
            Assert.That(fixture.State.TopReason, Is.EqualTo(BuildingConditionReason.NoRoadAccess));
        }

        [Test]
        public void SnapshotRoundTripPreservesTimelineAndNextMonthResult()
        {
            var source = CreateFixture(BadEnvironment());
            source.RunMonths(5);
            var dto = source.System.ToDto();
            var checksum = source.System.ComputeChecksum();

            var restored = CreateFixture(BadEnvironment());
            restored.System.RestoreFromDto(dto);
            restored.NextMonthIndex = 5;
            Assert.That(restored.System.ComputeChecksum(), Is.EqualTo(checksum));

            source.RunMonths(1);
            restored.RunMonths(1);
            Assert.That(restored.System.ComputeChecksum(), Is.EqualTo(source.System.ComputeChecksum()));
            Assert.That(restored.State.Stage, Is.EqualTo(source.State.Stage));
        }

        [Test]
        public void PreCoreConditionCapacityForcesPopulationDownInSameMonth()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x620UL,
                TicksPerMonth = 1,
                InitialFunds = 10_000
            });
            var definition = runtime.BuildingDefinitions.Definitions
                .First(item => item.ZoneType == ZoneType.ResidentialLow);
            var building = new BuildingState(
                new StableId(0xB200000000000001UL),
                definition.Id,
                new StableId(0xA200000000000001UL),
                new CellCoord(2, 2),
                definition.Footprint,
                definition.OccupancyCapacity,
                0);
            runtime.Buildings.Add(building);

            var environment = new MutableEnvironmentResolver(BadEnvironment());
            var profiles = new SingleProfileResolver(
                definition.Id,
                new BuildingConditionProfile(
                    BuildingConditionUse.Residential,
                    definition.OccupancyCapacity,
                    0));
            var conditions = new BuildingConditionSystem(
                runtime.Buildings,
                profiles,
                environment);
            runtime.PopulationSystem.CapacityResolver = conditions;
            var scheduler = new MonthlySimulationScheduler(
                new IMonthlySimulationSystem[] { runtime.PopulationSystem },
                ticksPerMonth: 1,
                firstMonth: new SimulationDate(1, 1));
            scheduler.RegisterPreCoreSystem(conditions);

            for (var month = 0; month < 2; month++)
                scheduler.ProcessTick(new SimulationTick(month));
            Assert.That(conditions.State.States.Single().Stage,
                Is.EqualTo(BuildingConditionStage.Stressed));
            Assert.That(runtime.Buildings.Buildings.Single().Occupancy,
                Is.EqualTo(definition.OccupancyCapacity * 800 / 1000));

            for (var month = 2; month < 7; month++)
                scheduler.ProcessTick(new SimulationTick(month));
            Assert.That(conditions.State.States.Single().Stage,
                Is.EqualTo(BuildingConditionStage.Abandoned));
            Assert.That(runtime.Buildings.Buildings.Single().Occupancy, Is.Zero);
            Assert.That(runtime.PopulationSystem.Population.TotalPopulation, Is.Zero);
        }

        [Test]
        public void ReadProjectionAndOverlayObservationDoNotMutateConditionChecksum()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x621UL
            });
            var definition = runtime.BuildingDefinitions.Definitions
                .First(item => item.ZoneType == ZoneType.ResidentialLow);
            runtime.Buildings.Add(new BuildingState(
                new StableId(0xB200000000000002UL),
                definition.Id,
                new StableId(0xA200000000000002UL),
                new CellCoord(2, 2),
                definition.Footprint,
                definition.OccupancyCapacity,
                0));
            var module = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            module.System.RunMonth(new SimulationDate(1, 1));
            var checksum = module.System.ComputeChecksum();

            module.ReadModel.RefreshIfNeeded();
            module.Overlay.RefreshIfNeeded();
            var row = module.ReadModel.Snapshot.Buildings.Single();
            var cells = module.Overlay.Cells;

            Assert.That(row.BuildingId, Is.EqualTo(runtime.Buildings.Buildings.Single().Id));
            Assert.That(cells.Count,
                Is.EqualTo(definition.Footprint.Width * definition.Footprint.Height));
            Assert.That(module.System.ComputeChecksum(), Is.EqualTo(checksum));
        }

        private static Fixture CreateFixture(BuildingConditionEnvironment environment)
        {
            var buildings = new BuildingStateCollection();
            var definitionId = new StableId(0xB210000000000001UL);
            var building = new BuildingState(
                new StableId(0xB220000000000001UL),
                definitionId,
                new StableId(0xA220000000000001UL),
                new CellCoord(1, 1),
                new BuildingFootprint(1, 1),
                10,
                0);
            buildings.Add(building);
            var resolver = new MutableEnvironmentResolver(environment);
            var system = new BuildingConditionSystem(
                buildings,
                new SingleProfileResolver(
                    definitionId,
                    new BuildingConditionProfile(BuildingConditionUse.Residential, 10, 0)),
                resolver);
            return new Fixture(buildings, building, system, resolver);
        }

        private static BuildingConditionEnvironment BadEnvironment()
        {
            return new BuildingConditionEnvironment(
                landValue: 100,
                pollution: 900,
                noise: 800,
                crime: 700,
                hasRoadAccess: true,
                hasPower: true);
        }

        private static BuildingConditionEnvironment GoodEnvironment()
        {
            return new BuildingConditionEnvironment(
                landValue: 650,
                pollution: 0,
                noise: 0,
                crime: 0,
                hasRoadAccess: true,
                hasPower: true);
        }

        private sealed class Fixture
        {
            public Fixture(
                BuildingStateCollection buildings,
                BuildingState building,
                BuildingConditionSystem system,
                MutableEnvironmentResolver environment)
            {
                Buildings = buildings;
                Building = building;
                System = system;
                Environment = environment;
            }

            public BuildingStateCollection Buildings { get; }
            public BuildingState Building { get; }
            public BuildingConditionSystem System { get; }
            public MutableEnvironmentResolver Environment { get; }
            public int NextMonthIndex { get; set; }
            public BuildingConditionState State => System.State.States.Single();

            public void RunMonths(int count)
            {
                for (var index = 0; index < count; index++)
                {
                    var absolute = NextMonthIndex++;
                    System.RunMonth(new SimulationDate(1 + absolute / 12, absolute % 12 + 1));
                }
            }
        }

        private sealed class SingleProfileResolver : IBuildingConditionProfileResolver
        {
            private readonly StableId _definitionId;
            private readonly BuildingConditionProfile _profile;

            public SingleProfileResolver(StableId definitionId, BuildingConditionProfile profile)
            {
                _definitionId = definitionId;
                _profile = profile;
            }

            public bool TryGetConditionProfile(StableId definitionId, out BuildingConditionProfile profile)
            {
                if (definitionId == _definitionId)
                {
                    profile = _profile;
                    return true;
                }
                profile = default;
                return false;
            }
        }

        private sealed class MutableEnvironmentResolver : IBuildingConditionEnvironmentResolver
        {
            public MutableEnvironmentResolver(BuildingConditionEnvironment value)
            {
                Value = value;
            }

            public BuildingConditionEnvironment Value { get; set; }
            public BuildingConditionEnvironment Resolve(BuildingState building) => Value;
        }
    }
}
