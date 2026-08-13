using System;
using OpenSimCity.Application.Settings;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Input
{
    public static class PresentationKeyboardInput
    {
        public static bool Pressed(PresentationInputAction action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryKey(action, out var key)) return false;
            return keyboard[key].wasPressedThisFrame;
        }

        public static bool Held(PresentationInputAction action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryKey(action, out var key)) return false;
            return keyboard[key].isPressed;
        }

        private static bool TryKey(PresentationInputAction action, out Key key)
        {
            var settings = PresentationSettingsContext.Current;
            var path = settings == null
                ? PresentationInputBindingDefaults.GetDefaultPath(action)
                : settings.GetControlPath(action);
            const string prefix = "<Keyboard>/";
            if (path == null || !path.StartsWith(prefix, StringComparison.Ordinal))
            {
                key = Key.None;
                return false;
            }
            return Enum.TryParse(path.Substring(prefix.Length), true, out key) && key != Key.None;
        }
    }
}
