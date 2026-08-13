using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Conditions;
using OpenSimCity.Presentation.Conditions;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.UI.Conditions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class BuildingConditionPlayModeTests
    {
        [UnityTest]
        public IEnumerator PollutionToAbandonmentRecoveryAndRedevelopmentKeepsBuildingIdentity()
        {
            BuildingConditionSelection.Current.Clear();
            BuildingConditionDatamapSelection.Current.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var host = Object.FindFirstObjectByType<BuildingConditionRuntimeHost>();
            var ui = Object.FindFirstObjectByType<BuildingConditionUiBinder>();
            var coordinator = Object.FindFirstObjectByType<BuildingConditionViewCoordinator>();
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(ui.Panel, Is.Not.Null);

            var runtime = context.Runtime;
            var roadY = runtime.Height / 2;
            Commit(context, ConstructionToolMode.Road,
                new CellCoord(0, roadY), new CellCoord(15, roadY));
            Commit(context, ConstructionToolMode.ZoneResidential,
                new CellCoord(2, roadY - 2), new CellCoord(3, roadY - 1));
            Commit(context, ConstructionToolMode.ZoneCommercial,
                new CellCoord(6, roadY - 2), new CellCoord(7, roadY - 1));
            Commit(context, ConstructionToolMode.ZoneIndustrial,
                new CellCoord(10, roadY - 2), new CellCoord(11, roadY - 1));
            Commit(context, ConstructionToolMode.PowerPlant,
                new CellCoord(15, roadY - 1), new CellCoord(15, roadY - 1));
            RunMonths(runtime, 6);
            yield return null;

            var residential = runtime.Buildings.Buildings.Single(building =>
                runtime.BuildingDefinitions.Get(building.DefinitionId).ZoneType == ZoneType.ResidentialLow);
            var buildingId = residential.Id;
            var footprint = residential.Footprint;
            host.SelectCellForTests(residential.Anchor);
            yield return null;
            Assert.That(BuildingConditionSelection.Current.BuildingId, Is.EqualTo(buildingId));

            var pollutionSourceId = new StableId(0xF630_0000_0000_0201UL);
            Assert.That(host.Module.SpatialFields.Execute(new UpsertSpatialSourceCommand(
                pollutionSourceId,
                SpatialSourceDefinitionIds.IndustrialEmitter,
                residential.Anchor)).IsApplied, Is.True);

            RunMonths(runtime, 7);
            host.Module.RefreshReadModels();
            Assert.That(host.Module.ReadModel.TryGet(buildingId, out var abandoned), Is.True);
            Assert.That(abandoned.Stage, Is.EqualTo(BuildingConditionStageView.Abandoned));
            Assert.That(abandoned.Occupancy, Is.Zero);
            Assert.That(abandoned.TopReason, Is.EqualTo(BuildingConditionReasonView.Pollution));
            Assert.That(runtime.Buildings.TryGet(buildingId, out var abandonedBuilding), Is.True);
            Assert.That(abandonedBuilding.Footprint, Is.EqualTo(footprint));

            BuildingConditionDatamapSelection.Current.SetVisible(true);
            yield return null;
            yield return null;
            Assert.That(coordinator.ActiveMarkerCount, Is.GreaterThan(0));
            Assert.That(coordinator.IsDatamapVisible, Is.True);
            Assert.That(coordinator.DatamapCellCount, Is.EqualTo(host.Module.Overlay.Cells.Count));
            Assert.That(coordinator.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => !ReferenceEquals(component, coordinator)), Is.Empty);
            var selectionTitle = ui.Panel.Q<Label>("osc-condition-selection-title");
            var factorLabel = ui.Panel.Q<Label>("osc-condition-factors");
            Assert.That(selectionTitle, Is.Not.Null);
            Assert.That(factorLabel, Is.Not.Null);
            Assert.That(selectionTitle.text, Does.Contain("폐건물"));
            Assert.That(factorLabel.text, Does.Contain("오염"));

            Assert.That(host.Module.SpatialFields.Execute(
                new RemoveSpatialSourceCommand(pollutionSourceId)).IsApplied, Is.True);
            RunMonths(runtime, 1);
            host.Module.RefreshReadModels();
            Assert.That(host.Module.ReadModel.TryGet(buildingId, out var recovering), Is.True);
            Assert.That(recovering.IsRedevelopmentEligible, Is.False);
            Assert.That(recovering.RedevelopmentBlockReason,
                Is.EqualTo(RedevelopmentBlockReasonView.RecoveryDelay));
            RunMonths(runtime, 1);
            host.Module.RefreshReadModels();
            Assert.That(host.Module.ReadModel.TryGet(buildingId, out var eligible), Is.True);
            Assert.That(eligible.IsRedevelopmentEligible, Is.True);

            var redevelopment = host.Module.Execute(
                new RequestBuildingRedevelopmentCommand(buildingId));
            Assert.That(redevelopment.IsApplied, Is.True, redevelopment.Message);
            host.Module.RefreshReadModels();
            Assert.That(host.Module.ReadModel.TryGet(buildingId, out var inProgress), Is.True);
            Assert.That(inProgress.Stage, Is.EqualTo(BuildingConditionStageView.Redeveloping));
            Assert.That(inProgress.RedevelopmentMonthsRemaining, Is.EqualTo(3));

            RunMonths(runtime, 3);
            host.Module.RefreshReadModels();
            Assert.That(host.Module.ReadModel.TryGet(buildingId, out var completed), Is.True);
            Assert.That(completed.Stage, Is.EqualTo(BuildingConditionStageView.Healthy));
            Assert.That(runtime.Buildings.TryGet(buildingId, out var completedBuilding), Is.True);
            Assert.That(completedBuilding.Id, Is.EqualTo(buildingId));
            Assert.That(completedBuilding.Footprint, Is.EqualTo(footprint));

            host.ClearTestSelectionOverride();
            var oldCoordinator = coordinator;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldCoordinator == null, Is.True);
            BuildingConditionSelection.Current.Clear();
            BuildingConditionDatamapSelection.Current.SetVisible(false);
        }

        private static void RunMonths(CityRuntime runtime, int count)
        {
            for (var index = 0; index < count; index++)
                runtime.ProcessSimulationTick(runtime.MonthlyScheduler.NextRunTick);
        }

        private static void Commit(
            CityRuntimeContext context,
            ConstructionToolMode mode,
            CellCoord start,
            CellCoord end)
        {
            context.SelectTool(mode);
            context.Tool.BeginDrag(start);
            context.Tool.UpdateDrag(end);
            var result = context.Tool.CommitDrag();
            Assert.That(result.Committed, Is.True, result.Message);
        }
    }
}
