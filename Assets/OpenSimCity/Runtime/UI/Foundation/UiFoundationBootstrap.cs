using System;
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

            var bottomDock = new VisualElement { name = "osc-bottom-command-dock" };
            bottomDock.AddToClassList("osc-bottom-command-dock");
            bottomDock.pickingMode = PickingMode.Ignore;
            bottomDock.style.flexDirection = FlexDirection.Column;
            bottomDock.style.alignItems = Align.Center;

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

            middle.Add(centre);
            middle.Add(SystemPanelDock);
            middle.Add(rightColumn);

            // City builders reserve the lower edge for construction commands so
            // the city remains the dominant visual surface. The contextual
            // palette appears directly above the persistent icon dock.
            bottomDock.Add(BuildPalette);
            bottomDock.Add(ToolRail);

            Root.Add(GlobalHud);
            Root.Add(middle);
            Root.Add(bottomDock);
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
        private const string TabsName = "osc-system-panel-tabs";

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

        private static readonly string[] PanelLabels =
        {
            "대중교통", "공공서비스", "도시환경", "물·밀도",
            "건물상태", "특화", "지구", "정책"
        };

        private sealed class DockState
        {
            public bool IsOpen;
            public string ActivePanelName = PanelNames[0];
            public VisualElement Tabs;
        }

        internal static ScrollView Create()
        {
            var dock = new ScrollView { name = Name };
            dock.AddToClassList("osc-system-panel-dock");
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
            dock.userData = new DockState();
            return dock;
        }

        internal static void Toggle(ScrollView dock)
        {
            if (dock == null)
                return;

            var state = State(dock);
            state.IsOpen = !state.IsOpen;
            ApplyPanelVisibility(dock, state);
            dock.style.display = state.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
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
            var state = State(dock);
            EnsureTabs(dock, state);
            var nextIndex = 1;
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

            ApplyPanelVisibility(dock, state);
            dock.style.display = !suppressed && state.IsOpen && nextIndex > 1
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static DockState State(ScrollView dock)
        {
            if (dock.userData is DockState state)
                return state;

            state = new DockState();
            dock.userData = state;
            return state;
        }

        private static void EnsureTabs(ScrollView dock, DockState state)
        {
            if (state.Tabs != null && ReferenceEquals(state.Tabs.parent, dock.contentContainer))
                return;

            state.Tabs = new VisualElement { name = TabsName };
            state.Tabs.AddToClassList("osc-system-panel-tabs");
            for (var index = 0; index < PanelNames.Length; index++)
            {
                var panelName = PanelNames[index];
                var tab = new Button(() => Select(dock, panelName))
                {
                    name = panelName + "-tab",
                    text = PanelLabels[index]
                };
                tab.AddToClassList(UiFoundationNames.ItemClass);
                tab.AddToClassList("osc-system-panel-tab");
                state.Tabs.Add(tab);
            }
            dock.contentContainer.Insert(0, state.Tabs);
        }

        private static void Select(ScrollView dock, string panelName)
        {
            var state = State(dock);
            state.ActivePanelName = panelName;
            state.IsOpen = true;
            ApplyPanelVisibility(dock, state);
            dock.style.display = DisplayStyle.Flex;
        }

        private static void ApplyPanelVisibility(ScrollView dock, DockState state)
        {
            for (var index = 0; index < PanelNames.Length; index++)
            {
                var panel = dock.contentContainer.Q<VisualElement>(PanelNames[index]);
                if (panel != null)
                    panel.style.display = string.Equals(panel.name, state.ActivePanelName, StringComparison.Ordinal)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                var tab = state.Tabs?.Q<Button>(PanelNames[index] + "-tab");
                tab?.EnableInClassList(
                    UiFoundationNames.ItemSelectedClass,
                    string.Equals(PanelNames[index], state.ActivePanelName, StringComparison.Ordinal));
            }
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
            panel.style.marginBottom = 0f;
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 16f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor = new Color(0.035f, 0.085f, 0.105f, 0.97f);
            panel.style.borderTopLeftRadius = 12f;
            panel.style.borderTopRightRadius = 12f;
            panel.style.borderBottomLeftRadius = 12f;
            panel.style.borderBottomRightRadius = 12f;
        }
    }
}
