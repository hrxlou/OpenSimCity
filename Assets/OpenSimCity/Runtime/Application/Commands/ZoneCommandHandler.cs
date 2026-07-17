using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Commands
{
    /// <summary>
    /// Application boundary for low-density zoning. It validates a normalized
    /// brush against world and road state, then applies one atomic zone batch.
    /// No view or gameplay object owns or edits the simulation arrays.
    /// </summary>
    public sealed class ZoneCommandHandler : ICityCommandHandler
    {
        private readonly ZoneNetworkState _zones;
        private readonly RoadNetworkState _roads;
        private readonly ZoneDefinitionCatalog _definitions;

        public ZoneCommandHandler(
            ZoneNetworkState zones,
            RoadNetworkState roads)
            : this(zones, roads, ZoneDefinitionCatalog.CreateDefault())
        {
        }

        public ZoneCommandHandler(
            RoadNetworkState roads,
            ZoneNetworkState zones)
            : this(zones, roads, ZoneDefinitionCatalog.CreateDefault())
        {
        }

        public ZoneCommandHandler(
            ZoneNetworkState zones,
            RoadNetworkState roads,
            IEnumerable<ZoneDefinition> definitions)
            : this(zones, roads, new ZoneDefinitionCatalog(definitions))
        {
        }

        public ZoneCommandHandler(
            ZoneNetworkState zones,
            RoadNetworkState roads,
            ZoneDefinitionCatalog definitions)
        {
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            if (_zones.Width != _roads.Width || _zones.Height != _roads.Height)
            {
                throw new ArgumentException(
                    "The zone and road networks must have matching dimensions.",
                    nameof(roads));
            }
        }

        public ZoneCommandHandler(RoadNetworkState roads)
            : this(
                CreateZones(roads),
                roads,
                ZoneDefinitionCatalog.CreateDefault())
        {
        }

        public ZoneCommandHandler(WorldState world)
            : this(
                new ZoneNetworkState(world),
                new RoadNetworkState(world),
                ZoneDefinitionCatalog.CreateDefault())
        {
        }

        public ZoneNetworkState Zones => _zones;

        public ZoneNetworkState ZoneNetwork => _zones;

        public RoadNetworkState RoadNetwork => _roads;

        public ZoneDefinitionCatalog Definitions => _definitions;

        public ZoneCommandResult LastResult { get; private set; }

        public bool CanHandle(ICityCommand command)
        {
            return command is ZoneAreaCommand || command is ClearZoneCommand;
        }

        public ZoneCommandResult Preview(ZoneAreaCommand command)
        {
            if (command == null)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    ZoneType.None,
                    StableId.Invalid,
                    ZoneCommandFailureReason.InvalidCommand,
                    null,
                    Array.Empty<CellCoord>());
            }

            return EvaluatePaint(command);
        }

        public ZoneCommandResult Preview(ClearZoneCommand command)
        {
            if (command == null)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Clear,
                    ZoneType.None,
                    StableId.Invalid,
                    ZoneCommandFailureReason.InvalidCommand,
                    null,
                    Array.Empty<CellCoord>());
            }

            return EvaluateClear(command);
        }

        public ZoneCommandResult Execute(ZoneAreaCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid
                ? CommitPaint(command)
                : preview;
            LastResult = result;
            return result;
        }

        public ZoneCommandResult Execute(ClearZoneCommand command)
        {
            var preview = Preview(command);
            var result = preview.IsValid
                ? CommitClear(command)
                : preview;
            LastResult = result;
            return result;
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            if (command.Command is ZoneAreaCommand paint)
            {
                var result = Preview(paint);
                return result.IsValid
                    ? CommandValidationResult.Valid
                    : CommandValidationResult.Invalid(result.Message, result);
            }

            if (command.Command is ClearZoneCommand clear)
            {
                var result = Preview(clear);
                return result.IsValid
                    ? CommandValidationResult.Valid
                    : CommandValidationResult.Invalid(result.Message, result);
            }

            return CommandValidationResult.Unsupported("No zone handler can execute this command.");
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            if (command.Command is ZoneAreaCommand paint)
            {
                var result = Execute(paint);
                return result.IsApplied
                    ? CommandExecutionResult.Applied(result)
                    : CommandExecutionResult.Rejected(result.Message).WithDetails(result);
            }

            if (command.Command is ClearZoneCommand clear)
            {
                var result = Execute(clear);
                return result.IsApplied
                    ? CommandExecutionResult.Applied(result)
                    : CommandExecutionResult.Rejected(result.Message).WithDetails(result);
            }

            return CommandExecutionResult.Unsupported("No zone handler can execute this command.");
        }

        private ZoneCommandResult EvaluatePaint(ZoneAreaCommand command)
        {
            var cells = command.Cells;
            if (cells == null || cells.Count == 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    command.ZoneType,
                    command.DefinitionId,
                    ZoneCommandFailureReason.EmptyBrush,
                    cells,
                    Array.Empty<CellCoord>());
            }

            if (!TryResolveDefinition(command, out var definition, out var definitionReason))
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    command.ZoneType,
                    command.DefinitionId,
                    definitionReason,
                    cells,
                    Array.Empty<CellCoord>());
            }

            var outOfBounds = FindOutOfBounds(cells);
            if (outOfBounds.Count != 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    definition.ZoneType,
                    definition.Id,
                    ZoneCommandFailureReason.OutOfBounds,
                    cells,
                    outOfBounds);
            }

            var roadCells = FindRoadCells(cells);
            if (roadCells.Count != 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    definition.ZoneType,
                    definition.Id,
                    ZoneCommandFailureReason.RoadOccupied,
                    cells,
                    roadCells);
            }

            var otherZoneCells = FindOtherZoneCells(cells, definition.ZoneType);
            if (otherZoneCells.Count != 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    definition.ZoneType,
                    definition.Id,
                    ZoneCommandFailureReason.DifferentZone,
                    cells,
                    otherZoneCells);
            }

            var changedCells = new List<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                var cell = _zones.GetCell(coordinate);
                if (cell.ZoneType != definition.ZoneType || cell.DefinitionId != definition.Id)
                    changedCells.Add(coordinate);
            }

            return ZoneCommandResult.Preview(
                ZoneCommandOperation.Paint,
                definition.ZoneType,
                definition.Id,
                cells,
                changedCells);
        }

        private ZoneCommandResult EvaluateClear(ClearZoneCommand command)
        {
            var cells = command.Cells;
            if (cells == null || cells.Count == 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Clear,
                    ZoneType.None,
                    StableId.Invalid,
                    ZoneCommandFailureReason.EmptyBrush,
                    cells,
                    Array.Empty<CellCoord>());
            }

            var outOfBounds = FindOutOfBounds(cells);
            if (outOfBounds.Count != 0)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Clear,
                    ZoneType.None,
                    StableId.Invalid,
                    ZoneCommandFailureReason.OutOfBounds,
                    cells,
                    outOfBounds);
            }

            var changedCells = new List<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                if (_zones.GetZoneType(coordinate).IsPaintable())
                    changedCells.Add(coordinate);
            }

            return ZoneCommandResult.Preview(
                ZoneCommandOperation.Clear,
                ZoneType.None,
                StableId.Invalid,
                cells,
                changedCells);
        }

        private ZoneCommandResult CommitPaint(ZoneAreaCommand command)
        {
            // Re-evaluate both road and zone state at commit time. This keeps a
            // stale preview from applying a brush after another command changed
            // an affected cell.
            var current = EvaluatePaint(command);
            if (!current.IsValid)
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    current.ZoneType,
                    current.DefinitionId,
                    ZoneCommandFailureReason.StateChangedBeforeCommit,
                    command.Cells,
                    Array.Empty<CellCoord>());
            }

            if (!_zones.TryApplyZone(
                    command.Cells,
                    current.ZoneType,
                    current.DefinitionId))
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Paint,
                    current.ZoneType,
                    current.DefinitionId,
                    ZoneCommandFailureReason.StateChangedBeforeCommit,
                    command.Cells,
                    Array.Empty<CellCoord>());
            }

            return ZoneCommandResult.Applied(
                ZoneCommandOperation.Paint,
                current.ZoneType,
                current.DefinitionId,
                command.Cells,
                current.ChangedCells);
        }

        private ZoneCommandResult CommitClear(ClearZoneCommand command)
        {
            var current = EvaluateClear(command);
            if (!current.IsValid || !_zones.TryApplyClear(command.Cells))
            {
                return ZoneCommandResult.Rejected(
                    ZoneCommandOperation.Clear,
                    ZoneType.None,
                    StableId.Invalid,
                    ZoneCommandFailureReason.StateChangedBeforeCommit,
                    command.Cells,
                    Array.Empty<CellCoord>());
            }

            return ZoneCommandResult.Applied(
                ZoneCommandOperation.Clear,
                ZoneType.None,
                StableId.Invalid,
                command.Cells,
                current.ChangedCells);
        }

        private bool TryResolveDefinition(
            ZoneAreaCommand command,
            out ZoneDefinition definition,
            out ZoneCommandFailureReason reason)
        {
            definition = null;
            reason = ZoneCommandFailureReason.None;
            if (!command.ZoneType.IsPaintable() && !command.DefinitionId.IsValid)
            {
                reason = ZoneCommandFailureReason.InvalidZoneType;
                return false;
            }

            if (command.DefinitionId.IsValid && _definitions.TryGet(command.DefinitionId, out definition))
            {
                if (command.ZoneType.IsPaintable() && command.ZoneType != definition.ZoneType)
                {
                    definition = null;
                    reason = ZoneCommandFailureReason.InvalidDefinition;
                    return false;
                }

                return true;
            }

            // A type-only command uses the built-in ID as a compatibility hint;
            // custom catalogs may assign another ID to the same stable enum.
            if (command.ZoneType.IsPaintable() &&
                command.DefinitionId == ZoneDefinitionIds.For(command.ZoneType) &&
                _definitions.TryGet(command.ZoneType, out definition))
            {
                return true;
            }

            reason = command.ZoneType.IsPaintable()
                ? ZoneCommandFailureReason.InvalidDefinition
                : ZoneCommandFailureReason.InvalidZoneType;
            return false;
        }

        private List<CellCoord> FindOutOfBounds(IReadOnlyList<CellCoord> cells)
        {
            var result = new List<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (!_zones.IsInBounds(cells[index]))
                    result.Add(cells[index]);
            }

            return result;
        }

        private List<CellCoord> FindRoadCells(IReadOnlyList<CellCoord> cells)
        {
            var result = new List<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (_roads.IsOccupied(cells[index]))
                    result.Add(cells[index]);
            }

            return result;
        }

        private List<CellCoord> FindOtherZoneCells(
            IReadOnlyList<CellCoord> cells,
            ZoneType requestedType)
        {
            var result = new List<CellCoord>();
            for (var index = 0; index < cells.Count; index++)
            {
                var current = _zones.GetZoneType(cells[index]);
                if (current.IsPaintable() && current != requestedType)
                    result.Add(cells[index]);
            }

            return result;
        }

        private static ZoneNetworkState CreateZones(RoadNetworkState roads)
        {
            if (roads == null)
                throw new ArgumentNullException(nameof(roads));
            return new ZoneNetworkState(roads.Width, roads.Height);
        }
    }
}
