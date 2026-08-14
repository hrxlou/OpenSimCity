using System;

namespace OpenSimCity.Application.Verification
{
    public enum ReleaseBuildPlatform : byte
    {
        Windows,
        MacOS
    }

    [Serializable]
    public sealed class ReleaseBuildProfileData
    {
        public string ProductName = string.Empty;
        public string ApplicationVersion = string.Empty;
        public int ContentCatalogVersion;
        public ReleaseBuildPlatform Platform;
        public string DefaultRelativeOutputPath = string.Empty;
        public string[] ProductScenes = Array.Empty<string>();
    }

    public static class ReleaseBuildProfile
    {
        public const string ProductName = "OpenSimCity";
        public const string ApplicationVersion = "1.0.0";
        public const int ContentCatalogVersion = 1;

        private static readonly string[] Scenes =
        {
            "Assets/OpenSimCity/Scenes/Bootstrap.unity",
            "Assets/OpenSimCity/Scenes/MainMenu.unity",
            "Assets/OpenSimCity/Scenes/CityPrototype.unity"
        };

        public static ReleaseBuildProfileData Create(ReleaseBuildPlatform platform)
        {
            return new ReleaseBuildProfileData
            {
                ProductName = ProductName,
                ApplicationVersion = ApplicationVersion,
                ContentCatalogVersion = ContentCatalogVersion,
                Platform = platform,
                DefaultRelativeOutputPath = platform == ReleaseBuildPlatform.Windows
                    ? "build/StandaloneWindows64/OpenSimCity.exe"
                    : "build/StandaloneOSX/OpenSimCity.app",
                ProductScenes = ProductScenePaths()
            };
        }

        public static string[] ProductScenePaths() => (string[])Scenes.Clone();
    }

    [Serializable]
    public sealed class ReleaseBuildExecutionEvidence
    {
        public ReleaseBuildPlatform Platform;
        public bool BuildSucceeded;
        public bool CleanCheckout;
        public string SourceCommit = string.Empty;
        public string UnityVersion = string.Empty;
        public string ProductName = string.Empty;
        public string ApplicationVersion = string.Empty;
        public int ContentCatalogVersion;
        public string OutputPath = string.Empty;
        public long OutputBytes;
        public string[] ProductScenes = Array.Empty<string>();
    }

    public enum ReleaseBuildPairGateReason : byte
    {
        Passed,
        MissingEvidence,
        WrongPlatform,
        BuildFailed,
        DirtyCheckout,
        MissingCommit,
        CommitMismatch,
        ProfileMismatch,
        UnityVersionMismatch,
        SceneMismatch,
        MissingOutput
    }

    public sealed class ReleaseBuildPairGateResult
    {
        public ReleaseBuildPairGateResult(bool passed, ReleaseBuildPairGateReason reason, string message)
        {
            Passed = passed;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Passed { get; }
        public ReleaseBuildPairGateReason Reason { get; }
        public string Message { get; }
    }

    public static class ReleaseBuildPairGate
    {
        public static ReleaseBuildPairGateResult Evaluate(
            ReleaseBuildExecutionEvidence windows,
            ReleaseBuildExecutionEvidence macOS)
        {
            if (windows == null || macOS == null)
                return Fail(ReleaseBuildPairGateReason.MissingEvidence,
                    "Windows and macOS build evidence are both required.");
            if (windows.Platform != ReleaseBuildPlatform.Windows || macOS.Platform != ReleaseBuildPlatform.MacOS)
                return Fail(ReleaseBuildPairGateReason.WrongPlatform,
                    "Build evidence must be assigned to the matching platform.");
            if (!windows.BuildSucceeded || !macOS.BuildSucceeded)
                return Fail(ReleaseBuildPairGateReason.BuildFailed,
                    "Both release candidates must report successful builds.");
            if (!windows.CleanCheckout || !macOS.CleanCheckout)
                return Fail(ReleaseBuildPairGateReason.DirtyCheckout,
                    "Release candidates must be produced from clean checkouts.");
            if (string.IsNullOrWhiteSpace(windows.SourceCommit) || string.IsNullOrWhiteSpace(macOS.SourceCommit))
                return Fail(ReleaseBuildPairGateReason.MissingCommit,
                    "Each build must record its source commit.");
            if (!string.Equals(windows.SourceCommit.Trim(), macOS.SourceCommit.Trim(), StringComparison.OrdinalIgnoreCase))
                return Fail(ReleaseBuildPairGateReason.CommitMismatch,
                    "Windows and macOS candidates must use the same source commit.");
            if (!ProfileMatches(windows) || !ProfileMatches(macOS))
                return Fail(ReleaseBuildPairGateReason.ProfileMismatch,
                    "Build metadata does not match the committed release profile.");
            if (string.IsNullOrWhiteSpace(windows.UnityVersion) ||
                !string.Equals(windows.UnityVersion, macOS.UnityVersion, StringComparison.Ordinal))
                return Fail(ReleaseBuildPairGateReason.UnityVersionMismatch,
                    "Both candidates must use the same recorded Unity version.");
            if (!ScenesMatch(windows.ProductScenes) || !ScenesMatch(macOS.ProductScenes))
                return Fail(ReleaseBuildPairGateReason.SceneMismatch,
                    "Release candidates must contain the exact product Scene sequence.");
            if (string.IsNullOrWhiteSpace(windows.OutputPath) || string.IsNullOrWhiteSpace(macOS.OutputPath))
                return Fail(ReleaseBuildPairGateReason.MissingOutput,
                    "Successful build evidence must record its output path.");

            return new ReleaseBuildPairGateResult(true, ReleaseBuildPairGateReason.Passed,
                "Windows and macOS candidates match one clean release profile and source commit.");
        }

        private static bool ProfileMatches(ReleaseBuildExecutionEvidence evidence)
        {
            var profile = ReleaseBuildProfile.Create(evidence.Platform);
            return string.Equals(evidence.ProductName, profile.ProductName, StringComparison.Ordinal) &&
                   string.Equals(evidence.ApplicationVersion, profile.ApplicationVersion, StringComparison.Ordinal) &&
                   evidence.ContentCatalogVersion == profile.ContentCatalogVersion;
        }

        private static bool ScenesMatch(string[] candidate)
        {
            var expected = ReleaseBuildProfile.ProductScenePaths();
            if (candidate == null || candidate.Length != expected.Length) return false;
            for (var index = 0; index < expected.Length; index++)
                if (!string.Equals(candidate[index], expected[index], StringComparison.Ordinal)) return false;
            return true;
        }

        private static ReleaseBuildPairGateResult Fail(ReleaseBuildPairGateReason reason, string message) =>
            new ReleaseBuildPairGateResult(false, reason, message);
    }
}
