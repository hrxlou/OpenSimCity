using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Transit
{
    /// <summary>Installs a transient derived transit presentation root per active scene.</summary>
    public static class TransitPresentationBootstrap
    {
        public const string BusResourcePath = "OpenSimCity/Vehicles/TransitBus";

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
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            if (UnityEngine.Object.FindFirstObjectByType<TransitViewCoordinator>() != null)
                return;
            var root = new GameObject(TransitViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var coordinator = root.AddComponent<TransitViewCoordinator>();
            coordinator.Configure(Resources.Load<GameObject>(BusResourcePath));
        }
    }
}
