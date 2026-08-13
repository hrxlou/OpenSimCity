using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.WaterDensity
{
    public static class WaterDensityUiBootstrap
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
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()) Install(scene);
        }
        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (Object.FindFirstObjectByType<WaterDensityUiBinder>() != null) return;
            var document = Object.FindFirstObjectByType<UIDocument>();
            if (document == null || document.gameObject.scene != scene) return;
            var binder = document.GetComponent<WaterDensityUiBinder>();
            if (binder == null) binder = document.gameObject.AddComponent<WaterDensityUiBinder>();
            binder.Configure(document);
        }
    }
}
