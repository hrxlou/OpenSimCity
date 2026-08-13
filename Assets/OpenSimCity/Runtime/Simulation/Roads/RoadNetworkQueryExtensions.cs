using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Roads
{
    public static class RoadNetworkQueryExtensions
    {
        public static RoadCellState Get(this RoadNetworkState network, CellCoord coordinate)
        {
            return network.GetCell(coordinate);
        }
    }
}
