using System;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Gameplay.Conditions
{
    /// <summary>
    /// Unity composition adapter for the pure condition module and selected-building
    /// projection. Grid selection remains non-authoritative and stores only a stable
    /// building ID in Application selection state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(230)]
    public sealed class BuildingConditionRuntimeHost : MonoBehaviour
    {
        [SerializeField] private CityRuntimeHost cityRuntimeHost;
        [SerializeField] private GridSelectionTool gridSelection;

        private CityRuntime _boundRuntime;
        private CellCoord? _observedCell;
        private bool _hasTestSelectionOverride;
        private CellCoord? _testSelectedCell;

        public BuildingConditionRuntimeModule Module { get; private set; }
        public event Action<BuildingConditionRuntimeModule> ModuleChanged;

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
            Module?.RefreshReadModels();
            RefreshSelection();
        }

        private void OnDisable()
        {
            _hasTestSelectionOverride = false;
            _testSelectedCell = null;
            BuildingConditionSelection.Current.Clear();
        }

        public void SelectCellForTests(CellCoord? cell)
        {
            _hasTestSelectionOverride = true;
            _testSelectedCell = cell;
            _observedCell = null;
            RefreshSelection();
        }

        public void ClearTestSelectionOverride()
        {
            _hasTestSelectionOverride = false;
            _testSelectedCell = null;
            _observedCell = null;
            RefreshSelection();
        }

        private void BindIfNeeded()
        {
            if (cityRuntimeHost == null)
                cityRuntimeHost = GetComponent<CityRuntimeHost>();
            var runtime = cityRuntimeHost != null ? cityRuntimeHost.Runtime : null;
            if (runtime == null || ReferenceEquals(runtime, _boundRuntime))
                return;
            _boundRuntime = runtime;
            Module = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            _observedCell = null;
            ModuleChanged?.Invoke(Module);
        }

        private void RefreshSelection()
        {
            if (Module == null)
                return;
            var selected = _hasTestSelectionOverride
                ? _testSelectedCell
                : gridSelection != null
                    ? gridSelection.SelectedCell
                    : null;
            if (_observedCell == selected)
                return;
            _observedCell = selected;
            ProjectCell(selected);
        }

        private void ProjectCell(CellCoord? cell)
        {
            if (!cell.HasValue)
            {
                BuildingConditionSelection.Current.Clear();
                return;
            }
            if (Module.Runtime.Buildings.TryFindAt(cell.Value, out var building))
                BuildingConditionSelection.Current.Select(building.Id);
            else
                BuildingConditionSelection.Current.Clear();
        }
    }

    public static class BuildingConditionGameplayBootstrap
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
            var host = cityHost.GetComponent<BuildingConditionRuntimeHost>();
            if (host == null)
                host = cityHost.gameObject.AddComponent<BuildingConditionRuntimeHost>();
            host.Configure(cityHost, UnityEngine.Object.FindFirstObjectByType<GridSelectionTool>());
        }
    }
}
