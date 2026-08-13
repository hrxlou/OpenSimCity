using NUnit.Framework;
using OpenSimCity.Application.Verification;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P8MapScopeContractTests
    {
        [Test]
        public void IncompleteEvidenceKeepsCurrent64By64Default()
        {
            var result = P8MapScopeDecision.Evaluate(
                new P8BenchmarkGateResult(false, P8BenchmarkGateReason.MissingApprovedBudget, "pending"),
                new P8BenchmarkGateResult(false, P8BenchmarkGateReason.MissingRuns, "pending"));
            Assert.That(result.Scope, Is.EqualTo(P8MapScopeKind.Fixed64));
            Assert.That(P8MapScopeDecision.MatchesCurrentWorldDefaults(), Is.True);
            Assert.That(CityRuntimeOptions.Default.Width, Is.EqualTo(WorldState.DefaultWidth));
            Assert.That(CityRuntimeOptions.Default.Height, Is.EqualTo(WorldState.DefaultHeight));
        }

        [Test]
        public void PassingBothLanesOnlyMakesExpansionEligible()
        {
            var result = P8MapScopeDecision.Evaluate(
                new P8BenchmarkGateResult(true, P8BenchmarkGateReason.Passed, "pass"),
                new P8BenchmarkGateResult(true, P8BenchmarkGateReason.Passed, "pass"));
            Assert.That(result.Scope, Is.EqualTo(P8MapScopeKind.ExpansionEligible));
            Assert.That(WorldState.DefaultWidth, Is.EqualTo(64));
            Assert.That(WorldState.DefaultHeight, Is.EqualTo(64));
        }
    }
}
