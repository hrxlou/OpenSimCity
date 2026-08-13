using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Spatial
{
    public static class SpatialFieldUiBootstrap
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
        private static void InstallInitial() => Install();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
                Install();
        }

        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<SpatialFieldUiBinder>() != null)
                return;
            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (document == null)
                return;
            var binder = document.GetComponent<SpatialFieldUiBinder>();
            if (binder == null)
                binder = document.gameObject.AddComponent<SpatialFieldUiBinder>();
            binder.Configure(document);
        }
    }
}
