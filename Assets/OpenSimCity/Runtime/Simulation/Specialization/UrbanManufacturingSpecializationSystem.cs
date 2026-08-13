using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Simulation.Specialization
{
    /// <summary>Integer-only raw material -> component -> finished export chain.</summary>
    public sealed class UrbanManufacturingSpecializationSystem : IMonthlySimulationSystem
    {
        private readonly ISpecializationEnvironmentProvider _environment;
        private readonly UrbanManufacturingParameters _parameters;

        public UrbanManufacturingSpecializationSystem(ISpecializationEnvironmentProvider environment, UrbanManufacturingParameters parameters = null)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _parameters = parameters ?? UrbanManufacturingParameters.Default;
            _parameters.Validate();
            State = new UrbanManufacturingState();
        }

        public UrbanManufacturingState State { get; }
        public UrbanManufacturingParameters Parameters => _parameters;

        public void RunMonth(SimulationDate month)
        {
            if (State.HasProcessedMonth && month.CompareTo(State.LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Specialization months must be strictly increasing.");

            var env = _environment.GetEnvironment(month);
            Unlock(env);
            var raw = State.RawInventory;
            var intermediate = State.IntermediateInventory;
            var finished = State.FinishedInventory;

            var imported = Import(env, raw);
            raw = checked(raw + imported);

            var material = Transform(
                State.Stage(SpecializationStageKind.MaterialPreparation), env, raw, intermediate,
                checked(env.PoweredIndustrialJobs * _parameters.MaterialCapacityPerPoweredJob),
                _parameters.MaterialYieldPermille, SpecializationBottleneckReason.RawMaterialShortage);
            raw -= material.Input;
            intermediate = checked(intermediate + material.Output);
            Apply(SpecializationStageKind.MaterialPreparation, material);

            var assembly = Transform(
                State.Stage(SpecializationStageKind.Assembly), env, intermediate, finished,
                checked(env.PoweredIndustrialJobs * _parameters.AssemblyCapacityPerPoweredJob),
                _parameters.AssemblyYieldPermille, SpecializationBottleneckReason.IntermediateShortage);
            intermediate -= assembly.Input;
            finished = checked(finished + assembly.Output);
            Apply(SpecializationStageKind.Assembly, assembly);

            var exported = Export(env, finished, out var exportUtilization, out var exportReason);
            finished -= exported;
            State.Stage(SpecializationStageKind.Export).SetPeriod(exported, exported, 0, exportUtilization, exportReason);
            State.SetInventories(raw, intermediate, finished);

            var revenue = checked(exported * _parameters.ExportUnitPrice);
            var cost = checked(imported * _parameters.RawImportUnitCost + env.PoweredIndustrialJobs * _parameters.OperatingCostPerPoweredJob);
            State.CompleteMonth(month, imported, material.Output, assembly.Output, exported, revenue, cost,
                Progress(env, exported), Diagnostics(env, material.Output + assembly.Output, revenue - cost));
        }

        public void RestoreFromDto(UrbanManufacturingStateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.RawInventory < 0 || dto.IntermediateInventory < 0 || dto.FinishedInventory < 0 ||
                dto.RawInventory > _parameters.InventoryLimit || dto.IntermediateInventory > _parameters.InventoryLimit || dto.FinishedInventory > _parameters.InventoryLimit ||
                dto.CumulativeImports < 0 || dto.CumulativeMaterialOutput < 0 || dto.CumulativeFinishedOutput < 0 || dto.CumulativeExports < 0 ||
                dto.CumulativeRevenue < 0 || dto.CumulativeCost < 0 || dto.LastRevenue < 0 || dto.LastCost < 0 ||
                dto.ProgressPermille < 0 || dto.ProgressPermille > 1000)
                throw new ArgumentException("Specialization snapshot is invalid.", nameof(dto));
            State.Restore(dto);
        }

        private void Unlock(SpecializationEnvironmentSnapshot env)
        {
            var material = State.Stage(SpecializationStageKind.MaterialPreparation);
            var assembly = State.Stage(SpecializationStageKind.Assembly);
            var export = State.Stage(SpecializationStageKind.Export);
            if (!material.IsUnlocked && env.ExternalMarketConnected && env.IndustrialJobs >= _parameters.MaterialUnlockJobs) material.Unlock();
            if (!assembly.IsUnlocked && material.IsUnlocked && env.IndustrialJobs >= _parameters.AssemblyUnlockJobs && State.CumulativeMaterialOutput >= _parameters.AssemblyUnlockCumulativeMaterial) assembly.Unlock();
            if (!export.IsUnlocked && assembly.IsUnlocked && env.IndustrialJobs >= _parameters.ExportUnlockJobs && State.CumulativeFinishedOutput >= _parameters.ExportUnlockCumulativeFinished && env.GoodsTransportPermille >= _parameters.ExportUnlockTransportPermille) export.Unlock();
        }

        private int Import(SpecializationEnvironmentSnapshot env, int inventory)
        {
            if (!State.Stage(SpecializationStageKind.MaterialPreparation).IsUnlocked || !env.ExternalMarketConnected) return 0;
            return Math.Min(_parameters.RawImportCapacity, Math.Max(0, _parameters.InventoryLimit - inventory));
        }

        private StageResult Transform(SpecializationStageState stage, SpecializationEnvironmentSnapshot env, int inputInventory, int outputInventory, int capacity, int yieldPermille, SpecializationBottleneckReason shortage)
        {
            if (!stage.IsUnlocked) return StageResult.Blocked(SpecializationBottleneckReason.Locked);
            if (env.IndustrialJobs <= 0) return StageResult.Blocked(SpecializationBottleneckReason.LaborShortage);
            if (env.PoweredIndustrialJobs <= 0) return StageResult.Blocked(SpecializationBottleneckReason.PowerShortage);
            if (inputInventory <= 0) return StageResult.Blocked(shortage);
            var free = _parameters.InventoryLimit - outputInventory;
            if (free <= 0) return StageResult.Blocked(SpecializationBottleneckReason.InventoryFull);

            var maxInputForInventory = checked((free * 1000 + yieldPermille - 1) / yieldPermille);
            var input = Math.Min(inputInventory, Math.Min(capacity, maxInputForInventory));
            var output = input * yieldPermille / 1000;
            var loss = input - output;
            var utilization = capacity <= 0 ? 0 : input * 1000 / capacity;
            SpecializationBottleneckReason reason;
            if (input < capacity && inputInventory <= input) reason = shortage;
            else if (input < capacity && free <= output) reason = SpecializationBottleneckReason.InventoryFull;
            else if (env.PoweredIndustrialJobs < env.IndustrialJobs) reason = SpecializationBottleneckReason.PowerShortage;
            else reason = SpecializationBottleneckReason.None;
            return new StageResult(input, output, loss, utilization, reason);
        }

        private int Export(SpecializationEnvironmentSnapshot env, int finished, out int utilization, out SpecializationBottleneckReason reason)
        {
            var stage = State.Stage(SpecializationStageKind.Export);
            if (!stage.IsUnlocked) { utilization = 0; reason = SpecializationBottleneckReason.Locked; return 0; }
            if (!env.ExternalMarketConnected) { utilization = 0; reason = SpecializationBottleneckReason.NoExternalMarket; return 0; }
            if (finished <= 0) { utilization = 0; reason = SpecializationBottleneckReason.FinishedGoodsShortage; return 0; }
            var capacity = _parameters.ExportCapacity * env.GoodsTransportPermille / 1000;
            var result = Math.Min(finished, capacity);
            utilization = _parameters.ExportCapacity <= 0 ? 0 : result * 1000 / _parameters.ExportCapacity;
            reason = env.GoodsTransportPermille < 1000 && result < finished
                ? SpecializationBottleneckReason.TransportBottleneck
                : result < _parameters.ExportCapacity && result == finished
                    ? SpecializationBottleneckReason.FinishedGoodsShortage
                    : SpecializationBottleneckReason.None;
            return result;
        }

        private void Apply(SpecializationStageKind kind, StageResult result)
        {
            State.Stage(kind).SetPeriod(result.Input, result.Output, result.Loss, result.UtilizationPermille, result.Reason);
        }

        private int Progress(SpecializationEnvironmentSnapshot env, int exported)
        {
            var value = 0;
            if (State.Stage(SpecializationStageKind.MaterialPreparation).IsUnlocked) value += 250;
            if (State.Stage(SpecializationStageKind.Assembly).IsUnlocked) value += 250;
            if (State.Stage(SpecializationStageKind.Export).IsUnlocked) value += 250;
            if (exported > 0) value += Math.Min(250, State.CumulativeExports + exported);
            if (env.PollutionPermille > 900) value -= 50;
            return Math.Max(0, Math.Min(1000, value));
        }

        private IReadOnlyList<SpecializationDiagnosticFactor> Diagnostics(SpecializationEnvironmentSnapshot env, int output, int profit)
        {
            var labor = env.IndustrialJobs <= 0 ? 0 : env.PoweredIndustrialJobs * 1000 / env.IndustrialJobs;
            return Array.AsReadOnly(new[]
            {
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Labor, labor),
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Power, labor),
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Traffic, env.GoodsTransportPermille),
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Pollution, Math.Min(1000, checked(output * _parameters.PollutionPressurePerOutput))),
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Services, env.ServiceCoveragePermille),
                new SpecializationDiagnosticFactor(SpecializationDiagnosticKind.Profit, Math.Max(-1000, Math.Min(1000, profit)))
            });
        }

        private readonly struct StageResult
        {
            public StageResult(int input, int output, int loss, int utilizationPermille, SpecializationBottleneckReason reason)
            { Input = input; Output = output; Loss = loss; UtilizationPermille = utilizationPermille; Reason = reason; }
            public int Input { get; }
            public int Output { get; }
            public int Loss { get; }
            public int UtilizationPermille { get; }
            public SpecializationBottleneckReason Reason { get; }
            public static StageResult Blocked(SpecializationBottleneckReason reason) => new StageResult(0, 0, 0, 0, reason);
        }
    }
}
