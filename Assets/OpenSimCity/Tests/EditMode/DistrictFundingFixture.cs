using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class DistrictFundingFixture
    {
        [Test]
        public void SettingDoesNotChangeTrafficInputWhenMonthlyCostCannotBePaid()
        {
            var runtime = NewCityRuntimeFactory.Create(new CityRuntimeOptions
            {
                Width = 8, Height = 8, Seed = 0xD707UL, TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1), InitialFunds = 10
            });
            var districts = DistrictRuntimeRegistry.GetOrCreate(runtime);
            var id = districts.NextDistrictId();
            districts.Execute(new CreateDistrictCommand(id, "A", 1u));
            var module = DistrictPolicyRuntimeRegistry.GetOrCreate(runtime);
            module.Execute(new SetDistrictPolicyCommand(id, DistrictPolicyKind.TransitSubsidy, true));
            module.RunMonth(runtime.MonthlyScheduler.NextMonth);
            module.Policies.TryGet(id, DistrictPolicyKind.TransitSubsidy, out var state);
            Assert.That(state.IsEnabled, Is.True);
            Assert.That(state.IsFundedAndEffective, Is.False);
            Assert.That(state.LastPaidCost, Is.Zero);
            Assert.That(state.LastReason, Is.EqualTo(DistrictPolicyOperatingReason.InsufficientFunding));
            Assert.That(runtime.Funds, Is.EqualTo(10));
        }
    }
}
