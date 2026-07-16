using System;

namespace OpenSimCity.Application.Input
{
    /// <summary>
    /// Neutral pointer/focus arbitration state shared by the UI and Gameplay
    /// assemblies. Neither references the other, so UI writes capture state here
    /// and world tools read it to decide whether an input belongs to them.
    /// </summary>
    public sealed class UiInputBoundaryState
    {
        private int _pointerCaptureCount;
        private int _keyboardCaptureCount;

        /// <summary>Raised whenever <see cref="BlocksWorldInput"/> changes.</summary>
        public event Action<bool> BlocksWorldInputChanged;

        /// <summary>True while a pointer rests on, or is dragging within, UI.</summary>
        public bool IsPointerOverUi => _pointerCaptureCount > 0;

        /// <summary>True while a UI element holds keyboard focus.</summary>
        public bool IsKeyboardFocusCaptured => _keyboardCaptureCount > 0;

        /// <summary>World tools must ignore pointer input while this is true.</summary>
        public bool BlocksWorldInput => IsPointerOverUi || IsKeyboardFocusCaptured;

        /// <summary>
        /// Counted rather than boolean: overlapping panels each capture and
        /// release independently, and a single flag would let the first release
        /// re-open world input while another panel is still under the pointer.
        /// </summary>
        public void CapturePointer()
        {
            var wasBlocking = BlocksWorldInput;
            _pointerCaptureCount++;
            RaiseIfChanged(wasBlocking);
        }

        public void ReleasePointer()
        {
            if (_pointerCaptureCount == 0)
                return;

            var wasBlocking = BlocksWorldInput;
            _pointerCaptureCount--;
            RaiseIfChanged(wasBlocking);
        }

        public void CaptureKeyboardFocus()
        {
            var wasBlocking = BlocksWorldInput;
            _keyboardCaptureCount++;
            RaiseIfChanged(wasBlocking);
        }

        public void ReleaseKeyboardFocus()
        {
            if (_keyboardCaptureCount == 0)
                return;

            var wasBlocking = BlocksWorldInput;
            _keyboardCaptureCount--;
            RaiseIfChanged(wasBlocking);
        }

        /// <summary>
        /// Drops every capture at once. Escape cancel and focus-loss paths use
        /// this so a panel destroyed mid-drag cannot strand world input.
        /// </summary>
        public void ReleaseAll()
        {
            var wasBlocking = BlocksWorldInput;
            _pointerCaptureCount = 0;
            _keyboardCaptureCount = 0;
            RaiseIfChanged(wasBlocking);
        }

        private void RaiseIfChanged(bool wasBlocking)
        {
            var isBlocking = BlocksWorldInput;
            if (isBlocking != wasBlocking)
                BlocksWorldInputChanged?.Invoke(isBlocking);
        }
    }
}
