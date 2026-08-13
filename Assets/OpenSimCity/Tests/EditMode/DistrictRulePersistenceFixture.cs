using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class DistrictRulePersistenceFixture
    {
        [Test]
        public void SaveRoundTripAndDistrictDeletionRemoveStaleSettings()
        {
            var runtime = Runtime();
            var districts = DistrictRuntimeRegistry.GetOrCreate(runtime);
            var id = districts.NextDistrictId();
            districts.Execute(new CreateDistrictCommand(id, "A", 1u));
            var module = DistrictPolicyRuntimeRegistry.GetOrCreate(runtime);
            module.Execute(new SetDistrictPolicyCommand(id, DistrictPolicyKind.HeavyVehicleRestriction, true));
            module.RunMonth(runtime.MonthlyScheduler.NextMonth);
            var saved = module.Capture();

            var restoredRuntime = Runtime();
            var restoredDistricts = DistrictRuntimeRegistry.GetOrCreate(restoredRuntime);
            restoredDistricts.Execute(new CreateDistrictCommand(id, "A", 1u));
            var restored = DistrictPolicyRuntimeRegistry.GetOrCreate(restoredRuntime);
            restored.Restore(saved);
            Assert.That(restored.Policies.TryGet(id, DistrictPolicyKind.HeavyVehicleRestriction, out var state), Is.True);
            Assert.That(state.IsEnabled, Is.True);
            Assert.That(state.LastPaidCost, Is.EqualTo(restored.Parameters.HeavyVehicleCost));

            restoredDistricts.Execute(new DeleteDistrictCommand(id));
            restored.PruneMissingDistricts();
            Assert.That(restored.Policies.Rows, Is.Empty);
        }

        private static CityRuntime Runtime() => NewCityRuntimeFactory.Create(new CityRuntimeOptions
        {
            Width = 8, Height = 8, Seed = 0xD708UL, TicksPerMonth = 1,
            FirstSimulationMonth = new SimulationDate(1, 1), InitialFunds = 1000
        });
    }
}
