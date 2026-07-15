using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSimCity.Gameplay.Selection
{
    public enum GridSelectionToolState
    {
        Disabled,
        Ready,
        PointerOverUi,
        CameraInteraction
    }

    /// <summary>
    /// Converts a left-click into a read-only cell selection. It deliberately
    /// exposes the selected coordinate instead of touching WorldState directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridSelectionTool : MonoBehaviour
    {
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private GridSelectionHighlightView highlightView;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private bool enableInput = true;

        private InputActionMap _selectionActionMap;
        private InputAction _pointerPositionAction;
        private InputAction _selectAction;
        private InputActionAsset _runtimeInputActions;
        private bool _ownsRuntimeInputActions;
        private bool _uiPointerBlocked;

        public bool HasSelection { get; private set; }

        public CellCoord SelectedCell { get; private set; }

        public GridSelectionToolState State { get; private set; } = GridSelectionToolState.Disabled;

        public IsometricCameraRig CameraRig => cameraRig;

        public GridSelectionHighlightView HighlightView => highlightView;

        public InputActionAsset ConfiguredInputActions => inputActions;

        /// <summary>
        /// UI pointer handlers call this relay while the pointer is over UI. The
        /// Gameplay assembly does not need a dependency on a specific UI framework.
        /// </summary>
        public void SetUiPointerBlocked(bool isBlocked)
        {
            _uiPointerBlocked = isBlocked;
        }

        public void ConfigureReferences(
            IsometricCameraRig configuredCameraRig,
            GridSelectionHighlightView configuredHighlightView,
            InputActionAsset configuredInputActions)
        {
            cameraRig = configuredCameraRig;
            highlightView = configuredHighlightView;
            inputActions = configuredInputActions;
        }

        public bool TrySelectScreenPosition(Vector2 screenPosition)
        {
            if (!enableInput || cameraRig == null)
            {
                State = GridSelectionToolState.Disabled;
                return false;
            }

            if (_uiPointerBlocked)
            {
                State = GridSelectionToolState.PointerOverUi;
                return false;
            }

            if (cameraRig.SuppressPointerClick)
            {
                State = GridSelectionToolState.CameraInteraction;
                return false;
            }

            State = GridSelectionToolState.Ready;
            if (!cameraRig.TryProjectPointerToGround(screenPosition, out var worldPosition) ||
                !GridSelectionMath.TryWorldToCell(worldPosition, cameraRig.WorldBounds, out var cell))
            {
                return false;
            }

            HasSelection = true;
            SelectedCell = cell;
            highlightView?.SetSelectedCell(cell, cameraRig.WorldBounds);
            return true;
        }

        public void ClearSelection()
        {
            HasSelection = false;
            SelectedCell = default;
            highlightView?.Clear();
        }

        private void OnEnable()
        {
            InitializeInputActions();
        }

        private void OnDisable()
        {
            if (_selectionActionMap != null)
                _selectionActionMap.Disable();
            ReleaseRuntimeInputActions();
            State = GridSelectionToolState.Disabled;
        }

        private void Update()
        {
            if (!enableInput)
            {
                State = GridSelectionToolState.Disabled;
                return;
            }

            if (_selectionActionMap == null)
                InitializeInputActions();

            if (_selectAction != null && _selectAction.WasReleasedThisFrame())
            {
                var pointerPosition = _pointerPositionAction == null
                    ? Vector2.zero
                    : _pointerPositionAction.ReadValue<Vector2>();
                TrySelectScreenPosition(pointerPosition);
            }
            else if (cameraRig != null && cameraRig.SuppressPointerClick)
            {
                State = GridSelectionToolState.CameraInteraction;
            }
            else if (enableInput)
            {
                State = GridSelectionToolState.Ready;
            }
        }

        private void InitializeInputActions()
        {
            if (_selectionActionMap != null)
                return;

            var actions = inputActions;
            if (actions == null)
            {
                actions = IsometricCameraInputActions.CreateDefaultAsset();
                _runtimeInputActions = actions;
                _ownsRuntimeInputActions = true;
            }
            else if (!GridSelectionInputActions.HasRequiredActions(actions))
            {
                GridSelectionInputActions.ConfigureAsset(actions);
            }

            _selectionActionMap = actions.FindActionMap(GridSelectionInputActions.MapName, throwIfNotFound: false);
            if (_selectionActionMap == null)
                return;

            _pointerPositionAction = _selectionActionMap.FindAction(
                GridSelectionInputActions.PointerPosition, throwIfNotFound: false);
            _selectAction = _selectionActionMap.FindAction(GridSelectionInputActions.Select, throwIfNotFound: false);
            _selectionActionMap.Enable();
        }

        private void ReleaseRuntimeInputActions()
        {
            _selectionActionMap = null;
            _pointerPositionAction = null;
            _selectAction = null;
            if (!_ownsRuntimeInputActions || _runtimeInputActions == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(_runtimeInputActions);
            else
                DestroyImmediate(_runtimeInputActions);

            _runtimeInputActions = null;
            _ownsRuntimeInputActions = false;
        }
    }
}
