using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Spatial
{
    /// <summary>
    /// Capability module for persistent custom sources and rebuildable road/building
    /// sources. Derived source IDs never enter the capability snapshot.
    /// </summary>
    public sealed class SpatialFieldRuntimeModule
    {
        private readonly CityRuntime _runtime;
        private readonly HashSet<StableId> _roadSourceIds = new HashSet<StableId>();
        private readonly HashSet<StableId> _buildingSourceIds = new HashSet<StableId>();
        private int _observedRoadVersion = -1;
        private int _observedBuildingVersion = -1;

        internal SpatialFieldRuntimeModule(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Fields = new SpatialFieldSystem(runtime.World);
            Suitability = new SpatialSuitabilityQuery(Fields);
            Commands = new SpatialFieldCommandHandler(Fields);
            ReadModel = new SpatialFieldReadModel(this);
            Overlay = new SpatialFieldOverlayReadModel(ReadModel);
            RefreshIfNeeded();
        }

        public CityRuntime Runtime => _runtime;
        public SpatialFieldSystem Fields { get; }
        public SpatialSuitabilityQuery Suitability { get; }
        public SpatialFieldCommandHandler Commands { get; }
        public SpatialFieldReadModel ReadModel { get; }
        public SpatialFieldOverlayReadModel Overlay { get; }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command))
                return CommandExecutionResult.Unsupported("The spatial-field module cannot execute this command.");

            var sequenceResult = _runtime.Execute(command);
            if (sequenceResult.Status != CommandExecutionStatus.Unsupported)
                return sequenceResult;
            var result = Commands.Execute(new CommandEnvelope(sequenceResult.Stamp, command))
                .WithStamp(sequenceResult.Stamp);
            RefreshIfNeeded();
            return result;
        }

        public bool RefreshIfNeeded()
        {
            var changed = false;
            if (_observedRoadVersion != _runtime.Roads.Version)
            {
                changed |= SynchronizeRoadSources();
                _observedRoadVersion = _runtime.Roads.Version;
            }
            if (_observedBuildingVersion != _runtime.Buildings.Version)
            {
                changed |= SynchronizeBuildingSources();
                _observedBuildingVersion = _runtime.Buildings.Version;
            }
            if (Fields.HasDirtyCells)
                changed |= Fields.RefreshDirty();
            changed |= ReadModel.RefreshIfNeeded();
            return changed;
        }

        public SpatialFieldRuntimeSnapshotDto Capture()
        {
            var rows = new List<SpatialFieldSourceStateDto>();
            var sources = Fields.Sources;
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (SpatialDerivedSourceIds.IsDerived(source.Id))
                    continue;
                rows.Add(new SpatialFieldSourceStateDto
                {
                    Id = source.Id.Value,
                    DefinitionId = source.DefinitionId.Value,
                    CellX = source.Cell.X,
                    CellY = source.Cell.Y,
                    IsEnabled = source.IsEnabled
                });
            }
            return new SpatialFieldRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Sources = rows.ToArray()
            };
        }

        public void Restore(SpatialFieldRuntimeSnapshotDto snapshot)
        {
            Fields.RestoreFromDto(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
            _roadSourceIds.Clear();
            _buildingSourceIds.Clear();
            _observedRoadVersion = -1;
            _observedBuildingVersion = -1;
            RefreshIfNeeded();
        }

        private bool SynchronizeRoadSources()
        {
            var current = new HashSet<StableId>();
            var roads = _runtime.Roads.Cells;
            var changed = false;
            for (var index = 0; index < roads.Count; index++)
            {
                if (!roads[index].IsOccupied)
                    continue;
                var id = SpatialDerivedSourceIds.Road(index);
                current.Add(id);
                changed |= Fields.UpsertSource(new SpatialFieldSourceState(
                    id,
                    SpatialSourceDefinitionIds.TrafficNoise,
                    roads[index].Coordinate));
            }
            foreach (var previous in _roadSourceIds)
            {
                if (!current.Contains(previous))
                    changed |= Fields.RemoveSource(previous);
            }
            _roadSourceIds.Clear();
            foreach (var id in current) _roadSourceIds.Add(id);
            return changed;
        }

        private bool SynchronizeBuildingSources()
        {
            var current = new HashSet<StableId>();
            var buildings = _runtime.Buildings.Buildings;
            var changed = false;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                var definition = _runtime.BuildingDefinitions.Get(building.DefinitionId);
                if (definition.ZoneType != ZoneType.IndustrialLow)
                    continue;
                var id = SpatialDerivedSourceIds.Building(building.Id);
                current.Add(id);
                changed |= Fields.UpsertSource(new SpatialFieldSourceState(
                    id,
                    SpatialSourceDefinitionIds.IndustrialEmitter,
                    building.Anchor));
            }
            foreach (var previous in _buildingSourceIds)
            {
                if (!current.Contains(previous))
                    changed |= Fields.RemoveSource(previous);
            }
            _buildingSourceIds.Clear();
            foreach (var id in current) _buildingSourceIds.Add(id);
            return changed;
        }
    }

    public static class SpatialFieldRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, SpatialFieldRuntimeModule> Modules =
            new ConditionalWeakTable<CityRuntime, SpatialFieldRuntimeModule>();

        public static SpatialFieldRuntimeModule GetOrCreate(CityRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing))
                return existing;
            var created = new SpatialFieldRuntimeModule(runtime);
            Modules.Add(runtime, created);
            return created;
        }

        public static bool TryGet(CityRuntime runtime, out SpatialFieldRuntimeModule module)
        {
            if (runtime == null)
            {
                module = null;
                return false;
            }
            return Modules.TryGetValue(runtime, out module);
        }
    }
}
