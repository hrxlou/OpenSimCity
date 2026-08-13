using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Specialization
{
    public static class SpecializationUiBootstrap
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
            var document = Object.FindFirstObjectByType<UIDocument>();
            if (document == null || document.gameObject.scene != scene) return;
            var binder = document.GetComponent<SpecializationUiBinder>();
            if (binder == null) binder = document.gameObject.AddComponent<SpecializationUiBinder>();
            binder.Configure(document);
        }
    }
}
