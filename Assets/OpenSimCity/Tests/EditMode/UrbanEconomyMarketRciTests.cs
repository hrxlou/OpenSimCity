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
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Population;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UrbanEconomyMarketRciTests
    {
        [Test]
        public void MarketAggregatesGoodsShortageAndBusinessUtilizationWithQueryableFactors()
        {
            var fixture = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, housingCapacity: 10, jobCapacity: 0, occupancy: 6, filledJobs: 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, housingCapacity: 0, jobCapacity: 8, occupancy: 0, filledJobs: 4),
                new EconomyBuildingSpec(3, EconomicSector.Industrial, housingCapacity: 0, jobCapacity: 4, occupancy: 0, filledJobs: 2));

            fixture.System.RunMonth(new SimulationDate(1, 1));

            var market = fixture.System.Market;
            Assert.That(market.GoodsSupply, Is.EqualTo(2));
            Assert.That(market.ConsumptionPower, Is.EqualTo(6));
            Assert.That(market.GoodsSold, Is.EqualTo(2));
            Assert.That(market.GoodsShortage, Is.EqualTo(4));
            Assert.That(market.GoodsSurplus, Is.Zero);
            Assert.That(market.CommercialThroughputCapacity, Is.EqualTo(8));
            Assert.That(market.CommercialThroughput, Is.EqualTo(2));
            Assert.That(market.BusinessUtilizationPermille, Is.EqualTo(250));

            // Residential demand: vacant jobs pull up, vacant housing pushes down.
            Assert.That(Factor(fixture.System.Rci.Residential, UrbanEconomySystem.VacantJobsFactor), Is.EqualTo(6));
            Assert.That(Factor(fixture.System.Rci.Residential, UrbanEconomySystem.VacantHousingFactor), Is.EqualTo(-4));
            Assert.That(Factor(fixture.System.Rci.Residential, UrbanEconomySystem.UnemploymentFactor), Is.Zero);
            Assert.That(fixture.System.Rci.Residential.RawPressure, Is.EqualTo(2));

            // Commercial demand: vacant shops push down.
            Assert.That(Factor(fixture.System.Rci.Commercial, UrbanEconomySystem.VacantCommercialJobsFactor), Is.EqualTo(-4));
            Assert.That(fixture.System.Rci.Commercial.RawPressure, Is.EqualTo(-4));

            // Industrial demand: unmet goods demand pulls up.
            Assert.That(Factor(fixture.System.Rci.Industrial, UrbanEconomySystem.GoodsShortageFactor), Is.EqualTo(4));
            Assert.That(fixture.System.Rci.Industrial.RawPressure, Is.EqualTo(4));
        }

        [Test]
        public void UnemploymentPushesResidentialDemandDownRelativeToVacantJobs()
        {
            var vacantJobs = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 10, 0, 4));
            var unemployed = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 1, 0, 1));

            vacantJobs.System.RunMonth(new SimulationDate(1, 1));
            unemployed.System.RunMonth(new SimulationDate(1, 1));

            // 4 residents, 10 commercial jobs, 4 filled -> 6 vacant jobs, no unemployment.
            Assert.That(Factor(vacantJobs.System.Rci.Residential, UrbanEconomySystem.VacantJobsFactor), Is.EqualTo(6));
            Assert.That(Factor(vacantJobs.System.Rci.Residential, UrbanEconomySystem.UnemploymentFactor), Is.Zero);

            // 4 residents, 1 commercial job filled -> 3 unemployed, no vacant jobs.
            Assert.That(Factor(unemployed.System.Rci.Residential, UrbanEconomySystem.VacantJobsFactor), Is.Zero);
            Assert.That(Factor(unemployed.System.Rci.Residential, UrbanEconomySystem.UnemploymentFactor), Is.EqualTo(-3));
            Assert.That(
                unemployed.System.Rci.Residential.RawPressure,
                Is.LessThan(vacantJobs.System.Rci.Residential.RawPressure));
        }

        [Test]
        public void GoodsSurplusPushesIndustrialDemandDown()
        {
            var parameters = new UrbanEconomyParameters(goodsPerIndustrialWorker: 3);
            var fixture = CreateFixture(
                parameters,
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0),
                new EconomyBuildingSpec(2, EconomicSector.Industrial, 0, 4, 0, 4));

            fixture.System.RunMonth(new SimulationDate(1, 1));

            // supply = 3 * 4 = 12, consumption power = 4 -> surplus 8.
            Assert.That(fixture.System.Market.GoodsSupply, Is.EqualTo(12));
            Assert.That(fixture.System.Market.GoodsSurplus, Is.EqualTo(8));
            Assert.That(fixture.System.Market.GoodsShortage, Is.Zero);
            Assert.That(Factor(fixture.System.Rci.Industrial, UrbanEconomySystem.GoodsSurplusFactor), Is.EqualTo(-8));
            Assert.That(fixture.System.Rci.Industrial.RawPressure, Is.EqualTo(-8));
        }

        [Test]
        public void RawPressureIsClampedSymmetricallyBeforeSmoothing()
        {
            var parameters = new UrbanEconomyParameters(rciClampMagnitude: 5);
            var positive = CreateFixture(
                parameters,
                new EconomyBuildingSpec(1, EconomicSector.Residential, 1, 0, 1, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 20, 0, 1));
            var negative = CreateFixture(
                parameters,
                new EconomyBuildingSpec(1, EconomicSector.Residential, 50, 0, 0, 0));

            positive.System.RunMonth(new SimulationDate(1, 1));
            negative.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(positive.System.Rci.Residential.RawPressure, Is.GreaterThan(5));
            Assert.That(positive.System.Rci.Residential.ClampedPressure, Is.EqualTo(5));
            Assert.That(negative.System.Rci.Residential.RawPressure, Is.LessThan(-5));
            Assert.That(negative.System.Rci.Residential.ClampedPressure, Is.EqualTo(-5));
        }

        [Test]
        public void SmoothingConvergesMonotonicallyWithoutOvershoot()
        {
            var fixture = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 1, 0, 1, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 21, 0, 1));

            var start = new SimulationDate(1, 1);
            fixture.System.RunMonth(start);
            var target = fixture.System.Rci.Residential.ClampedPressure;
            Assert.That(target, Is.EqualTo(20));
            // First step closes a quarter of the gap: 20 -> 5.
            Assert.That(fixture.System.Rci.Residential.SmoothedDemand, Is.EqualTo(5));

            var previous = fixture.System.Rci.Residential.SmoothedDemand;
            for (var month = 1; month < 40; month++)
            {
                fixture.System.RunMonth(start.AddMonths(month));
                var current = fixture.System.Rci.Residential.SmoothedDemand;
                Assert.That(current, Is.GreaterThanOrEqualTo(previous), "smoothing must be monotonic");
                Assert.That(current, Is.LessThanOrEqualTo(target), "smoothing must not overshoot");
                previous = current;
            }

            Assert.That(fixture.System.Rci.Residential.SmoothedDemand, Is.EqualTo(target));
        }

        [Test]
        public void TaxRatePortReducesDemandWithTruncatingIntegerRoundingAndNoFiscalCoupling()
        {
            var untaxed = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 10, 0, 0),
                new EconomyBuildingSpec(3, EconomicSector.Industrial, 0, 10, 0, 0));
            var taxed = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0),
                new EconomyBuildingSpec(2, EconomicSector.Commercial, 0, 10, 0, 0),
                new EconomyBuildingSpec(3, EconomicSector.Industrial, 0, 10, 0, 0));

            // 205 permille * sensitivity 100 / 1000 = 20 (truncated toward zero).
            taxed.System.TaxRates = new UrbanEconomyTaxRates(205, 205, 205);

            untaxed.System.RunMonth(new SimulationDate(1, 1));
            taxed.System.RunMonth(new SimulationDate(1, 1));

            Assert.That(Factor(untaxed.System.Rci.Residential, UrbanEconomySystem.TaxFactor), Is.Zero);
            Assert.That(Factor(taxed.System.Rci.Residential, UrbanEconomySystem.TaxFactor), Is.EqualTo(-20));
            Assert.That(Factor(taxed.System.Rci.Commercial, UrbanEconomySystem.TaxFactor), Is.EqualTo(-20));
            Assert.That(Factor(taxed.System.Rci.Industrial, UrbanEconomySystem.TaxFactor), Is.EqualTo(-20));
            Assert.That(
                taxed.System.Rci.Residential.RawPressure,
                Is.EqualTo(untaxed.System.Rci.Residential.RawPressure - 20));
        }

        [Test]
        public void SchedulerRunsEconomyAfterPopulationOnMonthBoundary()
        {
            var buildings = new BuildingStateCollection();
            var profiles = new MutableEconomyProfileResolver();
            profiles.Set(Id(1), EconomicSector.Residential, housingCapacity: 10, jobCapacity: 0);
            profiles.Set(Id(2), EconomicSector.Commercial, housingCapacity: 0, jobCapacity: 4);
            buildings.Create(Id(101), Id(1), Id(9001), new CellCoord(1, 0));
            buildings.Create(Id(102), Id(2), Id(9002), new CellCoord(2, 0));

            var connections = new[]
            {
                new ExternalConnectionState(Id(900), ExternalConnectionKind.Road, new CellCoord(0, 0), true)
            };
            var population = new PopulationEmploymentMigrationSystem(buildings, profiles, connections);
            var economy = new UrbanEconomySystem(buildings, profiles);
            var scheduler = new MonthlySimulationScheduler(
                new IMonthlySimulationSystem[] { population, economy },
                ticksPerMonth: 2,
                firstMonth: new SimulationDate(1, 1));

            scheduler.ProcessTick(new SimulationTick(0));
            Assert.That(economy.HasProcessedMonth, Is.False);

            scheduler.ProcessTick(new SimulationTick(1));

            // Migration filled 4 residents into jobs before the economy ran, so the
            // market must observe the post-population counts (order proof).
            Assert.That(scheduler.CompletedMonths, Is.EqualTo(1));
            Assert.That(population.Population.TotalPopulation, Is.EqualTo(4));
            Assert.That(economy.Market.ConsumptionPower, Is.EqualTo(4));
            Assert.That(economy.Market.GoodsSupply, Is.Zero);
            Assert.That(economy.LastProcessedMonth, Is.EqualTo(new SimulationDate(1, 1)));
        }

        [Test]
        public void MonthlyEconomyRunsExactlyOnceAcrossSpeedAndPauseChanges()
        {
            var fixture = CreateFixture(
                new EconomyBuildingSpec(1, EconomicSector.Residential, 4, 0, 4, 0));
            var scheduler = new MonthlySimulationScheduler(
                new IMonthlySimulationSystem[] { fixture.System },
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
            Assert.That(fixture.System.Version, Is.Zero);
            clock.Speed = SimulationSpeed.Paused;
            Assert.That(runner.Advance(5d), Is.Zero);
            clock.Speed = SimulationSpeed.Four;
            Assert.That(runner.Advance(0.1d), Is.EqualTo(4));

            Assert.That(scheduler.CompletedMonths, Is.EqualTo(2));
            Assert.That(fixture.System.Version, Is.EqualTo(2));
            Assert.That(fixture.System.LastProcessedMonth, Is.EqualTo(new SimulationDate(2001, 1)));
        }

        [Test]
        public void MonthlySnapshotChecksumIsIndependentOfBuildingInsertionOrder()
        {
            var first = CreateFixture(
                new EconomyBuildingSpec(30, EconomicSector.Industrial, 0, 5, 0, 3),
                new EconomyBuildingSpec(10, EconomicSector.Residential, 8, 0, 6, 0),
                new EconomyBuildingSpec(20, EconomicSector.Commercial, 0, 4, 0, 2));
            var second = CreateFixture(
                new EconomyBuildingSpec(20, EconomicSector.Commercial, 0, 4, 0, 2),
                new EconomyBuildingSpec(10, EconomicSector.Residential, 8, 0, 6, 0),
                new EconomyBuildingSpec(30, EconomicSector.Industrial, 0, 5, 0, 3));

            first.System.RunMonth(new SimulationDate(1, 1));
            second.System.RunMonth(new SimulationDate(1, 1));

            var firstJson = JsonUtility.ToJson(new UrbanEconomyReadModel(first.System).Snapshot);
            var secondJson = JsonUtility.ToJson(new UrbanEconomyReadModel(second.System).Snapshot);

            Assert.That(secondJson, Is.EqualTo(firstJson));
        }

        [Test]
        public void EmptyCitySnapshotIsZeroedAndSerializable()
        {
            var fixture = CreateFixture();
            var readModel = new UrbanEconomyReadModel(fixture.System);

            fixture.System.RunMonth(new SimulationDate(2040, 12));
            Assert.That(readModel.RefreshIfNeeded(), Is.True);
            var json = JsonUtility.ToJson(readModel.Snapshot);
            var restored = JsonUtility.FromJson<UrbanEconomySnapshotDto>(json);

            Assert.That(restored.Date, Is.EqualTo(new SimulationDate(2040, 12)));
            Assert.That(restored.Market.GoodsSupply, Is.Zero);
            Assert.That(restored.Market.ConsumptionPower, Is.Zero);
            Assert.That(restored.Market.BusinessUtilizationPermille, Is.Zero);
            Assert.That(restored.Rci.Residential.SmoothedDemand, Is.Zero);
            Assert.That(restored.Rci.Commercial.SmoothedDemand, Is.Zero);
            Assert.That(restored.Rci.Industrial.SmoothedDemand, Is.Zero);
        }

        private static int Factor(RciSectorDemand demand, string name)
        {
            var match = demand.Factors.FirstOrDefault(factor => factor.Name == name);
            Assert.That(match, Is.Not.Null, $"factor {name} must be queryable");
            return match.Contribution;
        }

        private static Fixture CreateFixture(params EconomyBuildingSpec[] specs)
        {
            return CreateFixture(UrbanEconomyParameters.Default, specs);
        }

        private static Fixture CreateFixture(UrbanEconomyParameters parameters, params EconomyBuildingSpec[] specs)
        {
            var buildings = new BuildingStateCollection();
            var profiles = new MutableEconomyProfileResolver();
            foreach (var spec in specs)
            {
                profiles.Set(Id(spec.DefinitionId), spec.Sector, spec.HousingCapacity, spec.JobCapacity);
                buildings.Create(
                    Id(spec.BuildingId),
                    Id(spec.DefinitionId),
                    Id(1000 + spec.BuildingId),
                    new CellCoord((int)spec.BuildingId, 0),
                    spec.Occupancy,
                    spec.FilledJobs);
            }

            return new Fixture(buildings, profiles, new UrbanEconomySystem(buildings, profiles, parameters));
        }

        private static StableId Id(ulong value) => new StableId(value);

        private readonly struct EconomyBuildingSpec
        {
            public EconomyBuildingSpec(
                ulong definitionAndBuildingId,
                EconomicSector sector,
                int housingCapacity,
                int jobCapacity,
                int occupancy,
                int filledJobs)
            {
                BuildingId = definitionAndBuildingId;
                DefinitionId = definitionAndBuildingId;
                Sector = sector;
                HousingCapacity = housingCapacity;
                JobCapacity = jobCapacity;
                Occupancy = occupancy;
                FilledJobs = filledJobs;
            }

            public ulong BuildingId { get; }
            public ulong DefinitionId { get; }
            public EconomicSector Sector { get; }
            public int HousingCapacity { get; }
            public int JobCapacity { get; }
            public int Occupancy { get; }
            public int FilledJobs { get; }
        }

        private sealed class Fixture
        {
            public Fixture(
                BuildingStateCollection buildings,
                MutableEconomyProfileResolver profiles,
                UrbanEconomySystem system)
            {
                Buildings = buildings;
                Profiles = profiles;
                System = system;
            }

            public BuildingStateCollection Buildings { get; }
            public MutableEconomyProfileResolver Profiles { get; }
            public UrbanEconomySystem System { get; }
        }

        private sealed class MutableEconomyProfileResolver :
            IPopulationEmploymentProfileResolver,
            IUrbanEconomyProfileResolver
        {
            private readonly Dictionary<StableId, UrbanEconomyProfile> _profiles =
                new Dictionary<StableId, UrbanEconomyProfile>();

            public void Set(StableId definitionId, EconomicSector sector, int housingCapacity, int jobCapacity)
            {
                _profiles[definitionId] = new UrbanEconomyProfile(definitionId, sector, housingCapacity, jobCapacity);
            }

            public bool TryGetPopulationEmploymentProfile(
                StableId definitionId,
                out PopulationEmploymentProfile profile)
            {
                if (_profiles.TryGetValue(definitionId, out var economy))
                {
                    profile = new PopulationEmploymentProfile(
                        definitionId,
                        economy.HousingCapacity,
                        economy.JobCapacity);
                    return true;
                }

                profile = default;
                return false;
            }

            public bool TryGetUrbanEconomyProfile(StableId definitionId, out UrbanEconomyProfile profile)
            {
                return _profiles.TryGetValue(definitionId, out profile);
            }
        }
    }
}
