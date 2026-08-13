using NUnit.Framework;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpecializationBottleneckTests
    {
        [Test]
        public void UnlockedChainReportsMarketPowerAndTrafficLimits()
        {
            var env = new MutableEnvironment();
            var system = new UrbanManufacturingSpecializationSystem(env);
            for (var month = 1; month <= 5; month++) system.RunMonth(new SimulationDate(1, month));

            env.Value = new SpecializationEnvironmentSnapshot(3, 24, 24, false, 0, 100, 800);
            system.RunMonth(new SimulationDate(1, 6));
            Assert.That(system.State.Stage(SpecializationStageKind.Export).Bottleneck,
                Is.EqualTo(SpecializationBottleneckReason.NoExternalMarket));

            env.Value = new SpecializationEnvironmentSnapshot(3, 24, 0, true, 1000, 100, 800);
            system.RunMonth(new SimulationDate(1, 7));
            Assert.That(system.State.Stage(SpecializationStageKind.MaterialPreparation).Bottleneck,
                Is.EqualTo(SpecializationBottleneckReason.PowerShortage));

            env.Value = new SpecializationEnvironmentSnapshot(3, 24, 24, true, 250, 100, 800);
            system.RunMonth(new SimulationDate(1, 8));
            Assert.That(system.State.Stage(SpecializationStageKind.Export).Bottleneck,
                Is.EqualTo(SpecializationBottleneckReason.TransportBottleneck));
        }

        private sealed class MutableEnvironment : ISpecializationEnvironmentProvider
        {
            public SpecializationEnvironmentSnapshot Value { get; set; } =
                new SpecializationEnvironmentSnapshot(3, 24, 24, true, 1000, 100, 800);
            public SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month) => Value;
        }
    }
}
