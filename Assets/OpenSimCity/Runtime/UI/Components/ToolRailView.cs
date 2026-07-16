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
                "Tools",
                "도구는 P2-01에서 연결됩니다.")
        {
        }
    }
}
