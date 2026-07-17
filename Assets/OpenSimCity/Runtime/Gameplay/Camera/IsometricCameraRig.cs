using System;
using OpenSimCity.Application.Queries;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;

namespace OpenSimCity.Gameplay.Camera
{
    /// <summary>
    /// Orthographic isometric camera controller for the prototype city.
    /// The rig owns only view state; world dimensions arrive through a read model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        private const float DefaultFallbackAspect = 16f / 9f;
        public const float DefaultZoomSensitivity = 4f;
        public const float DefaultMaximumZoomStep = 6f;

        [Header("References")]
        [SerializeField] private UnityCamera controlledCamera;
        [SerializeField] private WorldBoundsReadModelSource worldBoundsSource;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Isometric view")]
        [SerializeField, Range(1f, 89f)] private float pitchDegrees = IsometricCameraMath.DefaultPitchDegrees;
        [SerializeField] private float yawDegrees = IsometricCameraMath.DefaultYawDegrees;
        [SerializeField, Min(1f)] private float cameraDistance = 100f;
        [SerializeField, Min(0.1f)] private float minOrthographicSize = 6f;
        [SerializeField, Min(0.1f)] private float maxOrthographicSize = 32f;
        [SerializeField, Min(0.1f)] private float initialOrthographicSize = 18f;
        [SerializeField] private int initialQuarterTurn;

        [Header("Input")]
        [SerializeField, Min(0f)] private float movementSpeed = 20f;
        [SerializeField, Min(0f)] private float zoomSensitivity = DefaultZoomSensitivity;
        [SerializeField, Min(0.1f)] private float maximumZoomStep = DefaultMaximumZoomStep;
        [SerializeField, Min(0f)] private float dragSensitivity = 1f;
        [SerializeField, Min(0f)] private float dragThresholdPixels = 6f;
        [SerializeField] private bool enableInput = true;

        private WorldBoundsReadModel _worldBounds;
        private bool _hasWorldBounds;
        private Vector2 _target;
        private int _quarterTurn;
        private float _orthographicSize;
        private bool _initialized;

        private InputActionAsset _runtimeInputActions;
        private InputActionMap _cameraActionMap;
        private InputAction _moveAction;
        private InputAction _zoomAction;
        private InputAction _rotateLeftAction;
        private InputAction _rotateRightAction;
        private InputAction _pointerPositionAction;
        private InputAction _pointerPressAction;
        private bool _ownsRuntimeInputActions;

        private bool _pointerDown;
        private bool _dragging;
        private bool _clickReleasedThisFrame;
        private bool _dragCompletedThisFrame;
        private bool _suppressClickThisFrame;
        private bool _hasLastPointerWorld;
        private Vector2 _pointerStart;
        private Vector3 _lastPointerWorld;

        public UnityCamera ControlledCamera => controlledCamera;

        public WorldBoundsReadModel WorldBounds
        {
            get
            {
                EnsureInitialized();
                return _worldBounds;
            }
        }

        /// <summary>World X/Z target represented as (x, z).</summary>
        public Vector2 Target
        {
            get
            {
                EnsureInitialized();
                return _target;
            }
        }

        public int QuarterTurn
        {
            get
            {
                EnsureInitialized();
                return _quarterTurn;
            }
        }

        public float OrthographicSize
        {
            get
            {
                EnsureInitialized();
                return _orthographicSize;
            }
        }

        public bool IsCameraDragActive => _dragging;

        public bool SuppressPointerClick => _dragging || _suppressClickThisFrame;

        public CameraPointerInteractionState PointerInteraction => new CameraPointerInteractionState(
            _pointerDown,
            _dragging,
            _clickReleasedThisFrame,
            _dragCompletedThisFrame,
            SuppressPointerClick);

        public InputActionAsset ConfiguredInputActions => inputActions;

        private void Reset()
        {
            controlledCamera = GetComponentInChildren<UnityCamera>(includeInactive: true);
            worldBoundsSource = GetComponent<WorldBoundsReadModelSource>();
        }

        private void Awake()
        {
            InitializeRig();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            InitializeInputActions();
        }

        private void OnDisable()
        {
            if (_cameraActionMap != null)
                _cameraActionMap.Disable();

            ReleaseRuntimeInputActions();
            ResetPointerGesture();
        }

        private void Update()
        {
            EnsureInitialized();
            ResetFrameGestureState();

            if (!enableInput)
                return;

            if (_cameraActionMap == null)
                InitializeInputActions();

            var deltaTime = Time.unscaledDeltaTime;
            var moveInput = _moveAction == null ? Vector2.zero : _moveAction.ReadValue<Vector2>();
            MoveBy(moveInput, deltaTime);

            if (_zoomAction != null)
            {
                var zoomInput = _zoomAction.ReadValue<float>();
                if (Mathf.Abs(zoomInput) > 0.0001f)
                    ApplyZoomDelta(zoomInput);
            }

            if (_rotateLeftAction != null && _rotateLeftAction.WasPressedThisFrame())
                RotateCounterClockwise();
            if (_rotateRightAction != null && _rotateRightAction.WasPressedThisFrame())
                RotateClockwise();

            ProcessPointerGesture();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ResetPointerGesture();
        }

        private void OnValidate()
        {
            pitchDegrees = Mathf.Clamp(pitchDegrees, 1f, 89f);
            cameraDistance = Mathf.Max(1f, cameraDistance);
            minOrthographicSize = Mathf.Max(0.1f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            initialOrthographicSize = Mathf.Clamp(initialOrthographicSize, minOrthographicSize, maxOrthographicSize);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            zoomSensitivity = Mathf.Max(0f, zoomSensitivity);
            maximumZoomStep = Mathf.Max(0.1f, maximumZoomStep);
            dragSensitivity = Mathf.Max(0f, dragSensitivity);
            dragThresholdPixels = Mathf.Max(0f, dragThresholdPixels);
        }

        /// <summary>
        /// Editor setup injects references here so the Scene does not need hand-authored YAML.
        /// </summary>
        public void ConfigureReferences(
            UnityCamera camera,
            WorldBoundsReadModelSource boundsSource,
            InputActionAsset cameraInputActions)
        {
            controlledCamera = camera;
            worldBoundsSource = boundsSource;
            inputActions = cameraInputActions;
            _initialized = false;
            _hasWorldBounds = false;
        }

        public void SetWorldBounds(WorldBoundsReadModel readModel)
        {
            EnsureInitialized();
            _worldBounds = readModel;
            _hasWorldBounds = true;
            ApplyPose();
        }

        public void RefreshWorldBounds()
        {
            EnsureInitialized();
            if (worldBoundsSource == null)
                return;

            _worldBounds = worldBoundsSource.ReadWorldBounds();
            _hasWorldBounds = true;
            ApplyPose();
        }

        public void SetTarget(Vector2 target)
        {
            EnsureInitialized();
            _target = target;
            ApplyPose();
        }

        public void MoveBy(Vector2 input, float deltaTime)
        {
            EnsureInitialized();
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");
            if (movementSpeed <= 0f || deltaTime == 0f)
                return;

            var pose = IsometricCameraMath.CalculatePose(_quarterTurn, pitchDegrees, yawDegrees);
            var movement = IsometricCameraMath.CalculatePlanarMovement(input, pose);
            _target += new Vector2(movement.x, movement.z) * (movementSpeed * deltaTime);
            ApplyPose();
        }

        public void SetOrthographicSize(float size)
        {
            EnsureInitialized();
            _orthographicSize = Mathf.Clamp(size, minOrthographicSize, maxOrthographicSize);
            ApplyPose();
        }

        public void ApplyZoomDelta(float input)
        {
            EnsureInitialized();
            if (float.IsNaN(input) || float.IsInfinity(input))
                throw new ArgumentOutOfRangeException(nameof(input), "Zoom input must be finite.");

            // Positive wheel input means zoom in. macOS smooth-scroll devices
            // report much smaller deltas than a Windows wheel notch, so amplify
            // small inputs while capping a single update for cross-device safety.
            var zoomDelta = Mathf.Clamp(
                input * zoomSensitivity,
                -maximumZoomStep,
                maximumZoomStep);
            SetOrthographicSize(_orthographicSize - zoomDelta);
        }

        public void SetQuarterTurn(int quarterTurn)
        {
            EnsureInitialized();
            _quarterTurn = IsometricCameraMath.NormalizeQuarterTurn(quarterTurn);
            ApplyPose();
        }

        public void RotateClockwise()
        {
            SetQuarterTurn(_quarterTurn + 1);
        }

        public void RotateCounterClockwise()
        {
            SetQuarterTurn(_quarterTurn - 1);
        }

        public bool TryProjectPointerToGround(Vector2 screenPosition, out Vector3 worldPosition)
        {
            EnsureInitialized();
            worldPosition = default;
            if (controlledCamera == null)
                return false;

            var ray = controlledCamera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out var distance) || distance < 0f)
                return false;

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private void InitializeRig()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponentInChildren<UnityCamera>(includeInactive: true);
            if (worldBoundsSource == null)
                worldBoundsSource = GetComponent<WorldBoundsReadModelSource>();

            if (!_hasWorldBounds)
            {
                _worldBounds = worldBoundsSource != null
                    ? worldBoundsSource.ReadWorldBounds()
                    : new WorldBoundsReadModel(64, 64, 1f);
                _hasWorldBounds = true;
            }

            _target = new Vector2(_worldBounds.CenterX, _worldBounds.CenterZ);
            _quarterTurn = IsometricCameraMath.NormalizeQuarterTurn(initialQuarterTurn);
            _orthographicSize = Mathf.Clamp(initialOrthographicSize, minOrthographicSize, maxOrthographicSize);
            _initialized = true;
            ApplyPose();
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                InitializeRig();
        }

        private void InitializeInputActions()
        {
            if (_cameraActionMap != null)
                return;

            var actions = inputActions;
            if (actions == null)
            {
                actions = IsometricCameraInputActions.CreateDefaultAsset();
                _runtimeInputActions = actions;
                _ownsRuntimeInputActions = true;
            }
            else if (!IsometricCameraInputActions.HasRequiredActions(actions))
            {
                IsometricCameraInputActions.ConfigureAsset(actions);
            }

            _cameraActionMap = actions.FindActionMap(IsometricCameraInputActions.MapName, throwIfNotFound: false);
            if (_cameraActionMap == null)
                return;

            _moveAction = _cameraActionMap.FindAction(IsometricCameraInputActions.Move, throwIfNotFound: false);
            _zoomAction = _cameraActionMap.FindAction(IsometricCameraInputActions.Zoom, throwIfNotFound: false);
            _rotateLeftAction = _cameraActionMap.FindAction(IsometricCameraInputActions.RotateLeft, throwIfNotFound: false);
            _rotateRightAction = _cameraActionMap.FindAction(IsometricCameraInputActions.RotateRight, throwIfNotFound: false);
            _pointerPositionAction = _cameraActionMap.FindAction(IsometricCameraInputActions.PointerPosition, throwIfNotFound: false);
            _pointerPressAction = _cameraActionMap.FindAction(IsometricCameraInputActions.PointerPress, throwIfNotFound: false);
            _cameraActionMap.Enable();
        }

        private void ReleaseRuntimeInputActions()
        {
            _cameraActionMap = null;
            _moveAction = null;
            _zoomAction = null;
            _rotateLeftAction = null;
            _rotateRightAction = null;
            _pointerPositionAction = null;
            _pointerPressAction = null;

            if (!_ownsRuntimeInputActions || _runtimeInputActions == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(_runtimeInputActions);
            else
                DestroyImmediate(_runtimeInputActions);

            _runtimeInputActions = null;
            _ownsRuntimeInputActions = false;
        }

        private void ApplyPose()
        {
            if (!_hasWorldBounds)
                return;

            var aspect = GetViewportAspect();
            _target = IsometricCameraMath.ClampTarget(
                _target,
                _worldBounds,
                _orthographicSize,
                aspect,
                _quarterTurn,
                pitchDegrees,
                yawDegrees);

            if (controlledCamera == null)
                return;

            var pose = IsometricCameraMath.CalculatePose(_quarterTurn, pitchDegrees, yawDegrees);
            var targetWorld = new Vector3(_target.x, 0f, _target.y);
            transform.position = targetWorld;
            controlledCamera.transform.SetPositionAndRotation(
                targetWorld - pose.Forward * cameraDistance,
                pose.Rotation);
            controlledCamera.orthographic = true;
            controlledCamera.orthographicSize = _orthographicSize;
            controlledCamera.nearClipPlane = Mathf.Max(0.05f, controlledCamera.nearClipPlane);
            controlledCamera.farClipPlane = Mathf.Max(controlledCamera.farClipPlane, cameraDistance + 100f);
        }

        private float GetViewportAspect()
        {
            if (controlledCamera != null && controlledCamera.aspect > 0f &&
                !float.IsNaN(controlledCamera.aspect) && !float.IsInfinity(controlledCamera.aspect))
            {
                return controlledCamera.aspect;
            }

            if (Screen.width > 0 && Screen.height > 0)
                return (float)Screen.width / Screen.height;

            return DefaultFallbackAspect;
        }

        private void ProcessPointerGesture()
        {
            if (_pointerPressAction == null || _pointerPositionAction == null)
                return;

            var position = _pointerPositionAction.ReadValue<Vector2>();
            var isPressed = _pointerPressAction.IsPressed();

            if (isPressed)
            {
                if (!_pointerDown)
                {
                    _pointerDown = true;
                    _dragging = false;
                    _hasLastPointerWorld = TryProjectPointerToGround(position, out _lastPointerWorld);
                    _pointerStart = position;
                }
                else
                {
                    ProcessPointerHeld(position);
                }

                return;
            }

            if (_pointerDown)
            {
                if (_dragging)
                {
                    _dragCompletedThisFrame = true;
                    _suppressClickThisFrame = true;
                }
                else
                {
                    _clickReleasedThisFrame = true;
                }

                ResetPointerGesture();
            }
        }

        private void ProcessPointerHeld(Vector2 position)
        {
            var offsetFromStart = position - _pointerStart;
            if (!_dragging && offsetFromStart.sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
            {
                _dragging = true;
                _suppressClickThisFrame = true;
                _hasLastPointerWorld = TryProjectPointerToGround(position, out _lastPointerWorld);
            }

            if (!_dragging)
                return;

            if (!TryProjectPointerToGround(position, out var currentPointerWorld))
                return;

            if (_hasLastPointerWorld)
            {
                var worldDelta = currentPointerWorld - _lastPointerWorld;
                _target += new Vector2(-worldDelta.x, -worldDelta.z) * dragSensitivity;
                ApplyPose();

                // Re-project after applying the new pose so the next frame only
                // accounts for the user's new pointer movement.
                _hasLastPointerWorld = TryProjectPointerToGround(position, out _lastPointerWorld);
            }
            else
            {
                _hasLastPointerWorld = true;
                _lastPointerWorld = currentPointerWorld;
            }
        }

        private void ResetFrameGestureState()
        {
            _clickReleasedThisFrame = false;
            _dragCompletedThisFrame = false;
            _suppressClickThisFrame = false;
        }

        private void ResetPointerGesture()
        {
            _pointerDown = false;
            _dragging = false;
            _hasLastPointerWorld = false;
        }
    }
}
