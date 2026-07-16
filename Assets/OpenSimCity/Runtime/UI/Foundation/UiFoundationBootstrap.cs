using OpenSimCity.Application.Input;
using OpenSimCity.UI.Components;
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

            var centre = new VisualElement { name = "osc-world-view-spacer" };
            centre.pickingMode = PickingMode.Ignore;
            centre.style.flexGrow = 1f;
            centre.style.justifyContent = Justify.FlexEnd;
            centre.Add(DataOverlayLegend);

            middle.Add(ToolRail);
            middle.Add(centre);
            middle.Add(ContextInspector);

            Root.Add(middle);
            Root.Add(BuildPalette);
            documentRoot.Add(Root);

            _bridge = new UiInputBoundaryBridge(Root, UiInputBoundary.Current);
            _bridge.AddPointerSource(ToolRail);
            _bridge.AddPointerSource(BuildPalette);
            _bridge.AddPointerSource(ContextInspector);
            _bridge.AddPointerSource(DataOverlayLegend);
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
}
