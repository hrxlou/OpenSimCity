using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9ThirdPartyProvenanceTests
    {
        [Test]
        public void PinnedManifestCoversProductionCatalog()
        {
            Assert.That(P9ThirdPartyProvenance.ValidateStaticManifest(), Is.True);
            Assert.That(P9ThirdPartyProvenance.Assets().Count, Is.EqualTo(6));
            Assert.That(P9ThirdPartyProvenance.Pack().LicenseSpdx, Is.EqualTo("CC0-1.0"));
        }

        [Test]
        public void FinalApprovalNeedsPlatformAndVisualEvidence()
        {
            Assert.That(P9ArtReleaseQaGate.IsApproved(null), Is.False);
            Assert.That(P9ArtReleaseQaGate.IsApproved(new P9ArtReleaseQaEvidence
            {
                CommitSha = "fixture",
                LicenseAuditEvidenceId = "fixture",
                ProvenanceValidatorPassed = true
            }), Is.False);
        }
    }
}
