using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class NewCityGrowthBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypePublicConstructionToolsReachFirstPopulationAndJobs()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var runtime = context.Runtime;
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);

            Assert.That(runtime.UrbanEconomySystem.Rci.Residential.SmoothedDemand, Is.EqualTo(64));
            Assert.That(runtime.UrbanEconomySystem.Rci.Commercial.SmoothedDemand, Is.EqualTo(64));
            Assert.That(runtime.UrbanEconomySystem.Rci.Industrial.SmoothedDemand, Is.EqualTo(64));

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

            Assert.That(runtime.Buildings.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(runtime.PopulationSystem.Population.TotalPopulation, Is.GreaterThan(0));
            Assert.That(runtime.PopulationSystem.Employment.EmployedWorkers, Is.GreaterThan(0));

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var population = bootstrap.GlobalHud.Q<Label>("osc-hud-population");
            var period = bootstrap.GlobalHud.Q<Label>("osc-hud-period");
            var status = bootstrap.GlobalHud.Q<Label>("osc-hud-development-status");
            Assert.That(population.text, Is.Not.EqualTo("0"));
            Assert.That(period.text, Is.EqualTo("1년 6월"));
            Assert.That(status.text, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator BootstrapDemandShowsPowerBlockAndToolPlacementRecovers()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var runtime = context.Runtime;
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);
            var roadY = runtime.Height / 2;

            Commit(context, ConstructionToolMode.Road,
                new CellCoord(0, roadY), new CellCoord(8, roadY));
            Commit(context, ConstructionToolMode.ZoneResidential,
                new CellCoord(2, roadY - 2), new CellCoord(3, roadY - 1));
            RunMonth(runtime, 0);
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var reasons = bootstrap.GlobalHud.Q<Label>("osc-hud-development-reasons");
            Assert.That(reasons.text, Does.Contain("전력 부족"));

            Commit(context, ConstructionToolMode.PowerPlant,
                new CellCoord(8, roadY - 1), new CellCoord(8, roadY - 1));
            RunMonth(runtime, 1);
            yield return null;

            Assert.That(reasons.text, Does.Not.Contain("전력 부족"));
            Assert.That(runtime.GrowthSystem.State.Count, Is.GreaterThan(0));
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
