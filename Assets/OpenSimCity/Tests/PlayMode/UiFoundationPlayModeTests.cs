using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Input;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Selection;
using OpenSimCity.UI.Components;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class UiFoundationPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            UiInputBoundary.Reset();
        }

        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityPrototypeBuildsTheFourShells()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "CityPrototype has no UiFoundationBootstrap.");
            Assert.That(bootstrap.Root, Is.Not.Null);
            Assert.That(bootstrap.ToolRail, Is.Not.Null);
            Assert.That(bootstrap.BuildPalette, Is.Not.Null);
            Assert.That(bootstrap.ContextInspector, Is.Not.Null);
            Assert.That(bootstrap.DataOverlayLegend, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator EveryShellDefinesAllSevenComponentStates()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var shells = new UiPanelShell[]
            {
                bootstrap.ToolRail, bootstrap.BuildPalette, bootstrap.ContextInspector, bootstrap.DataOverlayLegend
            };

            var expected = System.Enum.GetValues(typeof(UiComponentState)).Cast<UiComponentState>();
            foreach (var shell in shells)
            {
                foreach (var state in expected)
                {
                    Assert.That(
                        shell.GetFixture(state),
                        Is.Not.Null,
                        $"{shell.name} has no fixture for {state}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator WarningAndErrorFixturesCarryANonColorMarker()
        {
            yield return LoadCityPrototype();

            var shell = Object.FindAnyObjectByType<UiFoundationBootstrap>().ToolRail;

            foreach (var state in new[] { UiComponentState.Warning, UiComponentState.Error })
            {
                var marker = shell.GetFixture(state)
                    .Q<Label>(className: UiFoundationNames.ItemMarkerClass);

                Assert.That(marker, Is.Not.Null);
                Assert.That(
                    marker.text,
                    Is.Not.Empty,
                    $"{state} must not rely on color alone (Docs/DESIGN.md 3.1).");
            }
        }

        [UnityTest]
        public IEnumerator PointerOverAShellBlocksCellSelectionAndReleaseRestoresIt()
        {
            yield return LoadCityPrototype();

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            var selectionTool = Object.FindAnyObjectByType<GridSelectionTool>();
            var relay = Object.FindAnyObjectByType<UiInputBoundaryRelay>();
            Assert.That(relay, Is.Not.Null, "CityPrototype has no UiInputBoundaryRelay.");

            var screenPosition = rig.ControlledCamera.WorldToScreenPoint(
                GridSelectionMath.CellCenter(new CellCoord(10, 10), rig.WorldBounds));
            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.True);
            var selectionBefore = selectionTool.SelectedCell;

            UiInputBoundary.Current.CapturePointer();
            yield return null;

            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.False);
            Assert.That(selectionTool.State, Is.EqualTo(GridSelectionToolState.PointerOverUi));
            Assert.That(selectionTool.SelectedCell, Is.EqualTo(selectionBefore));

            UiInputBoundary.Current.ReleasePointer();
            yield return null;

            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.True);
        }

        /// <summary>
        /// Covers the bridge itself: a real pointer entering a shell must reach
        /// the boundary. The other pointer test drives the boundary directly, so
        /// without this one the shell-to-boundary wiring could be missing entirely
        /// and still look green.
        /// </summary>
        [UnityTest]
        public IEnumerator RealPointerEventsOnAShellDriveTheBoundary()
        {
            yield return LoadCityPrototype();

            var shell = Object.FindAnyObjectByType<UiFoundationBootstrap>().ToolRail;
            Assert.That(UiInputBoundary.Current.IsPointerOverUi, Is.False);

            using (var enter = PointerEnterEvent.GetPooled())
            {
                enter.target = shell;
                shell.SendEvent(enter);
            }

            yield return null;
            Assert.That(
                UiInputBoundary.Current.IsPointerOverUi,
                Is.True,
                "PointerEnter on a shell did not reach the input boundary.");

            using (var leave = PointerLeaveEvent.GetPooled())
            {
                leave.target = shell;
                shell.SendEvent(leave);
            }

            yield return null;
            Assert.That(UiInputBoundary.Current.IsPointerOverUi, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardFocusOnUiBlocksCellSelection()
        {
            yield return LoadCityPrototype();

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            var selectionTool = Object.FindAnyObjectByType<GridSelectionTool>();
            var screenPosition = rig.ControlledCamera.WorldToScreenPoint(
                GridSelectionMath.CellCenter(new CellCoord(12, 12), rig.WorldBounds));

            UiInputBoundary.Current.CaptureKeyboardFocus();
            yield return null;

            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.False);

            UiInputBoundary.Current.ReleaseKeyboardFocus();
            yield return null;

            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.True);
        }

        [UnityTest]
        public IEnumerator ShellFixturesAreFocusableForKeyboardTraversal()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var focusable = bootstrap.Root
                .Query<VisualElement>(className: UiFoundationNames.ItemClass)
                .ToList()
                .Where(element => element.focusable)
                .ToList();

            Assert.That(
                focusable,
                Is.Not.Empty,
                "Keyboard traversal needs focusable stops in the shells.");
        }

        [UnityTest]
        public IEnumerator FocusingAShellCapturesAndBlurringReleasesTheBoundary()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var item = bootstrap.ToolRail.GetFixture(UiComponentState.Default);

            item.Focus();
            yield return null;
            Assert.That(UiInputBoundary.Current.IsKeyboardFocusCaptured, Is.True);

            item.Blur();
            yield return null;
            Assert.That(UiInputBoundary.Current.IsKeyboardFocusCaptured, Is.False);
        }

        [UnityTest]
        public IEnumerator RootIsPickIgnoredSoWorldClicksPassBetweenPanels()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();

            Assert.That(
                bootstrap.Root.pickingMode,
                Is.EqualTo(PickingMode.Ignore),
                "A picking root would swallow every world click.");
            Assert.That(
                bootstrap.ToolRail.pickingMode,
                Is.EqualTo(PickingMode.Position),
                "Panels themselves must still pick so they can capture the pointer.");
        }

        /// <summary>
        /// PanelSettings scales with screen size against a fixed reference
        /// resolution, so token-driven widths must resolve to the same panel-space
        /// value on 16:9, ultrawide and a small window. A width that drifts with
        /// the aspect ratio means something bypassed the tokens or the panel scale
        /// mode regressed.
        /// </summary>
        [UnityTest]
        public IEnumerator TokenDrivenWidthsSurviveAspectRatioAndResizeChanges()
        {
            yield return LoadCityPrototype();

            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var resolutions = new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(3440, 1440),
                new Vector2Int(1280, 720)
            };

            var railWidths = new System.Collections.Generic.List<float>();
            var inspectorWidths = new System.Collections.Generic.List<float>();

            foreach (var resolution in resolutions)
            {
                Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
                yield return null;
                yield return null;

                var railWidth = bootstrap.ToolRail.resolvedStyle.width;
                var inspectorWidth = bootstrap.ContextInspector.resolvedStyle.width;

                Assert.That(
                    railWidth,
                    Is.GreaterThan(0f),
                    $"ToolRail collapsed at {resolution.x}x{resolution.y}.");
                Assert.That(
                    inspectorWidth,
                    Is.GreaterThan(0f),
                    $"ContextInspector collapsed at {resolution.x}x{resolution.y}.");

                railWidths.Add(railWidth);
                inspectorWidths.Add(inspectorWidth);
            }

            Assert.That(railWidths, Is.All.EqualTo(railWidths[0]).Within(0.5f));
            Assert.That(inspectorWidths, Is.All.EqualTo(inspectorWidths[0]).Within(0.5f));
        }
    }
}
