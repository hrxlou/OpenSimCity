using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictRuntimeModule
    {
        private const ulong DistrictIdPrefix = 0xD760000000000000UL;

        internal DistrictRuntimeModule(CityRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Districts = new DistrictCollection();
            Commands = new DistrictCommandHandler(runtime, Districts);
            ReadModel = new DistrictReadModel(Districts);
        }

        public CityRuntime Runtime { get; }
        public DistrictCollection Districts { get; }
        public DistrictCommandHandler Commands { get; }
        public DistrictReadModel ReadModel { get; }

        public StableId NextDistrictId()
        {
            ulong suffix = 1;
            while (Districts.TryGet(new StableId(DistrictIdPrefix | suffix), out _)) suffix++;
            return new StableId(DistrictIdPrefix | suffix);
        }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command)) return CommandExecutionResult.Unsupported("Unsupported district command.");
            var sequenceResult = Runtime.Execute(command);
            if (sequenceResult.Status != CommandExecutionStatus.Unsupported) return sequenceResult;
            var result = Commands.Execute(new CommandEnvelope(sequenceResult.Stamp, command)).WithStamp(sequenceResult.Stamp);
            ReadModel.RefreshIfNeeded();
            return result;
        }

        public DistrictSaveData Capture()
        {
            var source = Districts.Districts;
            var rows = new DistrictRow[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                var district = source[index];
                var cells = new DistrictCellDto[district.Cells.Count];
                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    cells[cellIndex] = new DistrictCellDto { X = district.Cells[cellIndex].X, Y = district.Cells[cellIndex].Y };
                rows[index] = new DistrictRow { Id = district.Id.Value, Name = district.Name, Color = district.ColorRgba, Cells = cells };
            }
            return new DistrictSaveData { Format = 1, Items = rows };
        }

        public void Restore(DistrictSaveData data)
        {
            if (data == null || data.Format != 1) throw new InvalidOperationException("Unsupported district save format.");
            var source = data.Items ?? Array.Empty<DistrictRow>();
            var restored = new List<DistrictState>(source.Length);
            ulong previous = 0;
            for (var index = 0; index < source.Length; index++)
            {
                var row = source[index] ?? throw new ArgumentException("District row cannot be null.", nameof(data));
                if (row.Id == 0 || row.Id <= previous) throw new ArgumentException("District rows require ascending stable IDs.", nameof(data));
                var cellDtos = row.Cells ?? Array.Empty<DistrictCellDto>();
                var cells = new CellCoord[cellDtos.Length];
                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    var cell = new CellCoord(cellDtos[cellIndex].X, cellDtos[cellIndex].Y);
                    if (!Runtime.World.IsInBounds(cell)) throw new ArgumentException("District cell is out of bounds.", nameof(data));
                    cells[cellIndex] = cell;
                }
                restored.Add(new DistrictState(new StableId(row.Id), row.Name, row.Color, cells));
                previous = row.Id;
            }
            Districts.ClearAndRestore(restored);
            ReadModel.RefreshIfNeeded();
        }
    }

    public static class DistrictRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, DistrictRuntimeModule> Modules = new ConditionalWeakTable<CityRuntime, DistrictRuntimeModule>();
        public static DistrictRuntimeModule GetOrCreate(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new DistrictRuntimeModule(runtime); Modules.Add(runtime, created); return created;
        }
    }
}
