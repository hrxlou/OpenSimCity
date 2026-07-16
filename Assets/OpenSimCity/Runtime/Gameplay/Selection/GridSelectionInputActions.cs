using System;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Selection
{
    /// <summary>
    /// Input names owned by the selection tool. This lives in a separate map from
    /// camera input so a tool can be disabled without affecting navigation.
    /// </summary>
    public static class GridSelectionInputActions
    {
        public const string MapName = "Selection";
        public const string PointerPosition = "PointerPosition";
        public const string Select = "Select";

        public static void ConfigureAsset(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var map = asset.FindActionMap(MapName, throwIfNotFound: false) ?? asset.AddActionMap(MapName);
            var pointerPosition = FindOrAddAction(map, PointerPosition, InputActionType.Value, "Vector2");
            var select = FindOrAddAction(map, Select, InputActionType.Button, "Button");

            EnsureBinding(pointerPosition, "<Pointer>/position");
            EnsureBinding(select, "<Mouse>/leftButton");
        }

        public static bool HasRequiredActions(InputActionAsset asset)
        {
            var map = asset?.FindActionMap(MapName, throwIfNotFound: false);
            return map != null &&
                   map.FindAction(PointerPosition, throwIfNotFound: false) != null &&
                   map.FindAction(Select, throwIfNotFound: false) != null;
        }

        private static InputAction FindOrAddAction(
            InputActionMap map,
            string actionName,
            InputActionType type,
            string expectedControlType)
        {
            var action = map.FindAction(actionName, throwIfNotFound: false) ?? map.AddAction(actionName, type: type);
            action.expectedControlType = expectedControlType;
            return action;
        }

        private static void EnsureBinding(InputAction action, string path)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.path == path)
                    return;
            }

            action.AddBinding(path);
        }
    }
}
