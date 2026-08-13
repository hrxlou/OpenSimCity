using System;
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
    public sealed class TransitCostAdjustmentTests
    {
        [Test]
        public void CostAdjustmentPreservesOdAndChangesOnlyBusGeneralizedCostInput()
        {
            var runtime = BuildGrowingCity();
            var module = BuildLine(runtime);
            module.RunCurrentMonth();
            var baseline = module.ModeChoice.State.Rows.First(row => row.BusGeneralizedCostSeconds > 0);
            var requested = module.ModeChoice.State.RequestedCommuteTrips;

            module.ModeChoice.CostPolicy = new HalfCostPolicy();
            Assert.That(module.RefreshDerived(), Is.True);
            var adjusted = module.ModeChoice.State.Rows.Single(row => row.Key.Equals(baseline.Key));

            Assert.That(adjusted.BusGeneralizedCostSeconds, Is.EqualTo(Math.Max(1, baseline.BusGeneralizedCostSeconds / 2)));
            Assert.That(adjusted.CarGeneralizedCostSeconds, Is.EqualTo(baseline.CarGeneralizedCostSeconds));
            Assert.That(module.ModeChoice.State.RequestedCommuteTrips, Is.EqualTo(requested));
            Assert.That(module.ModeChoice.State.Rows.All(row => row.BusTrips + row.PrivateVehicleTrips == row.RequestedTrips), Is.True);
        }

        private sealed class HalfCostPolicy : ITransitGeneralizedCostPolicy
        {
            public int Version => 1;
            public int AdjustBusCostSeconds(TransitOdKey key, SimulationDate month, int baseCostSeconds) => Math.Max(1, baseCostSeconds / 2);
        }

        private static TransitRuntimeModule BuildLine(CityRuntime runtime)
        {
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var y = runtime.Height / 2;
            var a = new TransitStopId(0xE100000000000011UL);
            var b = new TransitStopId(0xE100000000000012UL);
            var c = new TransitStopId(0xE100000000000013UL);
            AssertApplied(module.Execute(new PlaceTransitStopCommand(a, new CellCoord(2, y), "A")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(b, new CellCoord(7, y), "B")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(c, new CellCoord(11, y), "C")));
            AssertApplied(module.Execute(new UpsertTransitLineCommand(new TransitLineId(0xE200000000000011UL), "Line", new[] { a, b, c }, 8)));
            return module;
        }

        private static CityRuntime BuildGrowingCity()
        {
            var runtime = NewCityRuntimeFactory.Create(new CityRuntimeOptions
            {
                Width = 20, Height = 12, Seed = 0xD709UL, TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1), InitialFunds = 100_000
            });
            var y = runtime.Height / 2;
            AssertApplied(runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, y), new CellCoord(15, y))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(ZoneType.ResidentialLow, new CellCoord(2, y - 2), new CellCoord(3, y - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(ZoneType.CommercialLow, new CellCoord(6, y - 2), new CellCoord(7, y - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(ZoneType.IndustrialLow, new CellCoord(10, y - 2), new CellCoord(11, y - 1))));
            var power = new CellCoord(15, y - 1);
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(runtime.PowerProducerIdFor(power), runtime.DefaultPowerPlantDefinitionId, power)));
            for (var month = 0; month < 6; month++) RunMonth(runtime, month);
            Assert.That(runtime.TripDemandSystem.State.CommuteMatched, Is.GreaterThan(0));
            return runtime;
        }

        private static void RunMonth(CityRuntime runtime, int monthIndex)
        {
            Assert.That(runtime.Clock.CurrentTick.Value, Is.EqualTo((long)monthIndex * runtime.MonthlyScheduler.TicksPerMonth));
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                Assert.That(runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds), Is.EqualTo(1));
        }

        private static void AssertApplied(CommandExecutionResult result) => Assert.That(result.IsApplied, Is.True, result.Message);
    }
}
