using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OpenSimCity.Application.Input;
using OpenSimCity.UI.Foundation;
using OpenSimCity.UI.Release;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class SystemPanelDockPlayModeTests
    {
        private static readonly string[] ExpectedPanels =
        {
            "osc-transit-panel",
            "osc-public-service-panel",
            "osc-spatial-fields-panel",
            "osc-water-density-panel",
            "osc-building-condition-panel",
            "osc-specialization-panel",
            "osc-district-panel",
            "osc-district-policy-panel"
        };

        [TearDown]
        public void TearDown()
        {
            UiInputBoundary.Reset();
        }

        [UnityTest]
        public IEnumerator ReleaseMenuStartsModalAndSuppressesSystemPanelDock()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "CityPrototype has no UiFoundationBootstrap.");
            Assert.That(bootstrap.SystemPanelDock, Is.Not.Null, "The shared system panel dock was not built.");

            var backdrop = bootstrap.Root.Q<VisualElement>("osc-release-menu-backdrop");
            Assert.That(backdrop, Is.Not.Null, "The release menu backdrop was not installed.");
            Assert.That(backdrop.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(backdrop.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(bootstrap.SystemPanelDock.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(UiInputBoundary.Current.IsPointerOverUi, Is.True,
                "An open release menu must block world pointer input.");

            var binder = Object.FindAnyObjectByType<ReleaseMenuBinder>();
            Assert.That(binder, Is.Not.Null, "The release menu binder was not installed.");
            var toggleMenu = typeof(ReleaseMenuBinder).GetMethod(
                "ToggleMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(toggleMenu, Is.Not.Null);
            toggleMenu.Invoke(binder, null);
            yield return null;

            Assert.That(backdrop.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(bootstrap.SystemPanelDock.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator AllSystemPanelsAreAdoptedNormalizedAndDeterministicallyOrdered()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var documentRoot = bootstrap.Root.parent;
            var dock = bootstrap.SystemPanelDock;
            var previousIndex = -1;

            for (var index = 0; index < ExpectedPanels.Length; index++)
            {
                var panel = documentRoot.Q<VisualElement>(ExpectedPanels[index]);
                Assert.That(panel, Is.Not.Null, ExpectedPanels[index] + " was not installed.");
                Assert.That(panel.parent, Is.SameAs(dock.contentContainer),
                    ExpectedPanels[index] + " did not move into the shared dock.");
                Assert.That(panel.style.position.value, Is.EqualTo(Position.Relative),
                    ExpectedPanels[index] + " still owns an absolute screen position.");

                var dockIndex = dock.contentContainer.IndexOf(panel);
                Assert.That(dockIndex, Is.GreaterThan(previousIndex),
                    ExpectedPanels[index] + " is out of deterministic dock order.");
                previousIndex = dockIndex;
            }
        }

        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }
    }
}
