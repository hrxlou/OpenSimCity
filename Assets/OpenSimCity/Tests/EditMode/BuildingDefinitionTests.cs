using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Core;
using OpenSimCity.Definitions.Buildings;
using OpenSimCity.Editor;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Zones;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingDefinitionTests
    {
        [Test]
        public void CatalogRejectsDuplicatesAndEnumeratesByStableId()
        {
            var high = Data(30, ZoneType.IndustrialLow);
            var low = Data(10, ZoneType.ResidentialLow);
            var catalog = new BuildingDefinitionCatalog(new[] { high, low });

            Assert.That(catalog.Definitions.Select(definition => definition.Id.Value),
                Is.EqualTo(new ulong[] { 10, 30 }));
            Assert.That(catalog.Get(low.Id), Is.SameAs(low));
            Assert.That(catalog.TryGet(new StableId(99), out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => catalog.Get(new StableId(99)));
            Assert.Throws<ArgumentException>(() => new BuildingDefinitionCatalog(new[] { low, low }));
        }

        [Test]
        public void PureDefinitionDataRejectsFootprintsAndCapacityOutsideCurrentContract()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingDefinitionData(
                new StableId(1),
                ZoneType.ResidentialLow,
                new BuildingFootprint(3, 1),
                4,
                0,
                "building.too-wide"));
            Assert.Throws<ArgumentException>(() => new BuildingDefinitionData(
                new StableId(2),
                ZoneType.CommercialLow,
                new BuildingFootprint(1, 1),
                2,
                4,
                "building.mixed"));
        }

        [Test]
        public void ScriptableObjectProjectsOnlyStaticDataIntoApplicationCatalog()
        {
            var asset = CreateDefinition(
                "Residential",
                101,
                ZoneType.ResidentialLow,
                width: 2,
                height: 1,
                occupancyCapacity: 6,
                jobCapacity: 0,
                presentationKey: "building.residential.test");

            try
            {
                Assert.That(BuildingDefinitionValidator.Validate(new[] { asset }), Is.Empty);
                var data = asset.ToData();
                Assert.That(data.Id, Is.EqualTo(new StableId(101)));
                Assert.That(data.ZoneType, Is.EqualTo(ZoneType.ResidentialLow));
                Assert.That(data.Footprint, Is.EqualTo(new BuildingFootprint(2, 1)));
                Assert.That(data.OccupancyCapacity, Is.EqualTo(6));
                Assert.That(data.JobCapacity, Is.Zero);
                Assert.That(data.PresentationKey, Is.EqualTo("building.residential.test"));
                Assert.That(typeof(BuildingDefinition).GetFields(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic)
                    .Any(field => typeof(GameObject).IsAssignableFrom(field.FieldType)), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetValidatorReportsMissingDuplicateFootprintAndPresentationKey()
        {
            var missing = CreateDefinition("Missing", 0, ZoneType.ResidentialLow, 1, 1, 4, 0, "");
            var first = CreateDefinition("First", 7, ZoneType.CommercialLow, 3, 1, 0, 4, "building.first");
            var duplicate = CreateDefinition("Duplicate", 7, ZoneType.IndustrialLow, 1, 1, 0, 4, "building.second");

            try
            {
                var kinds = BuildingDefinitionValidator.Validate(new[] { missing, first, duplicate })
                    .Select(issue => issue.Kind)
                    .ToArray();

                Assert.That(kinds, Does.Contain(BuildingDefinitionIssueKind.MissingId));
                Assert.That(kinds, Does.Contain(BuildingDefinitionIssueKind.DuplicateId));
                Assert.That(kinds, Does.Contain(BuildingDefinitionIssueKind.InvalidFootprint));
                Assert.That(kinds, Does.Contain(BuildingDefinitionIssueKind.MissingPresentationKey));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(missing);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void GeneratedMinimumRciAssetsPassCatalogAndSchemaValidation()
        {
            var issues = BuildingDefinitionSetup.Validate();
            var definitions = BuildingDefinitionSetup.LoadDefinitions();

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
            Assert.That(definitions.Select(definition => definition.ZoneType), Does.Contain(ZoneType.ResidentialLow));
            Assert.That(definitions.Select(definition => definition.ZoneType), Does.Contain(ZoneType.CommercialLow));
            Assert.That(definitions.Select(definition => definition.ZoneType), Does.Contain(ZoneType.IndustrialLow));
            Assert.That(new BuildingDefinitionCatalog(definitions.Select(definition => definition.ToData())).Count,
                Is.EqualTo(definitions.Count));
        }

        private static BuildingDefinitionData Data(ulong id, ZoneType zoneType)
        {
            var residential = zoneType == ZoneType.ResidentialLow;
            return new BuildingDefinitionData(
                new StableId(id),
                zoneType,
                new BuildingFootprint(1, 1),
                residential ? 4 : 0,
                residential ? 0 : 4,
                $"building.{id}");
        }

        private static BuildingDefinition CreateDefinition(
            string name,
            ulong id,
            ZoneType zoneType,
            int width,
            int height,
            int occupancyCapacity,
            int jobCapacity,
            string presentationKey)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.name = name;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").ulongValue = id;
            serialized.FindProperty("zoneType").intValue = (int)zoneType;
            serialized.FindProperty("footprintWidth").intValue = width;
            serialized.FindProperty("footprintHeight").intValue = height;
            serialized.FindProperty("occupancyCapacity").intValue = occupancyCapacity;
            serialized.FindProperty("jobCapacity").intValue = jobCapacity;
            serialized.FindProperty("presentationKey").stringValue = presentationKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
