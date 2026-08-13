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
                    if (rows[index].Anchor == cell)
                    {
                        building = rows[index];
                        return true;
                    }
                }
            }

            building = null;
            return false;
        }
    }
}
