using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ReleaseCandidateGateTests
    {
        [Test]
        public void MissingBuildEvidenceFailsClosed()
        {
            var result = ReleaseCandidateGate.Evaluate(
                new ReleaseCandidateEvidence(),
                new P9ArtReleaseQaEvidence());

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.BuildPairFailed));
        }

        [Test]
        public void CompleteSameCommitEvidenceCanPass()
        {
            var result = ReleaseCandidateGate.Evaluate(PassingEvidence(), PassingArtEvidence());

            Assert.That(result.Passed, Is.True);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.Passed));
        }

        [Test]
        public void TwentyNineMinuteSoakIsRejected()
        {
            var evidence = PassingEvidence();
            evidence.Soak[0].DurationMinutes = ReleaseCandidateGate.MinimumSoakMinutes - 1;

            var result = ReleaseCandidateGate.Evaluate(evidence, PassingArtEvidence());

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.SoakTooShort));
        }

        [Test]
        public void UnevaluatedPerformanceGateIsRejected()
        {
            var evidence = PassingEvidence();
            evidence.Performance[1].GateEvaluated = false;

            var result = ReleaseCandidateGate.Evaluate(evidence, PassingArtEvidence());

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.PerformanceGateNotPassed));
        }

        [Test]
        public void CrossCommitSaveEvidenceIsRejected()
        {
            var evidence = PassingEvidence();
            evidence.SaveCompatibility.CommitSha = "different";

            var result = ReleaseCandidateGate.Evaluate(evidence, PassingArtEvidence());

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.SaveCompatibilityCommitMismatch));
        }

        [Test]
        public void ArtQaMustPassExistingP9Gate()
        {
            var art = PassingArtEvidence();
            art.ProvenanceValidatorPassed = false;

            var result = ReleaseCandidateGate.Evaluate(PassingEvidence(), art);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseCandidateGateReason.ArtQaFailed));
        }

        private static ReleaseCandidateEvidence PassingEvidence()
        {
            const string commit = "0123456789abcdef";
            var windowsProfile = ReleaseBuildProfile.Create(ReleaseBuildPlatform.Windows);
            var macProfile = ReleaseBuildProfile.Create(ReleaseBuildPlatform.MacOS);

            return new ReleaseCandidateEvidence
            {
                WindowsBuild = Build(ReleaseBuildPlatform.Windows, windowsProfile, commit),
                MacOSBuild = Build(ReleaseBuildPlatform.MacOS, macProfile, commit),
                Regression = new ReleaseRegressionEvidence
                {
                    CommitSha = commit,
                    EditModeExecuted = true,
                    EditModePassed = true,
                    EditModeTestCount = 1,
                    EditModeEvidenceId = "edit.xml",
                    PlayModeExecuted = true,
                    PlayModePassed = true,
                    PlayModeTestCount = 1,
                    PlayModeEvidenceId = "play.xml"
                },
                Soak = new[]
                {
                    Soak(ReleaseBuildPlatform.Windows, commit, "windows-soak.log"),
                    Soak(ReleaseBuildPlatform.MacOS, commit, "macos-soak.log")
                },
                Performance = new[]
                {
                    Performance(ReleaseBuildPlatform.Windows, commit, "windows-p8.json"),
                    Performance(ReleaseBuildPlatform.MacOS, commit, "macos-p8.json")
                },
                SaveCompatibility = new ReleaseSaveCompatibilityEvidence
                {
                    CommitSha = commit,
                    CurrentSaveRoundTripPassed = true,
                    PreviousSupportedSaveLoadPassed = true,
                    CrossPlatformExchangePassed = true,
                    MissingDefinitionFailsClosed = true,
                    UnknownCatalogFailsClosed = true,
                    EvidenceId = "save-compatibility.json"
                },
                Packaging = new ReleasePackagingEvidence
                {
                    CommitSha = commit,
                    WindowsPackageLaunchPassed = true,
                    MacOSPackageLaunchPassed = true,
                    WindowsUserDataPathChecked = true,
                    MacOSGatekeeperPolicyChecked = true,
                    SigningNotarizationPolicyRecorded = true,
                    CrashAndLogPathsRecorded = true,
                    KnownIssuesReviewed = true,
                    BuildShaRecorded = true,
                    EvidenceId = "packaging-checklist.md"
                },
                KnownIssues = new string[0]
            };
        }

        private static ReleaseBuildExecutionEvidence Build(
            ReleaseBuildPlatform platform,
            ReleaseBuildProfileData profile,
            string commit)
        {
            return new ReleaseBuildExecutionEvidence
            {
                Platform = platform,
                BuildSucceeded = true,
                CleanCheckout = true,
                SourceCommit = commit,
                UnityVersion = "6000.5.3f1",
                ProductName = profile.ProductName,
                ApplicationVersion = profile.ApplicationVersion,
                ContentCatalogVersion = profile.ContentCatalogVersion,
                OutputPath = profile.DefaultRelativeOutputPath,
                OutputBytes = 1,
                ProductScenes = profile.ProductScenes
            };
        }

        private static ReleaseSoakEvidence Soak(ReleaseBuildPlatform platform, string commit, string evidenceId)
        {
            return new ReleaseSoakEvidence
            {
                Platform = platform,
                CommitSha = commit,
                DurationMinutes = ReleaseCandidateGate.MinimumSoakMinutes,
                Completed = true,
                CrashFree = true,
                FatalLogFree = true,
                EvidenceId = evidenceId
            };
        }

        private static ReleasePerformanceEvidence Performance(
            ReleaseBuildPlatform platform,
            string commit,
            string evidenceId)
        {
            return new ReleasePerformanceEvidence
            {
                Platform = platform,
                CommitSha = commit,
                Scenario = P8BenchmarkScenarioKind.Target128,
                GateEvaluated = true,
                GatePassed = true,
                GateReason = P8BenchmarkGateReason.Passed,
                EvidenceId = evidenceId
            };
        }

        private static P9ArtReleaseQaEvidence PassingArtEvidence()
        {
            return new P9ArtReleaseQaEvidence
            {
                CommitSha = "0123456789abcdef",
                LicenseAuditEvidenceId = "license-audit.md",
                WindowsBuildEvidenceId = "windows-build.json",
                MacBuildEvidenceId = "macos-build.json",
                VisualQaEvidenceId = "visual-qa.md",
                ProvenanceValidatorPassed = true,
                PerformanceBudgetApproved = true
            };
        }
    }
}
