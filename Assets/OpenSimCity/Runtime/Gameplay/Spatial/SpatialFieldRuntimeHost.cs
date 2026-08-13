using System;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Gameplay.Construction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Gameplay.Spatial
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(220)]
    public sealed class SpatialFieldRuntimeHost : MonoBehaviour
    {
        [SerializeField] private CityRuntimeHost cityRuntimeHost;
        private CityRuntime _boundRuntime;

        public SpatialFieldRuntimeModule Module { get; private set; }
        public event Action<SpatialFieldRuntimeModule> ModuleChanged;

        public void Configure(CityRuntimeHost host)
        {
            cityRuntimeHost = host;
            BindIfNeeded();
        }

        private void OnEnable() => BindIfNeeded();

        private void Update()
        {
            BindIfNeeded();
            Module?.RefreshIfNeeded();
        }

        private void BindIfNeeded()
        {
            if (cityRuntimeHost == null)
                cityRuntimeHost = GetComponent<CityRuntimeHost>();
            var runtime = cityRuntimeHost != null ? cityRuntimeHost.Runtime : null;
            if (runtime == null || ReferenceEquals(runtime, _boundRuntime))
                return;
            _boundRuntime = runtime;
            Module = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            ModuleChanged?.Invoke(Module);
        }
    }

    public static class SpatialFieldGameplayBootstrap
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
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
                Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var cityHost = UnityEngine.Object.FindFirstObjectByType<CityRuntimeHost>();
            if (cityHost == null || cityHost.gameObject.scene != scene)
                return;
            var host = cityHost.GetComponent<SpatialFieldRuntimeHost>();
            if (host == null)
                host = cityHost.gameObject.AddComponent<SpatialFieldRuntimeHost>();
            host.Configure(cityHost);
        }
    }
}
