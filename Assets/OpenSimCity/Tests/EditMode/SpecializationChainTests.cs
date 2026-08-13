using NUnit.Framework;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpecializationChainTests
    {
        [Test]
        public void FiveMonthsUnlockAllStagesAndConservePeriodInventories()
        {
            var env = new FixtureEnvironment();
            var system = new UrbanManufacturingSpecializationSystem(env);
            var raw = 0;
            var intermediate = 0;
            var finished = 0;
            for (var month = 1; month <= 5; month++)
            {
                var beforeImports = system.State.CumulativeImports;
                system.RunMonth(new SimulationDate(1, month));
                var imported = system.State.CumulativeImports - beforeImports;
                var material = system.State.Stage(SpecializationStageKind.MaterialPreparation);
                var assembly = system.State.Stage(SpecializationStageKind.Assembly);
                var export = system.State.Stage(SpecializationStageKind.Export);
                Assert.That(system.State.RawInventory, Is.EqualTo(raw + imported - material.LastInput));
                Assert.That(system.State.IntermediateInventory, Is.EqualTo(intermediate + material.LastOutput - assembly.LastInput));
                Assert.That(system.State.FinishedInventory, Is.EqualTo(finished + assembly.LastOutput - export.LastOutput));
                Assert.That(material.LastInput, Is.EqualTo(material.LastOutput + material.LastLoss));
                Assert.That(assembly.LastInput, Is.EqualTo(assembly.LastOutput + assembly.LastLoss));
                raw = system.State.RawInventory;
                intermediate = system.State.IntermediateInventory;
                finished = system.State.FinishedInventory;
            }
            Assert.That(system.State.Stage(SpecializationStageKind.MaterialPreparation).IsUnlocked, Is.True);
            Assert.That(system.State.Stage(SpecializationStageKind.Assembly).IsUnlocked, Is.True);
            Assert.That(system.State.Stage(SpecializationStageKind.Export).IsUnlocked, Is.True);
            Assert.That(system.State.CumulativeExports, Is.GreaterThan(0));
        }

        [Test]
        public void RestorePreservesStateAndRejectsInventoryBeyondLimit()
        {
            var env = new FixtureEnvironment();
            var source = new UrbanManufacturingSpecializationSystem(env);
            for (var month = 1; month <= 6; month++) source.RunMonth(new SimulationDate(1, month));
            var dto = source.State.ToDto();
            var restored = new UrbanManufacturingSpecializationSystem(env);
            restored.RestoreFromDto(dto);
            var copy = restored.State.ToDto();
            Assert.That(copy.CumulativeExports, Is.EqualTo(dto.CumulativeExports));
            Assert.That(copy.CumulativeRevenue, Is.EqualTo(dto.CumulativeRevenue));
            Assert.That(copy.RawInventory, Is.EqualTo(dto.RawInventory));
            Assert.That(copy.Stages[2].IsUnlocked, Is.EqualTo(dto.Stages[2].IsUnlocked));
            dto.RawInventory = source.Parameters.InventoryLimit + 1;
            Assert.Throws<System.ArgumentException>(() => restored.RestoreFromDto(dto));
        }

        private sealed class FixtureEnvironment : ISpecializationEnvironmentProvider
        {
            public SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month)
            {
                return new SpecializationEnvironmentSnapshot(3, 24, 24, true, 1000, 200, 700);
            }
        }
    }
}
