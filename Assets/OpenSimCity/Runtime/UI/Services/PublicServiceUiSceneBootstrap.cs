using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Services
{
    /// <summary>Installs the transient service panel after subsequent scene loads.</summary>
    public static class PublicServiceUiSceneBootstrap
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
            if (Object.FindFirstObjectByType<PublicServiceUiBinder>() != null) return;
            var document = Object.FindFirstObjectByType<UIDocument>();
            if (document == null || document.gameObject.scene != scene) return;
            var binder = document.GetComponent<PublicServiceUiBinder>();
            if (binder == null) binder = document.gameObject.AddComponent<PublicServiceUiBinder>();
            binder.Configure(document);
        }
    }
}
