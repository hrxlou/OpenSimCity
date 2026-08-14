using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingConditionPersistenceGuardTests
    {
        [Test]
        public void CapabilitySnapshotRoundTripsKnownBuildingAndPreservesNextResult()
        {
            var runtime = CreateRuntimeWithResidentialBuilding(out var buildingId);
            var module = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            module.System.RunMonth(new SimulationDate(1, 1));
            module.System.RunMonth(new SimulationDate(1, 2));
            var snapshot = module.Capture();
            var checksum = module.System.ComputeChecksum();

            var restoredRuntime = CreateRuntimeWithResidentialBuilding(out var restoredId);
            Assert.That(restoredId, Is.EqualTo(buildingId));
            var restored = BuildingConditionRuntimeRegistry.GetOrCreate(restoredRuntime);
            restored.Restore(snapshot);
            Assert.That(restored.System.ComputeChecksum(), Is.EqualTo(checksum));

            module.System.RunMonth(new SimulationDate(1, 3));
            restored.System.RunMonth(new SimulationDate(1, 3));
            Assert.That(restored.System.ComputeChecksum(),
                Is.EqualTo(module.System.ComputeChecksum()));
            Assert.That(restored.ReadModel.TryGet(buildingId, out var row), Is.True);
            Assert.That(row.BuildingId, Is.EqualTo(buildingId));
        }

        [Test]
        public void UnknownBuildingReferenceIsRejectedBeforeStateReplacement()
        {
            var runtime = CreateRuntimeWithResidentialBuilding(out var buildingId);
            var module = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            module.System.RunMonth(new SimulationDate(1, 1));
            var checksum = module.System.ComputeChecksum();
            var snapshot = module.Capture();
            snapshot.States[0].BuildingId = 0xB2FF_0000_0000_0001UL;

            Assert.Throws<InvalidOperationException>(() => module.Restore(snapshot));
            Assert.That(module.System.ComputeChecksum(), Is.EqualTo(checksum));
            Assert.That(module.System.State.States.Single().BuildingId, Is.EqualTo(buildingId));
        }

        [Test]
        public void DuplicateOrUnorderedRowsAreRejectedBySimulationRestore()
        {
            var runtime = CreateRuntimeWithResidentialBuilding(out _);
            var module = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            module.System.RunMonth(new SimulationDate(1, 1));
            var snapshot = module.Capture();
            var row = snapshot.States[0];
            snapshot.States = new[] { row, row };

            Assert.Throws<ArgumentException>(() => module.System.RestoreFromDto(snapshot));
        }

        private static CityRuntime CreateRuntimeWithResidentialBuilding(out StableId buildingId)
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x622UL,
                InitialFunds = 10_000
            });
            var definition = runtime.BuildingDefinitions.Definitions
                .First(item => item.ZoneType == ZoneType.ResidentialLow);
            buildingId = new StableId(0xB220_0000_0000_0301UL);
            runtime.Buildings.Add(new BuildingState(
                buildingId,
                definition.Id,
                new StableId(0xA220_0000_0000_0301UL),
                new CellCoord(2, 2),
                definition.Footprint,
                definition.OccupancyCapacity,
                0));
            return runtime;
        }
    }
}
