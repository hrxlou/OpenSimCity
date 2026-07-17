using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Population;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PopulationEmploymentMigrationTests
    {
        [Test]
        public void AggregatesPopulationAndMatchesJobsInStableBuildingOrder()
        {
            var fixture = CreateFixture(
                enabledConnection: false,
                new BuildingSpec(30, 3, 0, 4, 0),
                new BuildingSpec(10, 1, 5, 0, 5),
                new BuildingSpec(20, 2, 0, 3, 0),
                new BuildingSpec(40, 4, 0, 3, 0));

            fixture.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(fixture.System.Population.TotalPopulation, Is.EqualTo(5));
            Assert.That(fixture.System.Population.LaborForce, Is.EqualTo(5));
            Assert.That(fixture.System.Employment.AvailableJobs, Is.EqualTo(10));
            Assert.That(fixture.System.Employment.EmployedWorkers, Is.EqualTo(5));
            Assert.That(fixture.System.Employment.UnemployedWorkers, Is.Zero);
            Assert.That(fixture.System.Employment.VacantJobs, Is.EqualTo(5));
            Assert.That(
                fixture.System.Employment.Buildings.Select(row => row.BuildingId.Value),
                Is.EqualTo(new ulong[] { 20, 30, 40 }));
            Assert.That(
                fixture.System.Employment.Buildings.Select(row => row.FilledJobs),
                Is.EqualTo(new[] { 3, 2, 0 }));
            Assert.That(fixture.Buildings.Get(Id(20)).FilledJobs, Is.EqualTo(3));
            Assert.That(fixture.Buildings.Get(Id(30)).FilledJobs, Is.EqualTo(2));
        }

        [Test]
        public void EnabledExternalConnectionAppliesHousingAndJobMigrationPressure()
        {
            var enabled = CreateFixture(
                enabledConnection: true,
                new BuildingSpec(10, 1, 0, 0, 6),
                new BuildingSpec(20, 2, 0, 4, 0));
            var blocked = CreateFixture(
                enabledConnection: false,
                new BuildingSpec(10, 1, 0, 0, 6),
                new BuildingSpec(20, 2, 0, 4, 0));

            enabled.System.RunMonth(new SimulationDate(1, 1));
            blocked.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(enabled.System.Migration.InboundPressure, Is.EqualTo(4));
            Assert.That(enabled.System.Migration.Arrivals, Is.EqualTo(4));
            Assert.That(enabled.System.Migration.NetMigration, Is.EqualTo(4));
            Assert.That(enabled.System.Population.TotalPopulation, Is.EqualTo(4));
            Assert.That(enabled.System.Employment.EmployedWorkers, Is.EqualTo(4));
            Assert.That(blocked.System.Migration.HasExternalConnection, Is.False);
            Assert.That(blocked.System.Migration.Arrivals, Is.Zero);
            Assert.That(blocked.System.Migration.Departures, Is.Zero);
            Assert.That(blocked.System.Population.TotalPopulation, Is.Zero);
        }

        [Test]
        public void CapacityReductionClampsOccupancyAndReportsForcedDepartures()
        {
            var fixture = CreateFixture(
                enabledConnection: false,
                new BuildingSpec(10, 1, 6, 0, 6));
            fixture.Profiles.Set(Id(1), housingCapacity: 3, jobCapacity: 0);

            fixture.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(fixture.System.Population.TotalPopulation, Is.EqualTo(3));
            Assert.That(fixture.System.Population.VacantHousing, Is.Zero);
            Assert.That(fixture.System.Migration.Departures, Is.EqualTo(3));
            Assert.That(fixture.Buildings.Get(Id(10)).Occupancy, Is.EqualTo(3));
            Assert.That(fixture.Buildings.Get(Id(10)).FilledJobs, Is.Zero);
        }

        [Test]
        public void BlockedExternalConnectionPreservesUnemployedResidents()
        {
            var fixture = CreateFixture(
                enabledConnection: false,
                new BuildingSpec(10, 1, 5, 0, 5),
                new BuildingSpec(20, 2, 0, 2, 0));

            fixture.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(fixture.System.Employment.EmployedWorkers, Is.EqualTo(2));
            Assert.That(fixture.System.Employment.UnemployedWorkers, Is.EqualTo(3));
            Assert.That(fixture.System.Migration.OutboundPressure, Is.EqualTo(3));
            Assert.That(fixture.System.Migration.Departures, Is.Zero);
            Assert.That(fixture.System.Population.TotalPopulation, Is.EqualTo(5));
        }

        [Test]
        public void EmptyCityAndReadModelDtoRemainValidAndSerializable()
        {
            var fixture = CreateFixture(enabledConnection: true);
            var readModel = new PopulationEmploymentReadModel(fixture.System);

            fixture.System.RunMonth(new SimulationDate(2040, 12));
            Assert.That(readModel.RefreshIfNeeded(), Is.True);
            var json = JsonUtility.ToJson(readModel.Snapshot);
            var restored = JsonUtility.FromJson<PopulationEmploymentSnapshotDto>(json);

            Assert.That(restored.Date, Is.EqualTo(new SimulationDate(2040, 12)));
            Assert.That(restored.Population.TotalPopulation, Is.Zero);
            Assert.That(restored.Employment.EmployedWorkers, Is.Zero);
            Assert.That(restored.Migration.NetMigration, Is.Zero);
            Assert.That(restored.Population.Buildings, Is.Empty);
        }

        [Test]
        public void MonthlyBoundaryRunsExactlyOnceAcrossSpeedAndPauseChanges()
        {
            var fixture = CreateFixture(enabledConnection: false);
            var scheduler = new MonthlySimulationScheduler(
                fixture.System,
                ticksPerMonth: 3,
                firstMonth: new SimulationDate(2000, 12));
            var clock = new SimulationClock(0.1d, 16);
            var runner = new SimulationRunner(
                clock,
                new CommandQueue(),
                new CommandDispatcher(Array.Empty<ICityCommandHandler>()),
                new[] { scheduler });

            clock.Speed = SimulationSpeed.Two;
            Assert.That(runner.Advance(0.1d), Is.EqualTo(2));
            Assert.That(scheduler.CompletedMonths, Is.Zero);
            clock.Speed = SimulationSpeed.Paused;
            Assert.That(runner.Advance(5d), Is.Zero);
            Assert.That(scheduler.CompletedMonths, Is.Zero);
            clock.Speed = SimulationSpeed.Four;
            Assert.That(runner.Advance(0.1d), Is.EqualTo(4));

            Assert.That(scheduler.CompletedMonths, Is.EqualTo(2));
            Assert.That(fixture.System.LastProcessedMonth, Is.EqualTo(new SimulationDate(2001, 1)));
            Assert.That(scheduler.NextMonth, Is.EqualTo(new SimulationDate(2001, 2)));
        }

        [Test]
        public void SameLogicalInputProducesIdenticalMonthlySnapshotRegardlessOfInsertionOrder()
        {
            var first = CreateFixture(
                enabledConnection: true,
                new BuildingSpec(30, 3, 0, 5, 0),
                new BuildingSpec(10, 1, 2, 0, 6),
                new BuildingSpec(20, 2, 0, 2, 0));
            var second = CreateFixture(
                enabledConnection: true,
                new BuildingSpec(20, 2, 0, 2, 0),
                new BuildingSpec(10, 1, 2, 0, 6),
                new BuildingSpec(30, 3, 0, 5, 0));

            first.System.RunMonth(new SimulationDate(1, 1));
            second.System.RunMonth(new SimulationDate(1, 1));
            var firstJson = JsonUtility.ToJson(new PopulationEmploymentReadModel(first.System).Snapshot);
            var secondJson = JsonUtility.ToJson(new PopulationEmploymentReadModel(second.System).Snapshot);

            Assert.That(secondJson, Is.EqualTo(firstJson));
        }

        private static Fixture CreateFixture(bool enabledConnection, params BuildingSpec[] specs)
        {
            var buildings = new BuildingStateCollection();
            var profiles = new MutableProfileResolver();
            foreach (var spec in specs)
            {
                profiles.Set(Id(spec.DefinitionId), spec.HousingCapacity, spec.JobCapacity);
                buildings.Create(
                    Id(spec.BuildingId),
                    Id(spec.DefinitionId),
                    Id(1000 + spec.BuildingId),
                    new CellCoord((int)spec.BuildingId, 0),
                    spec.Occupancy);
            }

            var connections = new[]
            {
                new ExternalConnectionState(Id(900), ExternalConnectionKind.Road, new CellCoord(0, 0), enabledConnection)
            };
            return new Fixture(
                buildings,
                profiles,
                new PopulationEmploymentMigrationSystem(buildings, profiles, connections));
        }

        private static StableId Id(ulong value) => new StableId(value);

        private readonly struct BuildingSpec
        {
            public BuildingSpec(
                ulong buildingId,
                ulong definitionId,
                int occupancy,
                int jobCapacity,
                int housingCapacity)
            {
                BuildingId = buildingId;
                DefinitionId = definitionId;
                Occupancy = occupancy;
                JobCapacity = jobCapacity;
                HousingCapacity = housingCapacity;
            }

            public ulong BuildingId { get; }
            public ulong DefinitionId { get; }
            public int Occupancy { get; }
            public int JobCapacity { get; }
            public int HousingCapacity { get; }
        }

        private sealed class Fixture
        {
            public Fixture(
                BuildingStateCollection buildings,
                MutableProfileResolver profiles,
                PopulationEmploymentMigrationSystem system)
            {
                Buildings = buildings;
                Profiles = profiles;
                System = system;
            }

            public BuildingStateCollection Buildings { get; }
            public MutableProfileResolver Profiles { get; }
            public PopulationEmploymentMigrationSystem System { get; }
        }

        private sealed class MutableProfileResolver : IPopulationEmploymentProfileResolver
        {
            private readonly Dictionary<StableId, PopulationEmploymentProfile> _profiles =
                new Dictionary<StableId, PopulationEmploymentProfile>();

            public void Set(StableId definitionId, int housingCapacity, int jobCapacity)
            {
                _profiles[definitionId] = new PopulationEmploymentProfile(
                    definitionId,
                    housingCapacity,
                    jobCapacity);
            }

            public bool TryGetPopulationEmploymentProfile(
                StableId definitionId,
                out PopulationEmploymentProfile profile)
            {
                return _profiles.TryGetValue(definitionId, out profile);
            }
        }
    }
}
