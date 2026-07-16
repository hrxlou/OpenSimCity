using OpenSimCity.UI.Foundation;

namespace OpenSimCity.UI.Components
{
    /// <summary>
    /// Bottom slot shell for the selected tool's subtypes, cost and shortcuts
    /// (Docs/DESIGN.md 3.3). Costs stay empty until P2-01 defines real commands.
    /// </summary>
    public sealed class BuildPaletteView : UiPanelShell
    {
        public BuildPaletteView()
            : base(
                UiFoundationNames.BuildPaletteName,
                UiFoundationNames.BuildPaletteClass,
                "Build",
                "배치 항목과 비용은 P2-01에서 연결됩니다.",
                horizontalFixtures: true)
        {
        }
    }
}
