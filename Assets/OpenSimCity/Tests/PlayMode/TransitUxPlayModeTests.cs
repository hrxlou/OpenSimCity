using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Gameplay.Transit;
using OpenSimCity.Presentation.Transit;
using OpenSimCity.Simulation.Transit;
using OpenSimCity.UI.Transit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class TransitUxPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypeInstallsPublicTransitUxAndBehaviourFreePooledBuses()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            Assert.That(context, Is.Not.Null);
            var runtime = context.Runtime;
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);
            var y = runtime.Height / 2;

            Commit(context, ConstructionToolMode.Road,
                new CellCoord(0, y), new CellCoord(15, y));
            Commit(context, ConstructionToolMode.ZoneResidential,
                new CellCoord(2, y - 2), new CellCoord(3, y - 1));
            Commit(context, ConstructionToolMode.ZoneCommercial,
                new CellCoord(6, y - 2), new CellCoord(7, y - 1));
            Commit(context, ConstructionToolMode.ZoneIndustrial,
                new CellCoord(10, y - 2), new CellCoord(11, y - 1));
            Commit(context, ConstructionToolMode.PowerPlant,
                new CellCoord(15, y - 1), new CellCoord(15, y - 1));
            for (var month = 0; month < 6; month++)
                RunMonth(runtime, month);
            yield return null;

            var host = Object.FindFirstObjectByType<TransitRuntimeHost>();
            var input = Object.FindFirstObjectByType<TransitConstructionInputDriver>();
            var ui = Object.FindFirstObjectByType<TransitUiBinder>();
            var coordinator = Object.FindFirstObjectByType<TransitViewCoordinator>();
            Assert.That(host, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(ui.Panel, Is.Not.Null);
            Assert.That(ui.Panel.Q<SliderInt>("osc-transit-budget"), Is.Not.Null);
            Assert.That(ui.Panel.Q<Label>("osc-transit-summary"), Is.Not.Null);

            var tool = host.Tool;
            AddStop(tool, new CellCoord(2, y));
            AddStop(tool, new CellCoord(7, y));
            AddStop(tool, new CellCoord(11, y));
            var ids = host.Module.Network.Stops.Select(stop => stop.Id).ToArray();
            tool.SelectLineEditor(name: "간선", serviceUnits: 12);
            for (var index = 0; index < ids.Length; index++)
            {
                host.Module.Network.TryGetStop(ids[index], out var stop);
                Assert.That(tool.PreviewLineStop(ids[index], stop.RoadCell).IsValid, Is.True);
                Assert.That(tool.AddPreviewedLineStop(), Is.True);
            }
            Assert.That(tool.Commit().IsApplied, Is.True);
            host.Module.RunCurrentMonth();
            yield return null;
            yield return null;

            var sourceChecksum = host.Module.ModeChoice.State.ComputeChecksum();
            Assert.That(host.Module.ModeChoice.State.BusTrips, Is.GreaterThan(0));
            coordinator.SetVisibleBusLimit(0);
            yield return null;
            Assert.That(coordinator.ActiveBusCount, Is.Zero);
            Assert.That(host.Module.ModeChoice.State.ComputeChecksum(), Is.EqualTo(sourceChecksum));

            coordinator.SetVisibleBusLimit(24);
            yield return null;
            yield return null;
            Assert.That(coordinator.ActiveBusCount,
                Is.EqualTo(Math.Min(24, host.Module.ModeChoice.State.BusTrips)));
            Assert.That(coordinator.RenderedLineCount, Is.EqualTo(1));
            Assert.That(coordinator.RenderedStopCount, Is.EqualTo(3));
            Assert.That(host.Module.ModeChoice.State.ComputeChecksum(), Is.EqualTo(sourceChecksum));
            Assert.That(
                coordinator.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(behaviour => !ReferenceEquals(behaviour, coordinator)),
                Is.Empty);

            var oldCoordinator = coordinator;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldCoordinator == null, Is.True);
        }

        [UnityTest]
        public IEnumerator TransitUiInputCaptureBlocksWorldAndReleasesOnDisable()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var ui = Object.FindFirstObjectByType<TransitUiBinder>();
            Assert.That(ui, Is.Not.Null);
            var panel = ui.Panel;
            Assert.That(panel, Is.Not.Null);
            var boundary = OpenSimCity.Application.Input.UiInputBoundary.Current;
            boundary.ReleaseAll();

            using (var enter = PointerEnterEvent.GetPooled())
            {
                enter.target = panel;
                panel.SendEvent(enter);
            }
            Assert.That(boundary.IsPointerOverUi, Is.True);

            ui.enabled = false;
            yield return null;
            Assert.That(boundary.BlocksWorldInput, Is.False);
        }

        private static void AddStop(TransitConstructionTool tool, CellCoord cell)
        {
            tool.SelectStopPlacement();
            var preview = tool.HoverCell(cell);
            Assert.That(preview.IsValid, Is.True, preview.Message);
            Assert.That(tool.Commit().IsApplied, Is.True);
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

        private static void RunMonth(CityRuntime runtime, int monthIndex)
        {
            var firstTick = monthIndex * runtime.MonthlyScheduler.TicksPerMonth;
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                runtime.ProcessSimulationTick(new SimulationTick(firstTick + offset));
        }
    }
}
