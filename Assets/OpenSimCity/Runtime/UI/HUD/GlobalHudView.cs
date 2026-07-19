using OpenSimCity.UI.Foundation;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.HUD
{
    /// <summary>P3's top-screen GlobalHud shell; content is supplied by the HUD binder from application ViewModels.</summary>
    public sealed class GlobalHudView : VisualElement
    {
        public GlobalHudView()
        {
            name = UiFoundationNames.GlobalHudName;
            AddToClassList(UiFoundationNames.PanelClass);
            AddToClassList(UiFoundationNames.GlobalHudClass);
        }
    }
}
