using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.WaterDensity;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Gameplay.WaterDensity
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class WaterDensityRuntimeHost : MonoBehaviour
    {
        [SerializeField] private CityRuntimeHost cityRuntimeHost;
        [SerializeField] private GridSelectionTool gridSelection;
        private CityRuntime _boundRuntime;
        private CellCoord? _observedCell;
        private bool _hasTestSelection;
        private CellCoord? _testSelection;

        public WaterDensityRuntimeModule Module { get; private set; }

        public void Configure(CityRuntimeHost host, GridSelectionTool selection)
        {
            cityRuntimeHost = host;
            gridSelection = selection;
            BindIfNeeded();
            RefreshSelection();
        }

        private void OnEnable()
        {
            BindIfNeeded();
            RefreshSelection();
        }

        private void Update()
        {
            BindIfNeeded();
            Module?.ReadModel.RefreshIfNeeded();
            RefreshSelection();
        }

        private void OnDisable()
        {
            _hasTestSelection = false;
            _testSelection = null;
            WaterDensitySelection.Current.SetSelectedCell(null);
        }

        public void SelectCellForTests(CellCoord? cell)
        {
            _hasTestSelection = true;
            _testSelection = cell;
            _observedCell = null;
            RefreshSelection();
        }

        public CommandExecutionResult PlacePump(CellCoord cell)
        {
            if (Module == null) return CommandExecutionResult.Failed("Water-density runtime is unavailable.");
            var producerId = Module.ProducerIdFor(cell);
            return Module.Execute(new PlaceWaterProducerCommand(
                producerId,
                WaterDensityDefinitionIds.Pump,
                cell));
        }

        private void BindIfNeeded()
        {
            if (cityRuntimeHost == null) cityRuntimeHost = GetComponent<CityRuntimeHost>();
            var runtime = cityRuntimeHost != null ? cityRuntimeHost.Runtime : null;
            if (runtime == null || ReferenceEquals(runtime, _boundRuntime)) return;
            _boundRuntime = runtime;
            Module = WaterDensityRuntimeRegistry.GetOrCreate(runtime);
            _observedCell = null;
        }

        private void RefreshSelection()
        {
            var selected = _hasTestSelection
                ? _testSelection
                : gridSelection != null
                    ? gridSelection.SelectedCell
                    : null;
            if (_observedCell == selected) return;
            _observedCell = selected;
            WaterDensitySelection.Current.SetSelectedCell(selected);
        }
    }

    public static class WaterDensityGameplayBootstrap
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
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()) Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var cityHost = UnityEngine.Object.FindFirstObjectByType<CityRuntimeHost>();
            if (cityHost == null || cityHost.gameObject.scene != scene) return;
            var host = cityHost.GetComponent<WaterDensityRuntimeHost>();
            if (host == null) host = cityHost.gameObject.AddComponent<WaterDensityRuntimeHost>();
            host.Configure(cityHost, UnityEngine.Object.FindFirstObjectByType<GridSelectionTool>());
        }
    }
}
