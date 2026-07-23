using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Employment
{
    [Serializable]
    public readonly struct BuildingEmploymentState : IEquatable<BuildingEmploymentState>
    {
        public BuildingEmploymentState(StableId buildingId, int filledJobs, int capacity)
        {
            if (!buildingId.IsValid)
                throw new ArgumentException("An employment row requires a valid building ID.", nameof(buildingId));
            if (filledJobs < 0 || filledJobs > capacity)
                throw new ArgumentOutOfRangeException(nameof(filledJobs));
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            BuildingId = buildingId;
            FilledJobs = filledJobs;
            Capacity = capacity;
        }

        public StableId BuildingId { get; }
        public int FilledJobs { get; }
        public int Capacity { get; }
        public int VacantJobs => Capacity - FilledJobs;

        public bool Equals(BuildingEmploymentState other) =>
            BuildingId == other.BuildingId && FilledJobs == other.FilledJobs && Capacity == other.Capacity;

        public override bool Equals(object obj) => obj is BuildingEmploymentState other && Equals(other);
        public override int GetHashCode() => BuildingId.GetHashCode();
    }

    [Serializable]
    public sealed class EmploymentState
    {
        private BuildingEmploymentState[] _buildings = Array.Empty<BuildingEmploymentState>();
        private IReadOnlyList<BuildingEmploymentState> _readOnlyBuildings =
            Array.AsReadOnly(Array.Empty<BuildingEmploymentState>());

        public int LaborForce { get; private set; }
        public int EmployedWorkers { get; private set; }
        public int UnemployedWorkers => LaborForce - EmployedWorkers;
        public int AvailableJobs { get; private set; }
        public int VacantJobs => AvailableJobs - EmployedWorkers;
        public IReadOnlyList<BuildingEmploymentState> Buildings => _readOnlyBuildings;

        internal void Set(int laborForce, BuildingEmploymentState[] buildings)
        {
            if (laborForce < 0)
                throw new ArgumentOutOfRangeException(nameof(laborForce));

            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _readOnlyBuildings = Array.AsReadOnly(_buildings);
            LaborForce = laborForce;
            EmployedWorkers = 0;
            AvailableJobs = 0;
            for (var index = 0; index < _buildings.Length; index++)
            {
                EmployedWorkers = checked(EmployedWorkers + _buildings[index].FilledJobs);
                AvailableJobs = checked(AvailableJobs + _buildings[index].Capacity);
            }

            if (EmployedWorkers > LaborForce)
                throw new InvalidOperationException("Filled jobs cannot exceed the labor force.");
        }

        public EmploymentStateDto ToDto()
        {
            var rows = new BuildingEmploymentStateDto[_buildings.Length];
            for (var index = 0; index < _buildings.Length; index++)
            {
                rows[index] = new BuildingEmploymentStateDto
                {
                    BuildingId = _buildings[index].BuildingId.Value,
                    FilledJobs = _buildings[index].FilledJobs,
                    Capacity = _buildings[index].Capacity
                };
            }

            return new EmploymentStateDto
            {
                LaborForce = LaborForce,
                EmployedWorkers = EmployedWorkers,
                AvailableJobs = AvailableJobs,
                Buildings = rows
            };
        }

        public void RestoreFromDto(EmploymentStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var source = dto.Buildings ?? Array.Empty<BuildingEmploymentStateDto>();
            var rows = new BuildingEmploymentState[source.Length];
            var previousId = StableId.Invalid;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null)
                    throw new ArgumentException("An employment DTO row cannot be null.", nameof(dto));

                var buildingId = new StableId(source[index].BuildingId);
                if (index > 0 && buildingId <= previousId)
                {
                    throw new ArgumentException(
                        "Employment DTO rows must use unique ascending building IDs.",
                        nameof(dto));
                }

                rows[index] = new BuildingEmploymentState(
                    buildingId,
                    source[index].FilledJobs,
                    source[index].Capacity);
                previousId = buildingId;
            }

            Set(dto.LaborForce, rows);
            if (EmployedWorkers != dto.EmployedWorkers || AvailableJobs != dto.AvailableJobs)
                throw new ArgumentException("Employment DTO aggregate values do not match its rows.", nameof(dto));
        }
    }

    [Serializable]
    public sealed class BuildingEmploymentStateDto
    {
        public ulong BuildingId;
        public int FilledJobs;
        public int Capacity;
    }

    [Serializable]
    public sealed class EmploymentStateDto
    {
        public int LaborForce;
        public int EmployedWorkers;
        public int AvailableJobs;
        public BuildingEmploymentStateDto[] Buildings = Array.Empty<BuildingEmploymentStateDto>();
    }
}
