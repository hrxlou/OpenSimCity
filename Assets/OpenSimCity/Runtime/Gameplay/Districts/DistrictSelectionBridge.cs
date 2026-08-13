using OpenSimCity.Application.Districts;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;

namespace OpenSimCity.Gameplay.Districts
{
    public sealed class DistrictSelectionBridge : MonoBehaviour
    {
        [SerializeField] private GridSelectionTool gridSelection;
        private void Update()
        {
            if (gridSelection == null) gridSelection = Object.FindFirstObjectByType<GridSelectionTool>();
            DistrictSelection.Current.SetSelectedCell(gridSelection != null ? gridSelection.SelectedCell : null);
        }
    }
}
