using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Services
{
    /// <summary>
    /// Reinstalls transient service presentation adapters after scene transitions.
    /// They remain scene-owned and are never persisted across loads.
    /// </summary>
    public static class PublicServiceSceneBootstrap
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitial() => Install(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
                Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (!string.Equals(scene.name, "CityPrototype", StringComparison.Ordinal)) return;

            if (Object.FindFirstObjectByType<PublicServiceViewCoordinator>() == null)
            {
                var root = new GameObject(PublicServiceViewCoordinator.RuntimeObjectName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<PublicServiceViewCoordinator>();
            }

            if (Object.FindFirstObjectByType<ServiceResponseView>() == null)
            {
                var root = new GameObject("ServiceResponseViews");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<ServiceResponseView>();
            }
        }
    }
}
