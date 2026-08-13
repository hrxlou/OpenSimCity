using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class DistrictPeriodFixture
    {
        [Test]
        public void ScheduledSettingStartsOnNextMonthlyBoundary()
        {
            var runtime = CreateRuntime(100);
            var districts = DistrictRuntimeRegistry.GetOrCreate(runtime);
            var id = districts.NextDistrictId();
            districts.Execute(new CreateDistrictCommand(id, "A", 1u));
            var module = DistrictPolicyRuntimeRegistry.GetOrCreate(runtime);
            module.Execute(new SetDistrictPolicyCommand(id, DistrictPolicyKind.HeavyVehicleRestriction, true));
            module.Policies.TryGet(id, DistrictPolicyKind.HeavyVehicleRestriction, out var state);
            Assert.That(state.IsEnabled, Is.False);
            var funds = runtime.Funds;
            module.RunMonth(runtime.MonthlyScheduler.NextMonth);
            Assert.That(state.IsEnabled, Is.True);
            Assert.That(state.IsFundedAndEffective, Is.True);
            Assert.That(runtime.Funds, Is.EqualTo(funds - module.Parameters.HeavyVehicleCost));
        }

        private static CityRuntime CreateRuntime(int funds) => NewCityRuntimeFactory.Create(new CityRuntimeOptions
        {
            Width = 8, Height = 8, Seed = 0xD706UL, TicksPerMonth = 1,
            FirstSimulationMonth = new SimulationDate(1, 1), InitialFunds = funds
        });
    }
}
