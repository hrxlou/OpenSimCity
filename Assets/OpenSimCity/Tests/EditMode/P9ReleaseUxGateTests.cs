using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9ReleaseUxGateTests
    {
        [Test]
        public void MissingImplementationSlicesFailBeforeHardwareEvidence()
        {
            var result = P9ReleaseUxGate.Evaluate(new P9ReleaseUxEvidence());
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P9ReleaseUxGateReason.MissingImplementationSlice));
        }

        [Test]
        public void CrossCommitPlatformEvidenceIsRejected()
        {
            var evidence = PassingEvidence();
            evidence.Platforms[1].CommitSha = "different-commit";

            var result = P9ReleaseUxGate.Evaluate(evidence);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P9ReleaseUxGateReason.PlatformCommitMismatch));
        }

        [Test]
        public void PresentationChecksumChangeIsRejected()
        {
            var evidence = PassingEvidence();
            evidence.TutorialWithPresentationChecksum++;

            var result = P9ReleaseUxGate.Evaluate(evidence);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P9ReleaseUxGateReason.ChecksumMismatch));
        }

        [Test]
        public void SameCommitWindowsAndMacEvidenceCanPass()
        {
            var result = P9ReleaseUxGate.Evaluate(PassingEvidence());
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Reason, Is.EqualTo(P9ReleaseUxGateReason.Passed));
        }

        private static P9ReleaseUxEvidence PassingEvidence()
        {
            const string commit = "0123456789abcdef";
            return new P9ReleaseUxEvidence
            {
                CommitSha = commit,
                OnboardingImplementationMerged = true,
                AccessibilityImplementationMerged = true,
                AudioDayNightImplementationMerged = true,
                First15MinuteFlowPassed = true,
                RepresentativeProblemRecoveryPassed = true,
                NonColorProblemCommunicationPassed = true,
                SettingsRoundTripPassed = true,
                HasTutorialPresentationChecksumEvidence = true,
                TutorialReferenceChecksum = 0x902UL,
                TutorialWithPresentationChecksum = 0x902UL,
                Platforms = new[]
                {
                    new P9ReleaseUxPlatformEvidence
                    {
                        Platform = P9ReleaseUxPlatform.Windows,
                        CommitSha = commit,
                        First15MinuteSmokePassed = true,
                        InputAndSettingsSmokePassed = true,
                        AudioAndDayNightSmokePassed = true
                    },
                    new P9ReleaseUxPlatformEvidence
                    {
                        Platform = P9ReleaseUxPlatform.MacOS,
                        CommitSha = commit,
                        First15MinuteSmokePassed = true,
                        InputAndSettingsSmokePassed = true,
                        AudioAndDayNightSmokePassed = true
                    }
                }
            };
        }
    }
}
