using System;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Buildings
{
    /// <summary>
    /// Unity-independent projection of a static building definition. The
    /// application catalog owns this data boundary; Simulation stores only its
    /// stable ID and never owns a ScriptableObject.
    /// </summary>
    [Serializable]
    public sealed class BuildingDefinitionData : IEquatable<BuildingDefinitionData>
    {
        public BuildingDefinitionData(
            StableId id,
            ZoneType zoneType,
            BuildingFootprint footprint,
            int occupancyCapacity,
            int jobCapacity,
            string presentationKey)
        {
            if (!id.IsValid)
                throw new ArgumentException("A building definition must have a valid stable ID.", nameof(id));
            if (!zoneType.IsPaintable())
                throw new ArgumentOutOfRangeException(nameof(zoneType), "A building definition must use a low-density R/C/I zone.");
            if (occupancyCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(occupancyCapacity), "Occupancy capacity cannot be negative.");
            if (jobCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(jobCapacity), "Job capacity cannot be negative.");
            if (footprint.Width > ParcelNetworkState.MaximumParcelWidth ||
                footprint.Height > ParcelNetworkState.MaximumParcelHeight)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(footprint),
                    "A building footprint must fit the current 1x1 to 2x2 parcel contract.");
            }
            var hasValidCapacity = zoneType == ZoneType.ResidentialLow
                ? occupancyCapacity > 0 && jobCapacity == 0
                : occupancyCapacity == 0 && jobCapacity > 0;
            if (!hasValidCapacity)
            {
                throw new ArgumentException(
                    "Residential definitions require occupancy capacity and commercial/industrial " +
                    "definitions require job capacity; mixed-use capacity is not supported.");
            }
            if (string.IsNullOrWhiteSpace(presentationKey))
                throw new ArgumentException("A building definition must have a presentation key.", nameof(presentationKey));

            Id = id;
            ZoneType = zoneType;
            Footprint = footprint;
            OccupancyCapacity = occupancyCapacity;
            JobCapacity = jobCapacity;
            PresentationKey = presentationKey;
        }

        public StableId Id { get; }

        public ZoneType ZoneType { get; }

        public BuildingFootprint Footprint { get; }

        public int OccupancyCapacity { get; }

        public int JobCapacity { get; }

        public string PresentationKey { get; }

        public bool Equals(BuildingDefinitionData other)
        {
            return other != null && Id == other.Id && ZoneType == other.ZoneType &&
                   Footprint == other.Footprint && OccupancyCapacity == other.OccupancyCapacity &&
                   JobCapacity == other.JobCapacity &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as BuildingDefinitionData);

        public override int GetHashCode() => Id.GetHashCode();
    }
}
