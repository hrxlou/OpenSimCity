using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace OpenSimCity.UI.Districts
{
    public static class DistrictUiPolicyHook
    {
        private static bool _subscribed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed) return;
            SceneManager.sceneLoaded += OnLoaded;
            _subscribed = true;
        }
        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            var document = Object.FindFirstObjectByType<UIDocument>();
            if (document == null || document.gameObject.scene != scene) return;
            var binder = document.GetComponent<DistrictPolicyUiBinder>();
            if (binder == null) binder = document.gameObject.AddComponent<DistrictPolicyUiBinder>();
            binder.Configure(document);
        }
    }
}
