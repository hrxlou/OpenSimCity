using OpenSimCity.UI.Foundation;

namespace OpenSimCity.UI.Components
{
    /// <summary>
    /// Left slot shell for top-level build and inspect tools (Docs/DESIGN.md 3.3).
    /// Real tools arrive with P2-01 road placement.
    /// </summary>
    public sealed class ToolRailView : UiPanelShell
    {
        public ToolRailView()
            : base(
                UiFoundationNames.ToolRailName,
                UiFoundationNames.ToolRailClass,
                "도시 도구",
                "건설과 데이터 보기를 선택하세요.")
        {
        }
    }
}
