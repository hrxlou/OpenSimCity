using UnityEngine.UIElements;

namespace OpenSimCity.UI.Foundation
{
    /// <summary>
    /// The component states every OpenSimCity UI component must define
    /// (Docs/DESIGN.md section 3.4).
    /// </summary>
    public enum UiComponentState
    {
        Default,
        Hover,
        Focus,
        Selected,
        Disabled,
        Warning,
        Error
    }

    public static class UiComponentStateExtensions
    {
        /// <summary>
        /// Applies a state fixture to an element.
        /// <para>
        /// Hover and focus are intentionally not settable through classes: they
        /// are pseudo-states owned by the panel's event system, and faking them
        /// with a class would let a fixture drift from real interaction. This
        /// clears the class-driven states and, for <see cref="UiComponentState.Focus"/>,
        /// asks the panel for real focus instead.
        /// </para>
        /// </summary>
        public static void ApplyState(this VisualElement element, UiComponentState state)
        {
            if (element == null)
                return;

            element.EnableInClassList(UiFoundationNames.ItemSelectedClass, state == UiComponentState.Selected);
            element.EnableInClassList(UiFoundationNames.ItemDisabledClass, state == UiComponentState.Disabled);
            element.EnableInClassList(UiFoundationNames.ItemWarningClass, state == UiComponentState.Warning);
            element.EnableInClassList(UiFoundationNames.ItemErrorClass, state == UiComponentState.Error);

            element.SetEnabled(state != UiComponentState.Disabled);

            if (state == UiComponentState.Focus)
                element.Focus();
        }

        /// <summary>
        /// Text shown in the marker slot so warning and error do not rely on hue
        /// alone. Returns null for states that carry no marker.
        /// </summary>
        public static string MarkerText(this UiComponentState state)
        {
            switch (state)
            {
                case UiComponentState.Warning:
                    return "!";
                case UiComponentState.Error:
                    return "X";
                default:
                    return null;
            }
        }
    }
}
