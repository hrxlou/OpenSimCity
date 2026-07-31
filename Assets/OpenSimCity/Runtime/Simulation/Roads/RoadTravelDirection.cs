using System;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Roads
{
    /// <summary>
    /// Directed exit rule owned by one road cell. Bidirectional preserves the P2
    /// behaviour. Cardinal values permit leaving the cell only in that direction;
    /// entering the cell from the opposite direction remains valid.
    /// </summary>
    public enum RoadTravelDirection : byte
    {
        Bidirectional = 0,
        North = 1,
        East = 2,
        South = 3,
        West = 4
    }

    public static class RoadTravelDirectionRules
    {
        public static bool IsDefined(RoadTravelDirection direction)
        {
            return direction >= RoadTravelDirection.Bidirectional &&
                   direction <= RoadTravelDirection.West;
        }

        public static bool AllowsTravel(
            RoadTravelDirection direction,
            CellCoord from,
            CellCoord to)
        {
            if (!IsDefined(direction) || from.ManhattanDistanceTo(to) != 1)
                return false;
            if (direction == RoadTravelDirection.Bidirectional)
                return true;

            var expected = FromStep(to.X - from.X, to.Y - from.Y);
            return expected == direction;
        }

        public static RoadTravelDirection FromPath(
            CellCoord start,
            CellCoord end,
            bool reverse)
        {
            var deltaX = Math.Sign(end.X - start.X);
            var deltaY = Math.Sign(end.Y - start.Y);
            if ((deltaX == 0) == (deltaY == 0))
            {
                throw new ArgumentException(
                    "A one-way road path must have distinct axis-aligned endpoints.");
            }

            if (reverse)
            {
                deltaX = -deltaX;
                deltaY = -deltaY;
            }
            return FromStep(deltaX, deltaY);
        }

        public static RoadTravelDirection Opposite(RoadTravelDirection direction)
        {
            switch (direction)
            {
                case RoadTravelDirection.Bidirectional:
                    return RoadTravelDirection.Bidirectional;
                case RoadTravelDirection.North:
                    return RoadTravelDirection.South;
                case RoadTravelDirection.East:
                    return RoadTravelDirection.West;
                case RoadTravelDirection.South:
                    return RoadTravelDirection.North;
                case RoadTravelDirection.West:
                    return RoadTravelDirection.East;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static RoadTravelDirection FromStep(int deltaX, int deltaY)
        {
            if (deltaX == 0 && deltaY == 1)
                return RoadTravelDirection.North;
            if (deltaX == 1 && deltaY == 0)
                return RoadTravelDirection.East;
            if (deltaX == 0 && deltaY == -1)
                return RoadTravelDirection.South;
            if (deltaX == -1 && deltaY == 0)
                return RoadTravelDirection.West;
            throw new ArgumentException("A road direction step must be one cardinal cell.");
        }
    }
}
