using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Camera
{
    /// <summary>
    /// Stable names and construction rules for the camera Input System action map.
    /// The Editor uses the same builder when it creates the committed .inputactions asset.
    /// </summary>
    public static class IsometricCameraInputActions
    {
        public const string MapName = "Camera";
        public const string Move = "Move";
        public const string Zoom = "Zoom";
        public const string RotateLeft = "RotateLeft";
        public const string RotateRight = "RotateRight";
        public const string PointerPosition = "PointerPosition";
        public const string PointerPress = "PointerPress";

        public static InputActionAsset CreateDefaultAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "IsometricCamera";
            ConfigureAsset(asset);
            return asset;
        }

        /// <summary>
        /// Adds the required map/actions/bindings to an asset without duplicating
        /// entries when the Editor setup menu is run repeatedly.
        /// </summary>
        public static void ConfigureAsset(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var map = asset.FindActionMap(MapName, throwIfNotFound: false) ?? asset.AddActionMap(MapName);
            var move = FindOrAddAction(map, Move, InputActionType.Value, "Vector2");
            var zoom = FindOrAddAction(map, Zoom, InputActionType.Value, "Axis");
            var rotateLeft = FindOrAddAction(map, RotateLeft, InputActionType.Button, "Button");
            var rotateRight = FindOrAddAction(map, RotateRight, InputActionType.Button, "Button");
            var pointerPosition = FindOrAddAction(map, PointerPosition, InputActionType.Value, "Vector2");
            var pointerPress = FindOrAddAction(map, PointerPress, InputActionType.Button, "Button");

            EnsureMoveBindings(move);
            EnsureBinding(zoom, "<Mouse>/scroll/y", "Scale(factor=0.05)");
            EnsureBinding(rotateLeft, "<Keyboard>/q");
            EnsureBinding(rotateRight, "<Keyboard>/e");
            EnsureBinding(pointerPosition, "<Pointer>/position");
            EnsureBinding(pointerPress, "<Mouse>/middleButton");
        }

        public static bool HasRequiredActions(InputActionAsset asset)
        {
            if (asset == null)
                return false;

            var map = asset.FindActionMap(MapName, throwIfNotFound: false);
            if (map == null)
                return false;

            return map.FindAction(Move, throwIfNotFound: false) != null &&
                   map.FindAction(Zoom, throwIfNotFound: false) != null &&
                   map.FindAction(RotateLeft, throwIfNotFound: false) != null &&
                   map.FindAction(RotateRight, throwIfNotFound: false) != null &&
                   map.FindAction(PointerPosition, throwIfNotFound: false) != null &&
                   map.FindAction(PointerPress, throwIfNotFound: false) != null;
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

        private static void EnsureMoveBindings(InputAction move)
        {
            if (!HasComposite(move, "2DVector"))
            {
                move.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }

        }

        private static bool HasComposite(InputAction action, string path, string name = null)
        {
            foreach (var binding in action.bindings)
            {
                if (!binding.isComposite || binding.path != path)
                    continue;
                if (name == null || binding.name == name)
                    return true;
            }

            return false;
        }

        private static void EnsureBinding(InputAction action, string path, string processors = null)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.path == path && binding.processors == (processors ?? string.Empty))
                    return;
            }

            action.AddBinding(path, processors: processors);
        }
    }
}
