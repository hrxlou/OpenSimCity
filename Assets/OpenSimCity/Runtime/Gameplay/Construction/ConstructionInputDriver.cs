using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Input;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Construction
{
    [DisallowMultipleComponent]
    public sealed class ConstructionInputDriver : MonoBehaviour
    {
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private bool enableInput = true;

        private bool _uiPointerBlocked;
        private bool _pointerDown;
        private bool _interactionEnabled = true;

        public IsometricCameraRig CameraRig => cameraRig;
        public CityRuntimeContext Context => CityRuntimeContext.Current;
        public bool InteractionEnabled => _interactionEnabled;

        public void ConfigureReferences(IsometricCameraRig rig) => cameraRig = rig;

        public void SetUiPointerBlocked(bool isBlocked)
        {
            _uiPointerBlocked = isBlocked;
            if (isBlocked && _pointerDown) CancelDrag();
        }

        public void SetInteractionEnabled(bool isEnabled)
        {
            _interactionEnabled = isEnabled;
            if (!isEnabled && _pointerDown) CancelDrag();
        }

        public void SelectTool(ConstructionToolMode mode) => Context?.SelectTool(mode);
        public void ToggleOverlay(PlacementOverlayKind kind) => Context?.ToggleOverlay(kind);

        public bool TryBeginAtScreen(Vector2 screenPosition)
        {
            if (!CanPlace() || !TryScreenToCell(screenPosition, out var cell)) return false;
            _pointerDown = true;
            Context.Tool.BeginDrag(cell);
            return true;
        }

        public bool TryUpdateAtScreen(Vector2 screenPosition)
        {
            if (!_pointerDown || Context == null || !TryScreenToCell(screenPosition, out var cell)) return false;
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
            return enableInput && _interactionEnabled && !_uiPointerBlocked &&
                   !UiInputBoundary.Current.IsPointerOverUi && Context != null &&
                   Context.Tool.Mode != ConstructionToolMode.None &&
                   (cameraRig == null || !cameraRig.SuppressPointerClick);
        }

        private bool TryScreenToCell(Vector2 screenPosition, out CellCoord cell)
        {
            cell = default;
            if (cameraRig == null || !cameraRig.TryProjectPointerToGround(screenPosition, out var world)) return false;
            return GridSelectionMath.TryWorldToCell(world, cameraRig.WorldBounds, out cell);
        }

        private void Update()
        {
            if (!enableInput || !_interactionEnabled || Context == null) return;
            HandleKeyboardToolSelection();
            HandlePointer();
        }

        private void HandleKeyboardToolSelection()
        {
            if (UiInputBoundary.Current.IsKeyboardFocusCaptured) return;

            if (PresentationKeyboardInput.Pressed(PresentationInputAction.Cancel)) CancelDrag();

            if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolRoad))
                SelectTool(ConstructionToolMode.Road);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolResidential))
                SelectTool(ConstructionToolMode.ZoneResidential);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolCommercial))
                SelectTool(ConstructionToolMode.ZoneCommercial);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolIndustrial))
                SelectTool(ConstructionToolMode.ZoneIndustrial);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolZoneClear))
                SelectTool(ConstructionToolMode.ZoneClear);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolDemolish))
                SelectTool(ConstructionToolMode.Demolish);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolPowerPlant))
                SelectTool(ConstructionToolMode.PowerPlant);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolRemovePowerPlant))
                SelectTool(ConstructionToolMode.RemovePowerPlant);
            else if (PresentationKeyboardInput.Pressed(PresentationInputAction.ToolRoadUpgrade))
                SelectTool(ConstructionToolMode.RoadUpgrade);

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.RoadConnectivity);
            else if (keyboard.f2Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.Accessibility);
            else if (keyboard.f3Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.Zone);
            else if (keyboard.f4Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.Parcel);
            else if (keyboard.f5Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.Power);
            else if (keyboard.f6Key.wasPressedThisFrame) ToggleOverlay(PlacementOverlayKind.RoadHierarchy);
        }

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame && _pointerDown) CancelDrag();
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) TryBeginAtScreen(position);
            else if (_pointerDown && mouse.leftButton.isPressed) TryUpdateAtScreen(position);
            else if (mouse.leftButton.wasReleasedThisFrame && _pointerDown) Commit();
        }
    }
}
