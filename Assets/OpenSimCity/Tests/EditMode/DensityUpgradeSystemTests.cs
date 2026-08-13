using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class DensityUpgradeSystemTests
    {
        [Test]
        public void MonthlyStartCapUsesStableBuildingOrderAndPreservesIdentity()
        {
            var fixture = CreateFixture(3, maximumStarts: 2);

            fixture.System.RunMonth(new SimulationDate(1, 1));
            var firstMonth = fixture.System.State.Buildings;
            Assert.That(firstMonth[0].IsUpgradeInProgress, Is.True);
            Assert.That(firstMonth[1].IsUpgradeInProgress, Is.True);
            Assert.That(firstMonth[2].IsUpgradeInProgress, Is.False);

            fixture.System.RunMonth(new SimulationDate(1, 2));
            Assert.That(fixture.System.State.Buildings[2].IsUpgradeInProgress, Is.True);
            fixture.System.RunMonth(new SimulationDate(1, 3));

            Assert.That(fixture.System.State.Buildings[0].Stage, Is.EqualTo(DensityStage.Medium));
            Assert.That(fixture.System.State.Buildings[1].Stage, Is.EqualTo(DensityStage.Medium));
            Assert.That(fixture.System.CapacityMultiplier(fixture.Buildings.Buildings[0].Id), Is.EqualTo(2));
            Assert.That(fixture.System.State.Buildings.Select(row => row.BuildingId),
                Is.EqualTo(fixture.Buildings.Buildings.Select(building => building.Id)));
        }

        [Test]
        public void LostEligibilityPausesProgressAndKeepsExplicitTopReason()
        {
            var fixture = CreateFixture(1, maximumStarts: 1);
            fixture.System.RunMonth(new SimulationDate(1, 1));
            var state = fixture.System.State.Buildings.Single();
            Assert.That(state.UpgradeMonthsRemaining, Is.EqualTo(2));

            fixture.Eligibility.IsEligible = false;
            fixture.Eligibility.Reason = DensityUpgradeBlockReason.NoWater;
            fixture.System.RunMonth(new SimulationDate(1, 2));
            Assert.That(state.UpgradeMonthsRemaining, Is.EqualTo(2));
            Assert.That(state.TopReason, Is.EqualTo(DensityUpgradeBlockReason.NoWater));

            fixture.Eligibility.IsEligible = true;
            fixture.Eligibility.Reason = DensityUpgradeBlockReason.None;
            fixture.System.RunMonth(new SimulationDate(1, 3));
            Assert.That(state.UpgradeMonthsRemaining, Is.EqualTo(1));
            fixture.System.RunMonth(new SimulationDate(1, 4));
            Assert.That(state.Stage, Is.EqualTo(DensityStage.Medium));
        }

        [Test]
        public void SameBuildingsAndEligibilityProduceSameChecksumRegardlessOfInsertionOrder()
        {
            var forward = CreateFixture(4, 2, reverse: false);
            var reverse = CreateFixture(4, 2, reverse: true);
            for (var month = 1; month <= 5; month++)
            {
                forward.System.RunMonth(new SimulationDate(1, month));
                reverse.System.RunMonth(new SimulationDate(1, month));
                Assert.That(reverse.System.ComputeChecksum(), Is.EqualTo(forward.System.ComputeChecksum()));
            }
        }

        private static Fixture CreateFixture(int count, int maximumStarts, bool reverse = false)
        {
            var definitions = new StableId[count];
            var rows = new List<BuildingState>();
            for (var index = 0; index < count; index++)
            {
                definitions[index] = new StableId(0xB701000000000000UL | (ulong)(index + 1));
                rows.Add(new BuildingState(
                    new StableId(0xB702000000000000UL | (ulong)(index + 1)),
                    definitions[index],
                    new StableId(0xA702000000000000UL | (ulong)(index + 1)),
                    new CellCoord(index + 1, 1),
                    new BuildingFootprint(1, 1),
                    10,
                    0));
            }
            if (reverse) rows.Reverse();
            var buildings = new BuildingStateCollection();
            foreach (var row in rows) buildings.Add(row);
            var profiles = new Profiles(definitions);
            var eligibility = new Eligibility();
            var parameters = new DensityUpgradeParameters(
                mediumUpgradeMonths: 2,
                highUpgradeMonths: 3,
                maximumStartsPerMonth: maximumStarts);
            var system = new DensityUpgradeSystem(buildings, profiles, eligibility, parameters);
            return new Fixture(buildings, system, eligibility);
        }

        private sealed class Fixture
        {
            public Fixture(
                BuildingStateCollection buildings,
                DensityUpgradeSystem system,
                Eligibility eligibility)
            {
                Buildings = buildings;
                System = system;
                Eligibility = eligibility;
            }
            public BuildingStateCollection Buildings { get; }
            public DensityUpgradeSystem System { get; }
            public Eligibility Eligibility { get; }
        }

        private sealed class Profiles : IDensityBuildingProfileResolver
        {
            private readonly HashSet<StableId> _ids;
            public Profiles(IEnumerable<StableId> ids) { _ids = new HashSet<StableId>(ids); }
            public bool TryGetDensityProfile(StableId definitionId, out DensityBuildingProfile profile)
            {
                if (_ids.Contains(definitionId))
                {
                    profile = new DensityBuildingProfile(DensityUse.Residential, 10, 0);
                    return true;
                }
                profile = default;
                return false;
            }
        }

        private sealed class Eligibility : IDensityEligibilityResolver
        {
            public bool IsEligible { get; set; } = true;
            public DensityUpgradeBlockReason Reason { get; set; } = DensityUpgradeBlockReason.None;
            public DensityEligibility Resolve(
                BuildingState building,
                DensityBuildingProfile profile,
                DensityStage currentStage,
                DensityStage targetStage)
            {
                return new DensityEligibility(
                    targetStage,
                    IsEligible ? DensityUpgradeBlockReason.None : Reason,
                    Array.AsReadOnly(new[]
                    {
                        new DensityUpgradeFactor(
                            DensityUpgradeFactorKind.WaterSupply,
                            IsEligible ? 1000 : 0,
                            800,
                            IsEligible)
                    }));
            }
        }
    }
}
