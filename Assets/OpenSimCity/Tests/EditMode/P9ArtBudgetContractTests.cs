using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9ArtBudgetContractTests
    {
        [Test]
        public void P3DerivedStaticBudgetsCoverProductionCatalog()
        {
            Assert.That(P9ArtBudgetManifest.CoversProductionCatalog(P9ProductionCatalog.Create()), Is.True);
            var budgets = P9ArtBudgetManifest.Create();
            Assert.That(budgets.Count, Is.EqualTo(6));
            Assert.That(budgets[0].MaximumTrianglesLod0, Is.EqualTo(18152));
            Assert.That(budgets[1].MaximumTrianglesLod0, Is.EqualTo(25612));
            Assert.That(budgets[2].MaximumTrianglesLod0, Is.EqualTo(44786));
            for (var index = 0; index < budgets.Count; index++)
            {
                Assert.That(budgets[index].LodDecision, Is.EqualTo(P9LodDecision.PendingMeasuredNeed));
                Assert.That(budgets[index].HasMeasuredTextureMemoryBudget, Is.False);
            }
        }

        [Test]
        public void StaticCeilingRejectsGeometryRegression()
        {
            Assert.That(P9ArtStaticBudgetValidator.IsWithinBudget(
                "OSC_REP_BLD_SMALL", 1, 13, 18152, false), Is.True);
            Assert.That(P9ArtStaticBudgetValidator.IsWithinBudget(
                "OSC_REP_BLD_SMALL", 1, 13, 18153, false), Is.False);
        }

        [Test]
        public void PerformanceApprovalRequiresProfilerAndP8Evidence()
        {
            Assert.That(P9ArtPerformanceGate.IsApproved(null), Is.False);
            Assert.That(P9ArtPerformanceGate.IsApproved(new P9ArtPerformanceEvidence
            {
                Platform = "fixture",
                HardwareId = "fixture",
                UnityVersion = "fixture",
                HasDrawCallMeasurement = true,
                HasGpuFrameMeasurement = true,
                HasTextureMemoryMeasurement = true,
                PassedApprovedP8Budget = false
            }), Is.False);
        }
    }
}
