using System;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Gameplay.Construction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Gameplay.Transit
{
    /// <summary>
    /// Unity composition adapter for the pure transit capability module. It observes
    /// completed base OD periods and runs transit once per new demand version.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class TransitRuntimeHost : MonoBehaviour
    {
        [SerializeField] private CityRuntimeHost cityRuntimeHost;

        private CityRuntime _boundRuntime;
        private int _observedDemandVersion = -1;

        public CityRuntimeHost CityRuntimeHost => cityRuntimeHost;
        public TransitRuntimeModule Module { get; private set; }
        public TransitConstructionTool Tool { get; private set; }

        public event Action<TransitRuntimeModule> ModuleChanged;

        public void Configure(CityRuntimeHost host)
        {
            cityRuntimeHost = host;
            BindIfNeeded();
        }

        private void OnEnable()
        {
            BindIfNeeded();
        }

        private void Update()
        {
            BindIfNeeded();
            if (Module == null || !_boundRuntime.TripDemandSystem.State.HasProcessedMonth)
                return;
            var demandVersion = _boundRuntime.TripDemandSystem.State.Version;
            if (_observedDemandVersion == demandVersion)
                return;
            Module.RunCurrentMonth();
            _observedDemandVersion = demandVersion;
        }

        private void BindIfNeeded()
        {
            if (cityRuntimeHost == null)
                cityRuntimeHost = GetComponent<CityRuntimeHost>();
            var runtime = cityRuntimeHost != null ? cityRuntimeHost.Runtime : null;
            if (runtime == null || ReferenceEquals(runtime, _boundRuntime))
                return;

            _boundRuntime = runtime;
            Module = TransitRuntimeRegistry.GetOrCreate(runtime);
            Tool = TransitConstructionToolRegistry.GetOrCreate(Module);
            _observedDemandVersion = -1;
            ModuleChanged?.Invoke(Module);
        }
    }

    public static class TransitGameplayBootstrap
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed)
                return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitial()
        {
            Install(SceneManager.GetActiveScene());
        }

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
            var transitHost = cityHost.GetComponent<TransitRuntimeHost>();
            if (transitHost == null)
                transitHost = cityHost.gameObject.AddComponent<TransitRuntimeHost>();
            transitHost.Configure(cityHost);

            var input = cityHost.GetComponent<TransitConstructionInputDriver>();
            if (input == null)
                input = cityHost.gameObject.AddComponent<TransitConstructionInputDriver>();
            var constructionInput = UnityEngine.Object.FindFirstObjectByType<ConstructionInputDriver>();
            input.Configure(transitHost, constructionInput != null ? constructionInput.CameraRig : null);
        }
    }
}
