namespace OpenSimCity.Gameplay.Camera
{
    /// <summary>
    /// Frame-level camera gesture state. Selection tools can use this contract
    /// to reject a click that was consumed by a camera drag.
    /// </summary>
    public readonly struct CameraPointerInteractionState
    {
        public CameraPointerInteractionState(
            bool isPointerDown,
            bool isDragging,
            bool clickReleasedThisFrame,
            bool dragCompletedThisFrame,
            bool shouldSuppressClick)
        {
            IsPointerDown = isPointerDown;
            IsDragging = isDragging;
            ClickReleasedThisFrame = clickReleasedThisFrame;
            DragCompletedThisFrame = dragCompletedThisFrame;
            ShouldSuppressClick = shouldSuppressClick;
        }

        public bool IsPointerDown { get; }

        public bool IsDragging { get; }

        public bool ClickReleasedThisFrame { get; }

        public bool DragCompletedThisFrame { get; }

        public bool ShouldSuppressClick { get; }
    }
}
