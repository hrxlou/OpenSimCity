using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.UI.Release
{
    /// <summary>
    /// Bridges the minimal Bootstrap scene to the playable release shell.
    /// The CityPrototype owns the actual release menu, so standalone players
    /// enter it immediately while editor scene smoke tests remain deterministic.
    /// </summary>
    public static class StandaloneSceneFlow
    {
        public const string BootstrapSceneName = "Bootstrap";
        public const string PlayableSceneName = "CityPrototype";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnterPlayableScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            var requestedScene = RequestedReviewScene(Environment.GetCommandLineArgs());
            if (!string.IsNullOrEmpty(requestedScene) &&
                UnityEngine.Application.CanStreamedLevelBeLoaded(requestedScene))
            {
                SceneManager.LoadScene(requestedScene, LoadSceneMode.Single);
                return;
            }

            if (ShouldEnterPlayableScene(activeScene.name, UnityEngine.Application.isEditor))
                SceneManager.LoadScene(PlayableSceneName, LoadSceneMode.Single);
        }

        public static bool ShouldEnterPlayableScene(string activeSceneName, bool isEditor)
        {
            return !isEditor &&
                   string.Equals(activeSceneName, BootstrapSceneName, StringComparison.Ordinal);
        }

        public static string RequestedReviewScene(string[] arguments)
        {
            if (arguments == null)
                return string.Empty;

            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-osc-benchmark", StringComparison.Ordinal))
                    return "Benchmark";
            }
            return string.Empty;
        }

        public static bool IsShowcaseRequested(string[] arguments)
        {
            if (arguments == null)
                return false;
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-osc-showcase", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
