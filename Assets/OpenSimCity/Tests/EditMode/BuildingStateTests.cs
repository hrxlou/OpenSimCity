using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingStateTests
    {
        [Test]
        public void LifecycleQueriesAndEnumerationUseStableBuildingIdOrder()
        {
            var states = new BuildingStateCollection();
            var third = states.Create(Id(30), Id(3), Id(300), new CellCoord(3, 0));
            var first = states.Create(Id(10), Id(1), Id(100), new CellCoord(1, 0), occupancy: 2);
            var second = states.Create(Id(20), Id(2), Id(200), new CellCoord(2, 0), filledJobs: 4);

            Assert.That(states.Buildings.Select(state => state.Id.Value), Is.EqualTo(new ulong[] { 10, 20, 30 }));
            Assert.That(states.Get(first.Id), Is.SameAs(first));
            Assert.That(states.TryGet(second.Id, out var queried), Is.True);
            Assert.That(queried, Is.SameAs(second));
            Assert.That(states.Remove(second.Id), Is.True);
            Assert.That(states.Remove(second.Id), Is.False);
            Assert.That(states.TryGet(second.Id, out _), Is.False);
            Assert.That(states.Buildings, Is.EqualTo(new[] { first, third }));
            Assert.Throws<KeyNotFoundException>(() => states.Get(second.Id));
        }

        [Test]
        public void DuplicateBuildingIdsAndInvalidRuntimeValuesAreRejected()
        {
            var states = new BuildingStateCollection();
            states.Create(Id(1), Id(10), Id(100), new CellCoord(0, 0));

            Assert.Throws<ArgumentException>(() =>
                states.Create(Id(1), Id(11), Id(101), new CellCoord(1, 0)));
            Assert.Throws<ArgumentException>(() =>
                new BuildingState(StableId.Invalid, Id(10), Id(100), new CellCoord(0, 0)));
            Assert.Throws<ArgumentException>(() =>
                new BuildingState(Id(1), StableId.Invalid, Id(100), new CellCoord(0, 0)));
            Assert.Throws<ArgumentException>(() =>
                new BuildingState(Id(1), Id(10), StableId.Invalid, new CellCoord(0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BuildingState(Id(1), Id(10), Id(100), new CellCoord(0, 0), occupancy: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BuildingState(Id(1), Id(10), Id(100), new CellCoord(0, 0), filledJobs: -1));
        }

        [Test]
        public void FieldOnlyDtoRoundTripsThroughUnityJsonWithoutObjectReferences()
        {
            var source = new BuildingStateCollection();
            source.Create(Id(20), Id(2), Id(200), new CellCoord(4, 5), filledJobs: 3);
            source.Create(Id(10), Id(1), Id(100), new CellCoord(1, 2), occupancy: 4);

            var json = JsonUtility.ToJson(source.ToDto());
            var serialized = JsonUtility.FromJson<BuildingStateCollectionDto>(json);
            var restored = BuildingStateCollection.FromDto(serialized);

            Assert.That(restored.Buildings, Is.EqualTo(source.Buildings));
            Assert.That(json, Does.Contain("DefinitionId"));
            Assert.That(json, Does.Contain("ParcelId"));
            var dtoFieldTypes = typeof(BuildingStateDto).GetFields().Select(field => field.FieldType).ToArray();
            Assert.That(dtoFieldTypes.Any(typeof(UnityEngine.Object).IsAssignableFrom), Is.False);
        }

        [Test]
        public void CatalogResolvesEveryKnownStateAndReportsUnknownDefinitionAndParcel()
        {
            var catalog = new BuildingDefinitionCatalog(new[]
            {
                Definition(2, ZoneType.CommercialLow),
                Definition(1, ZoneType.ResidentialLow)
            });
            var states = new[]
            {
                new BuildingState(Id(10), Id(1), Id(100), new CellCoord(0, 0)),
                new BuildingState(Id(20), Id(9), Id(200), new CellCoord(1, 0))
            };

            Assert.That(catalog.TryResolve(states[0], out var resolved), Is.True);
            Assert.That(resolved.Id, Is.EqualTo(Id(1)));
            var issues = BuildingStateReferenceValidator.Validate(states, catalog, new[] { Id(100) });

            Assert.That(issues.Select(issue => issue.Kind), Is.EqualTo(new[]
            {
                BuildingStateReferenceIssueKind.UnknownDefinition,
                BuildingStateReferenceIssueKind.UnknownParcel
            }));
            Assert.That(issues.All(issue => issue.BuildingId == Id(20)), Is.True);
        }

        [Test]
        public void ReferenceValidatorReportsRuntimeCountsBeyondStaticCapacity()
        {
            var catalog = new BuildingDefinitionCatalog(new[]
            {
                Definition(1, ZoneType.ResidentialLow),
                Definition(2, ZoneType.CommercialLow)
            });
            var states = new[]
            {
                new BuildingState(Id(10), Id(1), Id(100), new CellCoord(0, 0), occupancy: 5),
                new BuildingState(Id(20), Id(2), Id(200), new CellCoord(1, 0), filledJobs: 5)
            };

            var issues = BuildingStateReferenceValidator.Validate(states, catalog, new[] { Id(100), Id(200) });

            Assert.That(issues.Select(issue => issue.Kind), Is.EqualTo(new[]
            {
                BuildingStateReferenceIssueKind.OccupancyExceedsCapacity,
                BuildingStateReferenceIssueKind.FilledJobsExceedCapacity
            }));
        }

        private static BuildingDefinitionData Definition(ulong id, ZoneType zoneType)
        {
            var residential = zoneType == ZoneType.ResidentialLow;
            return new BuildingDefinitionData(
                Id(id),
                zoneType,
                new BuildingFootprint(1, 1),
                residential ? 4 : 0,
                residential ? 0 : 4,
                $"building.{id}");
        }

        private static StableId Id(ulong value) => new StableId(value);
    }
}
