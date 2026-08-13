using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Services;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Services
{
    public sealed class PlaceServiceFacilityCommand : ICityCommand
    {
        public PlaceServiceFacilityCommand(StableId facilityId, StableId definitionId, CellCoord cell)
        {
            FacilityId = facilityId;
            DefinitionId = definitionId;
            Cell = cell;
        }
        public StableId FacilityId { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
    }

    public sealed class RemoveServiceFacilityCommand : ICityCommand
    {
        public RemoveServiceFacilityCommand(StableId facilityId) { FacilityId = facilityId; }
        public StableId FacilityId { get; }
    }

    public sealed class SetServiceBudgetCommand : ICityCommand
    {
        public SetServiceBudgetCommand(StableId facilityId, int budgetPermille)
        {
            FacilityId = facilityId;
            BudgetPermille = budgetPermille;
        }
        public StableId FacilityId { get; }
        public int BudgetPermille { get; }
    }

    public enum ServiceCommandFailureReason : byte
    {
        None,
        InvalidCommand,
        UnknownDefinition,
        FacilityNotFound,
        DuplicateFacility,
        CellOutOfBounds,
        CellOccupied,
        NoRoadAccess,
        InsufficientFunds,
        InvalidBudget,
        StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class ServiceCommandResult
    {
        private ServiceCommandResult(
            bool valid,
            bool applied,
            ServiceCommandFailureReason failure,
            StableId facilityId,
            int cost,
            string message)
        {
            IsValid = valid;
            IsApplied = applied;
            FailureReason = failure;
            FacilityId = facilityId;
            Cost = cost;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public bool IsApplied { get; }
        public ServiceCommandFailureReason FailureReason { get; }
        public StableId FacilityId { get; }
        public int Cost { get; }
        public string Message { get; }

        public static ServiceCommandResult Valid(StableId id, int cost) =>
            new ServiceCommandResult(true, false, ServiceCommandFailureReason.None, id, cost, string.Empty);
        public static ServiceCommandResult Applied(StableId id, int cost) =>
            new ServiceCommandResult(true, true, ServiceCommandFailureReason.None, id, cost, string.Empty);
        public static ServiceCommandResult Rejected(StableId id, ServiceCommandFailureReason reason, int cost = 0) =>
            new ServiceCommandResult(false, false, reason, id, cost, MessageFor(reason));

        private static string MessageFor(ServiceCommandFailureReason reason)
        {
            switch (reason)
            {
                case ServiceCommandFailureReason.UnknownDefinition: return "알 수 없는 공공서비스 시설입니다.";
                case ServiceCommandFailureReason.FacilityNotFound: return "공공서비스 시설을 찾을 수 없습니다.";
                case ServiceCommandFailureReason.DuplicateFacility: return "같은 stable ID의 시설이 이미 있습니다.";
                case ServiceCommandFailureReason.CellOutOfBounds: return "월드 경계 밖에는 시설을 배치할 수 없습니다.";
                case ServiceCommandFailureReason.CellOccupied: return "도로·건물·다른 시설과 겹칩니다.";
                case ServiceCommandFailureReason.NoRoadAccess: return "출입 가능한 인접 도로가 필요합니다.";
                case ServiceCommandFailureReason.InsufficientFunds: return "시설 건설 비용을 지불할 시 예산이 부족합니다.";
                case ServiceCommandFailureReason.InvalidBudget: return "시설 예산은 0%에서 150% 사이여야 합니다.";
                case ServiceCommandFailureReason.StateChangedBeforeCommit: return "미리보기 이후 시설 상태가 변경되었습니다.";
                default: return "공공서비스 명령이 유효하지 않습니다.";
            }
        }
    }

    public sealed class PublicServiceCommandHandler : ICityCommandHandler
    {
        private readonly CityRuntime _runtime;
        private readonly ServiceFacilityCollection _facilities;
        private readonly ServiceDefinitionCatalog _definitions;
        private readonly MunicipalFinanceState _finance;

        public PublicServiceCommandHandler(
            CityRuntime runtime,
            ServiceFacilityCollection facilities,
            ServiceDefinitionCatalog definitions)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _finance = runtime.Finance;
        }

        public bool CanHandle(ICityCommand command) =>
            command is PlaceServiceFacilityCommand ||
            command is RemoveServiceFacilityCommand ||
            command is SetServiceBudgetCommand;

        public ServiceCommandResult Preview(PlaceServiceFacilityCommand command)
        {
            if (command == null || !command.FacilityId.IsValid || !command.DefinitionId.IsValid)
                return ServiceCommandResult.Rejected(command?.FacilityId ?? default, ServiceCommandFailureReason.InvalidCommand);
            if (!_definitions.TryGet(command.DefinitionId, out var definition))
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.UnknownDefinition);
            if (!_runtime.World.IsInBounds(command.Cell))
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.CellOutOfBounds, definition.ConstructionCost);
            if (_facilities.TryGet(command.FacilityId, out _))
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.DuplicateFacility, definition.ConstructionCost);
            var existing = _facilities.Facilities;
            for (var index = 0; index < existing.Count; index++)
            {
                if (existing[index].Cell == command.Cell)
                    return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.CellOccupied, definition.ConstructionCost);
            }
            if (_runtime.Roads.HasRoad(command.Cell) || _runtime.Buildings.TryFindAt(command.Cell, out _))
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.CellOccupied, definition.ConstructionCost);
            if (!TryFindAdjacentRoad(command.Cell, out _))
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.NoRoadAccess, definition.ConstructionCost);
            if (_finance.Funds < definition.ConstructionCost)
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.InsufficientFunds, definition.ConstructionCost);
            return ServiceCommandResult.Valid(command.FacilityId, definition.ConstructionCost);
        }

        public ServiceCommandResult Preview(RemoveServiceFacilityCommand command)
        {
            if (command == null || !command.FacilityId.IsValid)
                return ServiceCommandResult.Rejected(command?.FacilityId ?? default, ServiceCommandFailureReason.InvalidCommand);
            return _facilities.TryGet(command.FacilityId, out _)
                ? ServiceCommandResult.Valid(command.FacilityId, 0)
                : ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.FacilityNotFound);
        }

        public ServiceCommandResult Preview(SetServiceBudgetCommand command)
        {
            if (command == null || !command.FacilityId.IsValid)
                return ServiceCommandResult.Rejected(command?.FacilityId ?? default, ServiceCommandFailureReason.InvalidCommand);
            if (command.BudgetPermille < 0 || command.BudgetPermille > 1500)
                return ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.InvalidBudget);
            return _facilities.TryGet(command.FacilityId, out _)
                ? ServiceCommandResult.Valid(command.FacilityId, 0)
                : ServiceCommandResult.Rejected(command.FacilityId, ServiceCommandFailureReason.FacilityNotFound);
        }

        public CommandValidationResult Validate(CommandEnvelope envelope)
        {
            ServiceCommandResult result;
            if (envelope.Command is PlaceServiceFacilityCommand place) result = Preview(place);
            else if (envelope.Command is RemoveServiceFacilityCommand remove) result = Preview(remove);
            else if (envelope.Command is SetServiceBudgetCommand budget) result = Preview(budget);
            else return CommandValidationResult.Unsupported("No public-service handler can execute this command.");
            return result.IsValid ? CommandValidationResult.Valid : CommandValidationResult.Invalid(result.Message, result);
        }

        public CommandExecutionResult Execute(CommandEnvelope envelope)
        {
            if (envelope.Command is PlaceServiceFacilityCommand place)
            {
                var preview = Preview(place);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                if (!_finance.TrySpend(preview.Cost))
                    return CommandExecutionResult.Rejected("시설 건설 비용을 지불할 수 없습니다.");
                if (!_facilities.Add(new ServiceFacilityState(place.FacilityId, place.DefinitionId, place.Cell)))
                {
                    _finance.TryRefund(preview.Cost);
                    var failed = ServiceCommandResult.Rejected(place.FacilityId, ServiceCommandFailureReason.StateChangedBeforeCommit, preview.Cost);
                    return CommandExecutionResult.Rejected(failed.Message).WithDetails(failed);
                }
                _finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, preview.Cost);
                return CommandExecutionResult.Applied(ServiceCommandResult.Applied(place.FacilityId, preview.Cost));
            }
            if (envelope.Command is RemoveServiceFacilityCommand remove)
            {
                var preview = Preview(remove);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                if (!_facilities.Remove(remove.FacilityId))
                    return CommandExecutionResult.Rejected("시설 상태가 변경되었습니다.");
                return CommandExecutionResult.Applied(ServiceCommandResult.Applied(remove.FacilityId, 0));
            }
            if (envelope.Command is SetServiceBudgetCommand budget)
            {
                var preview = Preview(budget);
                if (!preview.IsValid) return CommandExecutionResult.Rejected(preview.Message).WithDetails(preview);
                _facilities.TryGet(budget.FacilityId, out var facility);
                facility.ScheduleBudget(budget.BudgetPermille, _runtime.MonthlyScheduler.NextMonth);
                _facilities.Touch();
                return CommandExecutionResult.Applied(ServiceCommandResult.Applied(budget.FacilityId, 0));
            }
            return CommandExecutionResult.Unsupported("No public-service handler can execute this command.");
        }

        private bool TryFindAdjacentRoad(CellCoord cell, out CellCoord roadCell)
        {
            var candidates = new[]
            {
                new CellCoord(cell.X, cell.Y - 1),
                new CellCoord(cell.X - 1, cell.Y),
                new CellCoord(cell.X + 1, cell.Y),
                new CellCoord(cell.X, cell.Y + 1)
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

    internal sealed class PublicServiceApplicationMonthlySystem : IMonthlySimulationSystem
    {
        private readonly ServiceFacilityCollection _facilities;
        private readonly ServiceDefinitionCatalog _definitions;
        private readonly PublicServiceSystem _services;
        private readonly MunicipalFinanceState _finance;

        public PublicServiceApplicationMonthlySystem(
            ServiceFacilityCollection facilities,
            ServiceDefinitionCatalog definitions,
            PublicServiceSystem services,
            MunicipalFinanceState finance)
        {
            _facilities = facilities;
            _definitions = definitions;
            _services = services;
            _finance = finance;
        }

        public void RunMonth(SimulationDate month)
        {
            long nominal = 0;
            var facilities = _facilities.Facilities;
            for (var index = 0; index < facilities.Count; index++)
            {
                var facility = facilities[index];
                var budget = facility.HasPendingBudget && month.CompareTo(facility.PendingEffectiveMonth) >= 0
                    ? facility.PendingBudgetPermille
                    : facility.ActiveBudgetPermille;
                nominal += (long)_definitions.Get(facility.DefinitionId).MonthlyOperatingCost * budget / 1000;
            }
            var funding = nominal <= 0
                ? 1000
                : (int)Math.Min(1000L, (long)_finance.Funds * 1000 / nominal);
            _services.AvailableFundingPermille = funding;
            _services.RunMonth(month);
            var actual = _services.State.MonthlyOperatingCost;
            if (actual <= 0) return;
            if (!_finance.TrySpend(actual))
                throw new InvalidOperationException("Service funding calculation exceeded available municipal funds.");
            _finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, actual);
        }
    }

    public enum ServiceKindView : byte { Police, Fire, School, Healthcare, Waste }
    public enum ServiceFailureReasonView : byte
    {
        None, NoFacility, NoRoadAccess, DifferentRoadComponent, OutOfRange, CapacityExceeded, BudgetTooLow
    }

    [Serializable]
    public sealed class ServiceFacilityReadModel
    {
        public StableId FacilityId;
        public StableId DefinitionId;
        public CellCoord Cell;
        public ServiceKindView Kind;
        public int ActiveBudgetPermille;
        public bool HasPendingBudget;
        public int PendingBudgetPermille;
        public int EffectiveBudgetPermille;
        public int Capacity;
        public int Usage;
        public int MaximumCost;
        public int MonthlyCost;
    }

    [Serializable]
    public sealed class ServiceCoverageReadModel
    {
        public StableId BuildingId;
        public CellCoord Cell;
        public ServiceKindView Kind;
        public StableId FacilityId;
        public int Requested;
        public int Served;
        public int CoveragePermille;
        public int TravelCost;
        public ServiceFailureReasonView FailureReason;
    }

    [Serializable]
    public sealed class PublicServiceReadModelSnapshot
    {
        public int Version;
        public bool HasProcessedMonth;
        public int Year;
        public int Month;
        public int RequestedTotal;
        public int ServedTotal;
        public int MonthlyCost;
        public ulong Checksum;
        public IReadOnlyList<ServiceFacilityReadModel> Facilities = Array.AsReadOnly(Array.Empty<ServiceFacilityReadModel>());
        public IReadOnlyList<ServiceCoverageReadModel> Coverage = Array.AsReadOnly(Array.Empty<ServiceCoverageReadModel>());
    }

    public sealed class PublicServiceReadModel
    {
        private readonly PublicServiceRuntimeModule _module;
        private int _facilityVersion = -1;
        private int _stateVersion = -1;
        private int _buildingVersion = -1;

        public PublicServiceReadModel(PublicServiceRuntimeModule module)
        {
            _module = module;
            Snapshot = new PublicServiceReadModelSnapshot();
        }

        public PublicServiceReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            if (_facilityVersion == _module.Facilities.Version &&
                _stateVersion == _module.System.State.Version &&
                _buildingVersion == _module.Runtime.Buildings.Version)
                return false;

            var usageById = new Dictionary<StableId, ServiceFacilityUsageRow>();
            var usage = _module.System.State.FacilityUsage;
            for (var index = 0; index < usage.Count; index++) usageById[usage[index].FacilityId] = usage[index];
            var facilityRows = new List<ServiceFacilityReadModel>();
            var facilities = _module.Facilities.Facilities;
            for (var index = 0; index < facilities.Count; index++)
            {
                var facility = facilities[index];
                var definition = _module.Definitions.Get(facility.DefinitionId);
                usageById.TryGetValue(facility.Id, out var row);
                facilityRows.Add(new ServiceFacilityReadModel
                {
                    FacilityId = facility.Id,
                    DefinitionId = facility.DefinitionId,
                    Cell = facility.Cell,
                    Kind = (ServiceKindView)(byte)definition.Kind,
                    ActiveBudgetPermille = facility.ActiveBudgetPermille,
                    HasPendingBudget = facility.HasPendingBudget,
                    PendingBudgetPermille = facility.PendingBudgetPermille,
                    EffectiveBudgetPermille = row.FacilityId.IsValid ? row.EffectiveBudgetPermille : 0,
                    Capacity = row.FacilityId.IsValid ? row.EffectiveCapacity : 0,
                    Usage = row.FacilityId.IsValid ? row.Usage : 0,
                    MaximumCost = row.FacilityId.IsValid ? row.EffectiveMaximumCost : definition.MaximumCost,
                    MonthlyCost = row.FacilityId.IsValid ? row.MonthlyCost : 0
                });
            }

            var coverageRows = new List<ServiceCoverageReadModel>();
            var coverage = _module.System.State.Coverage;
            for (var index = 0; index < coverage.Count; index++)
            {
                if (!_module.Runtime.Buildings.TryGet(coverage[index].BuildingId, out var building)) continue;
                coverageRows.Add(new ServiceCoverageReadModel
                {
                    BuildingId = coverage[index].BuildingId,
                    Cell = building.Anchor,
                    Kind = (ServiceKindView)(byte)coverage[index].Kind,
                    FacilityId = coverage[index].FacilityId,
                    Requested = coverage[index].Requested,
                    Served = coverage[index].Served,
                    CoveragePermille = coverage[index].CoveragePermille,
                    TravelCost = coverage[index].TravelCost,
                    FailureReason = (ServiceFailureReasonView)(byte)coverage[index].FailureReason
                });
            }

            Snapshot = new PublicServiceReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                HasProcessedMonth = _module.System.State.HasProcessedMonth,
                Year = _module.System.State.HasProcessedMonth ? _module.System.State.LastProcessedMonth.Year : 0,
                Month = _module.System.State.HasProcessedMonth ? _module.System.State.LastProcessedMonth.Month : 0,
                RequestedTotal = _module.System.State.RequestedTotal,
                ServedTotal = _module.System.State.ServedTotal,
                MonthlyCost = _module.System.State.MonthlyOperatingCost,
                Checksum = _module.System.State.ComputeChecksum(),
                Facilities = facilityRows.AsReadOnly(),
                Coverage = coverageRows.AsReadOnly()
            };
            _facilityVersion = _module.Facilities.Version;
            _stateVersion = _module.System.State.Version;
            _buildingVersion = _module.Runtime.Buildings.Version;
            return true;
        }
    }

    public sealed class PublicServiceSelection
    {
        private static readonly PublicServiceSelection Instance = new PublicServiceSelection();
        public static PublicServiceSelection Current => Instance;
        public ServiceKindView ActiveKind { get; private set; }
        public bool IsCoverageVisible { get; private set; }
        public CellCoord? SelectedCell { get; private set; }
        public StableId SelectedFacilityId { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;

        public void SetKind(ServiceKindView kind) { if (ActiveKind == kind) return; ActiveKind = kind; Touch(); }
        public void SetCoverageVisible(bool value) { if (IsCoverageVisible == value) return; IsCoverageVisible = value; Touch(); }
        public void SetSelectedCell(CellCoord? cell) { if (SelectedCell == cell) return; SelectedCell = cell; Touch(); }
        public void SelectFacility(StableId id) { if (SelectedFacilityId == id) return; SelectedFacilityId = id; Touch(); }
        private void Touch() { Version = checked(Version + 1); Changed?.Invoke(); }
    }

    public sealed class PublicServiceRuntimeModule
    {
        private readonly PublicServiceApplicationMonthlySystem _monthly;

        internal PublicServiceRuntimeModule(CityRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Definitions = ServiceDefinitionCatalog.CreateDefault();
            Facilities = new ServiceFacilityCollection();
            var demand = new RuntimeDemandProvider(runtime);
            var coverage = new RuntimeCoverageResolver(runtime);
            System = new PublicServiceSystem(Facilities, Definitions, demand, coverage);
            Commands = new PublicServiceCommandHandler(runtime, Facilities, Definitions);
            ReadModel = new PublicServiceReadModel(this);
            _monthly = new PublicServiceApplicationMonthlySystem(Facilities, Definitions, System, runtime.Finance);
            runtime.MunicipalFinanceSystem.RegisterPostTripDemandSystem(_monthly);
        }

        public CityRuntime Runtime { get; }
        public ServiceDefinitionCatalog Definitions { get; }
        public ServiceFacilityCollection Facilities { get; }
        public PublicServiceSystem System { get; }
        public PublicServiceCommandHandler Commands { get; }
        public PublicServiceReadModel ReadModel { get; }

        public StableId FacilityIdFor(StableId definitionId, CellCoord cell)
        {
            if (!Runtime.World.IsInBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            var index = Runtime.World.GetIndex(cell);
            return new StableId(0xF710_0000_0000_0000UL |
                                ((definitionId.Value & 0xFFUL) << 32) |
                                (uint)(index + 1));
        }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command)) return CommandExecutionResult.Unsupported("Unsupported service command.");
            var sequenceResult = Runtime.Execute(command);
            if (sequenceResult.Status != CommandExecutionStatus.Unsupported) return sequenceResult;
            var result = Commands.Execute(new CommandEnvelope(sequenceResult.Stamp, command)).WithStamp(sequenceResult.Stamp);
            ReadModel.RefreshIfNeeded();
            return result;
        }

        public PublicServiceRuntimeSnapshotDto Capture()
        {
            var facilities = Facilities.Facilities;
            var rows = new ServiceFacilityStateDto[facilities.Count];
            for (var index = 0; index < rows.Length; index++)
            {
                var facility = facilities[index];
                rows[index] = new ServiceFacilityStateDto
                {
                    Id = facility.Id.Value,
                    DefinitionId = facility.DefinitionId.Value,
                    CellX = facility.Cell.X,
                    CellY = facility.Cell.Y,
                    ActiveBudgetPermille = facility.ActiveBudgetPermille,
                    HasPendingBudget = facility.HasPendingBudget,
                    PendingBudgetPermille = facility.PendingBudgetPermille,
                    PendingEffectiveYear = facility.HasPendingBudget ? facility.PendingEffectiveMonth.Year : 0,
                    PendingEffectiveMonth = facility.HasPendingBudget ? facility.PendingEffectiveMonth.Month : 0
                };
            }
            return new PublicServiceRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                HasProcessedMonth = System.State.HasProcessedMonth,
                LastProcessedYear = System.State.HasProcessedMonth ? System.State.LastProcessedMonth.Year : 0,
                LastProcessedMonth = System.State.HasProcessedMonth ? System.State.LastProcessedMonth.Month : 0,
                Facilities = rows
            };
        }

        public void Restore(PublicServiceRuntimeSnapshotDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion != 1) throw new InvalidOperationException("Unsupported service snapshot schema.");
            var source = snapshot.Facilities ?? Array.Empty<ServiceFacilityStateDto>();
            var restored = new List<ServiceFacilityState>(source.Length);
            ulong previous = 0;
            for (var index = 0; index < source.Length; index++)
            {
                var row = source[index] ?? throw new ArgumentException("Service facility rows cannot be null.", nameof(snapshot));
                if (row.Id == 0 || row.Id <= previous || !Definitions.TryGet(new StableId(row.DefinitionId), out _))
                    throw new ArgumentException("Service facility rows require ordered known IDs.", nameof(snapshot));
                var cell = new CellCoord(row.CellX, row.CellY);
                if (!Runtime.World.IsInBounds(cell)) throw new ArgumentException("Service facility cell is out of bounds.", nameof(snapshot));
                var facility = new ServiceFacilityState(new StableId(row.Id), new StableId(row.DefinitionId), cell);
                facility.RestoreBudget(
                    row.ActiveBudgetPermille,
                    row.HasPendingBudget,
                    row.PendingBudgetPermille,
                    row.HasPendingBudget
                        ? new SimulationDate(row.PendingEffectiveYear, row.PendingEffectiveMonth)
                        : default);
                restored.Add(facility);
                previous = row.Id;
            }
            Facilities.ClearAndRestore(restored);
            ReadModel.RefreshIfNeeded();
        }

        private sealed class RuntimeDemandProvider : IServiceDemandProvider
        {
            private readonly CityRuntime _runtime;
            public RuntimeDemandProvider(CityRuntime runtime) { _runtime = runtime; }

            public IReadOnlyList<ServiceDemandNode> GetDemand(ServiceKind kind)
            {
                _runtime.RoadGraph.RefreshIfNeeded();
                _runtime.Parcels.RefreshIfNeeded();
                var rows = new List<ServiceDemandNode>();
                var buildings = _runtime.Buildings.Buildings;
                for (var index = 0; index < buildings.Count; index++)
                {
                    var building = buildings[index];
                    var amount = kind == ServiceKind.School
                        ? building.Occupancy
                        : checked(building.Occupancy + building.FilledJobs);
                    if (amount <= 0) continue;
                    var roadCell = building.Anchor;
                    var component = -1;
                    if (_runtime.Parcels.TryGet(building.ParcelId, out var parcel))
                    {
                        var frontages = parcel.Frontages;
                        for (var frontageIndex = 0; frontageIndex < frontages.Count; frontageIndex++)
                        {
                            var connectivity = _runtime.RoadGraph.Get(frontages[frontageIndex].RoadCell);
                            if (!connectivity.HasRoad) continue;
                            roadCell = frontages[frontageIndex].RoadCell;
                            component = connectivity.ComponentId;
                            break;
                        }
                    }
                    rows.Add(new ServiceDemandNode(building.Id, building.Anchor, roadCell, component, amount));
                }
                rows.Sort();
                return rows.AsReadOnly();
            }
        }

        private sealed class RuntimeCoverageResolver : IServiceCoverageCostResolver
        {
            private readonly CityRuntime _runtime;
            public RuntimeCoverageResolver(CityRuntime runtime) { _runtime = runtime; }

            public ServiceCoverageCostResult Resolve(
                ServiceFacilityState facility,
                ServiceDemandNode demand,
                ServiceCoverageStrategy strategy)
            {
                if (strategy == ServiceCoverageStrategy.SpatialDistance)
                    return ServiceCoverageCostResult.Reachable(facility.Cell.ManhattanDistanceTo(demand.Cell));
                if (!demand.HasRoadAccess || !TryFacilityRoad(facility.Cell, out var roadCell, out var component))
                    return ServiceCoverageCostResult.Unreachable(ServiceCoverageFailureReason.NoRoadAccess);
                if (component != demand.RoadComponentId)
                    return ServiceCoverageCostResult.Unreachable(ServiceCoverageFailureReason.DifferentRoadComponent);
                var routes = _runtime.TrafficRoutingSystem.Planner.GetRoutes(roadCell, demand.RoadCell);
                return routes.IsReachable
                    ? ServiceCoverageCostResult.Reachable(routes.Routes[0].FreeFlowTravelSeconds)
                    : ServiceCoverageCostResult.Unreachable(ServiceCoverageFailureReason.DifferentRoadComponent);
            }

            private bool TryFacilityRoad(CellCoord cell, out CellCoord roadCell, out int component)
            {
                _runtime.RoadGraph.RefreshIfNeeded();
                var candidates = new[]
                {
                    new CellCoord(cell.X, cell.Y - 1), new CellCoord(cell.X - 1, cell.Y),
                    new CellCoord(cell.X + 1, cell.Y), new CellCoord(cell.X, cell.Y + 1)
                };
                Array.Sort(candidates);
                for (var index = 0; index < candidates.Length; index++)
                {
                    if (!_runtime.World.IsInBounds(candidates[index])) continue;
                    var connectivity = _runtime.RoadGraph.Get(candidates[index]);
                    if (!connectivity.HasRoad) continue;
                    roadCell = candidates[index];
                    component = connectivity.ComponentId;
                    return true;
                }
                roadCell = default;
                component = -1;
                return false;
            }
        }
    }

    public static class PublicServiceRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, PublicServiceRuntimeModule> Modules =
            new ConditionalWeakTable<CityRuntime, PublicServiceRuntimeModule>();
        public static PublicServiceRuntimeModule GetOrCreate(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new PublicServiceRuntimeModule(runtime);
            Modules.Add(runtime, created);
            return created;
        }
    }
}
