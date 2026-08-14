using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Population;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.WaterDensity
{
    public sealed class PlaceWaterProducerCommand : ICityCommand
    {
        public PlaceWaterProducerCommand(StableId producerId, StableId definitionId, CellCoord cell)
        {
            ProducerId = producerId;
            DefinitionId = definitionId;
            Cell = cell;
        }
        public StableId ProducerId { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
    }

    public sealed class RemoveWaterProducerCommand : ICityCommand
    {
        public RemoveWaterProducerCommand(StableId producerId) { ProducerId = producerId; }
        public StableId ProducerId { get; }
    }

    public enum WaterCommandFailureReason : byte
    {
        None,
        InvalidCommand,
        UnknownDefinition,
        ProducerNotFound,
        DuplicateProducer,
        CellOutOfBounds,
        CellOccupied,
        NoRoadAccess,
        InsufficientFunds,
        StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class WaterCommandResult
    {
        private WaterCommandResult(
            bool valid,
            bool applied,
            WaterCommandFailureReason failure,
            StableId producerId,
            int cost,
            string message)
        {
            IsValid = valid;
            IsApplied = applied;
            FailureReason = failure;
            ProducerId = producerId;
            Cost = cost;
            Message = message ?? string.Empty;
        }
        public bool IsValid { get; }
        public bool IsApplied { get; }
        public WaterCommandFailureReason FailureReason { get; }
        public StableId ProducerId { get; }
        public int Cost { get; }
        public string Message { get; }
        public static WaterCommandResult Valid(StableId id, int cost) =>
            new WaterCommandResult(true, false, WaterCommandFailureReason.None, id, cost, string.Empty);
        public static WaterCommandResult Applied(StableId id, int cost) =>
            new WaterCommandResult(true, true, WaterCommandFailureReason.None, id, cost, string.Empty);
        public static WaterCommandResult Rejected(StableId id, WaterCommandFailureReason reason, int cost = 0) =>
            new WaterCommandResult(false, false, reason, id, cost, MessageFor(reason));
        private static string MessageFor(WaterCommandFailureReason reason)
        {
            switch (reason)
            {
                case WaterCommandFailureReason.UnknownDefinition: return "알 수 없는 수도 시설입니다.";
                case WaterCommandFailureReason.ProducerNotFound: return "수도 시설을 찾을 수 없습니다.";
                case WaterCommandFailureReason.DuplicateProducer: return "같은 stable ID의 수도 시설이 이미 있습니다.";
                case WaterCommandFailureReason.CellOutOfBounds: return "월드 경계 밖에는 수도 시설을 배치할 수 없습니다.";
                case WaterCommandFailureReason.CellOccupied: return "도로·건물·다른 시설과 겹칩니다.";
                case WaterCommandFailureReason.NoRoadAccess: return "인접한 도로 연결이 필요합니다.";
                case WaterCommandFailureReason.InsufficientFunds: return "수도 시설 건설 비용을 지불할 시 예산이 부족합니다.";
                case WaterCommandFailureReason.StateChangedBeforeCommit: return "미리보기 이후 수도 상태가 변경되었습니다.";
                default: return "수도 명령이 유효하지 않습니다.";
            }
        }
    }

    public sealed class WaterCommandHandler : ICityCommandHandler
    {
        private readonly CityRuntime _runtime;
        private readonly WaterProducerCollection _producers;
        private readonly WaterProducerDefinitionCatalog _definitions;
        private readonly PublicServiceRuntimeModule _services;

        public WaterCommandHandler(
            CityRuntime runtime,
            WaterProducerCollection producers,
            WaterProducerDefinitionCatalog definitions,
            PublicServiceRuntimeModule services)
        {
            _runtime = runtime;
            _producers = producers;
            _definitions = definitions;
            _services = services;
        }

        public bool CanHandle(ICityCommand command) =>
            command is PlaceWaterProducerCommand || command is RemoveWaterProducerCommand;

        public WaterCommandResult Preview(PlaceWaterProducerCommand command)
        {
            if (command == null || !command.ProducerId.IsValid || !command.DefinitionId.IsValid)
                return WaterCommandResult.Rejected(command?.ProducerId ?? default, WaterCommandFailureReason.InvalidCommand);
            if (!_definitions.TryGet(command.DefinitionId, out var definition))
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.UnknownDefinition);
            if (!_runtime.World.IsInBounds(command.Cell))
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.CellOutOfBounds, definition.ConstructionCost);
            if (_producers.TryGet(command.ProducerId, out _))
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.DuplicateProducer, definition.ConstructionCost);
            if (_runtime.Roads.HasRoad(command.Cell) || _runtime.Buildings.TryFindAt(command.Cell, out _) || HasFacilityAt(command.Cell))
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.CellOccupied, definition.ConstructionCost);
            if (!TryAdjacentRoad(command.Cell, out _))
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.NoRoadAccess, definition.ConstructionCost);
            if (_runtime.Finance.Funds < definition.ConstructionCost)
                return WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.InsufficientFunds, definition.ConstructionCost);
            return WaterCommandResult.Valid(command.ProducerId, definition.ConstructionCost);
        }

        public WaterCommandResult Preview(RemoveWaterProducerCommand command)
        {
            if (command == null || !command.ProducerId.IsValid)
                return WaterCommandResult.Rejected(command?.ProducerId ?? default, WaterCommandFailureReason.InvalidCommand);
            return _producers.TryGet(command.ProducerId, out _)
                ? WaterCommandResult.Valid(command.ProducerId, 0)
                : WaterCommandResult.Rejected(command.ProducerId, WaterCommandFailureReason.ProducerNotFound);
        }

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            WaterCommandResult result;
            if (command.Command is PlaceWaterProducerCommand place) result = Preview(place);
            else if (command.Command is RemoveWaterProducerCommand remove) result = Preview(remove);
            else return CommandValidationResult.Unsupported("No water handler can execute this command.");
            return result.IsValid ? CommandValidationResult.Valid : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            if (command.Command is PlaceWaterProducerCommand place)
            {
                var preview = Preview(place);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                if (!_runtime.Finance.TrySpend(preview.Cost))
                    return CommandExecutionResult.Rejected("수도 시설 건설 비용을 지불할 수 없습니다.");
                if (!_producers.Add(new WaterProducerState(place.ProducerId, place.DefinitionId, place.Cell)))
                {
                    _runtime.Finance.TryRefund(preview.Cost);
                    var failed = WaterCommandResult.Rejected(place.ProducerId, WaterCommandFailureReason.StateChangedBeforeCommit, preview.Cost);
                    return CommandExecutionResult.Rejected(failed.Message).WithDetails(failed);
                }
                _runtime.Finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, preview.Cost);
                return CommandExecutionResult.Applied(WaterCommandResult.Applied(place.ProducerId, preview.Cost));
            }
            if (command.Command is RemoveWaterProducerCommand remove)
            {
                var preview = Preview(remove);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                return _producers.Remove(remove.ProducerId)
                    ? CommandExecutionResult.Applied(WaterCommandResult.Applied(remove.ProducerId, 0))
                    : CommandExecutionResult.Rejected("수도 시설 상태가 변경되었습니다.");
            }
            return CommandExecutionResult.Unsupported("No water handler can execute this command.");
        }

        private bool HasFacilityAt(CellCoord cell)
        {
            var services = _services.Facilities.Facilities;
            for (var index = 0; index < services.Count; index++)
            {
                if (services[index].Cell == cell) return true;
            }
            var water = _producers.Producers;
            for (var index = 0; index < water.Count; index++)
            {
                if (water[index].Cell == cell) return true;
            }
            return false;
        }

        private bool TryAdjacentRoad(CellCoord cell, out CellCoord roadCell)
        {
            var candidates = new[]
            {
                new CellCoord(cell.X, cell.Y - 1), new CellCoord(cell.X - 1, cell.Y),
                new CellCoord(cell.X + 1, cell.Y), new CellCoord(cell.X, cell.Y + 1)
            };
            Array.Sort(candidates);
            for (var index = 0; index < candidates.Length; index++)
            {
                if (_runtime.World.IsInBounds(candidates[index]) && _runtime.Roads.HasRoad(candidates[index]))
                {
                    roadCell = candidates[index];
                    return true;
                }
            }
            roadCell = default;
            return false;
        }
    }

    internal sealed class WaterDensityMonthlySystem : IMonthlySimulationSystem
    {
        private readonly WaterUtilitySystem _water;
        private readonly DensityUpgradeSystem _density;
        private readonly WaterProducerCollection _producers;
        private readonly WaterProducerDefinitionCatalog _waterDefinitions;
        private readonly CityRuntime _runtime;
        private readonly IDensityBuildingProfileResolver _profiles;
        private readonly MunicipalFinanceParameters _financeParameters;

        public WaterDensityMonthlySystem(
            WaterUtilitySystem water,
            DensityUpgradeSystem density,
            WaterProducerCollection producers,
            WaterProducerDefinitionCatalog waterDefinitions,
            CityRuntime runtime,
            IDensityBuildingProfileResolver profiles)
        {
            _water = water;
            _density = density;
            _producers = producers;
            _waterDefinitions = waterDefinitions;
            _runtime = runtime;
            _profiles = profiles;
            _financeParameters = MunicipalFinanceParameters.Default;
        }

        public void RunMonth(SimulationDate month)
        {
            _water.RunMonth(month);
            _density.RunMonth(month);
            RecordDensityTax();
            ChargeWaterMaintenance();
        }

        private void RecordDensityTax()
        {
            var rates = _runtime.Finance.ActiveTaxRates;
            var buildings = _runtime.Buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (!_profiles.TryGetDensityProfile(building.DefinitionId, out var profile)) continue;
                var multiplier = _density.CapacityMultiplier(building.Id);
                if (multiplier <= 1) continue;
                int units;
                int taxBase;
                int rate;
                EconomicSector sector;
                switch (profile.Use)
                {
                    case DensityUse.Residential:
                        units = checked(building.Occupancy * (multiplier - 1));
                        taxBase = _financeParameters.ResidentialTaxBasePerResident;
                        rate = rates.ResidentialPermille;
                        sector = EconomicSector.Residential;
                        break;
                    case DensityUse.Commercial:
                        units = checked(building.FilledJobs * (multiplier - 1));
                        taxBase = _financeParameters.CommercialTaxBasePerFilledJob;
                        rate = rates.CommercialPermille;
                        sector = EconomicSector.Commercial;
                        break;
                    default:
                        units = checked(building.FilledJobs * (multiplier - 1));
                        taxBase = _financeParameters.IndustrialTaxBasePerFilledJob;
                        rate = rates.IndustrialPermille;
                        sector = EconomicSector.Industrial;
                        break;
                }
                var amount = checked(units * taxBase * rate / UrbanEconomyTaxRates.MaxPermille);
                if (amount > 0) _runtime.Finance.RecordSupplementalTaxIncome(sector, amount);
            }
        }

        private void ChargeWaterMaintenance()
        {
            var total = 0;
            var producers = _producers.Producers;
            for (var index = 0; index < producers.Count; index++)
                total = checked(total + _waterDefinitions.Get(producers[index].DefinitionId).MonthlyMaintenanceCost);
            if (total <= 0) return;
            if (!_runtime.Finance.TrySpend(total))
                throw new InvalidOperationException("Water maintenance exceeds available municipal funds.");
            _runtime.Finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, total);
        }
    }

    public enum WaterSupplyReasonView : byte { None, NoRoadAccess, NoProducer, DifferentRoadComponent, CapacityExceeded }
    public enum DensityStageView : byte { Low, Medium, High }
    public enum DensityBlockReasonView : byte
    {
        None, UnsupportedBuilding, CityPopulationTooLow, NoWater, LandValueTooLow,
        RoadTierTooLow, ServicesTooLow, AlreadyMaximum, UpgradeInProgress
    }

    [Serializable]
    public sealed class WaterProducerReadModel
    {
        public StableId ProducerId;
        public CellCoord Cell;
        public int Capacity;
        public int Used;
        public int MonthlyMaintenanceCost;
    }

    [Serializable]
    public sealed class DensityFactorReadModel
    {
        public DensityUpgradeFactorKind Kind;
        public int CurrentValue;
        public int RequiredValue;
        public bool IsSatisfied;
    }

    [Serializable]
    public sealed class DensityBuildingReadModel
    {
        public StableId BuildingId;
        public CellCoord Cell;
        public DensityStageView Stage;
        public DensityStageView TargetStage;
        public int UpgradeMonthsRemaining;
        public int CapacityMultiplier;
        public int EffectiveHousingCapacity;
        public int EffectiveJobCapacity;
        public int WaterSupplyPermille;
        public WaterSupplyReasonView WaterReason;
        public DensityBlockReasonView TopReason;
        public int ProjectedSupplementalTax;
        public IReadOnlyList<DensityFactorReadModel> Factors = Array.AsReadOnly(Array.Empty<DensityFactorReadModel>());
    }

    [Serializable]
    public sealed class WaterDensityReadModelSnapshot
    {
        public int Version;
        public bool HasProcessedMonth;
        public int Year;
        public int Month;
        public int WaterRequested;
        public int WaterSupplied;
        public ulong WaterChecksum;
        public ulong DensityChecksum;
        public IReadOnlyList<WaterProducerReadModel> Producers = Array.AsReadOnly(Array.Empty<WaterProducerReadModel>());
        public IReadOnlyList<DensityBuildingReadModel> Buildings = Array.AsReadOnly(Array.Empty<DensityBuildingReadModel>());
    }

    public sealed class WaterDensityReadModel
    {
        private readonly WaterDensityRuntimeModule _module;
        private int _waterVersion = -1;
        private int _densityVersion = -1;
        private int _buildingVersion = -1;
        private int _producerVersion = -1;
        private int _financeVersion = -1;

        public WaterDensityReadModel(WaterDensityRuntimeModule module)
        {
            _module = module;
            Snapshot = new WaterDensityReadModelSnapshot();
        }

        public WaterDensityReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            if (_waterVersion == _module.Water.State.Version &&
                _densityVersion == _module.Density.State.Version &&
                _buildingVersion == _module.Runtime.Buildings.Version &&
                _producerVersion == _module.WaterProducers.Version &&
                _financeVersion == _module.Runtime.Finance.Version)
                return false;

            var usageById = new Dictionary<StableId, WaterProducerUsageRow>();
            var usage = _module.Water.State.ProducerUsage;
            for (var index = 0; index < usage.Count; index++) usageById[usage[index].ProducerId] = usage[index];
            var producers = new List<WaterProducerReadModel>();
            var producerStates = _module.WaterProducers.Producers;
            for (var index = 0; index < producerStates.Count; index++)
            {
                var producer = producerStates[index];
                var definition = _module.WaterDefinitions.Get(producer.DefinitionId);
                usageById.TryGetValue(producer.Id, out var row);
                producers.Add(new WaterProducerReadModel
                {
                    ProducerId = producer.Id,
                    Cell = producer.Cell,
                    Capacity = definition.Capacity,
                    Used = row.ProducerId.IsValid ? row.Used : 0,
                    MonthlyMaintenanceCost = definition.MonthlyMaintenanceCost
                });
            }

            var densityRows = new List<DensityBuildingReadModel>();
            var states = _module.Density.State.Buildings;
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                if (!_module.Runtime.Buildings.TryGet(state.BuildingId, out var building) ||
                    !_module.Profiles.TryGetDensityProfile(building.DefinitionId, out var profile)) continue;
                var multiplier = _module.Density.Parameters.CapacityMultiplier(state.Stage);
                _module.Water.State.TryGet(building.Id, out var water);
                var factors = new DensityFactorReadModel[state.Factors.Count];
                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++)
                {
                    var factor = state.Factors[factorIndex];
                    factors[factorIndex] = new DensityFactorReadModel
                    {
                        Kind = factor.Kind,
                        CurrentValue = factor.CurrentValue,
                        RequiredValue = factor.RequiredValue,
                        IsSatisfied = factor.IsSatisfied
                    };
                }
                densityRows.Add(new DensityBuildingReadModel
                {
                    BuildingId = building.Id,
                    Cell = building.Anchor,
                    Stage = (DensityStageView)(byte)state.Stage,
                    TargetStage = (DensityStageView)(byte)state.TargetStage,
                    UpgradeMonthsRemaining = state.UpgradeMonthsRemaining,
                    CapacityMultiplier = multiplier,
                    EffectiveHousingCapacity = checked(profile.BaseHousingCapacity * multiplier),
                    EffectiveJobCapacity = checked(profile.BaseJobCapacity * multiplier),
                    WaterSupplyPermille = water.BuildingId.IsValid ? water.SupplyPermille : 0,
                    WaterReason = water.BuildingId.IsValid ? (WaterSupplyReasonView)(byte)water.FailureReason : WaterSupplyReasonView.NoProducer,
                    TopReason = (DensityBlockReasonView)(byte)state.TopReason,
                    ProjectedSupplementalTax = ProjectedTax(building, profile, multiplier),
                    Factors = Array.AsReadOnly(factors)
                });
            }

            Snapshot = new WaterDensityReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                HasProcessedMonth = _module.Density.State.HasProcessedMonth,
                Year = _module.Density.State.HasProcessedMonth ? _module.Density.State.LastProcessedMonth.Year : 0,
                Month = _module.Density.State.HasProcessedMonth ? _module.Density.State.LastProcessedMonth.Month : 0,
                WaterRequested = _module.Water.State.RequestedTotal,
                WaterSupplied = _module.Water.State.SuppliedTotal,
                WaterChecksum = _module.Water.State.ComputeChecksum(),
                DensityChecksum = _module.Density.ComputeChecksum(),
                Producers = producers.AsReadOnly(),
                Buildings = densityRows.AsReadOnly()
            };
            _waterVersion = _module.Water.State.Version;
            _densityVersion = _module.Density.State.Version;
            _buildingVersion = _module.Runtime.Buildings.Version;
            _producerVersion = _module.WaterProducers.Version;
            _financeVersion = _module.Runtime.Finance.Version;
            return true;
        }

        private int ProjectedTax(BuildingState building, DensityBuildingProfile profile, int multiplier)
        {
            if (multiplier <= 1) return 0;
            var parameters = MunicipalFinanceParameters.Default;
            var rates = _module.Runtime.Finance.ActiveTaxRates;
            int units;
            int taxBase;
            int rate;
            switch (profile.Use)
            {
                case DensityUse.Residential:
                    units = building.Occupancy * (multiplier - 1);
                    taxBase = parameters.ResidentialTaxBasePerResident;
                    rate = rates.ResidentialPermille;
                    break;
                case DensityUse.Commercial:
                    units = building.FilledJobs * (multiplier - 1);
                    taxBase = parameters.CommercialTaxBasePerFilledJob;
                    rate = rates.CommercialPermille;
                    break;
                default:
                    units = building.FilledJobs * (multiplier - 1);
                    taxBase = parameters.IndustrialTaxBasePerFilledJob;
                    rate = rates.IndustrialPermille;
                    break;
            }
            return checked(units * taxBase * rate / UrbanEconomyTaxRates.MaxPermille);
        }
    }

    public enum WaterDensityOverlayKind : byte { Water, Density }

    public sealed class WaterDensitySelection
    {
        private static readonly WaterDensitySelection Instance = new WaterDensitySelection();
        public static WaterDensitySelection Current => Instance;
        public WaterDensityOverlayKind OverlayKind { get; private set; }
        public bool IsOverlayVisible { get; private set; }
        public CellCoord? SelectedCell { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;
        public void SetOverlay(WaterDensityOverlayKind kind, bool visible)
        {
            if (OverlayKind == kind && IsOverlayVisible == visible) return;
            OverlayKind = kind; IsOverlayVisible = visible; Touch();
        }
        public void SetSelectedCell(CellCoord? cell)
        {
            if (SelectedCell == cell) return;
            SelectedCell = cell; Touch();
        }
        private void Touch() { Version = checked(Version + 1); Changed?.Invoke(); }
    }

    [Serializable]
    public sealed class WaterDensityRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public WaterRuntimeSnapshotDto Water;
        public DensityRuntimeSnapshotDto Density;
    }

    public sealed class WaterDensityRuntimeModule
    {
        private readonly WaterDensityMonthlySystem _monthly;
        private readonly CompositeDensityCapacityResolver _capacityResolver;

        internal WaterDensityRuntimeModule(CityRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Services = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            Conditions = BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
            SpatialFields = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            WaterDefinitions = WaterProducerDefinitionCatalog.CreateDefault();
            WaterProducers = new WaterProducerCollection();
            Profiles = new RuntimeDensityProfileResolver(runtime);
            Network = new RuntimeWaterNetworkResolver(this);
            Water = new WaterUtilitySystem(WaterProducers, WaterDefinitions, Network);
            Eligibility = new RuntimeDensityEligibilityResolver(this);
            Density = new DensityUpgradeSystem(runtime.Buildings, Profiles, Eligibility);
            Commands = new WaterCommandHandler(runtime, WaterProducers, WaterDefinitions, Services);
            ReadModel = new WaterDensityReadModel(this);
            _capacityResolver = new CompositeDensityCapacityResolver(this);
            runtime.PopulationSystem.CapacityResolver = _capacityResolver;
            _monthly = new WaterDensityMonthlySystem(
                Water, Density, WaterProducers, WaterDefinitions, runtime, Profiles);
            runtime.MunicipalFinanceSystem.RegisterPostTripDemandSystem(_monthly);
        }

        public CityRuntime Runtime { get; }
        public PublicServiceRuntimeModule Services { get; }
        public BuildingConditionRuntimeModule Conditions { get; }
        public SpatialFieldRuntimeModule SpatialFields { get; }
        public WaterProducerDefinitionCatalog WaterDefinitions { get; }
        public WaterProducerCollection WaterProducers { get; }
        public RuntimeDensityProfileResolver Profiles { get; }
        public RuntimeWaterNetworkResolver Network { get; }
        public WaterUtilitySystem Water { get; }
        public RuntimeDensityEligibilityResolver Eligibility { get; }
        public DensityUpgradeSystem Density { get; }
        public WaterCommandHandler Commands { get; }
        public WaterDensityReadModel ReadModel { get; }

        public StableId ProducerIdFor(CellCoord cell)
        {
            if (!Runtime.World.IsInBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return new StableId(0xF720_0000_0000_0000UL |
                                (uint)(cell.Y * Runtime.World.Width + cell.X + 1));
        }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command)) return CommandExecutionResult.Unsupported("Unsupported water command.");
            var sequence = Runtime.Execute(command);
            if (sequence.Status != CommandExecutionStatus.Unsupported) return sequence;
            var result = Commands.Execute(new CommandEnvelope(sequence.Stamp, command)).WithStamp(sequence.Stamp);
            ReadModel.RefreshIfNeeded();
            return result;
        }

        public WaterDensityRuntimeSnapshotDto Capture()
        {
            var producerStates = WaterProducers.Producers;
            var producerRows = new WaterProducerStateDto[producerStates.Count];
            for (var index = 0; index < producerRows.Length; index++)
            {
                var producer = producerStates[index];
                producerRows[index] = new WaterProducerStateDto
                {
                    Id = producer.Id.Value,
                    DefinitionId = producer.DefinitionId.Value,
                    CellX = producer.Cell.X,
                    CellY = producer.Cell.Y
                };
            }
            var densityStates = Density.State.Buildings;
            var densityRows = new DensityBuildingStateDto[densityStates.Count];
            for (var index = 0; index < densityRows.Length; index++)
            {
                var state = densityStates[index];
                var factors = new DensityUpgradeFactorDto[state.Factors.Count];
                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++)
                {
                    factors[factorIndex] = new DensityUpgradeFactorDto
                    {
                        Kind = (int)state.Factors[factorIndex].Kind,
                        CurrentValue = state.Factors[factorIndex].CurrentValue,
                        RequiredValue = state.Factors[factorIndex].RequiredValue,
                        IsSatisfied = state.Factors[factorIndex].IsSatisfied
                    };
                }
                densityRows[index] = new DensityBuildingStateDto
                {
                    BuildingId = state.BuildingId.Value,
                    Stage = (int)state.Stage,
                    TargetStage = (int)state.TargetStage,
                    UpgradeMonthsRemaining = state.UpgradeMonthsRemaining,
                    StageAgeMonths = state.StageAgeMonths,
                    TopReason = (int)state.TopReason,
                    Factors = factors
                };
            }
            return new WaterDensityRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Water = new WaterRuntimeSnapshotDto { SchemaVersion = 1, Producers = producerRows },
                Density = new DensityRuntimeSnapshotDto
                {
                    SchemaVersion = 1,
                    HasProcessedMonth = Density.State.HasProcessedMonth,
                    LastProcessedYear = Density.State.HasProcessedMonth ? Density.State.LastProcessedMonth.Year : 0,
                    LastProcessedMonth = Density.State.HasProcessedMonth ? Density.State.LastProcessedMonth.Month : 0,
                    Buildings = densityRows
                }
            };
        }

        public void Restore(WaterDensityRuntimeSnapshotDto snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Water == null || snapshot.Density == null)
                throw new InvalidOperationException("Unsupported water-density snapshot.");
            if (snapshot.Water.SchemaVersion != 1 || snapshot.Density.SchemaVersion != 1)
                throw new InvalidOperationException("Unsupported water-density capability schema.");

            var producerDtos = snapshot.Water.Producers ?? Array.Empty<WaterProducerStateDto>();
            var producers = new List<WaterProducerState>(producerDtos.Length);
            ulong previousProducer = 0;
            for (var index = 0; index < producerDtos.Length; index++)
            {
                var row = producerDtos[index] ?? throw new ArgumentException("Water producer rows cannot be null.", nameof(snapshot));
                if (row.Id == 0 || row.Id <= previousProducer || !WaterDefinitions.TryGet(new StableId(row.DefinitionId), out _))
                    throw new ArgumentException("Water producer rows require ordered known IDs.", nameof(snapshot));
                var cell = new CellCoord(row.CellX, row.CellY);
                if (!Runtime.World.IsInBounds(cell)) throw new ArgumentException("Water producer cell is out of bounds.", nameof(snapshot));
                producers.Add(new WaterProducerState(new StableId(row.Id), new StableId(row.DefinitionId), cell));
                previousProducer = row.Id;
            }

            var densityDtos = snapshot.Density.Buildings ?? Array.Empty<DensityBuildingStateDto>();
            var densityRows = new List<DensityBuildingState>(densityDtos.Length);
            ulong previousBuilding = 0;
            for (var index = 0; index < densityDtos.Length; index++)
            {
                var row = densityDtos[index] ?? throw new ArgumentException("Density rows cannot be null.", nameof(snapshot));
                if (row.BuildingId == 0 || row.BuildingId <= previousBuilding ||
                    !Runtime.Buildings.TryGet(new StableId(row.BuildingId), out _))
                    throw new ArgumentException("Density rows require ordered known building IDs.", nameof(snapshot));
                if (!IsDefinedByteEnum<DensityStage>(row.Stage) ||
                    !IsDefinedByteEnum<DensityStage>(row.TargetStage) ||
                    !IsDefinedByteEnum<DensityUpgradeBlockReason>(row.TopReason))
                    throw new ArgumentException("Density row contains an unknown enum.", nameof(snapshot));
                var factorDtos = row.Factors ?? Array.Empty<DensityUpgradeFactorDto>();
                var factors = new DensityUpgradeFactor[factorDtos.Length];
                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++)
                {
                    var factor = factorDtos[factorIndex] ?? throw new ArgumentException("Density factors cannot contain null.", nameof(snapshot));
                    if (!IsDefinedByteEnum<DensityUpgradeFactorKind>(factor.Kind))
                        throw new ArgumentException("Density factor kind is unknown.", nameof(snapshot));
                    factors[factorIndex] = new DensityUpgradeFactor(
                        (DensityUpgradeFactorKind)factor.Kind,
                        factor.CurrentValue,
                        factor.RequiredValue,
                        factor.IsSatisfied);
                }
                var state = new DensityBuildingState(new StableId(row.BuildingId));
                state.Restore(
                    (DensityStage)row.Stage,
                    (DensityStage)row.TargetStage,
                    row.UpgradeMonthsRemaining,
                    row.StageAgeMonths,
                    (DensityUpgradeBlockReason)row.TopReason,
                    Array.AsReadOnly(factors));
                densityRows.Add(state);
                previousBuilding = row.BuildingId;
            }

            WaterProducers.ClearAndRestore(producers);
            Density.State.ClearAndRestore(
                densityRows,
                snapshot.Density.HasProcessedMonth,
                snapshot.Density.HasProcessedMonth
                    ? new SimulationDate(snapshot.Density.LastProcessedYear, snapshot.Density.LastProcessedMonth)
                    : default);
            Runtime.PopulationSystem.CapacityResolver = _capacityResolver;
            ReadModel.RefreshIfNeeded();
        }

        private static bool IsDefinedByteEnum<TEnum>(int value) where TEnum : struct
        {
            if (value < byte.MinValue || value > byte.MaxValue)
                return false;
            return Enum.IsDefined(typeof(TEnum), Enum.ToObject(typeof(TEnum), (byte)value));
        }

        public sealed class RuntimeDensityProfileResolver : IDensityBuildingProfileResolver
        {
            private readonly CityRuntime _runtime;
            public RuntimeDensityProfileResolver(CityRuntime runtime) { _runtime = runtime; }
            public bool TryGetDensityProfile(StableId definitionId, out DensityBuildingProfile profile)
            {
                if (!_runtime.BuildingDefinitions.TryGet(definitionId, out var definition))
                {
                    profile = default;
                    return false;
                }
                switch (definition.ZoneType)
                {
                    case ZoneType.ResidentialLow:
                        profile = new DensityBuildingProfile(DensityUse.Residential, definition.OccupancyCapacity, 0);
                        return true;
                    case ZoneType.CommercialLow:
                        profile = new DensityBuildingProfile(DensityUse.Commercial, 0, definition.JobCapacity);
                        return true;
                    case ZoneType.IndustrialLow:
                        profile = new DensityBuildingProfile(DensityUse.Industrial, 0, definition.JobCapacity);
                        return true;
                    default:
                        profile = default;
                        return false;
                }
            }
        }

        public sealed class RuntimeWaterNetworkResolver : IWaterNetworkResolver
        {
            private readonly WaterDensityRuntimeModule _module;
            public RuntimeWaterNetworkResolver(WaterDensityRuntimeModule module) { _module = module; }

            public IReadOnlyList<WaterConsumerNode> GetConsumers()
            {
                var runtime = _module.Runtime;
                runtime.RoadGraph.RefreshIfNeeded();
                runtime.Parcels.RefreshIfNeeded();
                var rows = new List<WaterConsumerNode>();
                var buildings = runtime.Buildings.Buildings;
                for (var index = 0; index < buildings.Count; index++)
                {
                    var building = buildings[index];
                    if (!_module.Profiles.TryGetDensityProfile(building.DefinitionId, out var profile)) continue;
                    var multiplier = _module.Density.CapacityMultiplier(building.Id);
                    var baseDemand = profile.Use == DensityUse.Residential
                        ? building.Occupancy
                        : building.FilledJobs;
                    var demand = Math.Max(1, checked(baseDemand * multiplier));
                    var component = ResolveBuildingComponent(building);
                    rows.Add(new WaterConsumerNode(building.Id, building.Anchor, component, demand));
                }
                rows.Sort();
                return rows.AsReadOnly();
            }

            public WaterProducerNetworkNode ResolveProducer(
                WaterProducerState producer,
                WaterProducerDefinition definition)
            {
                var component = ResolveAdjacentComponent(producer.Cell);
                return new WaterProducerNetworkNode(producer.Id, component, definition.Capacity);
            }

            private int ResolveBuildingComponent(BuildingState building)
            {
                var runtime = _module.Runtime;
                if (!runtime.Parcels.TryGet(building.ParcelId, out var parcel)) return -1;
                var frontages = parcel.Frontages;
                for (var index = 0; index < frontages.Count; index++)
                {
                    var connectivity = runtime.RoadGraph.Get(frontages[index].RoadCell);
                    if (connectivity.HasRoad) return connectivity.ComponentId;
                }
                return -1;
            }

            private int ResolveAdjacentComponent(CellCoord cell)
            {
                var runtime = _module.Runtime;
                runtime.RoadGraph.RefreshIfNeeded();
                var candidates = new[]
                {
                    new CellCoord(cell.X, cell.Y - 1), new CellCoord(cell.X - 1, cell.Y),
                    new CellCoord(cell.X + 1, cell.Y), new CellCoord(cell.X, cell.Y + 1)
                };
                Array.Sort(candidates);
                for (var index = 0; index < candidates.Length; index++)
                {
                    if (!runtime.World.IsInBounds(candidates[index])) continue;
                    var connectivity = runtime.RoadGraph.Get(candidates[index]);
                    if (connectivity.HasRoad) return connectivity.ComponentId;
                }
                return -1;
            }
        }

        public sealed class RuntimeDensityEligibilityResolver : IDensityEligibilityResolver
        {
            private readonly WaterDensityRuntimeModule _module;
            public RuntimeDensityEligibilityResolver(WaterDensityRuntimeModule module) { _module = module; }

            public DensityEligibility Resolve(
                BuildingState building,
                DensityBuildingProfile profile,
                DensityStage currentStage,
                DensityStage targetStage)
            {
                var parameters = _module.Density.Parameters;
                var population = _module.Runtime.PopulationSystem.Population.TotalPopulation;
                _module.Water.State.TryGet(building.Id, out var water);
                var waterPermille = water.BuildingId.IsValid ? water.SupplyPermille : 0;
                var landValue = AverageLandValue(building);
                var roadTier = RoadTier(building);
                var service = AverageServiceCoverage(building.Id);
                var factors = new[]
                {
                    new DensityUpgradeFactor(DensityUpgradeFactorKind.CityPopulation,
                        population, parameters.RequiredPopulation(targetStage),
                        population >= parameters.RequiredPopulation(targetStage)),
                    new DensityUpgradeFactor(DensityUpgradeFactorKind.WaterSupply,
                        waterPermille, parameters.RequiredWater(targetStage),
                        waterPermille >= parameters.RequiredWater(targetStage)),
                    new DensityUpgradeFactor(DensityUpgradeFactorKind.LandValue,
                        landValue, parameters.RequiredLandValue(targetStage),
                        landValue >= parameters.RequiredLandValue(targetStage)),
                    new DensityUpgradeFactor(DensityUpgradeFactorKind.RoadTier,
                        roadTier, parameters.RequiredRoadTier(targetStage),
                        roadTier >= parameters.RequiredRoadTier(targetStage)),
                    new DensityUpgradeFactor(DensityUpgradeFactorKind.ServiceCoverage,
                        service, parameters.RequiredService(targetStage),
                        service >= parameters.RequiredService(targetStage))
                };
                var reason = DensityUpgradeBlockReason.None;
                if (!factors[0].IsSatisfied) reason = DensityUpgradeBlockReason.CityPopulationTooLow;
                else if (!factors[1].IsSatisfied) reason = DensityUpgradeBlockReason.NoWater;
                else if (!factors[2].IsSatisfied) reason = DensityUpgradeBlockReason.LandValueTooLow;
                else if (!factors[3].IsSatisfied) reason = DensityUpgradeBlockReason.RoadTierTooLow;
                else if (!factors[4].IsSatisfied) reason = DensityUpgradeBlockReason.ServicesTooLow;
                return new DensityEligibility(targetStage, reason, Array.AsReadOnly(factors));
            }

            private int AverageLandValue(BuildingState building)
            {
                _module.SpatialFields.RefreshIfNeeded();
                long total = 0;
                var count = 0;
                building.Footprint.ForEachCell(building.Anchor, cell =>
                {
                    total += _module.SpatialFields.Fields.Get(cell).LandValue;
                    count++;
                });
                return count == 0 ? 0 : (int)(total / count);
            }

            private int RoadTier(BuildingState building)
            {
                var runtime = _module.Runtime;
                runtime.Parcels.RefreshIfNeeded();
                if (!runtime.Parcels.TryGet(building.ParcelId, out var parcel)) return 0;
                var frontages = parcel.Frontages;
                for (var index = 0; index < frontages.Count; index++)
                {
                    var road = runtime.Roads.Get(frontages[index].RoadCell);
                    if (!road.IsOccupied) continue;
                    if (road.DefinitionId == RoadHierarchyDefaults.AvenueDefinitionId) return 2;
                    if (road.DefinitionId == RoadHierarchyDefaults.StreetDefinitionId) return 1;
                    return 0;
                }
                return 0;
            }

            private int AverageServiceCoverage(StableId buildingId)
            {
                _module.Services.ReadModel.RefreshIfNeeded();
                var rows = _module.Services.ReadModel.Snapshot.Coverage;
                var total = 0;
                var count = 0;
                for (var index = 0; index < rows.Count; index++)
                {
                    if (rows[index].BuildingId != buildingId) continue;
                    total += rows[index].CoveragePermille;
                    count++;
                }
                return count == 0 ? 0 : total / count;
            }
        }

        private sealed class CompositeDensityCapacityResolver : IBuildingCapacityResolver
        {
            private readonly WaterDensityRuntimeModule _module;
            public CompositeDensityCapacityResolver(WaterDensityRuntimeModule module) { _module = module; }
            public int ResolveHousingCapacity(BuildingState building, int definitionCapacity)
            {
                var expanded = checked(definitionCapacity * _module.Density.CapacityMultiplier(building.Id));
                return _module.Conditions.System.ResolveHousingCapacity(building, expanded);
            }
            public int ResolveJobCapacity(BuildingState building, int definitionCapacity)
            {
                var expanded = checked(definitionCapacity * _module.Density.CapacityMultiplier(building.Id));
                return _module.Conditions.System.ResolveJobCapacity(building, expanded);
            }
        }
    }

    public static class WaterDensityRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, WaterDensityRuntimeModule> Modules =
            new ConditionalWeakTable<CityRuntime, WaterDensityRuntimeModule>();
        public static WaterDensityRuntimeModule GetOrCreate(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new WaterDensityRuntimeModule(runtime);
            Modules.Add(runtime, created);
            return created;
        }
    }
}
