using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TrafficVisualizationPersistenceTests
    {
        [Test]
        public void SnapshotOmitsViewsAndRebuildsIdenticalSamples()
        {
            var options = RuntimeOptions();
            var runtime = NewCityRuntimeFactory.Create(options);
            ApplyPublicGrowthLayout(runtime);
            for (var month = 0; month < 6; month++)
                RunRuntimeMonth(runtime, month);

            var sourceChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();
            var visualization = new TrafficVisualizationReadModel(runtime.TrafficRoutingModel);
            visualization.SetMaximumVehicleSamples(500);
            visualization.RefreshIfNeeded();
            Assert.That(visualization.VehicleSamples, Is.Not.Empty);
            var visualizationChecksum = visualization.VisualizationChecksum;
            var sampleIds = visualization.VehicleSamples.Select(row => row.SampleId).ToArray();

            var json = JsonUtility.ToJson(CitySnapshotSerializer.Capture(runtime));
            Assert.That(json, Does.Not.Contain("TrafficVisualization"));
            Assert.That(json, Does.Not.Contain("VehicleSample"));
            Assert.That(json, Does.Not.Contain("CongestionDataMap"));
            Assert.That(json, Does.Not.Contain("PooledVehicle"));

            var dto = JsonUtility.FromJson<CitySnapshotDto>(json);
            var restored = CitySnapshotSerializer.Restore(dto, options);
            var restoredVisualization = new TrafficVisualizationReadModel(restored.TrafficRoutingModel);
            restoredVisualization.SetMaximumVehicleSamples(500);
            restoredVisualization.RefreshIfNeeded();

            Assert.That(restored.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(sourceChecksum));
            Assert.That(restoredVisualization.SourceSimulationChecksum, Is.EqualTo(sourceChecksum));
            Assert.That(restoredVisualization.VisualizationChecksum, Is.EqualTo(visualizationChecksum));
            Assert.That(restoredVisualization.VehicleSamples.Select(row => row.SampleId), Is.EqualTo(sampleIds));
        }

        private static CityRuntimeOptions RuntimeOptions()
        {
            return new CityRuntimeOptions
            {
                Width = 20,
                Height = 12,
                Seed = 0x503UL,
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
