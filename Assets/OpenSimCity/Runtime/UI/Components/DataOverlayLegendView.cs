using OpenSimCity.UI.Foundation;

namespace OpenSimCity.UI.Components
{
    /// <summary>
    /// Legend shell for datamap overlays: ranges, period and the non-color
    /// pattern channel (Docs/DESIGN.md 3.3, 3.4). Overlay geometry itself is a
    /// Presentation renderer concern and is out of scope for P2-00.
    /// </summary>
    public sealed class DataOverlayLegendView : UiPanelShell
    {
        public DataOverlayLegendView()
            : base(
                UiFoundationNames.DataOverlayLegendName,
                UiFoundationNames.DataOverlayLegendClass,
                "Legend",
                "overlay 범례는 P2-05에서 연결됩니다.")
        {
        }
    }
}
