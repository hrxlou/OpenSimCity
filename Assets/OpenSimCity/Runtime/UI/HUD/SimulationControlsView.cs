using OpenSimCity.UI.Foundation;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.HUD
{
    /// <summary>P3's lower-right pause/speed control shell.</summary>
    public sealed class SimulationControlsView : VisualElement
    {
        public SimulationControlsView()
        {
            name = UiFoundationNames.SimulationControlsName;
            AddToClassList(UiFoundationNames.PanelClass);
            AddToClassList(UiFoundationNames.SimulationControlsClass);
        }
    }
}
