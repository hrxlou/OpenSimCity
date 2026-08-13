using NUnit.Framework;
using OpenSimCity.Application.Specialization;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpecializationRecoveryTests
    {
        [Test]
        public void TrafficBottleneckRecoversAndReadModelKeepsSamePeriod()
        {
            var env = new MutableEnvironment { Value = new SpecializationEnvironmentSnapshot(3, 24, 24, true, 1000, 300, 700) };
            var runtime = new CityRuntime(new CityRuntimeOptions { Width = 8, Height = 8, Seed = 0x705UL });
            var module = SpecializationRuntimeRegistry.GetOrCreate(runtime, env);
            for (var month = 1; month <= 5; month++) module.System.RunMonth(new SimulationDate(1, month));

            env.Value = new SpecializationEnvironmentSnapshot(3, 24, 24, true, 250, 300, 700);
            module.System.RunMonth(new SimulationDate(1, 6));
            var blocked = module.System.State.CumulativeExports;
            Assert.That(module.System.State.Stage(SpecializationStageKind.Export).Bottleneck,
                Is.EqualTo(SpecializationBottleneckReason.TransportBottleneck));

            env.Value = new SpecializationEnvironmentSnapshot(3, 24, 24, true, 1000, 300, 700);
            module.System.RunMonth(new SimulationDate(1, 7));
            var before = module.System.State.ToDto();
            var readModel = new SpecializationReadModel(module);
            readModel.RefreshIfNeeded();
            var after = module.System.State.ToDto();

            Assert.That(module.System.State.CumulativeExports, Is.GreaterThan(blocked));
            Assert.That(module.System.State.Stage(SpecializationStageKind.Export).Bottleneck,
                Is.Not.EqualTo(SpecializationBottleneckReason.TransportBottleneck));
            Assert.That(readModel.Snapshot.Year, Is.EqualTo(1));
            Assert.That(readModel.Snapshot.Month, Is.EqualTo(7));
            Assert.That(readModel.Snapshot.LastRevenue, Is.EqualTo(module.System.State.LastRevenue));
            Assert.That(after.CumulativeExports, Is.EqualTo(before.CumulativeExports));
            Assert.That(after.RawInventory, Is.EqualTo(before.RawInventory));
        }

        private sealed class MutableEnvironment : ISpecializationEnvironmentProvider
        {
            public SpecializationEnvironmentSnapshot Value { get; set; }
            public SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month) => Value;
        }
    }
}
