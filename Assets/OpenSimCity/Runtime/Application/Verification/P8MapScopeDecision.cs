using System;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Application.Verification
{
    public enum P8MapScopeKind : byte
    {
        Fixed64,
        ExpansionEligible
    }

    public enum P8MapScopeReason : byte
    {
        BenchmarkEvidenceIncomplete,
        BenchmarkBudgetFailed,
        TargetAndStressBudgetsPassed
    }

    public sealed class P8MapScopeDecisionResult
    {
        public P8MapScopeDecisionResult(P8MapScopeKind scope, P8MapScopeReason reason, string message)
        { Scope = scope; Reason = reason; Message = message ?? string.Empty; }
        public P8MapScopeKind Scope { get; }
        public P8MapScopeReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// The release map remains the current finite 64x64 world unless both the
    /// target and stress P8 lanes have complete approved evidence. Passing the
    /// evidence gate makes an expansion slice eligible for product review; it does
    /// not silently enlarge existing saves or the default world.
    /// </summary>
    public static class P8MapScopeDecision
    {
        public const int ReleaseDefaultWidth = 64;
        public const int ReleaseDefaultHeight = 64;

        public static P8MapScopeDecisionResult Evaluate(
            P8BenchmarkGateResult target128,
            P8BenchmarkGateResult stress256)
        {
            if (target128 == null || stress256 == null)
                return Fixed(P8MapScopeReason.BenchmarkEvidenceIncomplete,
                    "Map expansion remains unavailable until both P8 benchmark lanes have evidence.");

            if (!target128.Passed || !stress256.Passed)
            {
                var incomplete = IsEvidenceIncomplete(target128) || IsEvidenceIncomplete(stress256);
                return Fixed(
                    incomplete ? P8MapScopeReason.BenchmarkEvidenceIncomplete : P8MapScopeReason.BenchmarkBudgetFailed,
                    incomplete
                        ? "The release default stays 64x64 because required benchmark evidence or approved budgets are incomplete."
                        : "The release default stays 64x64 because at least one approved benchmark lane failed its budget.");
            }

            return new P8MapScopeDecisionResult(
                P8MapScopeKind.ExpansionEligible,
                P8MapScopeReason.TargetAndStressBudgetsPassed,
                "Both target and stress evidence passed. A separately reviewed region-unlock slice may now be considered.");
        }

        public static bool MatchesCurrentWorldDefaults() =>
            WorldState.DefaultWidth == ReleaseDefaultWidth &&
            WorldState.DefaultHeight == ReleaseDefaultHeight;

        private static bool IsEvidenceIncomplete(P8BenchmarkGateResult result)
        {
            if (result.Passed) return false;
            switch (result.Reason)
            {
                case P8BenchmarkGateReason.MissingRuns:
                case P8BenchmarkGateReason.EnvironmentMismatch:
                case P8BenchmarkGateReason.FixtureBelowRequiredScale:
                case P8BenchmarkGateReason.ChecksumMismatch:
                case P8BenchmarkGateReason.MissingMeasurements:
                case P8BenchmarkGateReason.MissingApprovedBudget:
                    return true;
                default:
                    return false;
            }
        }

        private static P8MapScopeDecisionResult Fixed(P8MapScopeReason reason, string message) =>
            new P8MapScopeDecisionResult(P8MapScopeKind.Fixed64, reason, message);
    }
}
