using System;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Buildings
{
    public static class BuildingFootprintIteration
    {
        public static void ForEachCell(this BuildingFootprint footprint, CellCoord anchor, Action<CellCoord> visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            for (var y = 0; y < footprint.Height; y++)
            for (var x = 0; x < footprint.Width; x++)
                visit(new CellCoord(anchor.X + x, anchor.Y + y));
        }
    }
}
