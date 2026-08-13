using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Conditions;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingConditionCommandTests
    {
        [Test]
        public void RedevelopmentPreviewRejectsUntilRecoveryAndCommitIsAtomic()
        {
            var fixture = CreateEligibleFixture();
            var finance = new MunicipalFinanceState(1_000);
            var handler = new BuildingConditionCommandHandler(fixture.System, finance, 400);
            var command = new RequestBuildingRedevelopmentCommand(fixture.Building.Id);
            var preview = handler.Preview(command);

            Assert.That(preview.IsValid, Is.True, preview.Message);
            Assert.That(preview.Cost, Is.EqualTo(400));
            Assert.That(finance.Funds, Is.EqualTo(1_000));
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Abandoned));

            var execution = handler.Execute(new CommandEnvelope(
                new CommandStamp(SimulationTick.Zero, CommandSequence.Zero),
                command));
            Assert.That(execution.IsApplied, Is.True, execution.Message);
            Assert.That(finance.Funds, Is.EqualTo(600));
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Redeveloping));
            Assert.That(fixture.State.BuildingId, Is.EqualTo(fixture.Building.Id));
            Assert.That(finance.Ledger, Is.Empty,
                "Construction is accrued for the current period and settles at the monthly boundary.");
        }

        [Test]
        public void InsufficientFundsRejectWithoutStartingRedevelopment()
        {
            var fixture = CreateEligibleFixture();
            var finance = new MunicipalFinanceState(100);
            var handler = new BuildingConditionCommandHandler(fixture.System, finance, 400);
            var result = handler.Execute(new CommandEnvelope(
                new CommandStamp(SimulationTick.Zero, CommandSequence.Zero),
                new RequestBuildingRedevelopmentCommand(fixture.Building.Id)));

            Assert.That(result.IsApplied, Is.False);
            Assert.That(((BuildingConditionCommandResult)result.Details).FailureReason,
                Is.EqualTo(BuildingConditionCommandFailureReason.InsufficientFunds));
            Assert.That(finance.Funds, Is.EqualTo(100));
            Assert.That(fixture.State.Stage, Is.EqualTo(BuildingConditionStage.Abandoned));
            Assert.That(fixture.State.IsRedevelopmentEligible, Is.True);
        }

        [Test]
        public void ExplicitBlockReasonIsPreservedInApplicationResult()
        {
            var buildingId = new StableId(0xB220000000000010UL);
            var definitionId = new StableId(0xB210000000000010UL);
            var buildings = new BuildingStateCollection();
            var building = new BuildingState(
                buildingId,
                definitionId,
                new StableId(0xA220000000000010UL),
                new CellCoord(1, 1),
                new BuildingFootprint(1, 1),
                10,
                0);
            buildings.Add(building);
            var environment = new MutableEnvironment(new BuildingConditionEnvironment(
                100, 900, 800, 700, true, true));
            var system = new BuildingConditionSystem(
                buildings,
                new ProfileResolver(definitionId),
                environment);
            for (var month = 0; month < 7; month++)
                system.RunMonth(new SimulationDate(1, month + 1));
            environment.Value = new BuildingConditionEnvironment(
                650, 0, 0, 0, false, true);
            system.RunMonth(new SimulationDate(1, 8));

            var handler = new BuildingConditionCommandHandler(
                system,
                new MunicipalFinanceState(1_000),
                400);
            var preview = handler.Preview(new RequestBuildingRedevelopmentCommand(buildingId));
            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.FailureReason,
                Is.EqualTo(BuildingConditionCommandFailureReason.NoRoadAccess));
            Assert.That(preview.Message, Does.Contain("도로"));
        }

        private static EligibleFixture CreateEligibleFixture()
        {
            var buildingId = new StableId(0xB220000000000020UL);
            var definitionId = new StableId(0xB210000000000020UL);
            var buildings = new BuildingStateCollection();
            var building = new BuildingState(
                buildingId,
                definitionId,
                new StableId(0xA220000000000020UL),
                new CellCoord(1, 1),
                new BuildingFootprint(1, 1),
                10,
                0);
            buildings.Add(building);
            var environment = new MutableEnvironment(new BuildingConditionEnvironment(
                100, 900, 800, 700, true, true));
            var system = new BuildingConditionSystem(
                buildings,
                new ProfileResolver(definitionId),
                environment);
            for (var month = 0; month < 7; month++)
                system.RunMonth(new SimulationDate(1, month + 1));
            environment.Value = new BuildingConditionEnvironment(
                650, 0, 0, 0, true, true);
            system.RunMonth(new SimulationDate(1, 8));
            system.RunMonth(new SimulationDate(1, 9));
            return new EligibleFixture(building, system);
        }

        private sealed class EligibleFixture
        {
            public EligibleFixture(BuildingState building, BuildingConditionSystem system)
            {
                Building = building;
                System = system;
            }
            public BuildingState Building { get; }
            public BuildingConditionSystem System { get; }
            public BuildingConditionState State => System.State.States[0];
        }

        private sealed class ProfileResolver : IBuildingConditionProfileResolver
        {
            private readonly StableId _definitionId;
            public ProfileResolver(StableId definitionId) { _definitionId = definitionId; }
            public bool TryGetConditionProfile(StableId definitionId, out BuildingConditionProfile profile)
            {
                if (definitionId == _definitionId)
                {
                    profile = new BuildingConditionProfile(BuildingConditionUse.Residential, 10, 0);
                    return true;
                }
                profile = default;
                return false;
            }
        }

        private sealed class MutableEnvironment : IBuildingConditionEnvironmentResolver
        {
            public MutableEnvironment(BuildingConditionEnvironment value) { Value = value; }
            public BuildingConditionEnvironment Value { get; set; }
            public BuildingConditionEnvironment Resolve(BuildingState building) => Value;
        }
    }
}
