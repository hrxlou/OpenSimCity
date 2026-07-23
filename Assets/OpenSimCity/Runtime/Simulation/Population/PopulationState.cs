using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Population
{
    [Serializable]
    public readonly struct PopulationEmploymentProfile
    {
        public PopulationEmploymentProfile(StableId definitionId, int housingCapacity, int jobCapacity)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A profile requires a valid definition ID.", nameof(definitionId));
            if (housingCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(housingCapacity));
            if (jobCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(jobCapacity));

            DefinitionId = definitionId;
            HousingCapacity = housingCapacity;
            JobCapacity = jobCapacity;
        }

        public StableId DefinitionId { get; }
        public int HousingCapacity { get; }
        public int JobCapacity { get; }
    }

    public interface IPopulationEmploymentProfileResolver
    {
        bool TryGetPopulationEmploymentProfile(
            StableId definitionId,
            out PopulationEmploymentProfile profile);
    }

    [Serializable]
    public readonly struct BuildingPopulationState : IEquatable<BuildingPopulationState>
    {
        public BuildingPopulationState(StableId buildingId, int residents, int capacity)
        {
            if (!buildingId.IsValid)
                throw new ArgumentException("A population row requires a valid building ID.", nameof(buildingId));
            if (residents < 0 || residents > capacity)
                throw new ArgumentOutOfRangeException(nameof(residents));
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            BuildingId = buildingId;
            Residents = residents;
            Capacity = capacity;
        }

        public StableId BuildingId { get; }
        public int Residents { get; }
        public int Capacity { get; }

        public bool Equals(BuildingPopulationState other) =>
            BuildingId == other.BuildingId && Residents == other.Residents && Capacity == other.Capacity;

        public override bool Equals(object obj) => obj is BuildingPopulationState other && Equals(other);
        public override int GetHashCode() => BuildingId.GetHashCode();
    }

    [Serializable]
    public sealed class PopulationState
    {
        private BuildingPopulationState[] _buildings = Array.Empty<BuildingPopulationState>();
        private IReadOnlyList<BuildingPopulationState> _readOnlyBuildings =
            Array.AsReadOnly(Array.Empty<BuildingPopulationState>());

        public int TotalPopulation { get; private set; }
        public int LaborForce => TotalPopulation;
        public int HousingCapacity { get; private set; }
        public int VacantHousing => HousingCapacity - TotalPopulation;
        public IReadOnlyList<BuildingPopulationState> Buildings => _readOnlyBuildings;

        internal void Set(BuildingPopulationState[] buildings)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _readOnlyBuildings = Array.AsReadOnly(_buildings);
            TotalPopulation = 0;
            HousingCapacity = 0;
            for (var index = 0; index < _buildings.Length; index++)
            {
                TotalPopulation = checked(TotalPopulation + _buildings[index].Residents);
                HousingCapacity = checked(HousingCapacity + _buildings[index].Capacity);
            }
        }

        public PopulationStateDto ToDto()
        {
            var rows = new BuildingPopulationStateDto[_buildings.Length];
            for (var index = 0; index < _buildings.Length; index++)
            {
                rows[index] = new BuildingPopulationStateDto
                {
                    BuildingId = _buildings[index].BuildingId.Value,
                    Residents = _buildings[index].Residents,
                    Capacity = _buildings[index].Capacity
                };
            }

            return new PopulationStateDto
            {
                TotalPopulation = TotalPopulation,
                HousingCapacity = HousingCapacity,
                Buildings = rows
            };
        }

        public void RestoreFromDto(PopulationStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var source = dto.Buildings ?? Array.Empty<BuildingPopulationStateDto>();
            var rows = new BuildingPopulationState[source.Length];
            var previousId = StableId.Invalid;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null)
                    throw new ArgumentException("A population DTO row cannot be null.", nameof(dto));

                var buildingId = new StableId(source[index].BuildingId);
                if (index > 0 && buildingId <= previousId)
                {
                    throw new ArgumentException(
                        "Population DTO rows must use unique ascending building IDs.",
                        nameof(dto));
                }

                rows[index] = new BuildingPopulationState(
                    buildingId,
                    source[index].Residents,
                    source[index].Capacity);
                previousId = buildingId;
            }

            Set(rows);
            if (TotalPopulation != dto.TotalPopulation || HousingCapacity != dto.HousingCapacity)
                throw new ArgumentException("Population DTO aggregate values do not match its rows.", nameof(dto));
        }
    }

    [Serializable]
    public sealed class BuildingPopulationStateDto
    {
        public ulong BuildingId;
        public int Residents;
        public int Capacity;
    }

    [Serializable]
    public sealed class PopulationStateDto
    {
        public int TotalPopulation;
        public int HousingCapacity;
        public BuildingPopulationStateDto[] Buildings = Array.Empty<BuildingPopulationStateDto>();
    }
}
