using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Verification
{
    public enum P8BenchmarkScenarioKind : byte { Target128, Stress256 }
    public enum P8BenchmarkContentMode : byte { ViewOff, Placeholder, ProductionLike }
    public enum P8BenchmarkMetric : byte
    {
        SimulationTickMicroseconds, MonthlyTickMicroseconds, CpuFrameMicroseconds, GpuFrameMicroseconds,
        GcBytesPerFrame, TotalMemoryBytes, DrawCalls, SaveBytes, SaveMicroseconds, LoadMicroseconds,
        SelectionFeedbackMicroseconds, PlacementPreviewMicroseconds, CommandFeedbackMicroseconds, HudRefreshMicroseconds
    }

    [Serializable]
    public sealed class P8BenchmarkScenarioSpec
    {
        public string Name;
        public P8BenchmarkScenarioKind Kind;
        public int Width;
        public int Height;
        public ulong Seed;
        public int RequiredBuildingCount;
        public int RequestedVisibleVehicleCount;
        public int WarmupFrames;
        public int SampleFrames;
        public int RequiredRepeatCount;
        public int SettlementMonths;

        public static P8BenchmarkScenarioSpec Target128() => new P8BenchmarkScenarioSpec
        {
            Name = "P8.Target128", Kind = P8BenchmarkScenarioKind.Target128,
            Width = 128, Height = 128, Seed = 0x0801_1280UL,
            RequiredBuildingCount = 2000, RequestedVisibleVehicleCount = 500,
            WarmupFrames = 300, SampleFrames = 600, RequiredRepeatCount = 3, SettlementMonths = 1
        };

        public static P8BenchmarkScenarioSpec Stress256() => new P8BenchmarkScenarioSpec
        {
            Name = "P8.Stress256", Kind = P8BenchmarkScenarioKind.Stress256,
            Width = 256, Height = 256, Seed = 0x0801_2560UL,
            RequiredBuildingCount = 2000, RequestedVisibleVehicleCount = 500,
            WarmupFrames = 300, SampleFrames = 600, RequiredRepeatCount = 3, SettlementMonths = 1
        };
    }

    [Serializable]
    public sealed class P8BenchmarkMetricValue
    {
        public P8BenchmarkMetric Metric;
        public bool IsMeasured;
        public long Median;
        public long Minimum;
        public long Maximum;
    }

    [Serializable]
    public sealed class P8BenchmarkRunEvidence
    {
        public string Platform;
        public string OperatingSystem;
        public string HardwareId;
        public string UnityVersion;
        public string GraphicsApi;
        public int ResolutionWidth;
        public int ResolutionHeight;
        public string QualityLevel;
        public P8BenchmarkScenarioKind Scenario;
        public P8BenchmarkContentMode ContentMode;
        public int RepeatIndex;
        public int BuildingCount;
        public int VisibleVehicleCount;
        public ulong SimulationChecksum;
        public P8BenchmarkMetricValue[] Metrics = Array.Empty<P8BenchmarkMetricValue>();
    }

    [Serializable]
    public sealed class P8BenchmarkMetricBudget
    {
        public P8BenchmarkMetric Metric;
        public bool IsApproved;
        public long Maximum;
    }

    [Serializable]
    public sealed class P8BenchmarkBudgetSet
    {
        public string Platform;
        public string HardwareClass;
        public P8BenchmarkScenarioKind Scenario;
        public P8BenchmarkContentMode ContentMode;
        public P8BenchmarkMetricBudget[] Metrics = Array.Empty<P8BenchmarkMetricBudget>();
    }

    public enum P8BenchmarkGateReason : byte
    {
        Passed, MissingRuns, EnvironmentMismatch, FixtureBelowRequiredScale, ChecksumMismatch,
        MissingMeasurements, MissingApprovedBudget, BudgetExceeded
    }

    public sealed class P8BenchmarkGateResult
    {
        public P8BenchmarkGateResult(bool passed, P8BenchmarkGateReason reason, string message)
        { Passed = passed; Reason = reason; Message = message ?? string.Empty; }
        public bool Passed { get; }
        public P8BenchmarkGateReason Reason { get; }
        public string Message { get; }
    }

    public static class P8BenchmarkGate
    {
        private static readonly P8BenchmarkMetric[] RequiredMetrics =
            (P8BenchmarkMetric[])Enum.GetValues(typeof(P8BenchmarkMetric));

        public static P8BenchmarkGateResult Evaluate(P8BenchmarkScenarioSpec spec,
            IReadOnlyList<P8BenchmarkRunEvidence> runs, P8BenchmarkBudgetSet budget)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            if (budget == null) throw new ArgumentNullException(nameof(budget));
            if (runs.Count < spec.RequiredRepeatCount)
                return Fail(P8BenchmarkGateReason.MissingRuns, "The scenario requires at least three recorded repetitions.");
            var first = runs[0];
            if (budget.Scenario != spec.Kind || budget.ContentMode != first.ContentMode ||
                !string.Equals(budget.Platform, first.Platform, StringComparison.Ordinal))
                return Fail(P8BenchmarkGateReason.EnvironmentMismatch, "Budget and evidence do not describe the same platform/scenario/content mode.");
            for (var index = 0; index < spec.RequiredRepeatCount; index++)
            {
                var run = runs[index];
                if (run.Scenario != spec.Kind || run.ContentMode != first.ContentMode ||
                    !string.Equals(run.Platform, first.Platform, StringComparison.Ordinal) ||
                    !string.Equals(run.HardwareId, first.HardwareId, StringComparison.Ordinal) ||
                    !string.Equals(run.UnityVersion, first.UnityVersion, StringComparison.Ordinal) ||
                    run.ResolutionWidth != first.ResolutionWidth || run.ResolutionHeight != first.ResolutionHeight ||
                    !string.Equals(run.QualityLevel, first.QualityLevel, StringComparison.Ordinal))
                    return Fail(P8BenchmarkGateReason.EnvironmentMismatch, "Repetitions must use one fixed hardware/runtime/quality environment.");
                if (run.BuildingCount < spec.RequiredBuildingCount ||
                    (run.ContentMode != P8BenchmarkContentMode.ViewOff && run.VisibleVehicleCount < spec.RequestedVisibleVehicleCount))
                    return Fail(P8BenchmarkGateReason.FixtureBelowRequiredScale, "Measured fixture did not reach the required city/view scale.");
                if (run.SimulationChecksum != first.SimulationChecksum)
                    return Fail(P8BenchmarkGateReason.ChecksumMismatch, "Repetitions must preserve one deterministic simulation checksum.");
                for (var metricIndex = 0; metricIndex < RequiredMetrics.Length; metricIndex++)
                    if (!TryMetric(run, RequiredMetrics[metricIndex], out _))
                        return Fail(P8BenchmarkGateReason.MissingMeasurements, $"Missing measured metric: {RequiredMetrics[metricIndex]}.");
            }
            for (var metricIndex = 0; metricIndex < RequiredMetrics.Length; metricIndex++)
            {
                var metric = RequiredMetrics[metricIndex];
                if (!TryBudget(budget, metric, out var maximum))
                    return Fail(P8BenchmarkGateReason.MissingApprovedBudget, $"No approved platform budget exists for {metric}.");
                for (var runIndex = 0; runIndex < spec.RequiredRepeatCount; runIndex++)
                {
                    TryMetric(runs[runIndex], metric, out var value);
                    if (value > maximum) return Fail(P8BenchmarkGateReason.BudgetExceeded, $"{metric} exceeded the approved maximum.");
                }
            }
            return new P8BenchmarkGateResult(true, P8BenchmarkGateReason.Passed,
                "All required repetitions, measurements and approved budgets passed.");
        }

        private static bool TryMetric(P8BenchmarkRunEvidence run, P8BenchmarkMetric metric, out long median)
        {
            var rows = run.Metrics ?? Array.Empty<P8BenchmarkMetricValue>();
            for (var index = 0; index < rows.Length; index++)
            {
                if (rows[index] == null || rows[index].Metric != metric || !rows[index].IsMeasured) continue;
                median = rows[index].Median; return true;
            }
            median = 0; return false;
        }

        private static bool TryBudget(P8BenchmarkBudgetSet budget, P8BenchmarkMetric metric, out long maximum)
        {
            var rows = budget.Metrics ?? Array.Empty<P8BenchmarkMetricBudget>();
            for (var index = 0; index < rows.Length; index++)
            {
                if (rows[index] == null || rows[index].Metric != metric || !rows[index].IsApproved || rows[index].Maximum < 0) continue;
                maximum = rows[index].Maximum; return true;
            }
            maximum = 0; return false;
        }

        private static P8BenchmarkGateResult Fail(P8BenchmarkGateReason reason, string message) =>
            new P8BenchmarkGateResult(false, reason, message);
    }
}
