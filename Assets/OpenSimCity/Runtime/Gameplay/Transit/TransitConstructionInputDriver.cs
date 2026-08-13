using OpenSimCity.Application.Input;
using OpenSimCity.Application.Transit;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Transit
{
    /// <summary>
    /// Pointer adapter for stop placement and ordered line editing. Public methods
    /// are the device-independent PlayMode seam; UI pointer blocking matches the
    /// existing construction input contract.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class TransitConstructionInputDriver : MonoBehaviour
    {
        [SerializeField] private TransitRuntimeHost transitHost;
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private bool enableInput = true;

        private bool _uiPointerBlocked;
        private bool _interactionEnabled = true;

        public TransitRuntimeHost TransitHost => transitHost;
        public bool InteractionEnabled => _interactionEnabled;

        public void Configure(TransitRuntimeHost host, IsometricCameraRig rig)
        {
            transitHost = host;
            cameraRig = rig;
        }

        public void SetUiPointerBlocked(bool value) => _uiPointerBlocked = value;
        public void SetInteractionEnabled(bool value) => _interactionEnabled = value;

        public void SelectStopTool() => transitHost?.Tool?.SelectStopPlacement();
        public void SelectLineTool() => transitHost?.Tool?.SelectLineEditor();
        public void Cancel() => transitHost?.Tool?.Cancel();
        public bool RemoveLastLineStop() => transitHost?.Tool != null && transitHost.Tool.RemoveLastLineStop();

        public bool TryHoverAtScreen(Vector2 screenPosition)
        {
            if (!CanInteract() || !TryScreenToCell(screenPosition, out var cell))
                return false;
            transitHost.Tool.HoverCell(cell);
            return true;
        }

        public bool TryClickAtScreen(Vector2 screenPosition)
        {
            if (!TryHoverAtScreen(screenPosition))
                return false;
            var tool = transitHost.Tool;
            if (tool.Mode == TransitConstructionToolMode.StopPlacement)
                return tool.Commit().IsApplied;
            if (tool.Mode == TransitConstructionToolMode.LineEditor)
                return tool.AddPreviewedLineStop();
            return false;
        }

        public bool CommitLine()
        {
            return transitHost?.Tool != null && transitHost.Tool.Commit().IsApplied;
        }

        private bool CanInteract()
        {
            return enableInput &&
                   _interactionEnabled &&
                   !_uiPointerBlocked &&
                   !UiInputBoundary.Current.IsPointerOverUi &&
                   transitHost != null &&
                   transitHost.Tool != null &&
                   transitHost.Tool.Mode != TransitConstructionToolMode.None &&
                   cameraRig != null &&
                   !cameraRig.SuppressPointerClick;
        }

        private bool TryScreenToCell(Vector2 screenPosition, out CellCoord cell)
        {
            cell = default(CellCoord);
            if (!cameraRig.TryProjectPointerToGround(screenPosition, out var world))
                return false;
            return GridSelectionMath.TryWorldToCell(world, cameraRig.WorldBounds, out cell);
        }

        private void Update()
        {
            if (!enableInput || !_interactionEnabled || transitHost?.Tool == null)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.bKey.wasPressedThisFrame)
                    SelectStopTool();
                else if (keyboard.nKey.wasPressedThisFrame)
                    SelectLineTool();
                if (keyboard.enterKey.wasPressedThisFrame && transitHost.Tool.Mode == TransitConstructionToolMode.LineEditor)
                    CommitLine();
                if (keyboard.backspaceKey.wasPressedThisFrame)
                    RemoveLastLineStop();
                if (keyboard.escapeKey.wasPressedThisFrame)
                    Cancel();
            }

            var mouse = Mouse.current;
            if (mouse == null || !CanInteract())
                return;
            var position = mouse.position.ReadValue();
            TryHoverAtScreen(position);
            if (mouse.leftButton.wasPressedThisFrame)
                TryClickAtScreen(position);
            if (mouse.rightButton.wasPressedThisFrame)
                Cancel();
        }
    }
}
