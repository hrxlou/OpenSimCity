using NUnit.Framework;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Onboarding;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class OnboardingProgressTests
    {
        [Test]
        public void CommittedRoadZoneAndPowerAdvanceTasks()
        {
            var controller = new OnboardingProgressController(new CityRuntime(CityRuntimeOptions.Default), new MemoryStore());
            Assert.That(controller.Snapshot.Task, Is.EqualTo(OnboardingTaskKind.BuildRoad));
            controller.ObserveConstruction(Commit(ConstructionToolMode.Road, ConstructionOperation.PlaceRoad));
            Assert.That(controller.Snapshot.Task, Is.EqualTo(OnboardingTaskKind.PaintZone));
            controller.ObserveConstruction(Commit(ConstructionToolMode.ZoneResidential, ConstructionOperation.PaintZone));
            Assert.That(controller.Snapshot.Task, Is.EqualTo(OnboardingTaskKind.PlacePower));
            controller.ObserveConstruction(Commit(ConstructionToolMode.PowerPlant, ConstructionOperation.PlacePowerProducer));
            Assert.That(controller.Snapshot.Task, Is.EqualTo(OnboardingTaskKind.ObserveFirstGrowth));
        }

        private static ConstructionCommitResult Commit(ConstructionToolMode mode, ConstructionOperation operation) =>
            new ConstructionCommitResult(mode, operation, true, true, 0, 100, 100,
                new[] { new CellCoord(1, 1) }, string.Empty, "fixture");

        private sealed class MemoryStore : IOnboardingProfileStore
        {
            private readonly OnboardingProfileDto _profile = new OnboardingProfileDto();
            public OnboardingProfileDto Load() => _profile;
            public void Save(OnboardingProfileDto profile) { }
        }
    }
}
