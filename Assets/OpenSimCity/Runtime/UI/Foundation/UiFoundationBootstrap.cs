using OpenSimCity.Application.Input;
using OpenSimCity.UI.Components;
using OpenSimCity.UI.HUD;
using OpenSimCity.UI.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Foundation
{
    /// <summary>
    /// Builds the runtime UI tree on a UIDocument: stylesheets, screen slots, the
    /// four P2-00 shells and the pointer/focus boundary.
    /// <para>
    /// The tree is built in code rather than authored as UXML so the layout is
    /// reproducible from source and reviewable as a diff, matching how the camera
    /// rig is set up.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class UiFoundationBootstrap : MonoBehaviour
    {
        [SerializeField] private StyleSheet tokensStyleSheet;
        [SerializeField] private StyleSheet componentsStyleSheet;
        [SerializeField] private bool applySafeArea = true;

        private UIDocument _document;
        private UiInputBoundaryBridge _bridge;

        public VisualElement Root { get; private set; }

        public ToolRailView ToolRail { get; private set; }

        public BuildPaletteView BuildPalette { get; private set; }

        public ContextInspectorView ContextInspector { get; private set; }

        public DataOverlayLegendView DataOverlayLegend { get; private set; }

        public GlobalHudView GlobalHud { get; private set; }

        public SimulationControlsView SimulationControls { get; private set; }

        public SavePanelView SavePanel { get; private set; }

        public ScrollView SystemPanelDock { get; private set; }

        public StyleSheet ConfiguredTokensStyleSheet => tokensStyleSheet;

        public StyleSheet ConfiguredComponentsStyleSheet => componentsStyleSheet;

        /// <summary>
        /// Clears the ambient input boundary before the first scene loads.
        /// <para>
        /// <see cref="UiInputBoundary"/> is static, so with domain reload disabled
        /// a capture leaked by one play session would survive into the next and
        /// silently block world input. Application cannot carry engine attributes,
        /// so the reset is anchored here.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInputBoundary()
        {
            UiInputBoundary.Reset();
        }

        public void ConfigureStyleSheets(StyleSheet tokens, StyleSheet components)
        {
            tokensStyleSheet = tokens;
            componentsStyleSheet = components;
        }

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null || _document.rootVisualElement == null)
                return;

            Build(_document.rootVisualElement);
        }

        private void OnDisable()
        {
            _bridge?.Detach();
            _bridge = null;

            // A panel torn down mid-drag would otherwise leave a capture behind and
            // world input would stay blocked for the rest of the session.
            UiInputBoundary.Current.ReleaseAll();

            Root?.RemoveFromHierarchy();
            Root = null;
            SystemPanelDock = null;
        }

        private void Build(VisualElement documentRoot)
        {
            documentRoot.Clear();

            if (tokensStyleSheet != null)
                documentRoot.styleSheets.Add(tokensStyleSheet);
            if (componentsStyleSheet != null)
                documentRoot.styleSheets.Add(componentsStyleSheet);

            Root = new VisualElement { name = UiFoundationNames.RootName };
            Root.AddToClassList(UiFoundationNames.RootClass);
            Root.AddToClassList(UiFoundationNames.RootTransparentClass);

            // The root spans the screen only to position slots. Leaving it
            // pick-transparent is what keeps the World View clickable between
            // panels; the shells themselves pick normally.
            Root.pickingMode = PickingMode.Ignore;
            Root.style.flexDirection = FlexDirection.Column;

            if (applySafeArea)
                ApplySafeArea(Root);

            var middle = new VisualElement { name = "osc-middle" };
            middle.pickingMode = PickingMode.Ignore;
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.flexGrow = 1f;

            ToolRail = new ToolRailView();
            ContextInspector = new ContextInspectorView();
            DataOverlayLegend = new DataOverlayLegendView();
            BuildPalette = new BuildPaletteView();
            GlobalHud = new GlobalHudView();
            SimulationControls = new SimulationControlsView();
            SavePanel = new SavePanelView();
            SystemPanelDock = UiSystemPanelDock.Create();

            var centre = new VisualElement { name = "osc-world-view-spacer" };
            centre.pickingMode = PickingMode.Ignore;
            centre.style.flexGrow = 1f;

            // Inspector and Legend share the right edge: Inspector at the top, the
            // overlay Legend anchored at the bottom. The legend is not a fixed slot
            // (Docs/DESIGN.md 3.3), so this keeps it hugging the screen edge instead
            // of floating inside the World View.
            var rightColumn = new VisualElement { name = "osc-right-column" };
            rightColumn.pickingMode = PickingMode.Ignore;
            rightColumn.style.flexDirection = FlexDirection.Column;
            rightColumn.style.justifyContent = Justify.SpaceBetween;
            var rightBottom = new VisualElement { name = "osc-right-bottom" };
            rightBottom.pickingMode = PickingMode.Ignore;
            rightBottom.style.flexDirection = FlexDirection.Column;
            rightBottom.Add(SavePanel);
            rightBottom.Add(DataOverlayLegend);
            rightBottom.Add(SimulationControls);
            rightColumn.Add(ContextInspector);
            rightColumn.Add(rightBottom);

            middle.Add(ToolRail);
            middle.Add(centre);
            middle.Add(SystemPanelDock);
            middle.Add(rightColumn);

            Root.Add(GlobalHud);
            Root.Add(middle);
            Root.Add(BuildPalette);
            documentRoot.Add(Root);

            _bridge = new UiInputBoundaryBridge(Root, UiInputBoundary.Current);
            _bridge.AddPointerSource(ToolRail);
            _bridge.AddPointerSource(BuildPalette);
            _bridge.AddPointerSource(ContextInspector);
            _bridge.AddPointerSource(DataOverlayLegend);
            _bridge.AddPointerSource(GlobalHud);
            _bridge.AddPointerSource(SimulationControls);
            _bridge.AddPointerSource(SavePanel);
            _bridge.AddPointerSource(SystemPanelDock);
            _bridge.Attach();
        }

        private static void ApplySafeArea(VisualElement root)
        {
            var safeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            root.style.paddingLeft = safeArea.xMin;
            root.style.paddingRight = Screen.width - safeArea.xMax;
            root.style.paddingTop = Screen.height - safeArea.yMax;
            root.style.paddingBottom = safeArea.yMin;
        }
    }

    /// <summary>
    /// Owns layout for feature panels that previously attached themselves directly
    /// to the UIDocument with unrelated absolute coordinates. Binders register new
    /// panels here, while Sync also adopts any legacy panel that appears at the
    /// document root so menu visibility and deterministic ordering stay central.
    /// </summary>
    internal static class UiSystemPanelDock
    {
        internal const string Name = "osc-system-panel-dock";

        private static readonly string[] PanelNames =
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

        internal static ScrollView Create()
        {
            var dock = new ScrollView(ScrollViewMode.Vertical) { name = Name };
            dock.pickingMode = PickingMode.Position;
            dock.style.width = 380f;
            dock.style.minWidth = 320f;
            dock.style.maxWidth = 380f;
            dock.style.flexShrink = 0f;
            dock.style.marginLeft = 8f;
            dock.style.marginRight = 8f;
            dock.style.marginTop = 8f;
            dock.style.marginBottom = 8f;
            dock.style.display = DisplayStyle.None;
            dock.contentContainer.style.flexDirection = FlexDirection.Column;
            dock.contentContainer.style.gap = 8f;
            return dock;
        }

        internal static void Dock(VisualElement documentRoot, VisualElement panel)
        {
            if (documentRoot == null || panel == null)
                return;

            var dock = documentRoot.Q<ScrollView>(Name);
            if (dock == null)
            {
                documentRoot.Add(panel);
                return;
            }

            Normalize(panel);
            dock.contentContainer.Add(panel);
        }

        internal static void Sync(VisualElement documentRoot, ScrollView dock, bool suppressed)
        {
            if (documentRoot == null || dock == null)
                return;

            var content = dock.contentContainer;
            var nextIndex = 0;
            for (var index = 0; index < PanelNames.Length; index++)
            {
                var panel = documentRoot.Q<VisualElement>(PanelNames[index]);
                if (panel == null || ReferenceEquals(panel, dock))
                    continue;

                Normalize(panel);
                if (!ReferenceEquals(panel.parent, content) || content.IndexOf(panel) != nextIndex)
                {
                    panel.RemoveFromHierarchy();
                    content.Insert(nextIndex, panel);
                }
                nextIndex++;
            }

            dock.style.display = !suppressed && nextIndex > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static void Normalize(VisualElement panel)
        {
            panel.style.position = Position.Relative;
            panel.style.left = StyleKeyword.Auto;
            panel.style.right = StyleKeyword.Auto;
            panel.style.top = StyleKeyword.Auto;
            panel.style.bottom = StyleKeyword.Auto;
            panel.style.width = StyleKeyword.Auto;
            panel.style.alignSelf = Align.Stretch;
            panel.style.flexShrink = 0f;
        }
    }
}
