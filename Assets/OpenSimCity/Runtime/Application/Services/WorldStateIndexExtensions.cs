using System;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Application.Services
{
    internal static class WorldStateIndexExtensions
    {
        public static int GetIndex(this WorldState world, CellCoord cell)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.IsInBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return checked(cell.Y * world.Width + cell.X);
        }
    }
}
