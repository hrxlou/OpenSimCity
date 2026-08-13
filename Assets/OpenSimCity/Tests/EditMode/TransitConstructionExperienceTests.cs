using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Transit;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TransitConstructionExperienceTests
    {
        [Test]
        public void StopAndLinePreviewAreNonDestructiveUntilPublicCommit()
        {
            var runtime = BuildGrowingCity();
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var tool = TransitConstructionToolRegistry.GetOrCreate(module);
            var y = runtime.Height / 2;
            var fundsBefore = runtime.Funds;
            var networkBefore = module.Network.ComputeChecksum();

            tool.SelectStopPlacement();
            var preview = tool.HoverCell(new CellCoord(2, y));
            Assert.That(preview.IsValid, Is.True, preview.Message);
            Assert.That(module.Network.Stops, Is.Empty);
            Assert.That(module.Network.ComputeChecksum(), Is.EqualTo(networkBefore));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));

            tool.Cancel();
            Assert.That(module.Network.Stops, Is.Empty);
            tool.SelectStopPlacement();
            tool.HoverCell(new CellCoord(2, y));
            Assert.That(tool.Commit().IsApplied, Is.True);
            Assert.That(module.Network.Stops, Has.Count.EqualTo(1));

            AddStop(tool, new CellCoord(7, y));
            AddStop(tool, new CellCoord(11, y));
            var stopIds = module.Network.Stops.Select(stop => stop.Id).ToArray();
            var stateBeforeLinePreview = module.Network.ComputeChecksum();
            tool.SelectLineEditor(name: "간선", serviceUnits: 8);

            Assert.That(tool.PreviewLineStop(stopIds[0], new CellCoord(2, y)).IsValid, Is.True);
            Assert.That(tool.AddPreviewedLineStop(), Is.True);
            Assert.That(tool.PreviewLineStop(stopIds[1], new CellCoord(7, y)).IsValid, Is.True);
            Assert.That(tool.AddPreviewedLineStop(), Is.True);
            Assert.That(tool.PreviewLineStop(stopIds[2], new CellCoord(11, y)).IsValid, Is.True);
            Assert.That(tool.AddPreviewedLineStop(), Is.True);

            Assert.That(module.Network.Lines, Is.Empty);
            Assert.That(module.Network.ComputeChecksum(), Is.EqualTo(stateBeforeLinePreview));
            Assert.That(tool.Commit().IsApplied, Is.True);
            Assert.That(module.Network.Lines, Has.Count.EqualTo(1));
            Assert.That(module.Network.Lines[0].StopIds, Is.EqualTo(stopIds));
        }

        [Test]
        public void DuplicateStopAndBrokenLineExposeActionablePreviewReasons()
        {
            var runtime = BuildGrowingCity();
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var tool = TransitConstructionToolRegistry.GetOrCreate(module);
            var y = runtime.Height / 2;
            AddStop(tool, new CellCoord(2, y));
            AddStop(tool, new CellCoord(7, y));
            var stopIds = module.Network.Stops.Select(stop => stop.Id).ToArray();

            tool.SelectLineEditor(name: "검증", serviceUnits: 4);
            tool.PreviewLineStop(stopIds[0], new CellCoord(2, y));
            Assert.That(tool.AddPreviewedLineStop(), Is.True);
            var duplicate = tool.PreviewLineStop(stopIds[0], new CellCoord(2, y));
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Message, Does.Contain("두 번"));

            tool.Cancel();
            var brokenRuntime = NewCityRuntimeFactory.Create(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                Seed = 0x505CUL,
                TicksPerMonth = 1,
                InitialFunds = 10_000
            });
            AssertApplied(brokenRuntime.Execute(new PlaceRoadCommand(
                brokenRuntime.DefaultRoadDefinitionId,
                new CellCoord(0, 1),
                new CellCoord(2, 1))));
            AssertApplied(brokenRuntime.Execute(new PlaceRoadCommand(
                brokenRuntime.DefaultRoadDefinitionId,
                new CellCoord(5, 1),
                new CellCoord(7, 1))));
            var broken = TransitRuntimeRegistry.GetOrCreate(brokenRuntime);
            var a = new TransitStopId(0xE100000000000101UL);
            var b = new TransitStopId(0xE100000000000102UL);
            AssertApplied(broken.Execute(new PlaceTransitStopCommand(a, new CellCoord(1, 1), "A")));
            AssertApplied(broken.Execute(new PlaceTransitStopCommand(b, new CellCoord(6, 1), "B")));
            var result = broken.Commands.Preview(new UpsertTransitLineCommand(
                new TransitLineId(0xE200000000000101UL),
                "단절",
                new[] { a, b },
                2));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(TransitCommandFailureReason.BrokenRoadPath));
            Assert.That(result.Message, Does.Contain("경로"));
        }

        [Test]
        public void VehicleSampleLimitChangesOnlyPresentationQuery()
        {
            var runtime = BuildGrowingCity();
            var module = AddLine(runtime, 10);
            module.RunCurrentMonth();
            var overlay = new TransitOverlayReadModel(module);
            var sampling = new TransitVehicleSamplingReadModel(overlay);
            var networkChecksum = module.Network.ComputeChecksum();
            var modeChecksum = module.ModeChoice.State.ComputeChecksum();
            var routingChecksum = module.PrivateVehicleRouting.State.ComputeChecksum();

            sampling.SetMaximumSamples(0);
            sampling.RefreshIfNeeded();
            Assert.That(sampling.Samples, Is.Empty);
            Assert.That(module.Network.ComputeChecksum(), Is.EqualTo(networkChecksum));
            Assert.That(module.ModeChoice.State.ComputeChecksum(), Is.EqualTo(modeChecksum));
            Assert.That(module.PrivateVehicleRouting.State.ComputeChecksum(), Is.EqualTo(routingChecksum));

            sampling.SetMaximumSamples(200);
            sampling.RefreshIfNeeded();
            Assert.That(sampling.Samples.Count, Is.EqualTo(Math.Min(200, module.ModeChoice.State.BusTrips)));
            Assert.That(sampling.Samples.Sum(sample => sample.RepresentedTripAmount),
                Is.EqualTo(module.ModeChoice.State.BusTrips));
            Assert.That(module.Network.ComputeChecksum(), Is.EqualTo(networkChecksum));
            Assert.That(module.ModeChoice.State.ComputeChecksum(), Is.EqualTo(modeChecksum));
            Assert.That(module.PrivateVehicleRouting.State.ComputeChecksum(), Is.EqualTo(routingChecksum));
        }

        [Test]
        public void RoadUpgradeAndBusInvestmentProvideDifferentViableTrafficStrategies()
        {
            var baseline = BuildGrowingCity();
            var baselineRouted = baseline.TrafficRoutingSystem.State.RoutedAmount;
            var baselineTravel = WeightedTravelSeconds(baseline.TrafficRoutingSystem.State.Assignments);
            var baselineBuildingIds = baseline.Buildings.Buildings.Select(building => building.Id).ToArray();

            var roadCity = BuildGrowingCity();
            var roadY = roadCity.Height / 2;
            var roadFundsBefore = roadCity.Funds;
            AssertApplied(roadCity.Execute(new UpgradeRoadCommand(
                RoadUpgradeDefinitionIds.Avenue,
                RoadDirectionMode.Bidirectional,
                new CellCoord(0, roadY),
                new CellCoord(15, roadY))));
            roadCity.TrafficRoutingSystem.RefreshIfNeeded();
            var roadTravel = WeightedTravelSeconds(roadCity.TrafficRoutingSystem.State.Assignments);

            var busCity = BuildGrowingCity();
            var busFundsBefore = busCity.Funds;
            var module = AddLine(busCity, 10);
            module.RunCurrentMonth();

            Assert.That(roadCity.TrafficRoutingSystem.State.RoutedAmount, Is.EqualTo(baselineRouted));
            Assert.That(roadTravel, Is.LessThanOrEqualTo(baselineTravel));
            Assert.That(roadCity.Funds, Is.LessThan(roadFundsBefore));
            Assert.That(module.PrivateVehicleRouting.State.RoutedAmount, Is.LessThan(baselineRouted));
            Assert.That(busCity.Funds, Is.LessThan(busFundsBefore));
            Assert.That(roadCity.Buildings.Buildings.Select(building => building.Id), Is.EqualTo(baselineBuildingIds));
            Assert.That(busCity.Buildings.Buildings.Select(building => building.Id), Is.EqualTo(baselineBuildingIds));
            Assert.That(module.ModeChoice.State.BusTrips, Is.GreaterThan(0));
        }

        [Test]
        public void OverlayUsesDirectionalLineGeometryAndApplicationReasons()
        {
            var runtime = BuildGrowingCity();
            var module = AddLine(runtime, 1);
            module.RunCurrentMonth();
            var overlay = new TransitOverlayReadModel(module);

            Assert.That(overlay.Stops, Has.Count.EqualTo(3));
            Assert.That(overlay.Lines, Has.Count.EqualTo(1));
            Assert.That(overlay.Lines[0].Cells.Count, Is.GreaterThan(2));
            Assert.That(overlay.Lines[0].Pattern, Is.EqualTo(TransitOverlayPattern.DirectionArrow));
            Assert.That(overlay.Lines[0].Capacity, Is.GreaterThan(0));
            Assert.That(overlay.Issues.All(issue =>
                Enum.IsDefined(typeof(TransitModeChoiceReasonView), issue.Reason)), Is.True);
        }

        private static void AddStop(TransitConstructionTool tool, CellCoord cell)
        {
            tool.SelectStopPlacement();
            var preview = tool.HoverCell(cell);
            Assert.That(preview.IsValid, Is.True, preview.Message);
            Assert.That(tool.Commit().IsApplied, Is.True);
        }

        private static TransitRuntimeModule AddLine(CityRuntime runtime, int serviceUnits)
        {
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var y = runtime.Height / 2;
            var a = new TransitStopId(0xE100000000000201UL);
            var b = new TransitStopId(0xE100000000000202UL);
            var c = new TransitStopId(0xE100000000000203UL);
            AssertApplied(module.Execute(new PlaceTransitStopCommand(a, new CellCoord(2, y), "주거")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(b, new CellCoord(7, y), "상업")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(c, new CellCoord(11, y), "산업")));
            AssertApplied(module.Execute(new UpsertTransitLineCommand(
                new TransitLineId(0xE200000000000201UL),
                "간선",
                new[] { a, b, c },
                serviceUnits)));
            return module;
        }

        private static CityRuntime BuildGrowingCity()
        {
            var runtime = NewCityRuntimeFactory.Create(new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x505DUL,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1),
                InitialFunds = 100_000
            });
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
            for (var month = 0; month < 6; month++)
                Assert.That(runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds), Is.EqualTo(1));
            Assert.That(runtime.TripDemandSystem.State.CommuteMatched, Is.GreaterThan(0));
            return runtime;
        }

        private static long WeightedTravelSeconds(
            System.Collections.Generic.IReadOnlyList<OpenSimCity.Simulation.Traffic.TrafficRouteAssignment> rows)
        {
            long total = 0;
            for (var index = 0; index < rows.Count; index++)
                total += (long)rows[index].Amount * rows[index].CongestedTravelSeconds;
            return total;
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
