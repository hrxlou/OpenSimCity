using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Conditions;

namespace OpenSimCity.Application.Conditions
{
    public sealed class RequestBuildingRedevelopmentCommand : ICityCommand
    {
        public RequestBuildingRedevelopmentCommand(StableId buildingId)
        {
            BuildingId = buildingId;
        }
        public StableId BuildingId { get; }
    }

    public enum BuildingConditionCommandFailureReason : byte
    {
        None,
        InvalidCommand,
        BuildingNotFound,
        NotAbandoned,
        NoRoadAccess,
        NoPower,
        EnvironmentNotRecovered,
        RecoveryDelay,
        AlreadyRedeveloping,
        InsufficientFunds,
        StateChangedBeforeCommit
    }

    [Serializable]
    public sealed class BuildingConditionCommandResult
    {
        private BuildingConditionCommandResult(
            bool isValid,
            bool isApplied,
            BuildingConditionCommandFailureReason failureReason,
            StableId buildingId,
            int cost,
            int fundsBefore,
            int fundsAfter,
            string message)
        {
            IsValid = isValid;
            IsApplied = isApplied;
            FailureReason = failureReason;
            BuildingId = buildingId;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public bool IsApplied { get; }
        public BuildingConditionCommandFailureReason FailureReason { get; }
        public StableId BuildingId { get; }
        public int Cost { get; }
        public int FundsBefore { get; }
        public int FundsAfter { get; }
        public string Message { get; }

        public static BuildingConditionCommandResult Valid(
            StableId buildingId,
            int cost,
            int funds) =>
            new BuildingConditionCommandResult(true, false,
                BuildingConditionCommandFailureReason.None,
                buildingId, cost, funds, funds, string.Empty);

        public static BuildingConditionCommandResult Applied(
            StableId buildingId,
            int cost,
            int fundsBefore,
            int fundsAfter) =>
            new BuildingConditionCommandResult(true, true,
                BuildingConditionCommandFailureReason.None,
                buildingId, cost, fundsBefore, fundsAfter, string.Empty);

        public static BuildingConditionCommandResult Rejected(
            StableId buildingId,
            BuildingConditionCommandFailureReason reason,
            int cost,
            int funds) =>
            new BuildingConditionCommandResult(false, false, reason,
                buildingId, cost, funds, funds, MessageFor(reason));

        public static BuildingConditionCommandFailureReason FromBlock(
            RedevelopmentBlockReason reason)
        {
            switch (reason)
            {
                case RedevelopmentBlockReason.NoRoadAccess:
                    return BuildingConditionCommandFailureReason.NoRoadAccess;
                case RedevelopmentBlockReason.NoPower:
                    return BuildingConditionCommandFailureReason.NoPower;
                case RedevelopmentBlockReason.EnvironmentNotRecovered:
                    return BuildingConditionCommandFailureReason.EnvironmentNotRecovered;
                case RedevelopmentBlockReason.RecoveryDelay:
                    return BuildingConditionCommandFailureReason.RecoveryDelay;
                case RedevelopmentBlockReason.AlreadyRedeveloping:
                    return BuildingConditionCommandFailureReason.AlreadyRedeveloping;
                default:
                    return BuildingConditionCommandFailureReason.NotAbandoned;
            }
        }

        private static string MessageFor(BuildingConditionCommandFailureReason reason)
        {
            switch (reason)
            {
                case BuildingConditionCommandFailureReason.BuildingNotFound:
                    return "건물을 찾을 수 없습니다.";
                case BuildingConditionCommandFailureReason.NotAbandoned:
                    return "폐건물만 재개발을 요청할 수 있습니다.";
                case BuildingConditionCommandFailureReason.NoRoadAccess:
                    return "도로·외부 연결을 먼저 복구해야 합니다.";
                case BuildingConditionCommandFailureReason.NoPower:
                    return "전력 공급을 먼저 복구해야 합니다.";
                case BuildingConditionCommandFailureReason.EnvironmentNotRecovered:
                    return "오염·소음·범죄 또는 토지가치 조건이 아직 회복되지 않았습니다.";
                case BuildingConditionCommandFailureReason.RecoveryDelay:
                    return "회복 조건을 유지하는 추가 기간이 필요합니다.";
                case BuildingConditionCommandFailureReason.AlreadyRedeveloping:
                    return "이미 재개발이 진행 중입니다.";
                case BuildingConditionCommandFailureReason.InsufficientFunds:
                    return "재개발 비용을 지불할 시 예산이 부족합니다.";
                case BuildingConditionCommandFailureReason.StateChangedBeforeCommit:
                    return "미리보기 이후 건물 상태가 변경되었습니다.";
                default:
                    return "재개발 명령이 유효하지 않습니다.";
            }
        }
    }
}
