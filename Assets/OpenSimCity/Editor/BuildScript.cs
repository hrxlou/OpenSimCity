using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Standalone 빌드의 단일 진입점이다.
    /// CI(game-ci unity-builder)와 로컬 Tools/CI/build-windows.ps1이 같은 메서드를 호출해
    /// 빌드 결과가 두 환경에서 동일하게 재현되도록 한다.
    /// </summary>
    public static class BuildScript
    {
        private const string ProductName = "OpenSimCity";
        private const string DefaultRelativePath = "build/StandaloneWindows64/OpenSimCity.exe";

        // 로컬 스크립트가 출력 경로를 넘길 때 사용하는 커스텀 인자.
        private const string BuildPathArg = "-osc-buildPath";
        private const string BuildResultPathArg = "-osc-buildResultPath";

        [MenuItem("OpenSimCity/Build/Windows Standalone")]
        public static void PerformWindowsBuildMenu()
        {
            BuildWindows(Path.Combine(ProjectRoot(), DefaultRelativePath));
        }

        /// <summary>
        /// 배치모드에서 -executeMethod 또는 buildMethod로 호출되는 진입점이다.
        /// 실패 시 배치모드에서는 0이 아닌 코드로 종료해 CI가 실패를 감지하게 한다.
        /// </summary>
        public static void PerformWindowsBuild()
        {
            string outputPath = ResolveOutputPath();
            BuildReport report = BuildWindows(outputPath);
            bool success = report != null && report.summary.result == BuildResult.Succeeded;
            WriteBuildResult(ResolveArgument(BuildResultPathArg), success, report);

            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static BuildReport BuildWindows(string outputPath)
        {
            string[] scenes = EnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("Windows build aborted: Build Settings에 활성화된 Scene이 없다. " +
                               "OpenSimCity/Setup/Configure Project로 Scene을 구성한 뒤 다시 실행한다.");
                return null;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            PlayerSettings.productName = ProductName;

            Debug.Log($"Windows build 시작: {outputPath}\nScenes:\n  {string.Join("\n  ", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Windows build 성공: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalTime})");
            }
            else
            {
                Debug.LogError($"Windows build 실패: result={summary.result}, errors={summary.totalErrors}");
            }

            return report;
        }

        private static string[] EnabledScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                .Select(scene => scene.path)
                .ToArray();
        }

        private static string ResolveOutputPath()
        {
            string configuredPath = ResolveArgument(BuildPathArg);
            return string.IsNullOrEmpty(configuredPath)
                ? Path.Combine(ProjectRoot(), DefaultRelativePath)
                : configuredPath;
        }

        private static string ResolveArgument(string argumentName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argumentName, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void WriteBuildResult(string resultPath, bool success, BuildReport report)
        {
            if (string.IsNullOrEmpty(resultPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string result = success ? "Succeeded" : "Failed";
            string detail = report == null
                ? "No build report was produced."
                : $"result={report.summary.result}; errors={report.summary.totalErrors}; output={report.summary.outputPath}";
            File.WriteAllText(resultPath, $"{result}{Environment.NewLine}{detail}{Environment.NewLine}");
        }

        private static string ProjectRoot()
        {
            // Application.dataPath는 <project>/Assets를 가리킨다.
            return Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        }
    }
}
