using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Presentation.Vehicles;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class TrafficVehiclePresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator VehiclePoolReusesFiveHundredBehaviourFreeViews()
        {
            var root = new GameObject("VehiclePoolTestRoot");
            var prefab = new GameObject("VehiclePoolTestPrefab");
            prefab.SetActive(false);
            var scheduler = new VehicleViewScheduler(root.transform, prefab);
            var samples = CreateSamples(500);

            scheduler.ApplySamples(samples);
            scheduler.UpdatePositions(new WorldBoundsReadModel(8, 2, 1f), 2.5d, 10d);
            yield return null;

            Assert.That(scheduler.ActiveCount, Is.EqualTo(500));
            Assert.That(scheduler.PooledCount, Is.Zero);
            for (var index = 0; index < scheduler.ActiveCount; index++)
            {
                Assert.That(
                    scheduler.GetActiveView(index).GetComponentsInChildren<MonoBehaviour>(true),
                    Is.Empty);
            }

            scheduler.ReleaseAll();
            Assert.That(scheduler.ActiveCount, Is.Zero);
            Assert.That(scheduler.PooledCount, Is.EqualTo(500));

            scheduler.ApplySamples(CreateSamples(100));
            scheduler.UpdatePositions(new WorldBoundsReadModel(8, 2, 1f), 7.5d, 10d);
            Assert.That(scheduler.ActiveCount, Is.EqualTo(100));
            Assert.That(scheduler.PooledCount, Is.EqualTo(400));
            Assert.That(scheduler.GetActiveView(0).transform.localPosition.x, Is.GreaterThan(0f));

            scheduler.Dispose();
            Object.Destroy(prefab);
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityPrototypeCoordinatorKeepsViewsReadOnlyAndDropsStaleRoutes()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            Assert.That(context, Is.Not.Null);
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
            yield return null;

            var coordinator = Object.FindFirstObjectByType<TrafficViewCoordinator>();
            Assert.That(coordinator, Is.Not.Null);
            var sourceChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();

            coordinator.SetVisibleVehicleLimit(0);
            yield return null;
            Assert.That(coordinator.ActiveVehicleCount, Is.Zero);
            Assert.That(coordinator.Model.SourceSimulationChecksum, Is.EqualTo(sourceChecksum));

            coordinator.SetVisibleVehicleLimit(100);
            coordinator.SetCongestionMapVisible(true);
            yield return null;
            yield return null;

            Assert.That(coordinator.ActiveVehicleCount,
                Is.EqualTo(Math.Min(100, runtime.TrafficRoutingModel.RoutedAmount)));
            Assert.That(coordinator.RenderedCongestionEdgeCount,
                Is.EqualTo(coordinator.Model.Edges.Count));
            Assert.That(coordinator.Model.SourceSimulationChecksum, Is.EqualTo(sourceChecksum));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(sourceChecksum));

            var routeToBreak = coordinator.Model.VehicleSamples
                .First(sample => sample.Path.Cells.Count > 2)
                .Path;
            var demolishedCell = routeToBreak.Cells[routeToBreak.Cells.Count / 2];
            Assert.That(routeToBreak.Cells.Contains(demolishedCell), Is.True);
            Commit(context, ConstructionToolMode.Demolish, demolishedCell, demolishedCell);
            yield return null;
            yield return null;

            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.Not.EqualTo(sourceChecksum));
            Assert.That(coordinator.Model.VehicleSamples.All(sample =>
                !sample.Path.Cells.Contains(demolishedCell)), Is.True);
            Assert.That(coordinator.ActiveVehicleCount, Is.EqualTo(coordinator.Model.VehicleSamples.Count));
            Assert.That(coordinator.RenderedCongestionEdgeCount, Is.EqualTo(coordinator.Model.Edges.Count));

            var oldCoordinator = coordinator;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldCoordinator == null, Is.True);
        }

        [UnityTest]
        public IEnumerator BenchmarkSceneMeasuresZeroHundredAndFiveHundredViews()
        {
            yield return SceneManager.LoadSceneAsync("Benchmark", LoadSceneMode.Single);
            yield return null;

            var root = new GameObject("TrafficVehicleBenchmarkRoot");
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "TrafficVehicleBenchmarkPrefab";
            var collider = prefab.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            prefab.SetActive(false);
            var scheduler = new VehicleViewScheduler(root.transform, prefab);
            var bounds = new WorldBoundsReadModel(128, 128, 1f);

            foreach (var count in new[] { 0, 100, 500 })
            {
                var samples = CreateSamples(count);
                var gcRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC Allocated In Frame",
                    1);
                var drawRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Render,
                    "Draw Calls Count",
                    1);
                var mainThreadRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal,
                    "Main Thread",
                    1);
                var stopwatch = Stopwatch.StartNew();
                scheduler.ApplySamples(samples);
                scheduler.UpdatePositions(bounds, count + 0.5d, 12d);
                stopwatch.Stop();
                yield return null;

                var gcAllocated = gcRecorder.Valid ? gcRecorder.LastValue : -1L;
                var drawCalls = drawRecorder.Valid ? drawRecorder.LastValue : -1L;
                var mainThreadNanoseconds = mainThreadRecorder.Valid
                    ? mainThreadRecorder.LastValue
                    : -1L;
                gcRecorder.Dispose();
                drawRecorder.Dispose();
                mainThreadRecorder.Dispose();

                Assert.That(scheduler.ActiveCount, Is.EqualTo(count));
                Assert.That(scheduler.Samples, Has.Count.EqualTo(count));
                TestContext.Out.WriteLine(
                    $"P5-03 Benchmark Scene views={count}: updateMs={stopwatch.Elapsed.TotalMilliseconds:0.000}, " +
                    $"gcAllocatedInFrame={gcAllocated}, drawCalls={drawCalls}, " +
                    $"mainThreadNs={mainThreadNanoseconds}, active={scheduler.ActiveCount}, " +
                    $"pooled={scheduler.PooledCount}");
            }

            scheduler.Dispose();
            Object.Destroy(prefab);
            Object.Destroy(root);
            yield return null;
        }

        private static IReadOnlyList<VehicleSampleData> CreateSamples(int count)
        {
            var path = new VehicleSamplePathData(
                new[]
                {
                    new CellCoord(0, 0),
                    new CellCoord(1, 0),
                    new CellCoord(2, 0),
                    new CellCoord(3, 0),
                    new CellCoord(4, 0),
                    new CellCoord(5, 0),
                    new CellCoord(6, 0),
                    new CellCoord(7, 0)
                },
                new[] { 60, 60, 90, 120, 90, 60, 60 });
            var samples = new VehicleSampleData[count];
            for (var index = 0; index < count; index++)
            {
                samples[index] = new VehicleSampleData(
                    new StableId((ulong)(index + 1)),
                    index % 2 == 0 ? TrafficFlowKind.Commute : TrafficFlowKind.Goods,
                    representedTripAmount: 1,
                    phaseOffsetPermille: count == 0 ? 0 : index * 1000 / count,
                    path);
            }
            return Array.AsReadOnly(samples);
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
