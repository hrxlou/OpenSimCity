using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P8EvidenceGateTests
    {
        [Test]
        public void ScenarioDefinitionsKeepRequiredScaleStable()
        {
            var target = P8BenchmarkScenarioSpec.Target128();
            var stress = P8BenchmarkScenarioSpec.Stress256();
            Assert.That(target.Width, Is.EqualTo(128));
            Assert.That(target.Height, Is.EqualTo(128));
            Assert.That(stress.Width, Is.EqualTo(256));
            Assert.That(stress.Height, Is.EqualTo(256));
            Assert.That(target.RequiredBuildingCount, Is.EqualTo(2000));
            Assert.That(target.RequestedVisibleVehicleCount, Is.EqualTo(500));
            Assert.That(TrafficVisualizationReadModel.MaximumAllowedVehicleSamples,
                Is.GreaterThanOrEqualTo(target.RequestedVisibleVehicleCount));
            Assert.That(target.RequiredRepeatCount, Is.EqualTo(3));
            Assert.That(target.WarmupFrames, Is.EqualTo(stress.WarmupFrames));
            Assert.That(target.SampleFrames, Is.EqualTo(stress.SampleFrames));
        }

        [Test]
        public void CompleteRunsStillFailUntilPlatformBudgetsAreApproved()
        {
            var spec = P8BenchmarkScenarioSpec.Target128();
            var runs = new[] { Run(spec, 0), Run(spec, 1), Run(spec, 2) };
            var result = P8BenchmarkGate.Evaluate(spec, runs, new P8BenchmarkBudgetSet
            {
                Platform = "Windows",
                HardwareClass = "fixture",
                Scenario = spec.Kind,
                ContentMode = P8BenchmarkContentMode.Placeholder
            });
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P8BenchmarkGateReason.MissingApprovedBudget));
        }

        [Test]
        public void ChecksumDriftAndUndersizedRunsAreRejected()
        {
            var spec = P8BenchmarkScenarioSpec.Target128();
            var runs = new[] { Run(spec, 0), Run(spec, 1), Run(spec, 2) };
            var budget = ApprovedBudget(spec);
            runs[1].SimulationChecksum++;
            Assert.That(P8BenchmarkGate.Evaluate(spec, runs, budget).Reason,
                Is.EqualTo(P8BenchmarkGateReason.ChecksumMismatch));
            runs[1].SimulationChecksum = runs[0].SimulationChecksum;
            runs[2].BuildingCount--;
            Assert.That(P8BenchmarkGate.Evaluate(spec, runs, budget).Reason,
                Is.EqualTo(P8BenchmarkGateReason.FixtureBelowRequiredScale));
        }

        private static P8BenchmarkRunEvidence Run(P8BenchmarkScenarioSpec spec, int repeat)
        {
            return new P8BenchmarkRunEvidence
            {
                Platform = "Windows",
                OperatingSystem = "fixture",
                HardwareId = "fixture-hardware",
                UnityVersion = "fixture-unity",
                GraphicsApi = "fixture-api",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                QualityLevel = "fixture-quality",
                Scenario = spec.Kind,
                ContentMode = P8BenchmarkContentMode.Placeholder,
                RepeatIndex = repeat,
                BuildingCount = spec.RequiredBuildingCount,
                VisibleVehicleCount = spec.RequestedVisibleVehicleCount,
                SimulationChecksum = 0x8001UL,
                Metrics = MetricValues()
            };
        }

        private static P8BenchmarkMetricValue[] MetricValues() =>
            ((P8BenchmarkMetric[])Enum.GetValues(typeof(P8BenchmarkMetric)))
                .Select(metric => new P8BenchmarkMetricValue
                {
                    Metric = metric,
                    IsMeasured = true,
                    Minimum = 1,
                    Median = 1,
                    Maximum = 1
                }).ToArray();

        private static P8BenchmarkBudgetSet ApprovedBudget(P8BenchmarkScenarioSpec spec) =>
            new P8BenchmarkBudgetSet
            {
                Platform = "Windows",
                HardwareClass = "fixture",
                Scenario = spec.Kind,
                ContentMode = P8BenchmarkContentMode.Placeholder,
                Metrics = ((P8BenchmarkMetric[])Enum.GetValues(typeof(P8BenchmarkMetric)))
                    .Select(metric => new P8BenchmarkMetricBudget { Metric = metric, IsApproved = true, Maximum = 10 })
                    .ToArray()
            };
    }
}
