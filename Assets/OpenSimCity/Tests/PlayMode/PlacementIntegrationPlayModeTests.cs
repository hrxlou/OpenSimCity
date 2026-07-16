using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Gameplay.Selection;
using OpenSimCity.Presentation.Overlays;
using OpenSimCity.UI.Placement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class PlacementIntegrationPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _spawned)
            {
                if (gameObject != null)
                    Object.Destroy(gameObject);
            }

            _spawned.Clear();
            CityRuntimeContext.Reset();
            UiInputBoundary.Reset();
        }

        private CityRuntimeHost SpawnHost()
        {
            var gameObject = new GameObject("CityRuntimeHost");
            gameObject.SetActive(false);
            _spawned.Add(gameObject);

            var boundsSource = gameObject.AddComponent<WorldBoundsReadModelSource>();
            var host = gameObject.AddComponent<CityRuntimeHost>();
            host.ConfigureReferences(boundsSource, null);
            gameObject.SetActive(true);
            return host;
        }

        [UnityTest]
        public IEnumerator HostBuildsAmbientContextOnEnable()
        {
            var host = SpawnHost();
            yield return null;

            Assert.That(CityRuntimeContext.Current, Is.Not.Null);
            Assert.That(CityRuntimeContext.Current, Is.SameAs(host.Context));
            Assert.That(host.Runtime, Is.Not.Null);
            Assert.That(host.Context.HasWorldBounds, Is.True);
        }

        [UnityTest]
        public IEnumerator ToolDragCommitPlacesRoadThroughContext()
        {
            var host = SpawnHost();
            yield return null;

            var context = host.Context;
            context.SelectTool(ConstructionToolMode.Road);
            context.Tool.BeginDrag(new CellCoord(0, 32));
            context.Tool.UpdateDrag(new CellCoord(4, 32));
            Assert.That(context.Tool.Preview.IsValid, Is.True);

            var result = context.Tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(host.Runtime.Roads.IsOccupied(new CellCoord(2, 32)), Is.True);
        }

        [UnityTest]
        public IEnumerator OverlayRendererBuildsCellsForActiveOverlay()
        {
            var host = SpawnHost();
            yield return null;

            var context = host.Context;
            context.SelectTool(ConstructionToolMode.Road);
            context.Tool.BeginDrag(new CellCoord(0, 32));
            context.Tool.UpdateDrag(new CellCoord(5, 32));
            context.Tool.CommitDrag();

            var rendererObject = new GameObject("PlacementOverlayRenderer");
            _spawned.Add(rendererObject);
            var renderer = rendererObject.AddComponent<PlacementOverlayRenderer>();

            // Let the renderer bind to the ambient context.
            yield return null;
            context.SetOverlay(PlacementOverlayKind.RoadConnectivity);
            yield return null;

            Assert.That(renderer.HasActiveOverlay, Is.True);
            Assert.That(renderer.OverlayCellCount, Is.EqualTo(6));
        }

        // --- Scene-wired fixture (produced by OpenSimCity/Setup/Configure Placement) ---

        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityPrototypeHasWiredPlacementFixture()
        {
            yield return LoadCityPrototype();

            Assert.That(Object.FindAnyObjectByType<CityRuntimeHost>(), Is.Not.Null,
                "CityPrototype has no CityRuntimeHost. Run OpenSimCity/Setup/Configure Placement.");
            Assert.That(Object.FindAnyObjectByType<ConstructionInputDriver>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PlacementOverlayRenderer>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PlacementUiBinder>(), Is.Not.Null);
            Assert.That(CityRuntimeContext.Current, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneInputDriverPlacesRoadFromScreenDrag()
        {
            yield return LoadCityPrototype();

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            var driver = Object.FindAnyObjectByType<ConstructionInputDriver>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);

            driver.SelectTool(ConstructionToolMode.Road);
            var start = rig.ControlledCamera.WorldToScreenPoint(
                GridSelectionMath.CellCenter(new CellCoord(30, 30), rig.WorldBounds));
            var end = rig.ControlledCamera.WorldToScreenPoint(
                GridSelectionMath.CellCenter(new CellCoord(34, 30), rig.WorldBounds));

            Assert.That(driver.TryBeginAtScreen(start), Is.True);
            driver.TryUpdateAtScreen(end);
            var result = driver.Commit();

            Assert.That(result.Committed, Is.True);
            Assert.That(CityRuntimeContext.Current.Runtime.Roads.IsOccupied(new CellCoord(32, 30)), Is.True);
        }

        [UnityTest]
        public IEnumerator SceneOverlayToggleShowsLegend()
        {
            yield return LoadCityPrototype();

            var context = CityRuntimeContext.Current;
            var binder = Object.FindAnyObjectByType<PlacementUiBinder>();
            var legend = Object.FindAnyObjectByType<UI.Foundation.UiFoundationBootstrap>().DataOverlayLegend;
            Assert.That(binder, Is.Not.Null);

            // Bound after a frame, and the legend starts hidden (no overlay yet).
            yield return null;
            Assert.That(binder.IsBound, Is.True);
            Assert.That(legend.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            context.SetOverlay(PlacementOverlayKind.Parcel);
            yield return null;

            Assert.That(legend.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
