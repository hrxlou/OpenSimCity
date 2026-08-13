using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace OpenSimCity.Gameplay.Districts
{
    public static class DistrictGameplayInstaller
    {
        private static bool _subscribed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() { if (_subscribed) return; SceneManager.sceneLoaded += OnLoaded; _subscribed = true; }
        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            var selection = Object.FindFirstObjectByType<GridSelectionTool>();
            if (selection != null && selection.gameObject.scene == scene && selection.GetComponent<DistrictSelectionBridge>() == null)
                selection.gameObject.AddComponent<DistrictSelectionBridge>();
        }
    }
}
