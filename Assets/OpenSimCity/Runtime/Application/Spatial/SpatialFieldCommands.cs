using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Spatial
{
    public sealed class UpsertSpatialSourceCommand : ICityCommand
    {
        public UpsertSpatialSourceCommand(
            StableId sourceId,
            StableId definitionId,
            CellCoord cell,
            bool isEnabled = true)
        {
            SourceId = sourceId;
            DefinitionId = definitionId;
            Cell = cell;
            IsEnabled = isEnabled;
        }
        public StableId SourceId { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
        public bool IsEnabled { get; }
    }

    public sealed class RemoveSpatialSourceCommand : ICityCommand
    {
        public RemoveSpatialSourceCommand(StableId sourceId) { SourceId = sourceId; }
        public StableId SourceId { get; }
    }

    public enum SpatialFieldCommandOperation : byte
    {
        Upsert,
        Remove
    }

    public enum SpatialFieldCommandFailureReason : byte
    {
        None,
        InvalidCommand,
        ReservedDerivedId,
        UnknownDefinition,
        OutOfBounds,
        SourceNotFound,
        StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class SpatialFieldCommandResult
    {
        private SpatialFieldCommandResult(
            SpatialFieldCommandOperation operation,
            bool isValid,
            bool isApplied,
            SpatialFieldCommandFailureReason reason,
            StableId sourceId,
            StableId definitionId,
            CellCoord cell,
            string message)
        {
            Operation = operation;
            IsValid = isValid;
            IsApplied = isApplied;
            FailureReason = reason;
            SourceId = sourceId;
            DefinitionId = definitionId;
            Cell = cell;
            Message = message ?? string.Empty;
        }
        public SpatialFieldCommandOperation Operation { get; }
        public bool IsValid { get; }
        public bool IsApplied { get; }
        public SpatialFieldCommandFailureReason FailureReason { get; }
        public StableId SourceId { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
        public string Message { get; }

        public static SpatialFieldCommandResult Valid(
            SpatialFieldCommandOperation operation,
            StableId sourceId,
            StableId definitionId,
            CellCoord cell) =>
            new SpatialFieldCommandResult(operation, true, false,
                SpatialFieldCommandFailureReason.None, sourceId, definitionId, cell, string.Empty);

        public static SpatialFieldCommandResult Applied(
            SpatialFieldCommandOperation operation,
            StableId sourceId,
            StableId definitionId,
            CellCoord cell) =>
            new SpatialFieldCommandResult(operation, true, true,
                SpatialFieldCommandFailureReason.None, sourceId, definitionId, cell, string.Empty);

        public static SpatialFieldCommandResult Rejected(
            SpatialFieldCommandOperation operation,
            SpatialFieldCommandFailureReason reason,
            StableId sourceId,
            StableId definitionId,
            CellCoord cell) =>
            new SpatialFieldCommandResult(operation, false, false, reason,
                sourceId, definitionId, cell, MessageFor(reason));

        private static string MessageFor(SpatialFieldCommandFailureReason reason)
        {
            switch (reason)
            {
                case SpatialFieldCommandFailureReason.ReservedDerivedId:
                    return "도로·건물 파생 source ID는 직접 사용할 수 없습니다.";
                case SpatialFieldCommandFailureReason.UnknownDefinition:
                    return "알 수 없는 공간 source 정의입니다.";
                case SpatialFieldCommandFailureReason.OutOfBounds:
                    return "공간 source 위치가 도시 경계 밖입니다.";
                case SpatialFieldCommandFailureReason.SourceNotFound:
                    return "제거할 공간 source를 찾을 수 없습니다.";
                case SpatialFieldCommandFailureReason.StateChangedBeforeCommit:
                    return "미리보기 이후 공간 source 상태가 변경되었습니다.";
                default:
                    return "공간 필드 명령이 유효하지 않습니다.";
            }
        }
    }

    public static class SpatialDerivedSourceIds
    {
        private const ulong PrefixMask = 0xFFFF_0000_0000_0000UL;
        private const ulong PayloadMask = 0x0000_FFFF_FFFF_FFFFUL;
        private const ulong RoadPrefix = 0xF610_0000_0000_0000UL;
        private const ulong BuildingPrefix = 0xF620_0000_0000_0000UL;

        public static StableId Road(int cellIndex)
        {
            if (cellIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(cellIndex));
            return new StableId(RoadPrefix | (ulong)(cellIndex + 1));
        }

        public static StableId Building(StableId buildingId)
        {
            if (!buildingId.IsValid)
                throw new ArgumentException("A building source requires a stable building ID.", nameof(buildingId));
            var payload = buildingId.Value & PayloadMask;
            if (payload == 0UL) payload = 1UL;
            return new StableId(BuildingPrefix | payload);
        }

        public static bool IsDerived(StableId id)
        {
            var prefix = id.Value & PrefixMask;
            return prefix == RoadPrefix || prefix == BuildingPrefix;
        }
    }
}
