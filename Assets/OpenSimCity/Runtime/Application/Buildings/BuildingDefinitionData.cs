using System;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Buildings
{
    [Serializable]
    public sealed class BuildingDefinitionData : IEquatable<BuildingDefinitionData>
    {
        public BuildingDefinitionData(
            StableId id,
            ZoneType zoneType,
            BuildingFootprint footprint,
            int occupancyCapacity,
            int jobCapacity,
            string presentationKey,
            int powerDemand = 1)
        {
            if (!id.IsValid) throw new ArgumentException("A building definition must have a valid stable ID.", nameof(id));
            if (!zoneType.IsPaintable()) throw new ArgumentOutOfRangeException(nameof(zoneType), "A building definition must use a low-density R/C/I zone.");
            if (occupancyCapacity < 0) throw new ArgumentOutOfRangeException(nameof(occupancyCapacity));
            if (jobCapacity < 0) throw new ArgumentOutOfRangeException(nameof(jobCapacity));
            if (footprint.Width > ParcelNetworkState.MaximumParcelWidth || footprint.Height > ParcelNetworkState.MaximumParcelHeight)
                throw new ArgumentOutOfRangeException(nameof(footprint), "A building footprint must fit the current parcel contract.");
            var hasValidCapacity = zoneType == ZoneType.ResidentialLow
                ? occupancyCapacity > 0 && jobCapacity == 0
                : occupancyCapacity == 0 && jobCapacity > 0;
            if (!hasValidCapacity) throw new ArgumentException("Residential definitions require occupancy capacity and commercial/industrial definitions require job capacity.");
            if (string.IsNullOrWhiteSpace(presentationKey)) throw new ArgumentException("A building definition must have a presentation key.", nameof(presentationKey));
            if (powerDemand <= 0) throw new ArgumentOutOfRangeException(nameof(powerDemand));

            Id = id;
            ZoneType = zoneType;
            Footprint = footprint;
            OccupancyCapacity = occupancyCapacity;
            JobCapacity = jobCapacity;
            PresentationKey = presentationKey;
            PowerDemand = powerDemand;
            BuildingFootprintLookup.Register(Id, Footprint);
        }

        public StableId Id { get; }
        public ZoneType ZoneType { get; }
        public BuildingFootprint Footprint { get; }
        public int OccupancyCapacity { get; }
        public int JobCapacity { get; }
        public string PresentationKey { get; }
        public int PowerDemand { get; }

        public bool Equals(BuildingDefinitionData other)
        {
            return other != null && Id == other.Id && ZoneType == other.ZoneType && Footprint == other.Footprint &&
                   OccupancyCapacity == other.OccupancyCapacity && JobCapacity == other.JobCapacity && PowerDemand == other.PowerDemand &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as BuildingDefinitionData);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
