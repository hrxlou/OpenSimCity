using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Specialization
{
    public enum SpecializationStageKind : byte
    {
        MaterialPreparation,
        Assembly,
        Export
    }

    public enum SpecializationBottleneckReason : byte
    {
        None,
        Locked,
        NoExternalMarket,
        LaborShortage,
        PowerShortage,
        RawMaterialShortage,
        IntermediateShortage,
        FinishedGoodsShortage,
        TransportBottleneck,
        InventoryFull
    }

    public enum SpecializationDiagnosticKind : byte
    {
        Labor,
        Power,
        Traffic,
        Pollution,
        Services,
        Profit
    }

    [Serializable]
    public readonly struct SpecializationEnvironmentSnapshot
    {
        public SpecializationEnvironmentSnapshot(
            int industrialBuildings,
            int industrialJobs,
            int poweredIndustrialJobs,
            bool externalMarketConnected,
            int goodsTransportPermille,
            int pollutionPermille,
            int serviceCoveragePermille)
        {
            if (industrialBuildings < 0 || industrialJobs < 0 || poweredIndustrialJobs < 0 ||
                poweredIndustrialJobs > industrialJobs)
                throw new ArgumentOutOfRangeException(nameof(industrialJobs));
            IndustrialBuildings = industrialBuildings;
            IndustrialJobs = industrialJobs;
            PoweredIndustrialJobs = poweredIndustrialJobs;
            ExternalMarketConnected = externalMarketConnected;
            GoodsTransportPermille = ClampPermille(goodsTransportPermille);
            PollutionPermille = ClampPermille(pollutionPermille);
            ServiceCoveragePermille = ClampPermille(serviceCoveragePermille);
        }

        public int IndustrialBuildings { get; }
        public int IndustrialJobs { get; }
        public int PoweredIndustrialJobs { get; }
        public bool ExternalMarketConnected { get; }
        public int GoodsTransportPermille { get; }
        public int PollutionPermille { get; }
        public int ServiceCoveragePermille { get; }

        private static int ClampPermille(int value) => Math.Max(0, Math.Min(1000, value));
    }

    public interface ISpecializationEnvironmentProvider
    {
        SpecializationEnvironmentSnapshot GetEnvironment(SimulationDate month);
    }

    [Serializable]
    public sealed class UrbanManufacturingParameters
    {
        public static UrbanManufacturingParameters Default => new UrbanManufacturingParameters();

        public int MaterialUnlockJobs = 8;
        public int AssemblyUnlockJobs = 16;
        public int AssemblyUnlockCumulativeMaterial = 120;
        public int ExportUnlockJobs = 24;
        public int ExportUnlockCumulativeFinished = 80;
        public int ExportUnlockTransportPermille = 500;
        public int RawImportCapacity = 180;
        public int MaterialCapacityPerPoweredJob = 4;
        public int AssemblyCapacityPerPoweredJob = 2;
        public int MaterialYieldPermille = 900;
        public int AssemblyYieldPermille = 950;
        public int ExportCapacity = 160;
        public int RawImportUnitCost = 2;
        public int ExportUnitPrice = 8;
        public int OperatingCostPerPoweredJob = 1;
        public int InventoryLimit = 5000;
        public int PollutionPressurePerOutput = 2;

        public void Validate()
        {
            if (MaterialUnlockJobs < 0 || AssemblyUnlockJobs < MaterialUnlockJobs || ExportUnlockJobs < AssemblyUnlockJobs ||
                AssemblyUnlockCumulativeMaterial < 0 || ExportUnlockCumulativeFinished < 0 ||
                ExportUnlockTransportPermille < 0 || ExportUnlockTransportPermille > 1000 ||
                RawImportCapacity < 0 || MaterialCapacityPerPoweredJob <= 0 || AssemblyCapacityPerPoweredJob <= 0 ||
                MaterialYieldPermille <= 0 || MaterialYieldPermille > 1000 || AssemblyYieldPermille <= 0 || AssemblyYieldPermille > 1000 ||
                ExportCapacity < 0 || RawImportUnitCost < 0 || ExportUnitPrice < 0 || OperatingCostPerPoweredJob < 0 ||
                InventoryLimit <= 0 || PollutionPressurePerOutput < 0)
                throw new InvalidOperationException("Urban manufacturing parameters are invalid.");
        }
    }

    [Serializable]
    public sealed class SpecializationStageState
    {
        public SpecializationStageState(SpecializationStageKind kind)
        {
            Kind = kind;
            Bottleneck = SpecializationBottleneckReason.Locked;
        }

        public SpecializationStageKind Kind { get; }
        public bool IsUnlocked { get; private set; }
        public int LastInput { get; private set; }
        public int LastOutput { get; private set; }
        public int LastLoss { get; private set; }
        public int UtilizationPermille { get; private set; }
        public SpecializationBottleneckReason Bottleneck { get; private set; }

        internal void Unlock() => IsUnlocked = true;

        internal void SetPeriod(int input, int output, int loss, int utilizationPermille, SpecializationBottleneckReason bottleneck)
        {
            if (input < 0 || output < 0 || loss < 0 || utilizationPermille < 0 || utilizationPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(input));
            LastInput = input;
            LastOutput = output;
            LastLoss = loss;
            UtilizationPermille = utilizationPermille;
            Bottleneck = bottleneck;
        }

        internal void Restore(bool unlocked, int input, int output, int loss, int utilization, SpecializationBottleneckReason bottleneck)
        {
            IsUnlocked = unlocked;
            SetPeriod(input, output, loss, utilization, bottleneck);
        }
    }

    [Serializable]
    public readonly struct SpecializationDiagnosticFactor
    {
        public SpecializationDiagnosticFactor(SpecializationDiagnosticKind kind, int valuePermille)
        {
            Kind = kind;
            ValuePermille = Math.Max(-1000, Math.Min(1000, valuePermille));
        }
        public SpecializationDiagnosticKind Kind { get; }
        public int ValuePermille { get; }
    }

    [Serializable]
    public sealed class UrbanManufacturingState
    {
        private readonly SpecializationStageState[] _stages =
        {
            new SpecializationStageState(SpecializationStageKind.MaterialPreparation),
            new SpecializationStageState(SpecializationStageKind.Assembly),
            new SpecializationStageState(SpecializationStageKind.Export)
        };
        private IReadOnlyList<SpecializationDiagnosticFactor> _diagnostics =
            Array.AsReadOnly(Array.Empty<SpecializationDiagnosticFactor>());

        public IReadOnlyList<SpecializationStageState> Stages => Array.AsReadOnly(_stages);
        public IReadOnlyList<SpecializationDiagnosticFactor> Diagnostics => _diagnostics;
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int RawInventory { get; private set; }
        public int IntermediateInventory { get; private set; }
        public int FinishedInventory { get; private set; }
        public int CumulativeImports { get; private set; }
        public int CumulativeMaterialOutput { get; private set; }
        public int CumulativeFinishedOutput { get; private set; }
        public int CumulativeExports { get; private set; }
        public long CumulativeRevenue { get; private set; }
        public long CumulativeCost { get; private set; }
        public int LastRevenue { get; private set; }
        public int LastCost { get; private set; }
        public int LastNetProfit => LastRevenue - LastCost;
        public int ProgressPermille { get; private set; }
        public int Version { get; private set; }

        public SpecializationStageState Stage(SpecializationStageKind kind) => _stages[(int)kind];

        internal void SetInventories(int raw, int intermediate, int finished)
        {
            if (raw < 0 || intermediate < 0 || finished < 0) throw new ArgumentOutOfRangeException(nameof(raw));
            RawInventory = raw;
            IntermediateInventory = intermediate;
            FinishedInventory = finished;
        }

        internal void CompleteMonth(
            SimulationDate month,
            int imported,
            int materialOutput,
            int finishedOutput,
            int exported,
            int revenue,
            int cost,
            int progressPermille,
            IReadOnlyList<SpecializationDiagnosticFactor> diagnostics)
        {
            CumulativeImports = checked(CumulativeImports + imported);
            CumulativeMaterialOutput = checked(CumulativeMaterialOutput + materialOutput);
            CumulativeFinishedOutput = checked(CumulativeFinishedOutput + finishedOutput);
            CumulativeExports = checked(CumulativeExports + exported);
            CumulativeRevenue = checked(CumulativeRevenue + revenue);
            CumulativeCost = checked(CumulativeCost + cost);
            LastRevenue = revenue;
            LastCost = cost;
            ProgressPermille = Math.Max(0, Math.Min(1000, progressPermille));
            _diagnostics = diagnostics ?? Array.AsReadOnly(Array.Empty<SpecializationDiagnosticFactor>());
            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        internal void Restore(UrbanManufacturingStateDto dto)
        {
            SetInventories(dto.RawInventory, dto.IntermediateInventory, dto.FinishedInventory);
            CumulativeImports = dto.CumulativeImports;
            CumulativeMaterialOutput = dto.CumulativeMaterialOutput;
            CumulativeFinishedOutput = dto.CumulativeFinishedOutput;
            CumulativeExports = dto.CumulativeExports;
            CumulativeRevenue = dto.CumulativeRevenue;
            CumulativeCost = dto.CumulativeCost;
            LastRevenue = dto.LastRevenue;
            LastCost = dto.LastCost;
            ProgressPermille = dto.ProgressPermille;
            HasProcessedMonth = dto.HasProcessedMonth;
            LastProcessedMonth = dto.HasProcessedMonth ? new SimulationDate(dto.LastProcessedYear, dto.LastProcessedMonth) : default;
            var rows = dto.Stages ?? Array.Empty<SpecializationStageStateDto>();
            if (rows.Length != _stages.Length) throw new ArgumentException("Specialization snapshot requires exactly three stage rows.", nameof(dto));
            for (var index = 0; index < rows.Length; index++)
            {
                if (rows[index] == null ||
                    rows[index].Kind != index ||
                    rows[index].Bottleneck < byte.MinValue ||
                    rows[index].Bottleneck > byte.MaxValue ||
                    !Enum.IsDefined(
                        typeof(SpecializationBottleneckReason),
                        (SpecializationBottleneckReason)rows[index].Bottleneck))
                    throw new ArgumentException("Specialization stage rows are invalid.", nameof(dto));
                _stages[index].Restore(rows[index].IsUnlocked, rows[index].LastInput, rows[index].LastOutput, rows[index].LastLoss, rows[index].UtilizationPermille, (SpecializationBottleneckReason)rows[index].Bottleneck);
            }
            Version = HasProcessedMonth ? 1 : 0;
        }

        public UrbanManufacturingStateDto ToDto()
        {
            var stages = new SpecializationStageStateDto[_stages.Length];
            for (var index = 0; index < stages.Length; index++)
            {
                var row = _stages[index];
                stages[index] = new SpecializationStageStateDto
                {
                    Kind = (int)row.Kind,
                    IsUnlocked = row.IsUnlocked,
                    LastInput = row.LastInput,
                    LastOutput = row.LastOutput,
                    LastLoss = row.LastLoss,
                    UtilizationPermille = row.UtilizationPermille,
                    Bottleneck = (int)row.Bottleneck
                };
            }
            return new UrbanManufacturingStateDto
            {
                HasProcessedMonth = HasProcessedMonth,
                LastProcessedYear = HasProcessedMonth ? LastProcessedMonth.Year : 0,
                LastProcessedMonth = HasProcessedMonth ? LastProcessedMonth.Month : 0,
                RawInventory = RawInventory,
                IntermediateInventory = IntermediateInventory,
                FinishedInventory = FinishedInventory,
                CumulativeImports = CumulativeImports,
                CumulativeMaterialOutput = CumulativeMaterialOutput,
                CumulativeFinishedOutput = CumulativeFinishedOutput,
                CumulativeExports = CumulativeExports,
                CumulativeRevenue = CumulativeRevenue,
                CumulativeCost = CumulativeCost,
                LastRevenue = LastRevenue,
                LastCost = LastCost,
                ProgressPermille = ProgressPermille,
                Stages = stages
            };
        }
    }

    [Serializable]
    public sealed class SpecializationStageStateDto
    {
        public int Kind;
        public bool IsUnlocked;
        public int LastInput;
        public int LastOutput;
        public int LastLoss;
        public int UtilizationPermille;
        public int Bottleneck;
    }

    [Serializable]
    public sealed class UrbanManufacturingStateDto
    {
        public bool HasProcessedMonth;
        public int LastProcessedYear;
        public int LastProcessedMonth;
        public int RawInventory;
        public int IntermediateInventory;
        public int FinishedInventory;
        public int CumulativeImports;
        public int CumulativeMaterialOutput;
        public int CumulativeFinishedOutput;
        public int CumulativeExports;
        public long CumulativeRevenue;
        public long CumulativeCost;
        public int LastRevenue;
        public int LastCost;
        public int ProgressPermille;
        public SpecializationStageStateDto[] Stages = Array.Empty<SpecializationStageStateDto>();
    }
}
