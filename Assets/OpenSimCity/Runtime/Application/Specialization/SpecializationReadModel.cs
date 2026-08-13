using System;
using System.Collections.Generic;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Application.Specialization
{
    public enum SpecializationStageView : byte { MaterialPreparation, Assembly, Export }
    public enum SpecializationBottleneckView : byte { None, Locked, NoExternalMarket, LaborShortage, PowerShortage, RawMaterialShortage, IntermediateShortage, FinishedGoodsShortage, TransportBottleneck, InventoryFull }
    public enum SpecializationDiagnosticView : byte { Labor, Power, Traffic, Pollution, Services, Profit }

    [Serializable]
    public readonly struct SpecializationStageRow
    {
        public SpecializationStageRow(SpecializationStageState source)
        {
            Kind = (SpecializationStageView)(byte)source.Kind;
            IsUnlocked = source.IsUnlocked;
            Input = source.LastInput;
            Output = source.LastOutput;
            Loss = source.LastLoss;
            UtilizationPermille = source.UtilizationPermille;
            Bottleneck = (SpecializationBottleneckView)(byte)source.Bottleneck;
        }
        public SpecializationStageView Kind { get; }
        public bool IsUnlocked { get; }
        public int Input { get; }
        public int Output { get; }
        public int Loss { get; }
        public int UtilizationPermille { get; }
        public SpecializationBottleneckView Bottleneck { get; }
    }

    [Serializable]
    public readonly struct SpecializationDiagnosticRow
    {
        public SpecializationDiagnosticRow(SpecializationDiagnosticFactor source)
        {
            Kind = (SpecializationDiagnosticView)(byte)source.Kind;
            ValuePermille = source.ValuePermille;
        }
        public SpecializationDiagnosticView Kind { get; }
        public int ValuePermille { get; }
    }

    [Serializable]
    public sealed class SpecializationReadModelSnapshot
    {
        public int Version;
        public bool HasProcessedMonth;
        public int Year;
        public int Month;
        public int ProgressPermille;
        public int RawInventory;
        public int IntermediateInventory;
        public int FinishedInventory;
        public int LastRevenue;
        public int LastCost;
        public int LastNetProfit;
        public int CumulativeExports;
        public IReadOnlyList<SpecializationStageRow> Stages = Array.AsReadOnly(Array.Empty<SpecializationStageRow>());
        public IReadOnlyList<SpecializationDiagnosticRow> Diagnostics = Array.AsReadOnly(Array.Empty<SpecializationDiagnosticRow>());
    }

    public sealed class SpecializationReadModel
    {
        private readonly SpecializationRuntimeModule _module;
        private int _stateVersion = -1;

        public SpecializationReadModel(SpecializationRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Snapshot = new SpecializationReadModelSnapshot();
            RefreshIfNeeded();
        }

        public SpecializationReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            var source = _module.System.State;
            if (_stateVersion == source.Version) return false;
            var stages = new SpecializationStageRow[source.Stages.Count];
            for (var index = 0; index < stages.Length; index++) stages[index] = new SpecializationStageRow(source.Stages[index]);
            var diagnostics = new SpecializationDiagnosticRow[source.Diagnostics.Count];
            for (var index = 0; index < diagnostics.Length; index++) diagnostics[index] = new SpecializationDiagnosticRow(source.Diagnostics[index]);
            Snapshot = new SpecializationReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                HasProcessedMonth = source.HasProcessedMonth,
                Year = source.HasProcessedMonth ? source.LastProcessedMonth.Year : 0,
                Month = source.HasProcessedMonth ? source.LastProcessedMonth.Month : 0,
                ProgressPermille = source.ProgressPermille,
                RawInventory = source.RawInventory,
                IntermediateInventory = source.IntermediateInventory,
                FinishedInventory = source.FinishedInventory,
                LastRevenue = source.LastRevenue,
                LastCost = source.LastCost,
                LastNetProfit = source.LastNetProfit,
                CumulativeExports = source.CumulativeExports,
                Stages = Array.AsReadOnly(stages),
                Diagnostics = Array.AsReadOnly(diagnostics)
            };
            _stateVersion = source.Version;
            return true;
        }
    }
}
