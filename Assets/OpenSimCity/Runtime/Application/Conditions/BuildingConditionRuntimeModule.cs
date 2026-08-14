using System;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Conditions;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Conditions
{
    public sealed class BuildingConditionRuntimeModule
    {
        private readonly CityRuntime _runtime;

        internal BuildingConditionRuntimeModule(CityRuntime runtime, BuildingConditionParameters parameters = null, int redevelopmentCost = 500)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            SpatialFields = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            var profiles = new DefinitionProfileResolver(runtime);
            var environment = new EnvironmentResolver(runtime, SpatialFields);
            System = new BuildingConditionSystem(runtime.Buildings, profiles, environment, parameters);
            Commands = new BuildingConditionCommandHandler(System, runtime.Finance, redevelopmentCost);
            ReadModel = new BuildingConditionReadModel(this);
            Overlay = new BuildingConditionOverlayReadModel(this);
            runtime.PopulationSystem.CapacityResolver = System;
            runtime.MonthlyScheduler.RegisterPreCoreSystem(System);
        }

        public CityRuntime Runtime => _runtime;
        public SpatialFieldRuntimeModule SpatialFields { get; }
        public BuildingConditionSystem System { get; }
        public BuildingConditionCommandHandler Commands { get; }
        public BuildingConditionReadModel ReadModel { get; }
        public BuildingConditionOverlayReadModel Overlay { get; }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command)) return CommandExecutionResult.Unsupported("The condition module cannot execute this command.");
            var sequenceResult = _runtime.Execute(command);
            if (sequenceResult.Status != CommandExecutionStatus.Unsupported) return sequenceResult;
            var result = Commands.Execute(new CommandEnvelope(sequenceResult.Stamp, command)).WithStamp(sequenceResult.Stamp);
            RefreshReadModels();
            return result;
        }

        public bool RefreshReadModels()
        {
            SpatialFields.RefreshIfNeeded();
            var changed = ReadModel.RefreshIfNeeded();
            changed |= Overlay.RefreshIfNeeded();
            return changed;
        }

        public BuildingConditionRuntimeSnapshotDto Capture() => System.ToDto();

        public void Restore(BuildingConditionRuntimeSnapshotDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var rows = snapshot.States ?? Array.Empty<BuildingConditionStateDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index] ?? throw new ArgumentException("Condition rows cannot contain null.", nameof(snapshot));
                if (!_runtime.Buildings.TryGet(new StableId(row.BuildingId), out _))
                    throw new InvalidOperationException($"Condition snapshot references unknown building {row.BuildingId}.");
            }
            System.RestoreFromDto(snapshot);
            _runtime.PopulationSystem.CapacityResolver = System;
            RefreshReadModels();
        }

        private sealed class DefinitionProfileResolver : IBuildingConditionProfileResolver
        {
            private readonly CityRuntime _runtime;
            public DefinitionProfileResolver(CityRuntime runtime) { _runtime = runtime; }
            public bool TryGetConditionProfile(StableId definitionId, out BuildingConditionProfile profile)
            {
                if (!_runtime.BuildingDefinitions.TryGet(definitionId, out var definition))
                {
                    profile = default;
                    return false;
                }
                if (definition.ZoneType == ZoneType.ResidentialLow)
                {
                    profile = new BuildingConditionProfile(BuildingConditionUse.Residential, definition.OccupancyCapacity, 0);
                    return true;
                }
                profile = new BuildingConditionProfile(BuildingConditionUse.Employment, 0, definition.JobCapacity);
                return true;
            }
        }

        private sealed class EnvironmentResolver : IBuildingConditionEnvironmentResolver
        {
            private readonly CityRuntime _runtime;
            private readonly SpatialFieldRuntimeModule _spatial;
            public EnvironmentResolver(CityRuntime runtime, SpatialFieldRuntimeModule spatial) { _runtime = runtime; _spatial = spatial; }

            public BuildingConditionEnvironment Resolve(BuildingState building)
            {
                _spatial.RefreshIfNeeded();
                _runtime.RoadGraph.RefreshIfNeeded();
                _runtime.Parcels.RefreshIfNeeded();
                _runtime.Power.RefreshIfNeeded();
                var definition = _runtime.BuildingDefinitions.Get(building.DefinitionId);
                var footprint = definition.Footprint;
                long land = 0;
                long pollution = 0;
                long noise = 0;
                long crime = 0;
                var count = 0;
                for (var y = 0; y < footprint.Height; y++)
                for (var x = 0; x < footprint.Width; x++)
                {
                    var value = _spatial.Fields.Get(new CellCoord(building.Anchor.X + x, building.Anchor.Y + y));
                    land += value.LandValue;
                    pollution += value.Pollution;
                    noise += value.Noise;
                    crime += value.Crime;
                    count++;
                }
                if (count <= 0) throw new InvalidOperationException("A building condition footprint cannot be empty.");
                var landValue = (int)(land / count);
                var pollutionValue = (int)(pollution / count);
                var noiseValue = (int)(noise / count);
                var crimeValue = (int)(crime / count);
                if (definition.ZoneType == ZoneType.IndustrialLow)
                {
                    landValue = Math.Max(400, landValue);
                    pollutionValue /= 3;
                    noiseValue /= 2;
                }
                var hasRoad = _runtime.Parcels.TryGet(building.ParcelId, out var parcel) && parcel.HasRoadFrontage && parcel.HasExternalRoadAccess;
                var hasPower = _runtime.Power.GetBuildingSupply(building.Id).IsPowered;
                return new BuildingConditionEnvironment(landValue, pollutionValue, noiseValue, crimeValue, hasRoad, hasPower);
            }
        }
    }

    public static class BuildingConditionRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, BuildingConditionRuntimeModule> Modules = new ConditionalWeakTable<CityRuntime, BuildingConditionRuntimeModule>();
        public static BuildingConditionRuntimeModule GetOrCreate(CityRuntime runtime, BuildingConditionParameters parameters = null, int redevelopmentCost = 500)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new BuildingConditionRuntimeModule(runtime, parameters, redevelopmentCost);
            Modules.Add(runtime, created);
            return created;
        }
        public static bool TryGet(CityRuntime runtime, out BuildingConditionRuntimeModule module)
        {
            if (runtime == null) { module = null; return false; }
            return Modules.TryGetValue(runtime, out module);
        }
    }
}
