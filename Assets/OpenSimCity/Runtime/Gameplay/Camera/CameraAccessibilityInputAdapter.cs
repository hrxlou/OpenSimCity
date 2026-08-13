using System;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Camera
{
    [DisallowMultipleComponent]
    public sealed class CameraAccessibilityInputAdapter : MonoBehaviour
    {
        private IsometricCameraRig _rig;
        private PresentationSettingsController _settings;
        private InputActionMap _map;
        private bool _disabledForUiFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<IsometricCameraRig>();
            if (rig != null && rig.GetComponent<CameraAccessibilityInputAdapter>() == null)
                rig.gameObject.AddComponent<CameraAccessibilityInputAdapter>();
        }

        private void OnEnable()
        {
            _rig = GetComponent<IsometricCameraRig>();
            BindSettings(PresentationSettingsContext.Current);
            Apply();
        }

        private void OnDisable()
        {
            BindSettings(null);
            _map = null;
        }

        private void Update()
        {
            if (!ReferenceEquals(_settings, PresentationSettingsContext.Current))
            {
                BindSettings(PresentationSettingsContext.Current);
                Apply();
            }
            EnsureMap();
            if (_map == null) return;
            var shouldDisable = UiInputBoundary.Current.IsKeyboardFocusCaptured;
            if (shouldDisable == _disabledForUiFocus) return;
            _disabledForUiFocus = shouldDisable;
            if (shouldDisable) _map.Disable(); else _map.Enable();
        }

        private void BindSettings(PresentationSettingsController settings)
        {
            if (_settings != null) _settings.Changed -= HandleSettingsChanged;
            _settings = settings;
            if (_settings != null) _settings.Changed += HandleSettingsChanged;
        }

        private void HandleSettingsChanged(PresentationSettingsDto _) => Apply();

        private void EnsureMap()
        {
            if (_map != null || _rig == null || _rig.ConfiguredInputActions == null) return;
            _map = _rig.ConfiguredInputActions.FindActionMap(IsometricCameraInputActions.MapName, false);
        }

        private void Apply()
        {
            EnsureMap();
            if (_map == null) return;
            var move = _map.FindAction(IsometricCameraInputActions.Move, false);
            ApplyCompositePart(move, "Up", Path(PresentationInputAction.MoveUp));
            ApplyCompositePart(move, "Down", Path(PresentationInputAction.MoveDown));
            ApplyCompositePart(move, "Left", Path(PresentationInputAction.MoveLeft));
            ApplyCompositePart(move, "Right", Path(PresentationInputAction.MoveRight));
            ApplyFirstBinding(_map.FindAction(IsometricCameraInputActions.RotateLeft, false), Path(PresentationInputAction.RotateLeft));
            ApplyFirstBinding(_map.FindAction(IsometricCameraInputActions.RotateRight, false), Path(PresentationInputAction.RotateRight));
        }

        private string Path(PresentationInputAction action) =>
            _settings == null ? PresentationInputBindingDefaults.GetDefaultPath(action) : _settings.GetControlPath(action);

        private static void ApplyCompositePart(InputAction action, string partName, string path)
        {
            if (action == null) return;
            for (var index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (binding.isPartOfComposite && string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase))
                {
                    action.ApplyBindingOverride(index, path);
                    return;
                }
            }
        }

        private static void ApplyFirstBinding(InputAction action, string path)
        {
            if (action != null && action.bindings.Count > 0) action.ApplyBindingOverride(0, path);
        }
    }
}
