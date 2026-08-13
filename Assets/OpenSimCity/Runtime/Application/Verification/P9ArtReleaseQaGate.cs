using System;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class P9ArtReleaseQaEvidence
    {
        public string CommitSha = string.Empty;
        public string LicenseAuditEvidenceId = string.Empty;
        public string WindowsBuildEvidenceId = string.Empty;
        public string MacBuildEvidenceId = string.Empty;
        public string VisualQaEvidenceId = string.Empty;
        public bool ProvenanceValidatorPassed;
        public bool PerformanceBudgetApproved;
    }

    public static class P9ArtReleaseQaGate
    {
        public static bool IsApproved(P9ArtReleaseQaEvidence evidence)
        {
            return evidence != null &&
                   !string.IsNullOrWhiteSpace(evidence.CommitSha) &&
                   !string.IsNullOrWhiteSpace(evidence.LicenseAuditEvidenceId) &&
                   !string.IsNullOrWhiteSpace(evidence.WindowsBuildEvidenceId) &&
                   !string.IsNullOrWhiteSpace(evidence.MacBuildEvidenceId) &&
                   !string.IsNullOrWhiteSpace(evidence.VisualQaEvidenceId) &&
                   evidence.ProvenanceValidatorPassed && evidence.PerformanceBudgetApproved;
        }
    }
}
