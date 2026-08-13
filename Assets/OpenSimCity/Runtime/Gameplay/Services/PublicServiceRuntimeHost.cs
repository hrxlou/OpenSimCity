using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Gameplay.Services
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(240)]
    public sealed class PublicServiceRuntimeHost : MonoBehaviour
    {
        [SerializeField] private CityRuntimeHost cityRuntimeHost;
        [SerializeField] private GridSelectionTool gridSelection;

        private CityRuntime _boundRuntime;
        private CellCoord? _observedSelection;
        private bool _hasTestSelection;
        private CellCoord? _testSelection;

        public PublicServiceRuntimeModule Module { get; private set; }
        public CellCoord? SelectedCell => PublicServiceSelection.Current.SelectedCell;

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
            PublicServiceSelection.Current.SetSelectedCell(null);
        }

        public void SelectCellForTests(CellCoord? cell)
        {
            _hasTestSelection = true;
            _testSelection = cell;
            _observedSelection = null;
            RefreshSelection();
        }

        public CommandExecutionResult Place(ServiceKindView kind, CellCoord cell)
        {
            if (Module == null)
                return CommandExecutionResult.Failed("Service runtime is not available.");
            var definitionId = PublicServiceDefinitionIds.For(kind);
            var facilityId = Module.FacilityIdFor(definitionId, cell);
            var result = Module.Execute(new PlaceServiceFacilityCommand(facilityId, definitionId, cell));
            if (result.IsApplied)
                PublicServiceSelection.Current.SelectFacility(facilityId);
            return result;
        }

        public CommandExecutionResult SetBudget(StableId facilityId, int budgetPermille)
        {
            return Module == null
                ? CommandExecutionResult.Failed("Service runtime is not available.")
                : Module.Execute(new SetServiceBudgetCommand(facilityId, budgetPermille));
        }

        private void BindIfNeeded()
        {
            if (cityRuntimeHost == null)
                cityRuntimeHost = GetComponent<CityRuntimeHost>();
            var runtime = cityRuntimeHost != null ? cityRuntimeHost.Runtime : null;
            if (runtime == null || ReferenceEquals(runtime, _boundRuntime))
                return;
            _boundRuntime = runtime;
            Module = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            _observedSelection = null;
        }

        private void RefreshSelection()
        {
            var selected = _hasTestSelection
                ? _testSelection
                : gridSelection != null
                    ? gridSelection.SelectedCell
                    : null;
            if (_observedSelection == selected)
                return;
            _observedSelection = selected;
            PublicServiceSelection.Current.SetSelectedCell(selected);
            if (Module == null || !selected.HasValue)
            {
                PublicServiceSelection.Current.SelectFacility(StableId.Invalid);
                return;
            }
            var facilities = Module.Facilities.Facilities;
            for (var index = 0; index < facilities.Count; index++)
            {
                if (facilities[index].Cell == selected.Value)
                {
                    PublicServiceSelection.Current.SelectFacility(facilities[index].Id);
                    return;
                }
            }
            PublicServiceSelection.Current.SelectFacility(StableId.Invalid);
        }
    }

    public static class PublicServiceGameplayBootstrap
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
            if (!scene.IsValid() || !scene.isLoaded) return;
            var cityHost = UnityEngine.Object.FindFirstObjectByType<CityRuntimeHost>();
            if (cityHost == null || cityHost.gameObject.scene != scene) return;
            var host = cityHost.GetComponent<PublicServiceRuntimeHost>();
            if (host == null) host = cityHost.gameObject.AddComponent<PublicServiceRuntimeHost>();
            host.Configure(cityHost, UnityEngine.Object.FindFirstObjectByType<GridSelectionTool>());
        }
    }
}
