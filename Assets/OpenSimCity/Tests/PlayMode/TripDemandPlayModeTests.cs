using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Traffic;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class TripDemandPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypeBuildToolsProducePresentationIndependentOdRows()
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

            var checksumBeforeObservation = runtime.TripDemandSystem.State.ComputeChecksum();
            var rows = runtime.TripDemandModel.Rows;
            Assert.That(runtime.TripDemandModel.CommuteRequested, Is.GreaterThan(0));
            Assert.That(runtime.TripDemandModel.GoodsRequested, Is.GreaterThan(0));
            Assert.That(rows, Is.Not.Empty);
            Assert.That(rows.Where(row => row.Status == TripDemandStatus.Matched)
                .All(row => row.OriginComponentId == row.DestinationComponentId), Is.True);
            Assert.That(runtime.TripDemandModel.Checksum, Is.EqualTo(checksumBeforeObservation));
            Assert.That(runtime.TripDemandSystem.State.ComputeChecksum(), Is.EqualTo(checksumBeforeObservation));
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
            Assert.That(runtime.Clock.CurrentTick.Value,
                Is.EqualTo((long)monthIndex * runtime.MonthlyScheduler.TicksPerMonth));
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                Assert.That(runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds), Is.EqualTo(1));
        }
    }
}
