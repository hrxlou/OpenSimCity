using OpenSimCity.UI.Foundation;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Persistence
{
    /// <summary>P4 save slot/list panel populated from the Application ViewModel.</summary>
    public sealed class SavePanelView : VisualElement
    {
        public SavePanelView()
        {
            name = UiFoundationNames.SavePanelName;
            AddToClassList(UiFoundationNames.PanelClass);
            AddToClassList(UiFoundationNames.PanelElevatedClass);
            AddToClassList(UiFoundationNames.SavePanelClass);
        }
    }
}
