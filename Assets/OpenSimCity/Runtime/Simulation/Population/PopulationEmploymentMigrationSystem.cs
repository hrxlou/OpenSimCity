using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Employment;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Migration;

namespace OpenSimCity.Simulation.Population
{
    /// <summary>
    /// Runs the aggregate monthly population loop without citizen or commuter agents.
    /// Every resident is currently part of the labor force; age and education cohorts
    /// are intentionally deferred. Building and job allocation always follows stable
    /// building-ID order supplied by BuildingStateCollection.
    /// </summary>
    public sealed class PopulationEmploymentMigrationSystem : IMonthlySimulationSystem
    {
        private readonly BuildingStateCollection _buildings;
        private readonly IPopulationEmploymentProfileResolver _profiles;
        private readonly IReadOnlyList<ExternalConnectionState> _externalConnections;
        private readonly Func<bool> _transportConnectionAvailability;
        private IBuildingCapacityResolver _capacityResolver =
            PassThroughBuildingCapacityResolver.Instance;

        public PopulationEmploymentMigrationSystem(
            BuildingStateCollection buildings,
            IPopulationEmploymentProfileResolver profiles,
            IReadOnlyList<ExternalConnectionState> externalConnections,
            Func<bool> transportConnectionAvailability = null)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _externalConnections = externalConnections ?? throw new ArgumentNullException(nameof(externalConnections));
            _transportConnectionAvailability = transportConnectionAvailability;
            Population = new PopulationState();
            Employment = new EmploymentState();
            Migration = new MigrationState();
        }

        public PopulationState Population { get; }
        public EmploymentState Employment { get; }
        public MigrationState Migration { get; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public bool HasProcessedMonth { get; private set; }
        public int Version { get; private set; }

        public IBuildingCapacityResolver CapacityResolver
        {
            get => _capacityResolver;
            set => _capacityResolver = value ?? throw new ArgumentNullException(nameof(value));
        }

        public PopulationEmploymentMigrationStateDto ToDto()
        {
            return new PopulationEmploymentMigrationStateDto
            {
                HasProcessedMonth = HasProcessedMonth,
                LastProcessedYear = HasProcessedMonth ? LastProcessedMonth.Year : 0,
                LastProcessedMonth = HasProcessedMonth ? LastProcessedMonth.Month : 0,
                Population = Population.ToDto(),
                Employment = Employment.ToDto(),
                Migration = Migration.ToDto()
            };
        }

        public void RestoreFromDto(PopulationEmploymentMigrationStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            Population.RestoreFromDto(dto.Population);
            Employment.RestoreFromDto(dto.Employment);
            Migration.RestoreFromDto(dto.Migration);
            HasProcessedMonth = dto.HasProcessedMonth;
            LastProcessedMonth = dto.HasProcessedMonth
                ? new SimulationDate(dto.LastProcessedYear, dto.LastProcessedMonth)
                : default(SimulationDate);
            Version = dto.HasProcessedMonth ? 1 : 0;
        }

        public void RunMonth(SimulationDate month)
        {
            if (HasProcessedMonth && month.CompareTo(LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Monthly population processing requires a strictly increasing month.");

            var source = _buildings.Buildings;
            var housingRows = new List<MutableHousingRow>();
            var jobRows = new List<MutableJobRow>();
            var forcedDepartures = 0;

            for (var index = 0; index < source.Count; index++)
            {
                var building = source[index];
                if (!_profiles.TryGetPopulationEmploymentProfile(building.DefinitionId, out var profile))
                    throw new InvalidOperationException($"Building {building.Id} has no population/employment profile.");

                if (profile.HousingCapacity > 0)
                {
                    var effectiveCapacity = ResolveHousingCapacity(building, profile.HousingCapacity);
                    var residents = Math.Min(building.Occupancy, effectiveCapacity);
                    forcedDepartures = checked(forcedDepartures + building.Occupancy - residents);
                    housingRows.Add(new MutableHousingRow(building, residents, effectiveCapacity));
                }

                if (profile.JobCapacity > 0)
                {
                    var effectiveCapacity = ResolveJobCapacity(building, profile.JobCapacity);
                    jobRows.Add(new MutableJobRow(building, effectiveCapacity));
                }
            }

            var populationBeforeEconomicMigration = SumResidents(housingRows);
            var jobCapacity = SumJobCapacity(jobRows);
            var initiallyEmployed = Math.Min(populationBeforeEconomicMigration, jobCapacity);
            var vacantHousing = SumHousingCapacity(housingRows) - populationBeforeEconomicMigration;
            var vacantJobs = jobCapacity - initiallyEmployed;
            var unemployment = populationBeforeEconomicMigration - initiallyEmployed;
            var inboundPressure = Math.Min(vacantHousing, vacantJobs);
            var outboundPressure = unemployment;
            var hasExternalConnection = HasEnabledTransportConnection();
            var arrivals = hasExternalConnection ? inboundPressure : 0;
            var economicDepartures = hasExternalConnection ? outboundPressure : 0;

            RemoveResidents(housingRows, economicDepartures);
            AddResidents(housingRows, arrivals);

            var finalPopulation = SumResidents(housingRows);
            MatchJobs(jobRows, finalPopulation);
            PersistBuildingCounts(source, housingRows, jobRows);

            var populationRows = new BuildingPopulationState[housingRows.Count];
            for (var index = 0; index < housingRows.Count; index++)
            {
                var row = housingRows[index];
                populationRows[index] = new BuildingPopulationState(row.Building.Id, row.Residents, row.Capacity);
            }

            var employmentRows = new BuildingEmploymentState[jobRows.Count];
            for (var index = 0; index < jobRows.Count; index++)
            {
                var row = jobRows[index];
                employmentRows[index] = new BuildingEmploymentState(row.Building.Id, row.FilledJobs, row.Capacity);
            }

            Population.Set(populationRows);
            Employment.Set(finalPopulation, employmentRows);
            Migration.Set(
                inboundPressure,
                outboundPressure,
                arrivals,
                checked(forcedDepartures + economicDepartures),
                hasExternalConnection);
            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        private int ResolveHousingCapacity(BuildingState building, int definitionCapacity)
        {
            var capacity = _capacityResolver.ResolveHousingCapacity(building, definitionCapacity);
            if (capacity < 0 || capacity > definitionCapacity)
            {
                throw new InvalidOperationException(
                    $"Building capacity resolver returned invalid housing capacity {capacity} for {building.Id}.");
            }
            return capacity;
        }

        private int ResolveJobCapacity(BuildingState building, int definitionCapacity)
        {
            var capacity = _capacityResolver.ResolveJobCapacity(building, definitionCapacity);
            if (capacity < 0 || capacity > definitionCapacity)
            {
                throw new InvalidOperationException(
                    $"Building capacity resolver returned invalid job capacity {capacity} for {building.Id}.");
            }
            return capacity;
        }

        private bool HasEnabledTransportConnection()
        {
            if (_transportConnectionAvailability != null)
                return _transportConnectionAvailability();

            for (var index = 0; index < _externalConnections.Count; index++)
            {
                var connection = _externalConnections[index];
                if (!connection.IsEnabled)
                    continue;
                if (connection.Kind == ExternalConnectionKind.Road ||
                    connection.Kind == ExternalConnectionKind.Rail ||
                    connection.Kind == ExternalConnectionKind.Sea ||
                    connection.Kind == ExternalConnectionKind.Air)
                    return true;
            }

            return false;
        }

        private void PersistBuildingCounts(
            IReadOnlyList<BuildingState> source,
            List<MutableHousingRow> housingRows,
            List<MutableJobRow> jobRows)
        {
            var housingIndex = 0;
            var jobIndex = 0;
            for (var index = 0; index < source.Count; index++)
            {
                var building = source[index];
                var occupancy = 0;
                var filledJobs = 0;
                if (housingIndex < housingRows.Count && housingRows[housingIndex].Building.Id == building.Id)
                    occupancy = housingRows[housingIndex++].Residents;
                if (jobIndex < jobRows.Count && jobRows[jobIndex].Building.Id == building.Id)
                    filledJobs = jobRows[jobIndex++].FilledJobs;

                if (building.Occupancy != occupancy || building.FilledJobs != filledJobs)
                    _buildings.Replace(building.WithCounts(occupancy, filledJobs));
            }
        }

        private static void MatchJobs(List<MutableJobRow> rows, int laborForce)
        {
            var remainingWorkers = laborForce;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                row.FilledJobs = Math.Min(row.Capacity, remainingWorkers);
                remainingWorkers -= row.FilledJobs;
            }
        }

        private static void RemoveResidents(List<MutableHousingRow> rows, int count)
        {
            var remaining = count;
            for (var index = rows.Count - 1; index >= 0 && remaining > 0; index--)
            {
                var row = rows[index];
                var removed = Math.Min(row.Residents, remaining);
                row.Residents -= removed;
                remaining -= removed;
            }
        }

        private static void AddResidents(List<MutableHousingRow> rows, int count)
        {
            var remaining = count;
            for (var index = 0; index < rows.Count && remaining > 0; index++)
            {
                var row = rows[index];
                var added = Math.Min(row.Capacity - row.Residents, remaining);
                row.Residents += added;
                remaining -= added;
            }
        }

        private static int SumResidents(List<MutableHousingRow> rows)
        {
            var total = 0;
            for (var index = 0; index < rows.Count; index++)
                total = checked(total + rows[index].Residents);
            return total;
        }

        private static int SumHousingCapacity(List<MutableHousingRow> rows)
        {
            var total = 0;
            for (var index = 0; index < rows.Count; index++)
                total = checked(total + rows[index].Capacity);
            return total;
        }

        private static int SumJobCapacity(List<MutableJobRow> rows)
        {
            var total = 0;
            for (var index = 0; index < rows.Count; index++)
                total = checked(total + rows[index].Capacity);
            return total;
        }

        private sealed class MutableHousingRow
        {
            public MutableHousingRow(BuildingState building, int residents, int capacity)
            {
                Building = building;
                Residents = residents;
                Capacity = capacity;
            }

            public BuildingState Building { get; }
            public int Residents { get; set; }
            public int Capacity { get; }
        }

        private sealed class MutableJobRow
        {
            public MutableJobRow(BuildingState building, int capacity)
            {
                Building = building;
                Capacity = capacity;
            }

            public BuildingState Building { get; }
            public int FilledJobs { get; set; }
            public int Capacity { get; }
        }
    }

    [Serializable]
    public sealed class PopulationEmploymentMigrationStateDto
    {
        public bool HasProcessedMonth;
        public int LastProcessedYear;
        public int LastProcessedMonth;
        public PopulationStateDto Population;
        public OpenSimCity.Simulation.Employment.EmploymentStateDto Employment;
        public OpenSimCity.Simulation.Migration.MigrationStateDto Migration;
    }
}
