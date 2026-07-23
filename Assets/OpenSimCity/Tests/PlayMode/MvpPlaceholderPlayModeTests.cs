using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Verification;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Presentation.Buildings;
using OpenSimCity.Presentation.Roads;
using OpenSimCity.Presentation.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
using UnityApplication = UnityEngine.Application;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class MvpPlaceholderPlayModeTests
    {
        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityPrototypeUsesCentralPooledRoadBuildingAndUtilityRenderers()
        {
            yield return LoadCityPrototype();

            var context = CityRuntimeContext.Current;
            var roadRenderer = Object.FindAnyObjectByType<RoadViewRenderer>();
            var buildingRenderer = Object.FindAnyObjectByType<BuildingViewRenderer>();
            var powerRenderer = Object.FindAnyObjectByType<PowerProducerViewRenderer>();
            Assert.That(roadRenderer, Is.Not.Null);
            Assert.That(buildingRenderer, Is.Not.Null);
            Assert.That(powerRenderer, Is.Not.Null);

            var runtime = context.Runtime;
            var roadY = runtime.Height / 2;
            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(5, roadY))).IsApplied, Is.True);
            var plant = new CellCoord(0, roadY - 1);
            Assert.That(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(plant),
                runtime.DefaultPowerPlantDefinitionId,
                plant)).IsApplied, Is.True);
            var definition = runtime.BuildingDefinitions.Definitions[0];
            runtime.Buildings.Create(
                new StableId(0x7701),
                definition.Id,
                new StableId(0x7702),
                new CellCoord(2, roadY - 2));
            runtime.RefreshReadModels();
            yield return null;

            Assert.That(roadRenderer.ActiveViewCount, Is.EqualTo(6));
            Assert.That(buildingRenderer.ActiveViewCount, Is.EqualTo(1));
            Assert.That(powerRenderer.ActiveViewCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<BuildingViewRenderer>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<RoadViewRenderer>(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BuildingViewsRecycleCentrallyWhenStateIsRemoved()
        {
            yield return LoadCityPrototype();
            var runtime = CityRuntimeContext.Current.Runtime;
            var renderer = Object.FindAnyObjectByType<BuildingViewRenderer>();
            var definition = runtime.BuildingDefinitions.Definitions[0];
            var id = new StableId(0x7710);
            runtime.Buildings.Create(
                id,
                definition.Id,
                new StableId(0x7711),
                new CellCoord(2, 2));
            runtime.RefreshReadModels();
            yield return null;
            Assert.That(renderer.ActiveViewCount, Is.EqualTo(1));

            Assert.That(runtime.Buildings.Remove(id), Is.True);
            runtime.RefreshReadModels();
            yield return null;
            Assert.That(renderer.ActiveViewCount, Is.Zero);
            Assert.That(renderer.PooledViewCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AcceleratedThirtyMinuteSoakRunsInPlayerEnvironmentWithoutFatalLogs()
        {
            var fatalLogs = new List<string>();
            UnityApplication.LogCallback callback = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    fatalLogs.Add($"{type}: {condition}\n{stackTrace}");
            };
            UnityApplication.logMessageReceived += callback;
            var allocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
            MvpSoakReport grid;
            MvpSoakReport branch;
            try
            {
                grid = MvpSoakFixture.Run(MvpRoadLayout.SimpleGrid, observePresentationReadModels: true);
                yield return null;
                branch = MvpSoakFixture.Run(MvpRoadLayout.EdgeBranch, observePresentationReadModels: true);
                yield return null;
            }
            finally
            {
                UnityApplication.logMessageReceived -= callback;
            }

            var allocatedAfter = Profiler.GetTotalAllocatedMemoryLong();
            TestContext.WriteLine(
                $"P3-07 accelerated player soak: grid={grid.FinalChecksum:X16}, " +
                $"branch={branch.FinalChecksum:X16}, allocatedBefore={allocatedBefore}, " +
                $"allocatedAfter={allocatedAfter}, delta={allocatedAfter - allocatedBefore}");
            Assert.That(fatalLogs, Is.Empty, string.Join("\n", fatalLogs));
            Assert.That(grid.HasFirstFeedbackLoop, Is.True);
            Assert.That(branch.HasFirstFeedbackLoop, Is.True);
            Assert.That(grid.SuggestedNextAction, Is.Not.Empty);
            Assert.That(branch.SuggestedNextAction, Is.Not.Empty);
        }
    }
}
