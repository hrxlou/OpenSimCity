namespace OpenSimCity.UI.Foundation
{
    /// <summary>
    /// USS class and element names shared by the foundation and its shells.
    /// Centralised so validators and tests assert against the same strings the
    /// runtime uses.
    /// </summary>
    public static class UiFoundationNames
    {
        public const string TokensStyleSheetPath = "Assets/OpenSimCity/Content/UI/Styles/Tokens.uss";
        public const string ComponentsStyleSheetPath = "Assets/OpenSimCity/Content/UI/Styles/Components.uss";

        public const string RootClass = "osc-root";
        public const string RootTransparentClass = "osc-root--transparent";
        public const string PanelClass = "osc-panel";
        public const string PanelElevatedClass = "osc-panel--elevated";
        public const string PanelTitleClass = "osc-panel__title";
        public const string PanelCaptionClass = "osc-panel__caption";

        public const string ItemClass = "osc-item";
        public const string ItemMarkerClass = "osc-item__marker";
        public const string ItemSelectedClass = "osc-item--selected";
        public const string ItemDisabledClass = "osc-item--disabled";
        public const string ItemWarningClass = "osc-item--warning";
        public const string ItemErrorClass = "osc-item--error";

        public const string EmptyNoteClass = "osc-empty-note";

        public const string FixturesClass = "osc-fixtures";
        public const string FixturesRowClass = "osc-fixtures--row";

        public const string ToolRailClass = "osc-tool-rail";
        public const string BuildPaletteClass = "osc-build-palette";
        public const string ContextInspectorClass = "osc-context-inspector";
        public const string DataOverlayLegendClass = "osc-data-overlay-legend";

        public const string RootName = "osc-root";
        public const string ToolRailName = "osc-tool-rail";
        public const string BuildPaletteName = "osc-build-palette";
        public const string ContextInspectorName = "osc-context-inspector";
        public const string DataOverlayLegendName = "osc-data-overlay-legend";
    }
}
