using OpenSimCity.Application.Input;
using UnityEngine;

namespace OpenSimCity.Gameplay.Selection
{
    /// <summary>
    /// Drives <see cref="GridSelectionTool.SetUiPointerBlocked"/> from the shared
    /// <see cref="UiInputBoundaryState"/>. This is the Gameplay half of the UI
    /// input boundary: it reads neutral Application state, so Gameplay keeps no
    /// dependency on UI Toolkit or the UI assembly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiInputBoundaryRelay : MonoBehaviour
    {
        [SerializeField] private GridSelectionTool selectionTool;

        private UiInputBoundaryState _state;

        public GridSelectionTool ConfiguredSelectionTool => selectionTool;

        public void ConfigureReferences(GridSelectionTool configuredSelectionTool)
        {
            selectionTool = configuredSelectionTool;
        }

        private void OnEnable()
        {
            _state = UiInputBoundary.Current;
            _state.BlocksWorldInputChanged += OnBlocksWorldInputChanged;
            Apply(_state.BlocksWorldInput);
        }

        private void OnDisable()
        {
            if (_state != null)
                _state.BlocksWorldInputChanged -= OnBlocksWorldInputChanged;
            _state = null;

            // Leave the tool unblocked; a disabled relay must not strand selection.
            Apply(false);
        }

        private void OnBlocksWorldInputChanged(bool blocksWorldInput)
        {
            Apply(blocksWorldInput);
        }

        private void Apply(bool blocksWorldInput)
        {
            if (selectionTool != null)
                selectionTool.SetUiPointerBlocked(blocksWorldInput);
        }
    }
}
