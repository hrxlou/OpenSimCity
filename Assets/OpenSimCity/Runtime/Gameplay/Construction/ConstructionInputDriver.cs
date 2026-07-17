using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Construction
{
    /// <summary>
    /// Translates pointer and keyboard input into construction tool actions:
    /// left drag places along a straight road path or zone area, right-click and
    /// Escape cancel, digit keys switch tools, and F-keys toggle overlays.
    /// <para>
    /// The screen-to-cell projection and drag lifecycle are exposed as public
    /// methods so both the Windows and macOS input paths — and the PlayMode smoke
    /// — drive the same deterministic tool without depending on a device.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConstructionInputDriver : MonoBehaviour
    {
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private bool enableInput = true;

        private bool _uiPointerBlocked;
        private bool _pointerDown;

        public IsometricCameraRig CameraRig => cameraRig;

        public CityRuntimeContext Context => CityRuntimeContext.Current;

        public void ConfigureReferences(IsometricCameraRig rig)
        {
            cameraRig = rig;
        }

        /// <summary>Relay from the UI pointer boundary; blocks placement over panels.</summary>
        public void SetUiPointerBlocked(bool isBlocked)
        {
            _uiPointerBlocked = isBlocked;
            if (isBlocked && _pointerDown)
                CancelDrag();
        }

        public void SelectTool(ConstructionToolMode mode)
        {
            Context?.SelectTool(mode);
        }

        public void ToggleOverlay(PlacementOverlayKind kind)
        {
            Context?.ToggleOverlay(kind);
        }

        public bool TryBeginAtScreen(Vector2 screenPosition)
        {
            if (!CanPlace() || !TryScreenToCell(screenPosition, out var cell))
                return false;

            _pointerDown = true;
            Context.Tool.BeginDrag(cell);
            return true;
        }

        public bool TryUpdateAtScreen(Vector2 screenPosition)
        {
            if (!_pointerDown || Context == null || !TryScreenToCell(screenPosition, out var cell))
                return false;

            Context.Tool.UpdateDrag(cell);
            return true;
        }

        public ConstructionCommitResult Commit()
        {
            _pointerDown = false;
            return Context != null ? Context.Tool.CommitDrag() : ConstructionCommitResult.None;
        }

        public void CancelDrag()
        {
            _pointerDown = false;
            Context?.Tool.CancelDrag();
        }

        private bool CanPlace()
        {
            return enableInput &&
                   !_uiPointerBlocked &&
                   !UiInputBoundary.Current.IsPointerOverUi &&
                   Context != null &&
                   Context.Tool.Mode != ConstructionToolMode.None &&
                   (cameraRig == null || !cameraRig.SuppressPointerClick);
        }

        private bool TryScreenToCell(Vector2 screenPosition, out CellCoord cell)
        {
            cell = default;
            if (cameraRig == null)
                return false;
            if (!cameraRig.TryProjectPointerToGround(screenPosition, out var world))
                return false;

            return GridSelectionMath.TryWorldToCell(world, cameraRig.WorldBounds, out cell);
        }

        private void Update()
        {
            if (!enableInput || Context == null)
                return;

            HandleKeyboardToolSelection();
            HandlePointer();
        }

        private void HandleKeyboardToolSelection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                CancelDrag();

            if (keyboard.digit1Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.Road);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.ZoneResidential);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.ZoneCommercial);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.ZoneIndustrial);
            else if (keyboard.digit5Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.ZoneClear);
            else if (keyboard.digit6Key.wasPressedThisFrame)
                SelectTool(ConstructionToolMode.Demolish);
        }

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.rightButton.wasPressedThisFrame && _pointerDown)
                CancelDrag();

            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                TryBeginAtScreen(position);
            else if (_pointerDown && mouse.leftButton.isPressed)
                TryUpdateAtScreen(position);
            else if (mouse.leftButton.wasReleasedThisFrame && _pointerDown)
                Commit();
        }
    }
}
