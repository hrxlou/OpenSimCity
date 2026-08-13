using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Services
{
    /// <summary>
    /// Reinstalls the transient service view root after every scene transition.
    /// The original AfterSceneLoad bootstrap covers initial startup; this hook owns
    /// subsequent single-scene loads without persisting view objects.
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

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (mode != LoadSceneMode.Single && scene != SceneManager.GetActiveScene()) return;
            if (Object.FindFirstObjectByType<PublicServiceViewCoordinator>() != null) return;
            var root = new GameObject(PublicServiceViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<PublicServiceViewCoordinator>();
        }
    }
}
