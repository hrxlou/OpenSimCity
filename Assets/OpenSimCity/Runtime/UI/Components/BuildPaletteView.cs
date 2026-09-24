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
                "건설 팔레트",
                "선택한 도구의 비용과 단축키를 확인하세요.",
                horizontalFixtures: true)
        {
        }
    }
}
