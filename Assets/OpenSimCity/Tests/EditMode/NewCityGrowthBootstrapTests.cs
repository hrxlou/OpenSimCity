using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class NewCityGrowthBootstrapTests
    {
        private enum Layout
        {
            Grid = 1,
            EdgeBranch = 2
        }

        [Test]
        public void FactorySeedsFiniteQueryableRciStateOnlyForNewCities()
        {
            var runtime = CreateRuntime();

            Assert.That(runtime.UrbanEconomySystem.HasProcessedMonth, Is.False);
            AssertSectorBootstrap(runtime.UrbanEconomySystem.Rci.Residential, 64);
            AssertSectorBootstrap(runtime.UrbanEconomySystem.Rci.Commercial, 64);
            AssertSectorBootstrap(runtime.UrbanEconomySystem.Rci.Industrial, 64);

            var bareRestoreRuntime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x404UL,
                TicksPerMonth = 1
            });
            Assert.That(bareRestoreRuntime.UrbanEconomySystem.Rci.Residential.SmoothedDemand, Is.Zero);
            Assert.That(bareRestoreRuntime.UrbanEconomySystem.Rci.Commercial.SmoothedDemand, Is.Zero);
            Assert.That(bareRestoreRuntime.UrbanEconomySystem.Rci.Industrial.SmoothedDemand, Is.Zero);
        }

        [Test]
        public void EmptyCityBootstrapHandsOffToNormalSmoothingAndReachesZero()
        {
            var runtime = CreateRuntime();
            var expected = new[] { 48, 36, 27, 21, 16, 12, 9, 7, 6, 5, 4, 3, 2, 1, 0 };

            for (var month = 0; month < expected.Length; month++)
            {
                RunMonth(runtime, month);
                Assert.That(
                    runtime.UrbanEconomySystem.Rci.Residential.SmoothedDemand,
                    Is.EqualTo(expected[month]),
                    $"unexpected residential bootstrap decay in month {month + 1}");
            }

            Assert.That(
                runtime.UrbanEconomySystem.Rci.Residential.Factors.Any(
                    factor => factor.Name == NewCityRuntimeFactory.BootstrapFactorName),
                Is.False,
                "ordinary monthly factors must replace the one-time bootstrap diagnostic");
        }

        [TestCase(Layout.Grid)]
        [TestCase(Layout.EdgeBranch)]
        public void TwoPublicCommandLayoutsReachPopulationAndEmploymentWithinSixMonths(Layout layout)
        {
            var runtime = CreateRuntime();
            ApplyLayout(runtime, layout, connectExternal: true, includePower: true);

            var firstPopulationMonth = -1;
            var firstEmploymentMonth = -1;
            for (var month = 0; month < 6; month++)
            {
                RunMonth(runtime, month);
                if (firstPopulationMonth < 0 && runtime.PopulationSystem.Population.TotalPopulation > 0)
                    firstPopulationMonth = month + 1;
                if (firstEmploymentMonth < 0 && runtime.PopulationSystem.Employment.EmployedWorkers > 0)
                    firstEmploymentMonth = month + 1;
            }

            Assert.That(firstPopulationMonth, Is.InRange(1, 6));
            Assert.That(firstEmploymentMonth, Is.InRange(1, 6));
            Assert.That(runtime.Buildings.Buildings.Any(building =>
                runtime.BuildingDefinitions.Get(building.DefinitionId).ZoneType == ZoneType.ResidentialLow), Is.True);
            Assert.That(runtime.Buildings.Buildings.Any(building =>
                runtime.BuildingDefinitions.Get(building.DefinitionId).ZoneType == ZoneType.CommercialLow), Is.True);
            Assert.That(runtime.Buildings.Buildings.Any(building =>
                runtime.BuildingDefinitions.Get(building.DefinitionId).ZoneType == ZoneType.IndustrialLow), Is.True);
        }

        [TestCase(Layout.Grid)]
        [TestCase(Layout.EdgeBranch)]
        public void SameSeedCommandsAndReadModelObservationReproduceEveryMonthlySnapshot(Layout layout)
        {
            var unobserved = CreateRuntime();
            var observed = CreateRuntime();
            ApplyLayout(unobserved, layout, connectExternal: true, includePower: true);
            ApplyLayout(observed, layout, connectExternal: true, includePower: true);

            for (var month = 0; month < 6; month++)
            {
                RunMonth(unobserved, month);
                RunMonth(observed, month);
                _ = observed.BuildingViews.Views.Count;
                _ = observed.RoadAccessibility.Cells.Count;
                _ = observed.PowerModel.ProducerViews.Count;
                _ = observed.HudModel.Snapshot.TotalFeasibleParcelCount;

                Assert.That(CaptureJson(observed), Is.EqualTo(CaptureJson(unobserved)),
                    $"layout {layout} diverged in month {month + 1}");
            }
        }

        [Test]
        public void MissingPowerReportsReasonAndNormalPlacementRecoversNextMonth()
        {
            var runtime = CreateRuntime();
            ApplyLayout(runtime, Layout.Grid, connectExternal: true, includePower: false);

            RunMonth(runtime, 0);
            Assert.That(runtime.GrowthSystem.State.Count, Is.Zero);
            Assert.That(runtime.HudModel.Snapshot.TopBlockReasons.Select(reason => reason.Code),
                Does.Contain("NoPowerCapacity"));

            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(GridPowerCell(runtime)),
                runtime.DefaultPowerPlantDefinitionId,
                GridPowerCell(runtime))));
            RunMonth(runtime, 1);

            Assert.That(runtime.GrowthSystem.State.Count, Is.GreaterThan(0));
        }

        [Test]
        public void MissingExternalRoadReportsReasonAndNormalRoadPlacementRecoversNextMonth()
        {
            var runtime = CreateRuntime();
            ApplyLayout(runtime, Layout.Grid, connectExternal: false, includePower: true);

            RunMonth(runtime, 0);
            Assert.That(runtime.GrowthSystem.State.Count, Is.Zero);
            Assert.That(runtime.HudModel.Snapshot.TopBlockReasons.Select(reason => reason.Code),
                Does.Contain("RoadIneligible"));

            var roadY = runtime.Height / 2;
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(0, roadY))));
            RunMonth(runtime, 1);

            Assert.That(runtime.GrowthSystem.State.Count, Is.GreaterThan(0));
            Assert.That(runtime.PopulationSystem.Migration.HasExternalConnection, Is.True);
        }

        [Test]
        public void SnapshotBeforeFirstConstructionPreservesBootstrapWithoutReapplication()
        {
            var original = CreateRuntime();
            ApplyLayout(original, Layout.Grid, connectExternal: true, includePower: true);
            var restored = RoundTrip(original);

            Assert.That(CaptureJson(restored), Is.EqualTo(CaptureJson(original)));
            Assert.That(restored.UrbanEconomySystem.Rci.Residential.SmoothedDemand, Is.EqualTo(64));
            Assert.That(restored.UrbanEconomySystem.Rci.Residential.Factors.Count(factor =>
                factor.Name == NewCityRuntimeFactory.BootstrapFactorName), Is.EqualTo(1));

            for (var month = 0; month < 6; month++)
            {
                RunMonth(original, month);
                RunMonth(restored, month);
                Assert.That(CaptureJson(restored), Is.EqualTo(CaptureJson(original)));
            }
        }

        [Test]
        public void SnapshotDuringConstructionPreservesSitesAndNextMonthTimeline()
        {
            var original = CreateRuntime();
            ApplyLayout(original, Layout.EdgeBranch, connectExternal: true, includePower: true);
            RunMonth(original, 0);
            Assert.That(original.GrowthSystem.State.Count, Is.EqualTo(3));
            Assert.That(original.Buildings.Count, Is.Zero);

            var restored = RoundTrip(original);
            Assert.That(CaptureJson(restored), Is.EqualTo(CaptureJson(original)));

            for (var month = 1; month < 6; month++)
            {
                RunMonth(original, month);
                RunMonth(restored, month);
                Assert.That(CaptureJson(restored), Is.EqualTo(CaptureJson(original)));
            }

            Assert.That(restored.PopulationSystem.Population.TotalPopulation, Is.GreaterThan(0));
            Assert.That(restored.PopulationSystem.Employment.EmployedWorkers, Is.GreaterThan(0));
        }

        [Test]
        public void BootstrapValuesMustFitConfiguredRciClamp()
        {
            var options = Options();
            options.UrbanEconomyParameters = new UrbanEconomyParameters(rciClampMagnitude: 32);

            Assert.That(
                () => NewCityRuntimeFactory.Create(options),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var runtime = NewCityRuntimeFactory.Create(
                options,
                new NewCityGrowthBootstrapParameters(32, 24, 16));
            Assert.That(runtime.UrbanEconomySystem.Rci.Residential.SmoothedDemand, Is.EqualTo(32));
        }

        private static CityRuntime CreateRuntime()
        {
            return NewCityRuntimeFactory.Create(Options());
        }

        private static CityRuntimeOptions Options()
        {
            return new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x404UL,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1),
                InitialFunds = 100_000
            };
        }

        private static void ApplyLayout(
            CityRuntime runtime,
            Layout layout,
            bool connectExternal,
            bool includePower)
        {
            var roadY = runtime.Height / 2;
            if (layout == Layout.Grid)
            {
                AssertApplied(runtime.Execute(new PlaceRoadCommand(
                    runtime.DefaultRoadDefinitionId,
                    new CellCoord(connectExternal ? 0 : 1, roadY),
                    new CellCoord(15, roadY))));
                PaintThreeSectors(runtime, roadY - 2, 2, 6, 10);
                if (includePower)
                {
                    var cell = GridPowerCell(runtime);
                    AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                        runtime.PowerProducerIdFor(cell),
                        runtime.DefaultPowerPlantDefinitionId,
                        cell)));
                }
                return;
            }

            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(connectExternal ? 0 : 1, roadY),
                new CellCoord(4, roadY))));
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(4, 3),
                new CellCoord(4, roadY - 1))));
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(4, roadY + 1),
                new CellCoord(4, 9))));
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(5, 3),
                new CellCoord(14, 3))));
            PaintThreeSectors(runtime, 4, 6, 9, 12);
            if (includePower)
            {
                var cell = new CellCoord(5, 4);
                AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                    runtime.PowerProducerIdFor(cell),
                    runtime.DefaultPowerPlantDefinitionId,
                    cell)));
            }
        }

        private static void PaintThreeSectors(
            CityRuntime runtime,
            int y,
            int residentialX,
            int commercialX,
            int industrialX)
        {
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(residentialX, y),
                new CellCoord(residentialX + 1, y + 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.CommercialLow,
                new CellCoord(commercialX, y),
                new CellCoord(commercialX + 1, y + 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new CellCoord(industrialX, y),
                new CellCoord(industrialX + 1, y + 1))));
        }

        private static CellCoord GridPowerCell(CityRuntime runtime)
        {
            return new CellCoord(15, runtime.Height / 2 - 1);
        }

        private static void RunMonth(CityRuntime runtime, int monthIndex)
        {
            var firstTick = monthIndex * runtime.MonthlyScheduler.TicksPerMonth;
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                runtime.ProcessSimulationTick(new SimulationTick(firstTick + offset));
        }

        private static CityRuntime RoundTrip(CityRuntime runtime)
        {
            var json = CaptureJson(runtime);
            var dto = JsonUtility.FromJson<CitySnapshotDto>(json);
            return CitySnapshotSerializer.Restore(dto, Options());
        }

        private static string CaptureJson(CityRuntime runtime)
        {
            return JsonUtility.ToJson(CitySnapshotSerializer.Capture(runtime));
        }

        private static void AssertSectorBootstrap(RciSectorDemand sector, int expected)
        {
            Assert.That(sector.RawPressure, Is.EqualTo(expected));
            Assert.That(sector.ClampedPressure, Is.EqualTo(expected));
            Assert.That(sector.SmoothedDemand, Is.EqualTo(expected));
            Assert.That(sector.Factors.Count, Is.EqualTo(1));
            Assert.That(sector.Factors[0].Name, Is.EqualTo(NewCityRuntimeFactory.BootstrapFactorName));
            Assert.That(sector.Factors[0].Contribution, Is.EqualTo(expected));
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
