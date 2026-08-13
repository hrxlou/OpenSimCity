using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Transit
{
    public sealed class PlaceTransitStopCommand : ICityCommand
    {
        public PlaceTransitStopCommand(TransitStopId stopId, CellCoord roadCell, string name)
        {
            StopId = stopId;
            RoadCell = roadCell;
            Name = name ?? string.Empty;
        }
        public TransitStopId StopId { get; }
        public CellCoord RoadCell { get; }
        public string Name { get; }
    }

    public sealed class RemoveTransitStopCommand : ICityCommand
    {
        public RemoveTransitStopCommand(TransitStopId stopId) { StopId = stopId; }
        public TransitStopId StopId { get; }
    }

    public sealed class UpsertTransitLineCommand : ICityCommand
    {
        private readonly TransitStopId[] _stopIds;
        private readonly IReadOnlyList<TransitStopId> _readOnlyStopIds;

        public UpsertTransitLineCommand(
            TransitLineId lineId,
            string name,
            IReadOnlyList<TransitStopId> stopIds,
            int serviceUnits,
            bool isEnabled = true)
        {
            if (stopIds == null)
                throw new ArgumentNullException(nameof(stopIds));
            _stopIds = new TransitStopId[stopIds.Count];
            for (var index = 0; index < stopIds.Count; index++)
                _stopIds[index] = stopIds[index];
            _readOnlyStopIds = Array.AsReadOnly(_stopIds);
            LineId = lineId;
            Name = name ?? string.Empty;
            ServiceUnits = serviceUnits;
            IsEnabled = isEnabled;
        }

        public TransitLineId LineId { get; }
        public string Name { get; }
        public IReadOnlyList<TransitStopId> StopIds => _readOnlyStopIds;
        public int ServiceUnits { get; }
        public bool IsEnabled { get; }
    }

    public sealed class DeleteTransitLineCommand : ICityCommand
    {
        public DeleteTransitLineCommand(TransitLineId lineId) { LineId = lineId; }
        public TransitLineId LineId { get; }
    }

    public sealed class SetTransitBudgetCommand : ICityCommand
    {
        public SetTransitBudgetCommand(int budgetPermille) { BudgetPermille = budgetPermille; }
        public int BudgetPermille { get; }
    }

    public enum TransitCommandOperation : byte
    {
        PlaceStop,
        RemoveStop,
        UpsertLine,
        DeleteLine,
        SetBudget
    }

    public enum TransitCommandFailureReason : byte
    {
        None,
        InvalidCommand,
        OutOfBounds,
        RoadRequired,
        DuplicateStopId,
        StopCellOccupied,
        StopNotFound,
        StopInUse,
        LineNotFound,
        InvalidStopOrder,
        UnknownStop,
        BrokenRoadPath,
        InvalidServiceUnits,
        InvalidBudget,
        InsufficientFunds,
        StateChangedBeforeCommit
    }

    public sealed class TransitCommandResult
    {
        private TransitCommandResult(
            TransitCommandOperation operation,
            bool isValid,
            bool isApplied,
            TransitCommandFailureReason failureReason,
            int cost,
            int fundsBefore,
            int fundsAfter,
            TransitStopId stopId,
            TransitLineId lineId,
            string message)
        {
            Operation = operation;
            IsValid = isValid;
            IsApplied = isApplied;
            FailureReason = failureReason;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            StopId = stopId;
            LineId = lineId;
            Message = message ?? string.Empty;
        }

        public TransitCommandOperation Operation { get; }
        public bool IsValid { get; }
        public bool IsApplied { get; }
        public TransitCommandFailureReason FailureReason { get; }
        public int Cost { get; }
        public int FundsBefore { get; }
        public int FundsAfter { get; }
        public TransitStopId StopId { get; }
        public TransitLineId LineId { get; }
        public string Message { get; }

        public static TransitCommandResult Valid(
            TransitCommandOperation operation,
            int cost,
            int funds,
            TransitStopId stopId = default(TransitStopId),
            TransitLineId lineId = default(TransitLineId)) =>
            new TransitCommandResult(operation, true, false, TransitCommandFailureReason.None,
                cost, funds, funds, stopId, lineId, string.Empty);

        public static TransitCommandResult Applied(
            TransitCommandOperation operation,
            int cost,
            int fundsBefore,
            int fundsAfter,
            TransitStopId stopId = default(TransitStopId),
            TransitLineId lineId = default(TransitLineId)) =>
            new TransitCommandResult(operation, true, true, TransitCommandFailureReason.None,
                cost, fundsBefore, fundsAfter, stopId, lineId, string.Empty);

        public static TransitCommandResult Rejected(
            TransitCommandOperation operation,
            TransitCommandFailureReason reason,
            int cost,
            int funds,
            TransitStopId stopId = default(TransitStopId),
            TransitLineId lineId = default(TransitLineId)) =>
            new TransitCommandResult(operation, false, false, reason, cost, funds, funds,
                stopId, lineId, MessageFor(reason));

        private static string MessageFor(TransitCommandFailureReason reason)
        {
            switch (reason)
            {
                case TransitCommandFailureReason.OutOfBounds: return "정류장 위치가 도시 경계 밖입니다.";
                case TransitCommandFailureReason.RoadRequired: return "정류장은 도로 셀에만 배치할 수 있습니다.";
                case TransitCommandFailureReason.DuplicateStopId: return "이미 존재하는 정류장 ID입니다.";
                case TransitCommandFailureReason.StopCellOccupied: return "해당 도로 셀에 이미 정류장이 있습니다.";
                case TransitCommandFailureReason.StopNotFound: return "정류장을 찾을 수 없습니다.";
                case TransitCommandFailureReason.StopInUse: return "노선이 사용하는 정류장은 먼저 노선에서 제거해야 합니다.";
                case TransitCommandFailureReason.LineNotFound: return "노선을 찾을 수 없습니다.";
                case TransitCommandFailureReason.InvalidStopOrder: return "노선에는 중복 없는 정류장 두 곳 이상이 필요합니다.";
                case TransitCommandFailureReason.UnknownStop: return "노선이 존재하지 않는 정류장을 참조합니다.";
                case TransitCommandFailureReason.BrokenRoadPath: return "연속 정류장 사이에 주행 가능한 도로 경로가 없습니다.";
                case TransitCommandFailureReason.InvalidServiceUnits: return "운행 대수는 1 이상이어야 합니다.";
                case TransitCommandFailureReason.InvalidBudget: return "운행 예산은 0~1000 범위여야 합니다.";
                case TransitCommandFailureReason.InsufficientFunds: return "시 예산이 부족합니다.";
                case TransitCommandFailureReason.StateChangedBeforeCommit: return "미리보기 이후 상태가 변경되었습니다.";
                default: return "대중교통 명령이 유효하지 않습니다.";
            }
        }
    }
}
