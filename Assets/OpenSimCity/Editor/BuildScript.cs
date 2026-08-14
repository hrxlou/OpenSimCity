using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSimCity.Application.Verification;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Standalone release build entry point. Windows and macOS consume the same
    /// ReleaseBuildProfile so product scenes/version/content identity cannot drift by platform.
    /// </summary>
    public static class BuildScript
    {
        private const string BuildPathArg = "-osc-buildPath";
        private const string BuildResultPathArg = "-osc-buildResultPath";
        private const string BuildEvidencePathArg = "-osc-buildEvidencePath";
        private const string BuildSourceCommitArg = "-osc-sourceCommit";
        private const string BuildCleanCheckoutArg = "-osc-cleanCheckout";

        [MenuItem("OpenSimCity/Build/Windows Standalone")]
        public static void PerformWindowsBuildMenu()
        {
            var profile = ReleaseBuildProfile.Create(ReleaseBuildPlatform.Windows);
            BuildStandalone(ReleaseBuildPlatform.Windows, BuildTarget.StandaloneWindows64,
                Path.Combine(ProjectRoot(), profile.DefaultRelativeOutputPath));
        }

        [MenuItem("OpenSimCity/Build/macOS Standalone")]
        public static void PerformMacOSBuildMenu()
        {
            var profile = ReleaseBuildProfile.Create(ReleaseBuildPlatform.MacOS);
            BuildStandalone(ReleaseBuildPlatform.MacOS, BuildTarget.StandaloneOSX,
                Path.Combine(ProjectRoot(), profile.DefaultRelativeOutputPath));
        }

        public static void PerformWindowsBuild()
        {
            PerformBatchBuild(ReleaseBuildPlatform.Windows, BuildTarget.StandaloneWindows64);
        }

        public static void PerformMacOSBuild()
        {
            PerformBatchBuild(ReleaseBuildPlatform.MacOS, BuildTarget.StandaloneOSX);
        }

        private static void PerformBatchBuild(ReleaseBuildPlatform platform, BuildTarget target)
        {
            var profile = ReleaseBuildProfile.Create(platform);
            var outputPath = ResolveOutputPath(profile);
            var scenes = ValidatedReleaseScenePaths();
            BuildReport report = null;
            if (scenes.Length > 0)
                report = BuildStandalone(platform, target, outputPath, scenes);

            var success = report != null && report.summary.result == BuildResult.Succeeded;
            WriteBuildResult(ResolveArgument(BuildResultPathArg), success, report);
            WriteBuildEvidence(ResolveArgument(BuildEvidencePathArg), platform, success, report, profile, scenes);

            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static BuildReport BuildStandalone(ReleaseBuildPlatform platform, BuildTarget target, string outputPath)
        {
            var scenes = ValidatedReleaseScenePaths();
            if (scenes.Length == 0) return null;
            return BuildStandalone(platform, target, outputPath, scenes);
        }

        private static BuildReport BuildStandalone(
            ReleaseBuildPlatform platform,
            BuildTarget target,
            string outputPath,
            string[] scenes)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            var profile = ReleaseBuildProfile.Create(platform);
            PlayerSettings.productName = profile.ProductName;
            PlayerSettings.bundleVersion = profile.ApplicationVersion;

            Debug.Log($"{platform} release build start: {outputPath}\nVersion: {profile.ApplicationVersion}\nContent: {profile.ContentCatalogVersion}\nScenes:\n  {string.Join("\n  ", scenes)}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"{platform} build succeeded: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalTime})");
            else
                Debug.LogError($"{platform} build failed: result={summary.result}, errors={summary.totalErrors}");

            return report;
        }

        public static IReadOnlyList<string> ReleaseScenePaths()
        {
            return Array.AsReadOnly(ReleaseBuildProfile.ProductScenePaths());
        }

        private static string[] ValidatedReleaseScenePaths()
        {
            var releaseScenes = ReleaseBuildProfile.ProductScenePaths();
            var missingScenes = releaseScenes
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                .ToArray();

            if (missingScenes.Length > 0)
            {
                Debug.LogError("Release Scene files are missing:\n  " + string.Join("\n  ", missingScenes));
                return Array.Empty<string>();
            }

            return releaseScenes;
        }

        private static string ResolveOutputPath(ReleaseBuildProfileData profile)
        {
            var configuredPath = ResolveArgument(BuildPathArg);
            return string.IsNullOrEmpty(configuredPath)
                ? Path.Combine(ProjectRoot(), profile.DefaultRelativeOutputPath)
                : configuredPath;
        }

        private static string ResolveArgument(string argumentName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], argumentName, StringComparison.Ordinal)) return args[index + 1];
            return null;
        }

        private static void WriteBuildResult(string resultPath, bool success, BuildReport report)
        {
            if (string.IsNullOrEmpty(resultPath)) return;
            EnsureParentDirectory(resultPath);
            var result = success ? "Succeeded" : "Failed";
            var detail = report == null
                ? "No build report was produced."
                : $"result={report.summary.result}; errors={report.summary.totalErrors}; output={report.summary.outputPath}";
            File.WriteAllText(resultPath, $"{result}{Environment.NewLine}{detail}{Environment.NewLine}");
        }

        private static void WriteBuildEvidence(
            string evidencePath,
            ReleaseBuildPlatform platform,
            bool success,
            BuildReport report,
            ReleaseBuildProfileData profile,
            string[] scenes)
        {
            if (string.IsNullOrEmpty(evidencePath)) return;
            EnsureParentDirectory(evidencePath);
            var evidence = new ReleaseBuildExecutionEvidence
            {
                Platform = platform,
                BuildSucceeded = success,
                CleanCheckout = string.Equals(ResolveArgument(BuildCleanCheckoutArg), "1", StringComparison.Ordinal),
                SourceCommit = ResolveArgument(BuildSourceCommitArg) ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
                UnityVersion = UnityEngine.Application.unityVersion,
                ProductName = profile.ProductName,
                ApplicationVersion = profile.ApplicationVersion,
                ContentCatalogVersion = profile.ContentCatalogVersion,
                OutputPath = report == null ? string.Empty : report.summary.outputPath,
                OutputBytes = report == null ? 0L : checked((long)report.summary.totalSize),
                ProductScenes = scenes ?? Array.Empty<string>()
            };
            File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true) + Environment.NewLine);
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        }
    }
}
