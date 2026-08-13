using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Conditions
{
    public static class BuildingConditionUiBootstrap
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
            if (UnityEngine.Object.FindFirstObjectByType<BuildingConditionUiBinder>() != null)
                return;
            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (document == null)
                return;
            var binder = document.GetComponent<BuildingConditionUiBinder>();
            if (binder == null)
                binder = document.gameObject.AddComponent<BuildingConditionUiBinder>();
            binder.Configure(document);
        }
    }
}
