using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Districts
{
    public static class DistrictSceneLoadHook
    {
        private static bool _subscribed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed) return;
            SceneManager.sceneLoaded += Handle;
            _subscribed = true;
        }
        private static void Handle(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindFirstObjectByType<DistrictBoundaryView>() != null) return;
            var root = new GameObject("DistrictBoundaryView");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<DistrictBoundaryView>();
        }
    }
}
