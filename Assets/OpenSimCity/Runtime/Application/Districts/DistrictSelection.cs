using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictSelection
    {
        private static readonly DistrictSelection Instance = new DistrictSelection();
        public static DistrictSelection Current => Instance;
        public StableId ActiveDistrictId { get; private set; }
        public CellCoord? SelectedCell { get; private set; }
        public bool OverlayVisible { get; private set; } = true;
        public int Version { get; private set; }
        public event Action Changed;
        public void SelectDistrict(StableId id) { if (ActiveDistrictId == id) return; ActiveDistrictId = id; Touch(); }
        public void SetSelectedCell(CellCoord? cell) { if (SelectedCell == cell) return; SelectedCell = cell; Touch(); }
        public void SetOverlayVisible(bool value) { if (OverlayVisible == value) return; OverlayVisible = value; Touch(); }
        private void Touch() { Version = checked(Version + 1); Changed?.Invoke(); }
    }
}
