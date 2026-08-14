using System;

namespace OpenSimCity.Application.Verification
{
    public enum P9ReleaseUxPlatform : byte
    {
        Windows,
        MacOS
    }

    [Serializable]
    public sealed class P9ReleaseUxPlatformEvidence
    {
        public P9ReleaseUxPlatform Platform;
        public string CommitSha = string.Empty;
        public bool First15MinuteSmokePassed;
        public bool InputAndSettingsSmokePassed;
        public bool AudioAndDayNightSmokePassed;
    }

    [Serializable]
    public sealed class P9ReleaseUxEvidence
    {
        public string CommitSha = string.Empty;
        public bool OnboardingImplementationMerged;
        public bool AccessibilityImplementationMerged;
        public bool AudioDayNightImplementationMerged;
        public bool First15MinuteFlowPassed;
        public bool RepresentativeProblemRecoveryPassed;
        public bool NonColorProblemCommunicationPassed;
        public bool SettingsRoundTripPassed;
        public bool HasTutorialPresentationChecksumEvidence;
        public ulong TutorialReferenceChecksum;
        public ulong TutorialWithPresentationChecksum;
        public P9ReleaseUxPlatformEvidence[] Platforms = Array.Empty<P9ReleaseUxPlatformEvidence>();
    }

    public enum P9ReleaseUxGateReason : byte
    {
        Passed,
        MissingImplementationSlice,
        MissingCommitSha,
        MissingFirst15MinuteFlow,
        MissingRepresentativeRecovery,
        MissingNonColorCommunication,
        MissingSettingsRoundTrip,
        MissingChecksumEvidence,
        ChecksumMismatch,
        MissingPlatformEvidence,
        PlatformCommitMismatch,
        PlatformSmokeFailed
    }

    public sealed class P9ReleaseUxGateResult
    {
        public P9ReleaseUxGateResult(bool passed, P9ReleaseUxGateReason reason, string message)
        {
            Passed = passed;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Passed { get; }
        public P9ReleaseUxGateReason Reason { get; }
        public string Message { get; }
    }

    public static class P9ReleaseUxGate
    {
        public static P9ReleaseUxGateResult Evaluate(P9ReleaseUxEvidence evidence)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            if (!evidence.OnboardingImplementationMerged ||
                !evidence.AccessibilityImplementationMerged ||
                !evidence.AudioDayNightImplementationMerged)
                return Fail(P9ReleaseUxGateReason.MissingImplementationSlice,
                    "P9-02A, P9-02B and P9-02C implementation must all be merged before the parent gate can pass.");

            if (string.IsNullOrWhiteSpace(evidence.CommitSha))
                return Fail(P9ReleaseUxGateReason.MissingCommitSha,
                    "Release UX evidence must be pinned to one commit SHA.");

            if (!evidence.First15MinuteFlowPassed)
                return Fail(P9ReleaseUxGateReason.MissingFirst15MinuteFlow,
                    "The new-profile first-15-minute flow has no passing evidence.");

            if (!evidence.RepresentativeProblemRecoveryPassed)
                return Fail(P9ReleaseUxGateReason.MissingRepresentativeRecovery,
                    "A representative diagnosed problem and normal-command recovery has not passed.");

            if (!evidence.NonColorProblemCommunicationPassed)
                return Fail(P9ReleaseUxGateReason.MissingNonColorCommunication,
                    "Major problem communication must have a verified non-color channel.");

            if (!evidence.SettingsRoundTripPassed)
                return Fail(P9ReleaseUxGateReason.MissingSettingsRoundTrip,
                    "UI/input/audio/day-night presentation settings have no round-trip evidence.");

            if (!evidence.HasTutorialPresentationChecksumEvidence)
                return Fail(P9ReleaseUxGateReason.MissingChecksumEvidence,
                    "Tutorial/presentation checksum evidence is required.");

            if (evidence.TutorialReferenceChecksum != evidence.TutorialWithPresentationChecksum)
                return Fail(P9ReleaseUxGateReason.ChecksumMismatch,
                    "Tutorial/presentation settings changed the simulation checksum.");

            var platforms = evidence.Platforms ?? Array.Empty<P9ReleaseUxPlatformEvidence>();
            var windows = Find(platforms, P9ReleaseUxPlatform.Windows);
            var macOS = Find(platforms, P9ReleaseUxPlatform.MacOS);
            if (windows == null || macOS == null)
                return Fail(P9ReleaseUxGateReason.MissingPlatformEvidence,
                    "Both Windows and macOS release UX evidence are required.");

            var commit = evidence.CommitSha.Trim();
            if (!SameCommit(windows.CommitSha, commit) || !SameCommit(macOS.CommitSha, commit))
                return Fail(P9ReleaseUxGateReason.PlatformCommitMismatch,
                    "Windows and macOS evidence must be produced from the same release commit.");

            if (!PlatformPassed(windows) || !PlatformPassed(macOS))
                return Fail(P9ReleaseUxGateReason.PlatformSmokeFailed,
                    "Each platform must pass first-15-minute, input/settings and audio/day-night smoke evidence.");

            return new P9ReleaseUxGateResult(true, P9ReleaseUxGateReason.Passed,
                "P9-02 release UX evidence passed on one commit for Windows and macOS.");
        }

        private static P9ReleaseUxPlatformEvidence Find(P9ReleaseUxPlatformEvidence[] rows, P9ReleaseUxPlatform platform)
        {
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && rows[index].Platform == platform) return rows[index];
            return null;
        }

        private static bool SameCommit(string candidate, string expected) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        private static bool PlatformPassed(P9ReleaseUxPlatformEvidence evidence) =>
            evidence.First15MinuteSmokePassed &&
            evidence.InputAndSettingsSmokePassed &&
            evidence.AudioAndDayNightSmokePassed;

        private static P9ReleaseUxGateResult Fail(P9ReleaseUxGateReason reason, string message) =>
            new P9ReleaseUxGateResult(false, reason, message);
    }
}
