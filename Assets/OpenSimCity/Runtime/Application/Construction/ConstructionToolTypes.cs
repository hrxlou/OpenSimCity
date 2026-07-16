using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Construction
{
    /// <summary>
    /// Top-level construction tools exposed by the Tool Rail. Each maps to one
    /// command family: straight roads, road demolition, low-density R/C/I zoning
    /// and zone clearing. No growth, finance, traffic or service tool is exposed
    /// in P2.
    /// </summary>
    public enum ConstructionToolMode
    {
        None,
        Road,
        Demolish,
        ZoneResidential,
        ZoneCommercial,
        ZoneIndustrial,
        ZoneClear
    }

    public enum ConstructionOperation
    {
        None,
        PlaceRoad,
        DemolishRoad,
        PaintZone,
        ClearZone
    }

    public enum ConstructionDragPhase
    {
        Idle,
        Dragging
    }

    public static class ConstructionToolModeExtensions
    {
        public static bool IsZoneTool(this ConstructionToolMode mode)
        {
            return mode == ConstructionToolMode.ZoneResidential ||
                   mode == ConstructionToolMode.ZoneCommercial ||
                   mode == ConstructionToolMode.ZoneIndustrial ||
                   mode == ConstructionToolMode.ZoneClear;
        }

        public static bool IsPaintZoneTool(this ConstructionToolMode mode)
        {
            return mode == ConstructionToolMode.ZoneResidential ||
                   mode == ConstructionToolMode.ZoneCommercial ||
                   mode == ConstructionToolMode.ZoneIndustrial;
        }

        public static ConstructionOperation ToOperation(this ConstructionToolMode mode)
        {
            switch (mode)
            {
                case ConstructionToolMode.Road:
                    return ConstructionOperation.PlaceRoad;
                case ConstructionToolMode.Demolish:
                    return ConstructionOperation.DemolishRoad;
                case ConstructionToolMode.ZoneResidential:
                case ConstructionToolMode.ZoneCommercial:
                case ConstructionToolMode.ZoneIndustrial:
                    return ConstructionOperation.PaintZone;
                case ConstructionToolMode.ZoneClear:
                    return ConstructionOperation.ClearZone;
                default:
                    return ConstructionOperation.None;
            }
        }

        public static ZoneType ToZoneType(this ConstructionToolMode mode)
        {
            switch (mode)
            {
                case ConstructionToolMode.ZoneResidential:
                    return ZoneType.ResidentialLow;
                case ConstructionToolMode.ZoneCommercial:
                    return ZoneType.CommercialLow;
                case ConstructionToolMode.ZoneIndustrial:
                    return ZoneType.IndustrialLow;
                default:
                    return ZoneType.None;
            }
        }
    }

    /// <summary>
    /// Immutable, framework-independent snapshot of the in-progress placement.
    /// It carries the cost, predicted balance, the reason-free valid cells and the
    /// rejected cells so overlays and the inspector describe both success and
    /// failure without touching simulation state.
    /// </summary>
    public sealed class ConstructionPreview
    {
        public static readonly ConstructionPreview Empty = new ConstructionPreview(
            ConstructionToolMode.None,
            ConstructionOperation.None,
            hasPreview: false,
            isValid: false,
            cost: 0,
            fundsBefore: 0,
            fundsAfter: 0,
            start: CellCoord.Zero,
            end: CellCoord.Zero,
            requestedCells: Array.Empty<CellCoord>(),
            validCells: Array.Empty<CellCoord>(),
            rejectedCells: Array.Empty<CellCoord>(),
            failureCode: string.Empty,
            failureReason: string.Empty);

        public ConstructionPreview(
            ConstructionToolMode mode,
            ConstructionOperation operation,
            bool hasPreview,
            bool isValid,
            int cost,
            int fundsBefore,
            int fundsAfter,
            CellCoord start,
            CellCoord end,
            IReadOnlyList<CellCoord> requestedCells,
            IReadOnlyList<CellCoord> validCells,
            IReadOnlyList<CellCoord> rejectedCells,
            string failureCode,
            string failureReason)
        {
            Mode = mode;
            Operation = operation;
            HasPreview = hasPreview;
            IsValid = isValid;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            Start = start;
            End = end;
            RequestedCells = requestedCells ?? Array.Empty<CellCoord>();
            ValidCells = validCells ?? Array.Empty<CellCoord>();
            RejectedCells = rejectedCells ?? Array.Empty<CellCoord>();
            FailureCode = failureCode ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public ConstructionToolMode Mode { get; }

        public ConstructionOperation Operation { get; }

        public bool HasPreview { get; }

        public bool IsValid { get; }

        public int Cost { get; }

        public int FundsBefore { get; }

        public int FundsAfter { get; }

        public CellCoord Start { get; }

        public CellCoord End { get; }

        public IReadOnlyList<CellCoord> RequestedCells { get; }

        /// <summary>Cells the command would change if committed now.</summary>
        public IReadOnlyList<CellCoord> ValidCells { get; }

        /// <summary>Cells the command explicitly rejected, when the state knows them.</summary>
        public IReadOnlyList<CellCoord> RejectedCells { get; }

        public string FailureCode { get; }

        public string FailureReason { get; }
    }

    /// <summary>
    /// Result of committing the active drag. A committed result mirrors the last
    /// preview's cost and cells; a rejected commit leaves simulation state and the
    /// budget unchanged.
    /// </summary>
    public sealed class ConstructionCommitResult
    {
        public static readonly ConstructionCommitResult None = new ConstructionCommitResult(
            ConstructionToolMode.None,
            ConstructionOperation.None,
            committed: false,
            hadPreview: false,
            cost: 0,
            fundsBefore: 0,
            fundsAfter: 0,
            changedCells: Array.Empty<CellCoord>(),
            failureCode: string.Empty,
            message: string.Empty);

        public ConstructionCommitResult(
            ConstructionToolMode mode,
            ConstructionOperation operation,
            bool committed,
            bool hadPreview,
            int cost,
            int fundsBefore,
            int fundsAfter,
            IReadOnlyList<CellCoord> changedCells,
            string failureCode,
            string message)
        {
            Mode = mode;
            Operation = operation;
            Committed = committed;
            HadPreview = hadPreview;
            Cost = cost;
            FundsBefore = fundsBefore;
            FundsAfter = fundsAfter;
            ChangedCells = changedCells ?? Array.Empty<CellCoord>();
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ConstructionToolMode Mode { get; }

        public ConstructionOperation Operation { get; }

        public bool Committed { get; }

        public bool HadPreview { get; }

        public int Cost { get; }

        public int FundsBefore { get; }

        public int FundsAfter { get; }

        public IReadOnlyList<CellCoord> ChangedCells { get; }

        public string FailureCode { get; }

        public string Message { get; }
    }
}
