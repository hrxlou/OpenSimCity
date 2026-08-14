using System;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class ReleaseRegressionEvidence
    {
        public string CommitSha = string.Empty;
        public bool EditModeExecuted;
        public bool EditModePassed;
        public int EditModeTestCount;
        public string EditModeEvidenceId = string.Empty;
        public bool PlayModeExecuted;
        public bool PlayModePassed;
        public int PlayModeTestCount;
        public string PlayModeEvidenceId = string.Empty;
    }

    [Serializable]
    public sealed class ReleaseSoakEvidence
    {
        public ReleaseBuildPlatform Platform;
        public string CommitSha = string.Empty;
        public int DurationMinutes;
        public bool Completed;
        public bool CrashFree;
        public bool FatalLogFree;
        public string EvidenceId = string.Empty;
    }

    [Serializable]
    public sealed class ReleasePerformanceEvidence
    {
        public ReleaseBuildPlatform Platform;
        public string CommitSha = string.Empty;
        public P8BenchmarkScenarioKind Scenario;
        public bool GateEvaluated;
        public bool GatePassed;
        public P8BenchmarkGateReason GateReason;
        public string EvidenceId = string.Empty;
    }

    [Serializable]
    public sealed class ReleaseSaveCompatibilityEvidence
    {
        public string CommitSha = string.Empty;
        public bool CurrentSaveRoundTripPassed;
        public bool PreviousSupportedSaveLoadPassed;
        public bool CrossPlatformExchangePassed;
        public bool MissingDefinitionFailsClosed;
        public bool UnknownCatalogFailsClosed;
        public string EvidenceId = string.Empty;
    }

    [Serializable]
    public sealed class ReleasePackagingEvidence
    {
        public string CommitSha = string.Empty;
        public bool WindowsPackageLaunchPassed;
        public bool MacOSPackageLaunchPassed;
        public bool WindowsUserDataPathChecked;
        public bool MacOSGatekeeperPolicyChecked;
        public bool SigningNotarizationPolicyRecorded;
        public bool CrashAndLogPathsRecorded;
        public bool KnownIssuesReviewed;
        public bool BuildShaRecorded;
        public string EvidenceId = string.Empty;
    }

    [Serializable]
    public sealed class ReleaseCandidateEvidence
    {
        public ReleaseBuildExecutionEvidence WindowsBuild;
        public ReleaseBuildExecutionEvidence MacOSBuild;
        public ReleaseRegressionEvidence Regression;
        public ReleaseSoakEvidence[] Soak = Array.Empty<ReleaseSoakEvidence>();
        public ReleasePerformanceEvidence[] Performance = Array.Empty<ReleasePerformanceEvidence>();
        public ReleaseSaveCompatibilityEvidence SaveCompatibility;
        public ReleasePackagingEvidence Packaging;
        public string[] KnownIssues = Array.Empty<string>();
    }

    public enum ReleaseCandidateGateReason : byte
    {
        Passed,
        BuildPairFailed,
        MissingRegressionEvidence,
        RegressionCommitMismatch,
        RegressionNotExecuted,
        RegressionFailed,
        MissingSoakEvidence,
        SoakCommitMismatch,
        SoakTooShort,
        SoakFailed,
        MissingPerformanceEvidence,
        PerformanceCommitMismatch,
        PerformanceGateNotPassed,
        MissingSaveCompatibilityEvidence,
        SaveCompatibilityCommitMismatch,
        SaveCompatibilityFailed,
        MissingArtQaEvidence,
        ArtQaCommitMismatch,
        ArtQaFailed,
        MissingPackagingEvidence,
        PackagingCommitMismatch,
        PackagingFailed,
        MissingKnownIssuesRecord
    }

    public sealed class ReleaseCandidateGateResult
    {
        public ReleaseCandidateGateResult(bool passed, ReleaseCandidateGateReason reason, string message)
        {
            Passed = passed;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Passed { get; }
        public ReleaseCandidateGateReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Final P9 release-candidate gate. It composes already-recorded build, benchmark,
    /// art QA, regression, soak, save and packaging evidence. Source/fixture presence
    /// never counts as an executed pass and every runtime evidence record is pinned to
    /// the same source commit as the Windows/macOS candidate pair.
    /// </summary>
    public static class ReleaseCandidateGate
    {
        public const int MinimumSoakMinutes = 30;

        public static ReleaseCandidateGateResult Evaluate(
            ReleaseCandidateEvidence evidence,
            P9ArtReleaseQaEvidence artQaEvidence)
        {
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));

            var buildPair = ReleaseBuildPairGate.Evaluate(evidence.WindowsBuild, evidence.MacOSBuild);
            if (!buildPair.Passed)
                return Fail(ReleaseCandidateGateReason.BuildPairFailed, buildPair.Message);

            var commit = evidence.WindowsBuild.SourceCommit.Trim();

            var regression = evidence.Regression;
            if (regression == null ||
                string.IsNullOrWhiteSpace(regression.EditModeEvidenceId) ||
                string.IsNullOrWhiteSpace(regression.PlayModeEvidenceId))
                return Fail(ReleaseCandidateGateReason.MissingRegressionEvidence,
                    "EditMode and PlayMode regression evidence IDs are required.");
            if (!SameCommit(regression.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.RegressionCommitMismatch,
                    "Regression evidence must be produced from the release candidate commit.");
            if (!regression.EditModeExecuted || !regression.PlayModeExecuted ||
                regression.EditModeTestCount <= 0 || regression.PlayModeTestCount <= 0)
                return Fail(ReleaseCandidateGateReason.RegressionNotExecuted,
                    "Both regression suites must be executed and discover at least one test.");
            if (!regression.EditModePassed || !regression.PlayModePassed)
                return Fail(ReleaseCandidateGateReason.RegressionFailed,
                    "EditMode and PlayMode regression suites must both pass.");

            var windowsSoak = FindSoak(evidence.Soak, ReleaseBuildPlatform.Windows);
            var macSoak = FindSoak(evidence.Soak, ReleaseBuildPlatform.MacOS);
            if (!HasSoakEvidence(windowsSoak) || !HasSoakEvidence(macSoak))
                return Fail(ReleaseCandidateGateReason.MissingSoakEvidence,
                    "Windows and macOS soak evidence IDs are required.");
            if (!SameCommit(windowsSoak.CommitSha, commit) || !SameCommit(macSoak.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.SoakCommitMismatch,
                    "Both soak runs must use the release candidate commit.");
            if (windowsSoak.DurationMinutes < MinimumSoakMinutes || macSoak.DurationMinutes < MinimumSoakMinutes)
                return Fail(ReleaseCandidateGateReason.SoakTooShort,
                    "Each platform requires at least 30 minutes of release-build soak evidence.");
            if (!SoakPassed(windowsSoak) || !SoakPassed(macSoak))
                return Fail(ReleaseCandidateGateReason.SoakFailed,
                    "Both soak runs must complete without crashes or fatal log findings.");

            var windowsPerformance = FindPerformance(evidence.Performance, ReleaseBuildPlatform.Windows);
            var macPerformance = FindPerformance(evidence.Performance, ReleaseBuildPlatform.MacOS);
            if (!HasPerformanceEvidence(windowsPerformance) || !HasPerformanceEvidence(macPerformance))
                return Fail(ReleaseCandidateGateReason.MissingPerformanceEvidence,
                    "Windows and macOS P8 Target128 evidence IDs are required.");
            if (!SameCommit(windowsPerformance.CommitSha, commit) || !SameCommit(macPerformance.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.PerformanceCommitMismatch,
                    "Performance evidence must use the release candidate commit.");
            if (!PerformancePassed(windowsPerformance) || !PerformancePassed(macPerformance))
                return Fail(ReleaseCandidateGateReason.PerformanceGateNotPassed,
                    "Both platforms must record a passing P8 Target128 benchmark gate result.");

            var save = evidence.SaveCompatibility;
            if (save == null || string.IsNullOrWhiteSpace(save.EvidenceId))
                return Fail(ReleaseCandidateGateReason.MissingSaveCompatibilityEvidence,
                    "Save compatibility evidence is required.");
            if (!SameCommit(save.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.SaveCompatibilityCommitMismatch,
                    "Save compatibility evidence must use the release candidate commit.");
            if (!save.CurrentSaveRoundTripPassed ||
                !save.PreviousSupportedSaveLoadPassed ||
                !save.CrossPlatformExchangePassed ||
                !save.MissingDefinitionFailsClosed ||
                !save.UnknownCatalogFailsClosed)
                return Fail(ReleaseCandidateGateReason.SaveCompatibilityFailed,
                    "Current, previous-supported, cross-platform and fail-closed save checks must all pass.");

            if (artQaEvidence == null ||
                string.IsNullOrWhiteSpace(artQaEvidence.LicenseAuditEvidenceId) ||
                string.IsNullOrWhiteSpace(artQaEvidence.VisualQaEvidenceId))
                return Fail(ReleaseCandidateGateReason.MissingArtQaEvidence,
                    "P9 art license/provenance/visual QA evidence is required.");
            if (!SameCommit(artQaEvidence.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.ArtQaCommitMismatch,
                    "P9 art QA evidence must use the release candidate commit.");
            if (!P9ArtReleaseQaGate.IsApproved(artQaEvidence))
                return Fail(ReleaseCandidateGateReason.ArtQaFailed,
                    "P9 production-art release QA gate has not passed.");

            var packaging = evidence.Packaging;
            if (packaging == null || string.IsNullOrWhiteSpace(packaging.EvidenceId))
                return Fail(ReleaseCandidateGateReason.MissingPackagingEvidence,
                    "Release packaging evidence is required.");
            if (!SameCommit(packaging.CommitSha, commit))
                return Fail(ReleaseCandidateGateReason.PackagingCommitMismatch,
                    "Packaging evidence must use the release candidate commit.");
            if (!PackagingPassed(packaging))
                return Fail(ReleaseCandidateGateReason.PackagingFailed,
                    "Both packages and the platform/log/version checklist must pass.");

            if (evidence.KnownIssues == null)
                return Fail(ReleaseCandidateGateReason.MissingKnownIssuesRecord,
                    "Known issues must be explicitly recorded, even when the list is empty.");

            return new ReleaseCandidateGateResult(true, ReleaseCandidateGateReason.Passed,
                "Release candidate evidence passed on one source commit for Windows and macOS.");
        }

        private static ReleaseSoakEvidence FindSoak(ReleaseSoakEvidence[] rows, ReleaseBuildPlatform platform)
        {
            rows = rows ?? Array.Empty<ReleaseSoakEvidence>();
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && rows[index].Platform == platform) return rows[index];
            return null;
        }

        private static ReleasePerformanceEvidence FindPerformance(ReleasePerformanceEvidence[] rows, ReleaseBuildPlatform platform)
        {
            rows = rows ?? Array.Empty<ReleasePerformanceEvidence>();
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && rows[index].Platform == platform &&
                    rows[index].Scenario == P8BenchmarkScenarioKind.Target128) return rows[index];
            return null;
        }

        private static bool HasSoakEvidence(ReleaseSoakEvidence evidence) =>
            evidence != null && !string.IsNullOrWhiteSpace(evidence.EvidenceId);

        private static bool SoakPassed(ReleaseSoakEvidence evidence) =>
            evidence.Completed && evidence.CrashFree && evidence.FatalLogFree;

        private static bool HasPerformanceEvidence(ReleasePerformanceEvidence evidence) =>
            evidence != null && !string.IsNullOrWhiteSpace(evidence.EvidenceId);

        private static bool PerformancePassed(ReleasePerformanceEvidence evidence) =>
            evidence.GateEvaluated && evidence.GatePassed &&
            evidence.GateReason == P8BenchmarkGateReason.Passed &&
            evidence.Scenario == P8BenchmarkScenarioKind.Target128;

        private static bool PackagingPassed(ReleasePackagingEvidence evidence) =>
            evidence.WindowsPackageLaunchPassed &&
            evidence.MacOSPackageLaunchPassed &&
            evidence.WindowsUserDataPathChecked &&
            evidence.MacOSGatekeeperPolicyChecked &&
            evidence.SigningNotarizationPolicyRecorded &&
            evidence.CrashAndLogPathsRecorded &&
            evidence.KnownIssuesReviewed &&
            evidence.BuildShaRecorded;

        private static bool SameCommit(string candidate, string expected) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(candidate.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

        private static ReleaseCandidateGateResult Fail(ReleaseCandidateGateReason reason, string message) =>
            new ReleaseCandidateGateResult(false, reason, message);
    }
}
