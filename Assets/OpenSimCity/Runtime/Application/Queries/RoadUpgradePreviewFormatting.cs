using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Converts Simulation-owned enums in a road-upgrade comparison into stable
    /// Application strings so UI does not add a Simulation assembly reference.
    /// </summary>
    public static class RoadUpgradePreviewFormatting
    {
        public static string DirectionLabel(RoadUpgradePreviewSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;
            switch (snapshot.ResolvedDirection)
            {
                case RoadTravelDirection.North:
                    return "↑ 북쪽";
                case RoadTravelDirection.East:
                    return "→ 동쪽";
                case RoadTravelDirection.South:
                    return "↓ 남쪽";
                case RoadTravelDirection.West:
                    return "← 서쪽";
                default:
                    return "↔ 양방향";
            }
        }

        public static string DirectionModeLabel(RoadDirectionMode mode)
        {
            switch (mode)
            {
                case RoadDirectionMode.Forward:
                    return "정방향";
                case RoadDirectionMode.Reverse:
                    return "역방향";
                default:
                    return "양방향";
            }
        }

        public static string PrimaryFailureLabel(RoadUpgradePreviewSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;
            switch (snapshot.PrimaryNewFailureReason)
            {
                case TrafficRouteFailureReason.NoOriginRoad:
                    return "출발 도로 없음";
                case TrafficRouteFailureReason.NoDestinationRoad:
                    return "도착 도로 없음";
                case TrafficRouteFailureReason.DifferentRoadComponent:
                    return "도로망 분리";
                case TrafficRouteFailureReason.NoPath:
                    return "방향 때문에 경로 없음";
                default:
                    return string.Empty;
            }
        }
    }
}
