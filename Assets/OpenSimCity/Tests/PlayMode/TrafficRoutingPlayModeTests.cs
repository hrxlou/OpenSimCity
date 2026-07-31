using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class TrafficRoutingPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypeRoutesDemandAndRemovesStalePathsAfterDemolition()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var runtime = context.Runtime;
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);
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

            for (var month = 0; month < 6; month++)
                RunMonth(runtime, month);
            yield return null;

            var routing = runtime.TrafficRoutingModel;
            Assert.That(routing.HasProcessedMonth, Is.True);
            Assert.That(routing.RoutedAmount, Is.GreaterThan(0));
            Assert.That(routing.Assignments, Is.Not.Empty);
            Assert.That(routing.Edges, Is.Not.Empty);
            Assert.That(routing.Edges.All(edge => edge.Volume > 0), Is.True);
            Assert.That(routing.Edges.All(edge =>
                edge.EffectiveSpeedCellsPerHour <= edge.FreeFlowSpeedCellsPerHour), Is.True);
            Assert.That(routing.Assignments.All(route =>
                route.CongestedTravelSeconds >= route.FreeFlowTravelSeconds), Is.True);

            var checksumBeforeDemolition = routing.Checksum;
            var demolishedCell = new CellCoord(8, roadY);
            Commit(context, ConstructionToolMode.Demolish, demolishedCell, demolishedCell);
            yield return null;

            Assert.That(routing.Checksum, Is.Not.EqualTo(checksumBeforeDemolition));
            Assert.That(routing.Assignments.All(route => !route.Route.Contains(demolishedCell)), Is.True);
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(routing.Checksum));
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
