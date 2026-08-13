using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.WaterDensity;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.WaterDensity;
using OpenSimCity.Presentation.WaterDensity;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.UI.WaterDensity;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class WaterDensityPlayModeTests
    {
        [UnityTest]
        public IEnumerator PumpOverlayAndRestoredDensityStagePreserveSimulationIdentity()
        {
            WaterDensitySelection.Current.SetOverlay(WaterDensityOverlayKind.Water, false);
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var host = Object.FindFirstObjectByType<WaterDensityRuntimeHost>();
            var ui = Object.FindFirstObjectByType<WaterDensityUiBinder>();
            var views = Object.FindFirstObjectByType<WaterDensityViewCoordinator>();
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            Assert.That(views, Is.Not.Null);

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

            var pumpCell = new CellCoord(4, roadY - 1);
            host.SelectCellForTests(pumpCell);
            var placement = host.PlacePump(pumpCell);
            Assert.That(placement.IsApplied, Is.True, placement.Message);
            RunMonths(runtime, 1);
            host.Module.ReadModel.RefreshIfNeeded();
            Assert.That(host.Module.ReadModel.Snapshot.Producers.Count, Is.EqualTo(1));
            Assert.That(host.Module.ReadModel.Snapshot.WaterRequested, Is.GreaterThan(0));

            var waterChecksum = host.Module.Water.State.ComputeChecksum();
            var densityChecksum = host.Module.Density.ComputeChecksum();
            WaterDensitySelection.Current.SetOverlay(WaterDensityOverlayKind.Water, true);
            yield return null;
            yield return null;
            Assert.That(views.ActivePumpViewCount, Is.EqualTo(1));
            Assert.That(views.IsOverlayVisible, Is.True);
            Assert.That(views.OverlayCellCount, Is.GreaterThan(0));
            Assert.That(host.Module.Water.State.ComputeChecksum(), Is.EqualTo(waterChecksum));
            Assert.That(host.Module.Density.ComputeChecksum(), Is.EqualTo(densityChecksum));
            Assert.That(views.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)
                .Where(component => !ReferenceEquals(component, views)), Is.Empty);

            var residential = runtime.Buildings.Buildings.Single(building =>
                runtime.BuildingDefinitions.Get(building.DefinitionId).ZoneType == ZoneType.ResidentialLow);
            var snapshot = host.Module.Capture();
            var row = snapshot.Density.Buildings.Single(item => item.BuildingId == residential.Id.Value);
            row.Stage = (int)DensityStage.Medium;
            row.TargetStage = (int)DensityStage.Medium;
            row.UpgradeMonthsRemaining = 0;
            row.TopReason = (int)DensityUpgradeBlockReason.None;
            host.Module.Restore(snapshot);
            host.SelectCellForTests(residential.Anchor);
            WaterDensitySelection.Current.SetOverlay(WaterDensityOverlayKind.Density, true);
            yield return null;
            yield return null;

            host.Module.ReadModel.RefreshIfNeeded();
            var density = host.Module.ReadModel.Snapshot.Buildings.Single(item => item.BuildingId == residential.Id);
            Assert.That(density.Stage, Is.EqualTo(DensityStageView.Medium));
            Assert.That(density.EffectiveHousingCapacity,
                Is.EqualTo(runtime.BuildingDefinitions.Get(residential.DefinitionId).OccupancyCapacity * 2));
            Assert.That(runtime.Buildings.TryGet(residential.Id, out var stableBuilding), Is.True);
            Assert.That(stableBuilding.Id, Is.EqualTo(residential.Id));
            Assert.That(views.ActiveDensityViewCount, Is.GreaterThan(0));
            var details = ui.Panel.Q<Label>("osc-water-density-details");
            Assert.That(details, Is.Not.Null);
            Assert.That(details.text, Does.Contain("중밀도"));

            var oldViews = views;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldViews == null, Is.True);
            WaterDensitySelection.Current.SetOverlay(WaterDensityOverlayKind.Water, false);
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
