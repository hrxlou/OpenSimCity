using NUnit.Framework;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9ProductionArtContractTests
    {
        [Test]
        public void VerticalSliceStaticContractPasses()
        {
            var rows = P9ProductionArtVerticalSlice.Create();
            var result = P9ProductionArtVerticalSlice.ValidateStaticContract(rows);
            Assert.That(result.Passed, Is.True, result.Message);
            for (var index = 0; index < rows.Count; index++)
                Assert.That(rows[index].PlaceholderFallbackForbidden, Is.True);
        }

        [Test]
        public void VisualApprovalIsUnverifiedWithoutEvidence()
        {
            var result = P9ProductionArtVerticalSlice.EvaluateVisualApproval(
                P9ProductionArtVerticalSlice.Create(), new P9ArtVisualApprovalEvidence[0]);
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P9ProductionArtGateReason.MissingVisualEvidence));
        }

        [Test]
        public void ServicePresentationKeysAreStable()
        {
            Assert.That(PublicServicePresentationKeys.For(ServiceKindView.Police), Is.EqualTo(PublicServicePresentationKeys.Police));
            Assert.That(PublicServicePresentationKeys.For(ServiceKindView.Fire), Is.EqualTo(PublicServicePresentationKeys.Fire));
            Assert.That(PublicServicePresentationKeys.For(ServiceKindView.School), Is.EqualTo(PublicServicePresentationKeys.School));
            Assert.That(PublicServicePresentationKeys.For(ServiceKindView.Healthcare), Is.EqualTo(PublicServicePresentationKeys.Healthcare));
            Assert.That(PublicServicePresentationKeys.For(ServiceKindView.Waste), Is.EqualTo(PublicServicePresentationKeys.Waste));
        }
    }
}
