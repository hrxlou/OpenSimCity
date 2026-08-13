using System;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Simulation.Spatial
{
    [Serializable]
    public readonly struct SpatialSuitabilityFactor
    {
        public SpatialSuitabilityFactor(
            ZoneType zoneType,
            int score,
            SpatialFieldCellValue fields)
        {
            ZoneType = zoneType;
            Score = score;
            Fields = fields;
        }
        public ZoneType ZoneType { get; }
        public int Score { get; }
        public SpatialFieldCellValue Fields { get; }
    }

    /// <summary>
    /// Named, integer-only use preference derived from public field values. It does
    /// not grant development eligibility and is independent of rendered overlays.
    /// </summary>
    public sealed class SpatialSuitabilityQuery
    {
        private readonly SpatialFieldSystem _fields;

        public SpatialSuitabilityQuery(SpatialFieldSystem fields)
        {
            _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public SpatialSuitabilityFactor Evaluate(ZoneType zoneType, CellCoord cell)
        {
            var value = _fields.Get(cell);
            long score;
            switch (zoneType)
            {
                case ZoneType.ResidentialLow:
                    score = value.LandValue + value.ParkAmenity / 2L -
                            value.Pollution - value.Noise / 2L - value.Crime;
                    break;
                case ZoneType.CommercialLow:
                    score = value.LandValue / 2L + value.ParkAmenity / 4L -
                            value.Pollution / 2L - value.Noise / 4L - value.Crime;
                    break;
                case ZoneType.IndustrialLow:
                    score = 500L - value.LandValue / 4L -
                            value.Pollution / 8L - value.Noise / 8L - value.Crime / 4L;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zoneType));
            }
            if (score < -1000L) score = -1000L;
            if (score > 1000L) score = 1000L;
            return new SpatialSuitabilityFactor(zoneType, (int)score, value);
        }
    }
}
