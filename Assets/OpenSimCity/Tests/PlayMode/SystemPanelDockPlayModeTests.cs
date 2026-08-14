using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Input;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class SystemPanelDockPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            UiInputBoundary.Reset();
        }

        [UnityTest]
        public IEnumerator ReleaseMenuStartsModalAndSuppressesSystemPanelDock()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

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
        }

        [Test]
        public void DockAdoptsNormalizesOrdersAndSuppressesKnownPanels()
        {
            var documentRoot = new VisualElement();
            var foundationRoot = new VisualElement();
            var dock = UiSystemPanelDock.Create();
            foundationRoot.Add(dock);
            documentRoot.Add(foundationRoot);

            var district = AbsolutePanel("osc-district-panel", 334f, 340f);
            var service = AbsolutePanel("osc-public-service-panel", 12f, 164f);
            var transit = AbsolutePanel("osc-transit-panel", 12f, 190f);
            documentRoot.Add(district);
            documentRoot.Add(service);
            documentRoot.Add(transit);

            UiSystemPanelDock.Sync(documentRoot, dock, false);

            Assert.That(transit.parent, Is.SameAs(dock.contentContainer));
            Assert.That(service.parent, Is.SameAs(dock.contentContainer));
            Assert.That(district.parent, Is.SameAs(dock.contentContainer));
            Assert.That(transit.style.position.value, Is.EqualTo(Position.Relative));
            Assert.That(service.style.position.value, Is.EqualTo(Position.Relative));
            Assert.That(district.style.position.value, Is.EqualTo(Position.Relative));
            Assert.That(dock.contentContainer.IndexOf(transit), Is.LessThan(dock.contentContainer.IndexOf(service)));
            Assert.That(dock.contentContainer.IndexOf(service), Is.LessThan(dock.contentContainer.IndexOf(district)));
            Assert.That(dock.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            UiSystemPanelDock.Sync(documentRoot, dock, true);
            Assert.That(dock.style.display.value, Is.EqualTo(DisplayStyle.None));

            UiSystemPanelDock.Sync(documentRoot, dock, false);
            Assert.That(dock.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        private static VisualElement AbsolutePanel(string name, float left, float top)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            panel.style.left = left;
            panel.style.top = top;
            panel.style.width = 330f;
            return panel;
        }
    }
}
