using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using OpenSimCity.UI.Release;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class ReleaseTitlePlayModeTests
    {
        private sealed class MemorySettings : IPresentationSettingsStore
        {
            public PresentationSettingsDto Load() => new PresentationSettingsDto();
            public void Save(PresentationSettingsDto settings) { }
        }

        [UnityTest]
        public IEnumerator TitleAndSettingsStayInsideViewportAtEveryUiScale()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var previous = PresentationSettingsContext.Current;
            var controller = new PresentationSettingsController(new MemorySettings());
            try
            {
                PresentationSettingsContext.SetCurrent(controller);
                var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
                foreach (var scale in new[] { UiScalePercent.Percent100, UiScalePercent.Percent125, UiScalePercent.Percent150 })
                {
                    controller.SetUiScale(scale);
                    yield return null;
                    yield return null;
                    yield return null;
                    var root = bootstrap.Root;
                    var viewport = root.parent.worldBound;
                    Assert.That(root.worldBound.xMax, Is.LessThanOrEqualTo(viewport.xMax + 1));
                    Assert.That(root.worldBound.yMax, Is.LessThanOrEqualTo(viewport.yMax + 1));
                    var lastButton = root.Q("osc-release-quit");
                    Assert.That(lastButton.worldBound.yMax, Is.LessThan(viewport.yMax), $"Menu clipped at {scale}");
                    Object.FindAnyObjectByType<ReleaseMenuBinder>().ToggleSettings();
                    yield return null;
                    yield return null;
                    var settings = root.Q("osc-release-settings-section");
                    Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(settings.worldBound.yMax, Is.LessThanOrEqualTo(viewport.yMax + 1));
                    Assert.That(settings.worldBound.xMax, Is.LessThanOrEqualTo(viewport.xMax + 1));
                    Assert.That(root.Q("osc-accessibility-settings"), Is.Not.Null);
                    Assert.That(root.Q("osc-audio-day-night-settings"), Is.Not.Null);
                    Object.FindAnyObjectByType<ReleaseMenuBinder>().ToggleSettings();
                    yield return null;
                }
            }
            finally { PresentationSettingsContext.SetCurrent(previous); }
        }

        [UnityTest]
        public IEnumerator InitialTitleHasCityArtworkAndHidesGameplayChrome()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var binder = Object.FindAnyObjectByType<ReleaseMenuBinder>();
            Assert.That(binder.IsTitleScreen, Is.True);
            Assert.That(bootstrap.GlobalHud.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
            Assert.That(bootstrap.ToolRail.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
            var backdrop = bootstrap.Root.Q("osc-release-menu-backdrop");
            Assert.That(backdrop.ClassListContains("osc-title-screen"), Is.True);
            Assert.That(backdrop.worldBound.width, Is.EqualTo(bootstrap.Root.worldBound.width).Within(1f));
            Assert.That(bootstrap.Root.Q("osc-title-city-image").resolvedStyle.backgroundImage.texture, Is.Not.Null);
            Assert.That(bootstrap.Root.Q("osc-release-menu").ClassListContains(UiFoundationNames.PanelClass), Is.False,
                "The initial title is a full-screen composition, not a small floating dialog.");
            Assert.That(bootstrap.Root.Q("osc-release-close"), Is.Null,
                "Do not offer Return to City before the player has entered one.");
        }

        [UnityTest]
        public IEnumerator NewCityUsesSessionActionAndPauseMenuRestoresSpeed()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var previous = ReleaseSessionContext.Current;
            var runtime = CityRuntimeContext.Current.Runtime;
            var oldSpeed = runtime.Clock.Speed;
            var calls = 0;
            try
            {
                ReleaseSessionContext.SetCurrent(new ReleaseSessionActions(
                    () => { calls++; return true; }, () => false, _ => false, () => null));
                var binder = Object.FindAnyObjectByType<ReleaseMenuBinder>();
                binder.RequestNewCity();
                yield return null;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(binder.IsTitleScreen, Is.False);
                Assert.That(binder.IsMenuOpen, Is.False);
                var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
                Assert.That(bootstrap.GlobalHud.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
                runtime.SetSimulationSpeed(SimulationSpeed.Two);
                binder.ToggleMenu();
                Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
                Assert.That(bootstrap.Root.Q("osc-release-menu-backdrop").ClassListContains("osc-pause-screen"), Is.True);
                binder.ToggleMenu();
                Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Two));
            }
            finally
            {
                runtime.SetSimulationSpeed(oldSpeed);
                ReleaseSessionContext.SetCurrent(previous);
            }
        }
    }
}
