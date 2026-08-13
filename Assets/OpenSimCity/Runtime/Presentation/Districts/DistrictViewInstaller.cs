using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Districts
{
    public static class DistrictViewInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<DistrictBoundaryView>() != null) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            var root = new GameObject("DistrictBoundaryView");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<DistrictBoundaryView>();
        }
    }
}
