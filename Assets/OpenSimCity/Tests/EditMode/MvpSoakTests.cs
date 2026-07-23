using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Verification;
using OpenSimCity.Editor;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class MvpSoakTests
    {
        [Test]
        public void PlaceholderCatalogMapsExactlyThreeDefinitionsPerLowDensitySector()
        {
            var issues = PlaceholderArtSetup.Validate();
            var definitions = BuildingDefinitionSetup.LoadDefinitions();

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
            Assert.That(definitions.Count(definition => definition.ZoneType == ZoneType.ResidentialLow), Is.EqualTo(3));
            Assert.That(definitions.Count(definition => definition.ZoneType == ZoneType.CommercialLow), Is.EqualTo(3));
            Assert.That(definitions.Count(definition => definition.ZoneType == ZoneType.IndustrialLow), Is.EqualTo(3));
            Assert.That(definitions.Select(definition => definition.DefinitionId).Distinct().Count(), Is.EqualTo(9));
            Assert.That(definitions.Select(definition => definition.PresentationKey).Distinct().Count(), Is.EqualTo(9));
        }

        [TestCase(MvpRoadLayout.SimpleGrid)]
        [TestCase(MvpRoadLayout.EdgeBranch)]
        public void ThirtyMinuteReplayProducesIdenticalMonthlyChecksums(MvpRoadLayout layout)
        {
            var first = MvpSoakFixture.Run(layout, observePresentationReadModels: false);
            var second = MvpSoakFixture.Run(layout, observePresentationReadModels: false);

            Assert.That(first.SimulatedMinutes, Is.EqualTo(30));
            Assert.That(first.Months, Has.Count.EqualTo(60));
            Assert.That(second.Months.Select(row => row.Checksum),
                Is.EqualTo(first.Months.Select(row => row.Checksum)));
            Assert.That(first.HasFirstFeedbackLoop, Is.True);
            Assert.That(first.SuggestedNextAction, Is.Not.Empty);
            var expectedFinal = layout == MvpRoadLayout.SimpleGrid
                ? 0xB4E6E15547236FCEUL
                : 0x726257FF395A38BBUL;
            Assert.That(first.FinalChecksum, Is.EqualTo(expectedFinal));
            TestContext.WriteLine(
                $"P3-07 {layout} monthly checksums: " +
                string.Join(",", first.Months.Select(row => row.Checksum.ToString("X16"))));
            TestContext.WriteLine(
                $"P3-07 {layout} final checksum: {first.FinalChecksum:X16}; " +
                $"population={first.Months.Last().Population}; buildings={first.Months.Last().BuildingCount}");
        }

        [TestCase(MvpRoadLayout.SimpleGrid)]
        [TestCase(MvpRoadLayout.EdgeBranch)]
        public void ObservingOrDisablingPresentationSnapshotsDoesNotChangeSimulation(MvpRoadLayout layout)
        {
            var hidden = MvpSoakFixture.Run(layout, observePresentationReadModels: false);
            var visible = MvpSoakFixture.Run(layout, observePresentationReadModels: true);

            Assert.That(visible.Months.Select(row => row.Checksum),
                Is.EqualTo(hidden.Months.Select(row => row.Checksum)));
        }

        [TestCase(MvpRoadLayout.SimpleGrid)]
        [TestCase(MvpRoadLayout.EdgeBranch)]
        public void PublicReasonsMatchPowerAndExternalRoadRecoveryPeriods(MvpRoadLayout layout)
        {
            var report = MvpSoakFixture.Run(layout, observePresentationReadModels: true);
            for (var index = 0; index < 4; index++)
            {
                var row = report.Months[index];
                TestContext.WriteLine(
                    $"{layout} month[{index}] pop={row.Population} jobs={row.EmployedWorkers} " +
                    $"tax={row.TaxIncome} feasible={row.FeasibleParcelCount} reason={row.TopBlockReason} " +
                    $"road={row.HasExternalConnection} power={row.HasConnectedPowerProducer}");
            }

            var powerBlocked = report.Months[MvpSoakFixture.PowerCutMonthIndex];
            var powerRecovered = report.Months[MvpSoakFixture.PowerRecoveryMonthIndex];
            Assert.That(powerBlocked.HasConnectedPowerProducer, Is.False);
            Assert.That(powerBlocked.TopBlockReason, Is.EqualTo("NoPowerCapacity"));
            Assert.That(powerRecovered.HasConnectedPowerProducer, Is.True);
            Assert.That(powerRecovered.TopBlockReason, Is.Not.EqualTo("NoPowerCapacity"));

            var roadBlocked = report.Months[MvpSoakFixture.ExternalCutMonthIndex];
            var roadRecovered = report.Months[MvpSoakFixture.ExternalRecoveryMonthIndex];
            Assert.That(roadBlocked.HasExternalConnection, Is.False);
            Assert.That(roadBlocked.TopBlockReason, Is.EqualTo("RoadIneligible"));
            Assert.That(roadRecovered.HasExternalConnection, Is.True);
            Assert.That(roadRecovered.TopBlockReason, Is.Not.EqualTo("RoadIneligible"));
        }

        [Test]
        public void BuildingViewSnapshotContainsOnlyValueDataAndNoUnityObjects()
        {
            var fields = typeof(BuildingViewData).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            Assert.That(fields.Any(field =>
                typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)), Is.False);
        }
    }
}
