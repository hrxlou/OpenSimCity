using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Transit;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TransitPersistenceTests
    {
        [Test]
        public void BaseCityAndTransitCapabilitySnapshotsRestoreSameNextDerivedResult()
        {
            var options = Options();
            var runtime = Build(runtimeOptions: options);
            var module = AddTransit(runtime);
            module.RunCurrentMonth();

            var citySnapshot = CitySnapshotSerializer.Capture(runtime);
            var transitSnapshot = module.Capture();
            var expectedNetwork = module.Network.ComputeChecksum();
            var expectedMode = module.ModeChoice.State.ComputeChecksum();
            var expectedRouting = module.PrivateVehicleRouting.State.ComputeChecksum();
            var expectedFunds = runtime.Funds;

            var restoredRuntime = CitySnapshotSerializer.Restore(citySnapshot, options);
            var restoredModule = TransitRuntimeRegistry.GetOrCreate(restoredRuntime);
            restoredModule.Restore(transitSnapshot);

            Assert.That(restoredModule.Network.ComputeChecksum(), Is.EqualTo(expectedNetwork));
            Assert.That(restoredModule.ModeChoice.State.ComputeChecksum(), Is.EqualTo(expectedMode));
            Assert.That(restoredModule.PrivateVehicleRouting.State.ComputeChecksum(), Is.EqualTo(expectedRouting));
            Assert.That(restoredRuntime.Funds, Is.EqualTo(expectedFunds));
            Assert.That(restoredModule.Capture().SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void RestoreRejectsUnknownCapabilitySchemaAndUnorderedStableIds()
        {
            var runtime = Build(Options());
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            Assert.Throws<System.InvalidOperationException>(() =>
                module.Restore(new TransitRuntimeSnapshotDto { SchemaVersion = 2 }));

            var invalid = new TransitRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Network = new TransitNetworkStateDto
                {
                    ActiveBudgetPermille = 1000,
                    Stops = new[]
                    {
                        new TransitStopStateDto { Id = 2, RoadCellX = 2, RoadCellY = 2, Name = "B" },
                        new TransitStopStateDto { Id = 1, RoadCellX = 1, RoadCellY = 2, Name = "A" }
                    }
                }
            };
            Assert.Throws<System.ArgumentException>(() => module.Restore(invalid));
        }

        private static CityRuntimeOptions Options() => new CityRuntimeOptions
        {
            Width = 20,
            Height = 12,
            Seed = 0x505BUL,
            TicksPerMonth = 1,
            FirstSimulationMonth = new SimulationDate(1, 1),
            InitialFunds = 100_000
        };

        private static CityRuntime Build(CityRuntimeOptions runtimeOptions)
        {
            var runtime = NewCityRuntimeFactory.Create(runtimeOptions);
            var y = runtime.Height / 2;
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, y),
                new CellCoord(15, y))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(2, y - 2),
                new CellCoord(3, y - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.CommercialLow,
                new CellCoord(6, y - 2),
                new CellCoord(7, y - 1))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new CellCoord(10, y - 2),
                new CellCoord(11, y - 1))));
            var powerCell = new CellCoord(15, y - 1);
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(powerCell),
                runtime.DefaultPowerPlantDefinitionId,
                powerCell)));
            for (var month = 0; month < 6; month++)
                Assert.That(runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds), Is.EqualTo(1));
            return runtime;
        }

        private static TransitRuntimeModule AddTransit(CityRuntime runtime)
        {
            var module = TransitRuntimeRegistry.GetOrCreate(runtime);
            var y = runtime.Height / 2;
            var a = new TransitStopId(0xE100000000000011UL);
            var b = new TransitStopId(0xE100000000000012UL);
            var c = new TransitStopId(0xE100000000000013UL);
            AssertApplied(module.Execute(new PlaceTransitStopCommand(a, new CellCoord(2, y), "A")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(b, new CellCoord(7, y), "B")));
            AssertApplied(module.Execute(new PlaceTransitStopCommand(c, new CellCoord(11, y), "C")));
            AssertApplied(module.Execute(new UpsertTransitLineCommand(
                new TransitLineId(0xE200000000000011UL),
                "L1",
                new[] { a, b, c },
                8)));
            return module;
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
