using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P8OptimizationEvidenceTests
    {
        [Test]
        public void OptimizationCannotBeApprovedBeforeP8BudgetExists()
        {
            var spec = P8BenchmarkScenarioSpec.Target128();
            var before = Runs(spec, 10, 10, "hardware");
            var after = Runs(spec, 8, 10, "hardware");
            var result = P8OptimizationEvidence.Evaluate(spec, before, after,
                new P8BenchmarkBudgetSet
                {
                    Platform = "Windows",
                    Scenario = spec.Kind,
                    ContentMode = P8BenchmarkContentMode.Placeholder
                },
                Plan());
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P8OptimizationGateReason.BaselineNotApproved));
        }

        [Test]
        public void SameFixtureImprovementPassesWhenProtectedMetricDoesNotRegress()
        {
            var spec = P8BenchmarkScenarioSpec.Target128();
            var result = P8OptimizationEvidence.Evaluate(
                spec,
                Runs(spec, 10, 10, "hardware"),
                Runs(spec, 8, 10, "hardware"),
                Budget(spec),
                Plan());
            Assert.That(result.Passed, Is.True, result.Message);
        }

        [Test]
        public void ProtectedRegressionAndHardwareDriftAreRejected()
        {
            var spec = P8BenchmarkScenarioSpec.Target128();
            var regression = P8OptimizationEvidence.Evaluate(
                spec,
                Runs(spec, 10, 10, "hardware"),
                Runs(spec, 8, 12, "hardware"),
                Budget(spec),
                Plan());
            Assert.That(regression.Reason, Is.EqualTo(P8OptimizationGateReason.ProtectedMetricRegressed));

            var drift = P8OptimizationEvidence.Evaluate(
                spec,
                Runs(spec, 10, 10, "before-hardware"),
                Runs(spec, 8, 10, "after-hardware"),
                Budget(spec),
                Plan());
            Assert.That(drift.Reason, Is.EqualTo(P8OptimizationGateReason.EnvironmentMismatch));
        }

        private static P8OptimizationComparisonPlan Plan() => new P8OptimizationComparisonPlan
        {
            TargetMetrics = new[] { P8BenchmarkMetric.CpuFrameMicroseconds },
            ProtectedMetrics = new[] { P8BenchmarkMetric.GcBytesPerFrame },
            MaximumProtectedRegressionPermille = 0
        };

        private static P8BenchmarkRunEvidence[] Runs(
            P8BenchmarkScenarioSpec spec,
            long cpu,
            long gc,
            string hardware)
        {
            return Enumerable.Range(0, 3).Select(index => new P8BenchmarkRunEvidence
            {
                Platform = "Windows",
                OperatingSystem = "fixture",
                HardwareId = hardware,
                UnityVersion = "fixture-unity",
                GraphicsApi = "fixture-api",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                QualityLevel = "fixture-quality",
                Scenario = spec.Kind,
                ContentMode = P8BenchmarkContentMode.Placeholder,
                RepeatIndex = index,
                BuildingCount = spec.RequiredBuildingCount,
                VisibleVehicleCount = spec.RequestedVisibleVehicleCount,
                SimulationChecksum = 0x8002UL,
                Metrics = Metrics(cpu, gc)
            }).ToArray();
        }

        private static P8BenchmarkMetricValue[] Metrics(long cpu, long gc)
        {
            return ((P8BenchmarkMetric[])Enum.GetValues(typeof(P8BenchmarkMetric)))
                .Select(metric => new P8BenchmarkMetricValue
                {
                    Metric = metric,
                    IsMeasured = true,
                    Minimum = metric == P8BenchmarkMetric.CpuFrameMicroseconds ? cpu :
                        metric == P8BenchmarkMetric.GcBytesPerFrame ? gc : 1,
                    Median = metric == P8BenchmarkMetric.CpuFrameMicroseconds ? cpu :
                        metric == P8BenchmarkMetric.GcBytesPerFrame ? gc : 1,
                    Maximum = metric == P8BenchmarkMetric.CpuFrameMicroseconds ? cpu :
                        metric == P8BenchmarkMetric.GcBytesPerFrame ? gc : 1
                }).ToArray();
        }

        private static P8BenchmarkBudgetSet Budget(P8BenchmarkScenarioSpec spec) =>
            new P8BenchmarkBudgetSet
            {
                Platform = "Windows",
                HardwareClass = "fixture",
                Scenario = spec.Kind,
                ContentMode = P8BenchmarkContentMode.Placeholder,
                Metrics = ((P8BenchmarkMetric[])Enum.GetValues(typeof(P8BenchmarkMetric)))
                    .Select(metric => new P8BenchmarkMetricBudget
                    {
                        Metric = metric,
                        IsApproved = true,
                        Maximum = 100
                    }).ToArray()
            };
    }
}
