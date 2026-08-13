using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Buildings
{
    public static class BuildingStateCollectionSpatialExtensions
    {
        public static bool TryFindAt(this BuildingStateCollection buildings, CellCoord cell, out BuildingState building)
        {
            if (buildings != null)
            {
                var rows = buildings.Buildings;
                for (var index = 0; index < rows.Count; index++)
                {
                    var candidate = rows[index];
                    var footprint = candidate.Footprint;
                    if (cell.X < candidate.Anchor.X || cell.X >= candidate.Anchor.X + footprint.Width ||
                        cell.Y < candidate.Anchor.Y || cell.Y >= candidate.Anchor.Y + footprint.Height)
                        continue;
                    building = candidate;
                    return true;
                }
            }

            building = null;
            return false;
        }
    }
}
