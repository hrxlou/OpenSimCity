using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.UI.Foundation;
using OpenSimCity.UI.HUD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class MunicipalHudPlayModeTests
    {
        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TaxAndSpeedButtonsUseApplicationBoundaries()
        {
            yield return LoadCityPrototype();

            var hud = Object.FindAnyObjectByType<MunicipalHudBinder>();
            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var context = CityRuntimeContext.Current;
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.IsBound, Is.True);
            Assert.That(bootstrap.GlobalHud, Is.Not.Null);
            Assert.That(bootstrap.SimulationControls, Is.Not.Null);

            Assert.That(bootstrap.GlobalHud.Q<Button>("osc-tax-residential-increase"), Is.Not.Null);
            hud.RequestResidentialTaxIncrease();
            yield return null;

            Assert.That(context.Runtime.Finance.HasPendingTaxRateChange, Is.True);
            Assert.That(context.Runtime.Finance.PendingTaxRates.ResidentialPermille, Is.EqualTo(10));
            Assert.That(context.Runtime.Finance.ActiveTaxRates.ResidentialPermille, Is.Zero);

            Assert.That(bootstrap.SimulationControls.Q<Button>("osc-simulation-pause"), Is.Not.Null);
            hud.RequestSimulationSpeed(OpenSimCity.Application.Bootstrap.SimulationSpeed.Paused);
            yield return null;
            Assert.That(context.Runtime.Clock.Speed, Is.EqualTo(OpenSimCity.Application.Bootstrap.SimulationSpeed.Paused));
        }

        [UnityTest]
        public IEnumerator HudShowsDemandWithoutFeasibleParcelsThenPowerRecoveryOnNextPeriod()
        {
            yield return LoadCityPrototype();

            var context = CityRuntimeContext.Current;
            var runtime = context.Runtime;
            var roadY = runtime.Height / 2;
            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(5, roadY))).IsApplied, Is.True);
            Assert.That(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(2, roadY - 2),
                new CellCoord(3, roadY - 1))).IsApplied, Is.True);
            var commercial = runtime.BuildingDefinitions.Definitions[1];
            runtime.Buildings.Create(
                new StableId(0xA11CE), commercial.Id, new StableId(0xB11CE), new CellCoord(8, roadY + 3));

            RunOneMonth(runtime, 0);
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var status = bootstrap.GlobalHud.Q<Label>("osc-hud-development-status");
            var reasons = bootstrap.GlobalHud.Q<Label>("osc-hud-development-reasons");
            var period = bootstrap.GlobalHud.Q<Label>("osc-hud-period");
            Assert.That(status.text, Is.EqualTo("수요 있음 / 개발 가능 필지 없음"));
            Assert.That(reasons.text, Does.Contain("전력 부족"));
            Assert.That(period.text, Is.EqualTo("1년 1월"));

            var plantCell = new CellCoord(0, roadY - 1);
            Assert.That(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(plantCell), runtime.DefaultPowerPlantDefinitionId, plantCell)).IsApplied, Is.True);
            RunOneMonth(runtime, runtime.MonthlyScheduler.TicksPerMonth);
            yield return null;

            Assert.That(status.text, Does.StartWith("수요 있음 / 개발 가능 "));
            Assert.That(status.text, Does.Not.Contain("없음"));
            Assert.That(period.text, Is.EqualTo("1년 2월"));
        }

        [UnityTest]
        public IEnumerator HudSlotsRemainVisibleAcrossAspectRatioSmoke()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var sizes = new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(3440, 1440),
                new Vector2Int(1280, 720)
            };

            foreach (var size in sizes)
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return null;
                yield return null;
                Assert.That(bootstrap.GlobalHud.resolvedStyle.height, Is.GreaterThan(0f));
                Assert.That(bootstrap.GlobalHud.resolvedStyle.height, Is.LessThanOrEqualTo(96f),
                    "The Global HUD must remain a compact two-row information bar.");
                Assert.That(bootstrap.SimulationControls.resolvedStyle.width, Is.GreaterThan(0f));
                Assert.That(bootstrap.GlobalHud.Q<Label>("osc-hud-date"), Is.Not.Null);
                Assert.That(bootstrap.GlobalHud.Q<Label>("osc-hud-development-status"), Is.Not.Null);
                Assert.That(bootstrap.GlobalHud.Q<VisualElement>("osc-hud-summary"), Is.Not.Null);
                Assert.That(bootstrap.GlobalHud.Q<VisualElement>("osc-hud-details"), Is.Not.Null);
            }
        }

        private static void RunOneMonth(CityRuntime runtime, int firstTick)
        {
            for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                runtime.ProcessSimulationTick(new SimulationTick(firstTick + offset));
        }
    }
}
