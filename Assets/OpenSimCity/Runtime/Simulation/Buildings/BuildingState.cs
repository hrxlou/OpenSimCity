using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Buildings
{
    [Serializable]
    public sealed class BuildingState : IEquatable<BuildingState>
    {
        private BuildingFootprint _fixtureFootprint;
        private bool _hasFixtureFootprint;

        public BuildingState(StableId id, StableId definitionId, StableId parcelId, CellCoord anchor, int occupancy = 0, int filledJobs = 0)
        {
            if (!id.IsValid) throw new ArgumentException("A building must have a valid stable ID.", nameof(id));
            if (!definitionId.IsValid) throw new ArgumentException("A building must reference a valid definition ID.", nameof(definitionId));
            if (!parcelId.IsValid) throw new ArgumentException("A building must reference a valid parcel ID.", nameof(parcelId));
            if (occupancy < 0) throw new ArgumentOutOfRangeException(nameof(occupancy));
            if (filledJobs < 0) throw new ArgumentOutOfRangeException(nameof(filledJobs));
            Id = id;
            DefinitionId = definitionId;
            ParcelId = parcelId;
            Anchor = anchor;
            Occupancy = occupancy;
            FilledJobs = filledJobs;
        }

        public BuildingState(StableId id, StableId definitionId, StableId parcelId, CellCoord anchor, BuildingFootprint footprint, int occupancy = 0, int filledJobs = 0)
            : this(id, definitionId, parcelId, anchor, occupancy, filledJobs)
        {
            if (footprint.Width <= 0 || footprint.Height <= 0) throw new ArgumentOutOfRangeException(nameof(footprint));
            _fixtureFootprint = footprint;
            _hasFixtureFootprint = true;
        }

        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public StableId ParcelId { get; }
        public CellCoord Anchor { get; }
        public int Occupancy { get; }
        public int FilledJobs { get; }
        public BuildingFootprint Footprint => _hasFixtureFootprint ? _fixtureFootprint : BuildingFootprintLookup.Resolve(DefinitionId);

        public BuildingState WithCounts(int occupancy, int filledJobs)
        {
            return _hasFixtureFootprint
                ? new BuildingState(Id, DefinitionId, ParcelId, Anchor, _fixtureFootprint, occupancy, filledJobs)
                : new BuildingState(Id, DefinitionId, ParcelId, Anchor, occupancy, filledJobs);
        }

        public BuildingStateDto ToDto()
        {
            return new BuildingStateDto
            {
                Id = Id.Value,
                DefinitionId = DefinitionId.Value,
                ParcelId = ParcelId.Value,
                AnchorX = Anchor.X,
                AnchorY = Anchor.Y,
                Occupancy = Occupancy,
                FilledJobs = FilledJobs
            };
        }

        public static BuildingState FromDto(BuildingStateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            return new BuildingState(new StableId(dto.Id), new StableId(dto.DefinitionId), new StableId(dto.ParcelId), new CellCoord(dto.AnchorX, dto.AnchorY), dto.Occupancy, dto.FilledJobs);
        }

        public bool Equals(BuildingState other)
        {
            return other != null && Id == other.Id && DefinitionId == other.DefinitionId && ParcelId == other.ParcelId && Anchor == other.Anchor && Occupancy == other.Occupancy && FilledJobs == other.FilledJobs;
        }

        public override bool Equals(object obj) => Equals(obj as BuildingState);
        public override int GetHashCode() => Id.GetHashCode();
    }

    [Serializable]
    public sealed class BuildingStateDto
    {
        public ulong Id;
        public ulong DefinitionId;
        public ulong ParcelId;
        public int AnchorX;
        public int AnchorY;
        public int Occupancy;
        public int FilledJobs;
    }

    [Serializable]
    public sealed class BuildingStateCollectionDto
    {
        public BuildingStateDto[] Buildings = Array.Empty<BuildingStateDto>();
    }
}
