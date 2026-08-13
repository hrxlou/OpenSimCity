using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Transit;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TransitModeChoiceTests
    {
        [Test]
        public void AccessibleCompetitiveLineConservesOdAndReducesPrivateVehicleRouting()
        {
            var runtime = BuildGrowingCity(out _);
            var module = BuildLine(runtime);

            module.RunCurrentMonth();
            var snapshot = module.ReadModel.Snapshot;

            Assert.That(snapshot.RequestedCommuteTrips, Is.GreaterThan(0));
            Assert.That(snapshot.BusTrips, Is.GreaterThan(0));
            Assert.That(snapshot.BusTrips + snapshot.PrivateVehicleTrips,
                Is.EqualTo(snapshot.RequestedCommuteTrips));
            Assert.That(snapshot.TransitAwareRoutedTrips, Is.LessThan(snapshot.BaselineRoutedTrips));
            Assert.That(snapshot.OdRows.All(row =>
                row.BusTrips + row.PrivateVehicleTrips == row.RequestedTrips), Is.True);
            Assert.That(snapshot.OdRows.Any(row => row.Reason == TransitModeChoiceReason.None), Is.True);
        }

        [Test]
        public void ZeroBudgetAppliesNextMonthAndKeepsAllCommutesInPrivateVehicles()
        {
            var runtime = BuildGrowingCity(out var completedMonths);
            var module = BuildLine(runtime);
            module.RunCurrentMonth();
            Assert.That(module.ModeChoice.State.BusTrips, Is.GreaterThan(0));

            AssertApplied(module.Execute(new SetTransitBudgetCommand(0)));
            RunRuntimeMonth(runtime, completedMonths);
            module.RunCurrentMonth();

            Assert.That(module.Network.ActiveBudgetPermille, Is.Zero);
            Assert.That(module.ModeChoice.State.BusTrips, Is.Zero);
            Assert.That(module.ModeChoice.State.PrivateVehicleTrips,
                Is.EqualTo(module.ModeChoice.State.RequestedCommuteTrips));
            Assert.That(module.ModeChoice.State.Rows.All(row =>
                row.Reason == TransitModeChoiceReason.BudgetDisabled), Is.True);
        }

        [Test]
        public void ReadModelObservationAndRefreshDoNotChangeSimulationChecksums()
        {
            var runtime = BuildGrowingCity(out _);
            var module = BuildLine(runtime);
            module.RunCurrentMonth();

            var network = module.Network.ComputeChecksum();
            var mode = module.ModeChoice.State.ComputeChecksum();
            var routing = module.PrivateVehicleRouting.State.ComputeChecksum();
            var first = module.ReadModel.Snapshot;
            module.ReadModel.RefreshIfNeeded();
            var second = module.ReadModel.Snapshot;

            Assert.That(module.Network.ComputeChecksum(), Is.EqualTo(network));
            Assert.That(module.ModeChoice.State.ComputeChecksum(), Is.EqualTo(mode));
            Assert.That(module.PrivateVehicleRouting.State.ComputeChecksum(), Is.EqualTo(routing));
            Assert.That(second.NetworkChecksum, Is.EqualTo(first.NetworkChecksum));
            Assert.That(second.ModeChoiceChecksum, Is.EqualTo(first.ModeChoiceChecksum));
            Assert.That(second.PrivateRoutingChecksum, Is.EqualTo(first.PrivateRoutingChecksum));
        }

        [Test]
        public void LineCommandRejectsUnknownAndDisconnectedStopsWithoutSpendingFunds()
        {
            var runtime = BuildGrowingCity(out _);
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var funds = runtime.Funds;
            var missing = new TransitStopId(0xE1000000000000FFUL);
            var line = new TransitLineId(0xE200000000000001UL);

            var result = module.Execute(new UpsertTransitLineCommand(
                line,
                "Invalid",
                new[] { missing, new TransitStopId(0xE1000000000000FEUL) },
                2));

            Assert.That(result.IsApplied, Is.False);
            Assert.That(result.Details, Is.TypeOf<TransitCommandResult>());
            Assert.That(((TransitCommandResult)result.Details).FailureReason,
                Is.EqualTo(TransitCommandFailureReason.UnknownStop));
            Assert.That(runtime.Funds, Is.EqualTo(funds));
            Assert.That(module.Network.Lines, Is.Empty);
        }

        private static TransitRuntimeModule BuildLine(CityRuntime runtime)
        {
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var y = runtime.Height / 2;
            var stopA = new TransitStopId(0xE100000000000001UL);
            var stopB = new TransitStopId(0xE100000000000002UL);
            var stopC = new TransitStopId(0xE100000000000003UL);
            AssertApplied(module.Execute(new PlaceTransitStopCommand(stopA, new CellCoord(2, y), "주거")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(stopB, new CellCoord(7, y), "상업")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(stopC, new CellCoord(11, y), "산업")));
            AssertApplied(module.Execute(new UpsertTransitLineCommand(
                new TransitLineId(0xE200000000000001UL),
                "간선 1",
                new[] { stopA, stopB, stopC },
                8)));
            return module;
        }

        private static CityRuntime BuildGrowingCity(out int completedMonths)
        {
            var options = new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x505AUL,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1),
                InitialFunds = 100_000
            };
            var runtime = NewCityRuntimeFactory.Create(options);
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

            completedMonths = 6;
            for (var month = 0; month < completedMonths; month++)
                RunRuntimeMonth(runtime, month);
            Assert.That(runtime.TripDemandSystem.State.CommuteMatched, Is.GreaterThan(0));
            return runtime;
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
    }
}
