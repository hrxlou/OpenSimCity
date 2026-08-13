using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpecializationBalanceTests
    {
        [Test]
        public void SpecializedFixtureExportsWithHigherPollutionAndLowerServiceCoverage()
        {
            var general = new UrbanManufacturingSpecializationSystem(new FixedEnvironment(new SpecializationEnvironmentSnapshot(1, 6, 6, true, 900, 100, 850)));
            var specialized = new UrbanManufacturingSpecializationSystem(new FixedEnvironment(new SpecializationEnvironmentSnapshot(3, 24, 24, true, 700, 650, 550)));
            for (var month = 1; month <= 6; month++)
            {
                general.RunMonth(new SimulationDate(1, month));
                specialized.RunMonth(new SimulationDate(1, month));
            }
            Assert.That(general.State.CumulativeExports, Is.Zero);
            Assert.That(specialized.State.CumulativeExports, Is.GreaterThan(0));
            Assert.That(specialized.State.Diagnostics.Single(x => x.Kind == SpecializationDiagnosticKind.Pollution).ValuePermille,
                Is.GreaterThan(general.State.Diagnostics.Single(x => x.Kind == SpecializationDiagnosticKind.Pollution).ValuePermille));
            Assert.That(specialized.State.Diagnostics.Single(x => x.Kind == SpecializationDiagnosticKind.Services).ValuePermille,
                Is.LessThan(general.State.Diagnostics.Single(x => x.Kind == SpecializationDiagnosticKind.Services).ValuePermille));
        }

        [Test]
        public void LongRunRevenueIsBoundedByDeclaredExportFlow()
        {
            var system = new UrbanManufacturingSpecializationSystem(new FixedEnvironment(new SpecializationEnvironmentSnapshot(8, 120, 120, true, 1000, 900, 300)));
            var month = new SimulationDate(1, 1);
            for (var index = 0; index < 60; index++)
            {
                system.RunMonth(month);
                month = month.AddMonths(1);
            }
            Assert.That(system.State.CumulativeExports, Is.LessThanOrEqualTo(system.State.CumulativeFinishedOutput));
            Assert.That(system.State.CumulativeRevenue, Is.EqualTo((long)system.State.CumulativeExports * system.Parameters.ExportUnitPrice));
            Assert.That(system.State.RawInventory, Is.InRange(0, system.Parameters.InventoryLimit));
            Assert.That(system.State.IntermediateInventory, Is.InRange(0, system.Parameters.InventoryLimit));
            Assert.That(system.State.FinishedInventory, Is.InRange(0, system.Parameters.InventoryLimit));
        }

        private sealed class FixedEnvironment : ISpecializationEnvironmentProvider
        {
            private readonly SpecializationEnvironmentSnapshot _value;
            public FixedEnvironment(SpecializationEnvironmentSnapshot value) { _value = value; }
            public SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month) => _value;
        }
    }
}
