using OpenSimCity.UI.Foundation;

namespace OpenSimCity.UI.Components
{
    /// <summary>
    /// Right slot shell for the current selection, failure reasons and available
    /// actions (Docs/DESIGN.md 3.3). Bound to real selection data from P2-01 on.
    /// </summary>
    public sealed class ContextInspectorView : UiPanelShell
    {
        public ContextInspectorView()
            : base(
                UiFoundationNames.ContextInspectorName,
                UiFoundationNames.ContextInspectorClass,
                "Inspector",
                "선택 대상 상세는 P2-01에서 연결됩니다.")
        {
        }
    }
}
