using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictCommandHandler : ICityCommandHandler
    {
        private readonly CityRuntime _runtime;
        private readonly DistrictCollection _districts;

        public DistrictCommandHandler(CityRuntime runtime, DistrictCollection districts)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _districts = districts ?? throw new ArgumentNullException(nameof(districts));
        }

        public bool CanHandle(ICityCommand command) =>
            command is CreateDistrictCommand || command is RenameDistrictCommand || command is DistrictColorCommand ||
            command is PaintDistrictCommand || command is EraseDistrictCommand || command is DeleteDistrictCommand;

        public CommandValidationResult Validate(CommandEnvelope envelope)
        {
            try
            {
                if (envelope.Command is CreateDistrictCommand create)
                {
                    if (!create.DistrictId.IsValid || _districts.TryGet(create.DistrictId, out _)) return CommandValidationResult.Invalid("District ID is invalid or already exists.");
                    _ = new DistrictState(create.DistrictId, create.Name, create.ColorRgba);
                    return CommandValidationResult.Valid;
                }
                if (envelope.Command is RenameDistrictCommand rename)
                {
                    if (!_districts.TryGet(rename.DistrictId, out _)) return CommandValidationResult.Invalid("District was not found.");
                    _ = new DistrictState(rename.DistrictId, rename.Name, 0u);
                    return CommandValidationResult.Valid;
                }
                if (envelope.Command is DistrictColorCommand color) return Exists(color.DistrictId);
                if (envelope.Command is DeleteDistrictCommand delete) return Exists(delete.DistrictId);
                if (envelope.Command is PaintDistrictCommand paint) return ValidateCells(paint.DistrictId, paint.Cells, true);
                if (envelope.Command is EraseDistrictCommand erase) return ValidateCells(erase.DistrictId, erase.Cells, false);
            }
            catch (ArgumentException exception) { return CommandValidationResult.Invalid(exception.Message); }
            return CommandValidationResult.Unsupported("Unsupported district command.");
        }

        public CommandExecutionResult Execute(CommandEnvelope envelope)
        {
            var validation = Validate(envelope);
            if (!validation.IsValid) return CommandExecutionResult.Rejected(validation.Message);
            bool applied;
            if (envelope.Command is CreateDistrictCommand create) applied = _districts.Create(create.DistrictId, create.Name, create.ColorRgba);
            else if (envelope.Command is RenameDistrictCommand rename) applied = _districts.Rename(rename.DistrictId, rename.Name);
            else if (envelope.Command is DistrictColorCommand color) applied = _districts.SetColor(color.DistrictId, color.Rgba);
            else if (envelope.Command is PaintDistrictCommand paint) applied = _districts.Paint(paint.DistrictId, paint.Cells);
            else if (envelope.Command is EraseDistrictCommand erase) applied = _districts.Erase(erase.DistrictId, erase.Cells);
            else if (envelope.Command is DeleteDistrictCommand delete) applied = _districts.Delete(delete.DistrictId);
            else return CommandExecutionResult.Unsupported("Unsupported district command.");
            return applied ? CommandExecutionResult.Applied() : CommandExecutionResult.Rejected("District state changed before commit.");
        }

        private CommandValidationResult Exists(StableId id) =>
            id.IsValid && _districts.TryGet(id, out _) ? CommandValidationResult.Valid : CommandValidationResult.Invalid("District was not found.");

        private CommandValidationResult ValidateCells(StableId id, System.Collections.Generic.IReadOnlyList<CellCoord> cells, bool rejectOtherOwner)
        {
            if (!_districts.TryGet(id, out _)) return CommandValidationResult.Invalid("District was not found.");
            if (cells.Count == 0) return CommandValidationResult.Invalid("At least one cell is required.");
            for (var index = 0; index < cells.Count; index++)
            {
                if (!_runtime.World.IsInBounds(cells[index])) return CommandValidationResult.Invalid("District cells must stay inside world bounds.");
                if (rejectOtherOwner && _districts.TryGetDistrictAt(cells[index], out var owner) && owner != id)
                    return CommandValidationResult.Invalid("A cell already belongs to another district.");
            }
            return CommandValidationResult.Valid;
        }
    }
}
