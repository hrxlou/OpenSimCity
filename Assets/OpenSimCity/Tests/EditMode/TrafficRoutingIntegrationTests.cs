using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TrafficRoutingIntegrationTests
    {
        [Test]
        public void SnapshotRebuildsIdenticalRoutingWithoutSchemaField()
        {
            var options = RuntimeOptions();
            var runtime = NewCityRuntimeFactory.Create(options);
            ApplyPublicGrowthLayout(runtime);
            for (var month = 0; month < 6; month++)
                RunRuntimeMonth(runtime, month);

            Assert.That(runtime.TrafficRoutingSystem.State.HasProcessedMonth, Is.True);
            Assert.That(runtime.TrafficRoutingSystem.State.RoutedAmount, Is.GreaterThan(0));
            Assert.That(runtime.TrafficRoutingSystem.State.Edges, Is.Not.Empty);
            var expectedChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();
            var expectedEdges = runtime.TrafficRoutingSystem.State.Edges.Count;
            var expectedAssignments = runtime.TrafficRoutingSystem.State.Assignments.Count;

            var observedChecksum = runtime.TrafficRoutingModel.Checksum;
            Assert.That(observedChecksum, Is.EqualTo(expectedChecksum));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(expectedChecksum));

            var json = JsonUtility.ToJson(CitySnapshotSerializer.Capture(runtime));
            Assert.That(json, Does.Not.Contain("TrafficRouting"));
            Assert.That(json, Does.Not.Contain("TrafficRoute"));
            var dto = JsonUtility.FromJson<CitySnapshotDto>(json);
            var restored = CitySnapshotSerializer.Restore(dto, options);

            Assert.That(restored.TrafficRoutingModel.HasProcessedMonth, Is.True);
            Assert.That(restored.TrafficRoutingModel.Edges, Has.Count.EqualTo(expectedEdges));
            Assert.That(restored.TrafficRoutingModel.Assignments, Has.Count.EqualTo(expectedAssignments));
            Assert.That(restored.TrafficRoutingModel.Checksum, Is.EqualTo(expectedChecksum));
            Assert.That(restored.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(expectedChecksum));
        }

        private static CityRuntimeOptions RuntimeOptions()
        {
            return new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x502UL,
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
    }
}
