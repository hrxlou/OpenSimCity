using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class P8OptimizationComparisonPlan
    {
        public P8BenchmarkMetric[] TargetMetrics = Array.Empty<P8BenchmarkMetric>();
        public P8BenchmarkMetric[] ProtectedMetrics = Array.Empty<P8BenchmarkMetric>();
        public int MaximumProtectedRegressionPermille;
    }

    public enum P8OptimizationGateReason : byte
    {
        Passed,
        InvalidPlan,
        BaselineNotApproved,
        CandidateBudgetFailed,
        EnvironmentMismatch,
        ChecksumMismatch,
        MissingComparisonMetric,
        NoMeasuredImprovement,
        ProtectedMetricRegressed
    }

    public sealed class P8OptimizationGateResult
    {
        public P8OptimizationGateResult(bool passed, P8OptimizationGateReason reason, string message)
        { Passed = passed; Reason = reason; Message = message ?? string.Empty; }
        public bool Passed { get; }
        public P8OptimizationGateReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Allows an optimization claim only after the P8 baseline budget is approved
    /// and both before/after evidence pass the same scenario gate. The comparison
    /// never accepts a different checksum or hardware/runtime environment.
    /// </summary>
    public static class P8OptimizationEvidence
    {
        public static P8OptimizationGateResult Evaluate(
            P8BenchmarkScenarioSpec spec,
            IReadOnlyList<P8BenchmarkRunEvidence> before,
            IReadOnlyList<P8BenchmarkRunEvidence> after,
            P8BenchmarkBudgetSet approvedBudget,
            P8OptimizationComparisonPlan plan)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (approvedBudget == null) throw new ArgumentNullException(nameof(approvedBudget));
            if (plan == null || plan.TargetMetrics == null || plan.TargetMetrics.Length == 0 ||
                plan.ProtectedMetrics == null || plan.MaximumProtectedRegressionPermille < 0 ||
                plan.MaximumProtectedRegressionPermille > 1000)
                return Fail(P8OptimizationGateReason.InvalidPlan, "Optimization comparison requires target metrics, protected metrics and a valid regression tolerance.");

            var baselineGate = P8BenchmarkGate.Evaluate(spec, before, approvedBudget);
            if (!baselineGate.Passed)
                return Fail(P8OptimizationGateReason.BaselineNotApproved,
                    $"The before evidence is not an approved P8 baseline: {baselineGate.Reason}.");
            var candidateGate = P8BenchmarkGate.Evaluate(spec, after, approvedBudget);
            if (!candidateGate.Passed)
                return Fail(P8OptimizationGateReason.CandidateBudgetFailed,
                    $"The after evidence does not satisfy the approved P8 budget: {candidateGate.Reason}.");

            var baseline = before[0];
            var candidate = after[0];
            if (!SameEnvironment(baseline, candidate))
                return Fail(P8OptimizationGateReason.EnvironmentMismatch,
                    "Before and after evidence must use the same platform, hardware, Unity, graphics API, resolution, quality and content lane.");
            if (baseline.SimulationChecksum != candidate.SimulationChecksum)
                return Fail(P8OptimizationGateReason.ChecksumMismatch,
                    "Optimization evidence must preserve the simulation checksum.");

            var improved = false;
            for (var index = 0; index < plan.TargetMetrics.Length; index++)
            {
                var metric = plan.TargetMetrics[index];
                if (!TryAggregate(before, metric, out var beforeValue) || !TryAggregate(after, metric, out var afterValue))
                    return Fail(P8OptimizationGateReason.MissingComparisonMetric, $"Missing target metric {metric}.");
                if (afterValue < beforeValue) improved = true;
            }
            if (!improved)
                return Fail(P8OptimizationGateReason.NoMeasuredImprovement,
                    "No declared target metric improved in the measured after evidence.");

            for (var index = 0; index < plan.ProtectedMetrics.Length; index++)
            {
                var metric = plan.ProtectedMetrics[index];
                if (!TryAggregate(before, metric, out var beforeValue) || !TryAggregate(after, metric, out var afterValue))
                    return Fail(P8OptimizationGateReason.MissingComparisonMetric, $"Missing protected metric {metric}.");
                var maximum = beforeValue == 0
                    ? 0
                    : checked(beforeValue * (1000L + plan.MaximumProtectedRegressionPermille) / 1000L);
                if (afterValue > maximum)
                    return Fail(P8OptimizationGateReason.ProtectedMetricRegressed,
                        $"Protected metric {metric} regressed beyond the declared tolerance.");
            }

            return new P8OptimizationGateResult(true, P8OptimizationGateReason.Passed,
                "Measured target improvement passed without protected-metric regression.");
        }

        private static bool SameEnvironment(P8BenchmarkRunEvidence left, P8BenchmarkRunEvidence right) =>
            left.Scenario == right.Scenario && left.ContentMode == right.ContentMode &&
            string.Equals(left.Platform, right.Platform, StringComparison.Ordinal) &&
            string.Equals(left.HardwareId, right.HardwareId, StringComparison.Ordinal) &&
            string.Equals(left.UnityVersion, right.UnityVersion, StringComparison.Ordinal) &&
            string.Equals(left.GraphicsApi, right.GraphicsApi, StringComparison.Ordinal) &&
            left.ResolutionWidth == right.ResolutionWidth && left.ResolutionHeight == right.ResolutionHeight &&
            string.Equals(left.QualityLevel, right.QualityLevel, StringComparison.Ordinal);

        private static bool TryAggregate(IReadOnlyList<P8BenchmarkRunEvidence> runs, P8BenchmarkMetric metric, out long value)
        {
            var medians = new List<long>();
            for (var runIndex = 0; runIndex < runs.Count; runIndex++)
            {
                var rows = runs[runIndex].Metrics ?? Array.Empty<P8BenchmarkMetricValue>();
                var found = false;
                for (var metricIndex = 0; metricIndex < rows.Length; metricIndex++)
                {
                    if (rows[metricIndex] == null || rows[metricIndex].Metric != metric || !rows[metricIndex].IsMeasured) continue;
                    medians.Add(rows[metricIndex].Median);
                    found = true;
                    break;
                }
                if (!found) { value = 0; return false; }
            }
            if (medians.Count == 0) { value = 0; return false; }
            medians.Sort();
            value = medians[medians.Count / 2];
            return true;
        }

        private static P8OptimizationGateResult Fail(P8OptimizationGateReason reason, string message) =>
            new P8OptimizationGateResult(false, reason, message);
    }
}
