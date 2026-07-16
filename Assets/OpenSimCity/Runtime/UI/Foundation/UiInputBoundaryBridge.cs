using System.Collections.Generic;
using OpenSimCity.Application.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Foundation
{
    /// <summary>
    /// Translates UI Toolkit pointer and focus events into
    /// <see cref="UiInputBoundaryState"/> captures, so world tools can reject
    /// input that belongs to UI without knowing UI Toolkit exists.
    /// <para>
    /// Pointer capture is registered per panel rather than on the panel root: the
    /// root is pick-ignored so world clicks pass between panels, and a
    /// pick-ignored element is not a reliable pointer-enter target. Focus and key
    /// events bubble, so those stay on the root.
    /// </para>
    /// </summary>
    public sealed class UiInputBoundaryBridge
    {
        private readonly UiInputBoundaryState _state;
        private readonly VisualElement _root;
        private readonly List<VisualElement> _pointerSources = new List<VisualElement>();
        private readonly HashSet<VisualElement> _capturedSources = new HashSet<VisualElement>();
        private bool _focusCaptured;

        public UiInputBoundaryBridge(VisualElement root, UiInputBoundaryState state)
        {
            _root = root;
            _state = state;
        }

        public bool IsAttached { get; private set; }

        /// <summary>
        /// Registers a pickable panel whose pointer presence blocks world input.
        /// Call before <see cref="Attach"/>.
        /// </summary>
        public void AddPointerSource(VisualElement source)
        {
            if (source != null && !_pointerSources.Contains(source))
                _pointerSources.Add(source);
        }

        public void Attach()
        {
            if (IsAttached || _root == null || _state == null)
                return;

            _root.RegisterCallback<FocusInEvent>(OnFocusIn);
            _root.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            foreach (var source in _pointerSources)
            {
                source.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
                source.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            IsAttached = true;
        }

        public void Detach()
        {
            if (!IsAttached)
                return;

            _root.UnregisterCallback<FocusInEvent>(OnFocusIn);
            _root.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);

            foreach (var source in _pointerSources)
            {
                source.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                source.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            // Release only what this bridge took; another panel may still hold a
            // legitimate capture.
            foreach (var source in new List<VisualElement>(_capturedSources))
                ReleasePointer(source);

            ReleaseFocus();
            IsAttached = false;
        }

        /// <summary>
        /// Escape releases UI focus and hands keyboard input back to the world.
        /// Panels needing Escape for their own cancel must consume the event
        /// before it reaches the root.
        /// </summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;

            if (evt.target is VisualElement focused)
                focused.Blur();

            ReleaseFocus();
            evt.StopPropagation();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            CapturePointer(evt.currentTarget as VisualElement);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            ReleasePointer(evt.currentTarget as VisualElement);
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (_focusCaptured)
                return;

            _state.CaptureKeyboardFocus();
            _focusCaptured = true;
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            // FocusOut also fires while focus moves between two children of this
            // root. Only release when focus actually left the subtree, otherwise a
            // Tab press would briefly hand keyboard input back to the world.
            if (evt.relatedTarget is VisualElement next && IsInSubtree(next))
                return;

            ReleaseFocus();
        }

        private bool IsInSubtree(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current == _root)
                    return true;
            }

            return false;
        }

        private void CapturePointer(VisualElement source)
        {
            if (source == null || !_capturedSources.Add(source))
                return;

            _state.CapturePointer();
        }

        private void ReleasePointer(VisualElement source)
        {
            if (source == null || !_capturedSources.Remove(source))
                return;

            _state.ReleasePointer();
        }

        private void ReleaseFocus()
        {
            if (!_focusCaptured)
                return;

            _state.ReleaseKeyboardFocus();
            _focusCaptured = false;
        }
    }
}
