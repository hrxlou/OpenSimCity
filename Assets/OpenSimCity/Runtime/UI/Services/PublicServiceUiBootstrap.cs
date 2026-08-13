using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Services
{
    public static class PublicServiceUiBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PublicServiceUiBinder>() != null) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (document == null) return;
            var binder = document.GetComponent<PublicServiceUiBinder>();
            if (binder == null) binder = document.gameObject.AddComponent<PublicServiceUiBinder>();
            binder.Configure(document);
        }
    }
}
