using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Vehicles
{
    /// <summary>
    /// Reproducibly installs the transient traffic presentation root for every
    /// loaded gameplay or benchmark scene. The root contains only derived views and
    /// is never part of snapshot state.
    /// </summary>
    public static class TrafficPresentationBootstrap
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed)
                return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterInitialScene()
        {
            Install(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
                Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            if (UnityEngine.Object.FindFirstObjectByType<TrafficViewCoordinator>() != null)
                return;

            var root = new GameObject(TrafficViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<TrafficViewCoordinator>();
        }
    }
}
