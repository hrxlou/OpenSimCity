using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Definitions.Buildings;
using OpenSimCity.Simulation.Zones;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Reproducibly creates and validates the minimal low-density R/C/I
    /// definition assets without hand-editing ScriptableObject YAML.
    /// </summary>
    public static class BuildingDefinitionSetup
    {
        public const string DefinitionsFolder = "Assets/OpenSimCity/Content/Definitions/Buildings";

        private static readonly DefinitionSeed[] Seeds =
        {
            new DefinitionSeed(
                "ResidentialLow.asset",
                0xB100000000000001UL,
                ZoneType.ResidentialLow,
                2,
                2,
                4,
                0,
                "building.residential.low.placeholder"),
            new DefinitionSeed(
                "CommercialLow.asset",
                0xB100000000000002UL,
                ZoneType.CommercialLow,
                2,
                2,
                0,
                6,
                "building.commercial.low.placeholder"),
            new DefinitionSeed(
                "IndustrialLow.asset",
                0xB100000000000003UL,
                ZoneType.IndustrialLow,
                2,
                2,
                0,
                8,
                "building.industrial.low.placeholder")
        };

        [MenuItem("OpenSimCity/Setup/Configure Building Definitions")]
        public static void ConfigureDefinitions()
        {
            EnsureFolder(DefinitionsFolder);
            for (var index = 0; index < Seeds.Length; index++)
                EnsureDefinition(Seeds[index]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity building definition setup completed.");
        }

        [MenuItem("OpenSimCity/Setup/Validate Building Definitions")]
        public static void ValidateDefinitionsMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("OpenSimCity building definition validation passed.");
                return;
            }

            for (var index = 0; index < issues.Count; index++)
                Debug.LogError($"OpenSimCity building definition validation: {issues[index]}");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var definitions = LoadDefinitions();
            if (definitions.Count == 0)
            {
                issues.Add(
                    $"No BuildingDefinition assets exist under '{DefinitionsFolder}'. " +
                    "Run Configure Building Definitions.");
                return issues;
            }

            foreach (var issue in BuildingDefinitionValidator.Validate(definitions))
                issues.Add(issue.Message);

            var requiredZones = new[]
            {
                ZoneType.ResidentialLow,
                ZoneType.CommercialLow,
                ZoneType.IndustrialLow
            };
            for (var index = 0; index < requiredZones.Length; index++)
            {
                var zone = requiredZones[index];
                if (definitions.All(definition => definition.ZoneType != zone))
                    issues.Add($"A minimum {zone} BuildingDefinition asset is required.");
            }

            if (issues.Count != 0)
                return issues;

            try
            {
                _ = new BuildingDefinitionCatalog(definitions.Select(definition => definition.ToData()));
            }
            catch (Exception exception)
            {
                issues.Add($"Building definition catalog creation failed: {exception.Message}");
            }

            return issues;
        }

        public static IReadOnlyList<BuildingDefinition> LoadDefinitions()
        {
            var definitions = AssetDatabase
                .FindAssets("t:BuildingDefinition", new[] { DefinitionsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingDefinition>)
                .Where(definition => definition != null)
                .ToList();
            return definitions.AsReadOnly();
        }

        private static void EnsureDefinition(DefinitionSeed seed)
        {
            var path = $"{DefinitionsFolder}/{seed.FileName}";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition != null)
            {
                ApplySeed(definition, seed);
                return;
            }

            definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.name = System.IO.Path.GetFileNameWithoutExtension(seed.FileName);
            ApplySeed(definition, seed);
            AssetDatabase.CreateAsset(definition, path);
        }

        private static void ApplySeed(BuildingDefinition definition, DefinitionSeed seed)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").ulongValue = seed.Id;
            serialized.FindProperty("zoneType").intValue = (int)seed.ZoneType;
            serialized.FindProperty("footprintWidth").intValue = seed.Width;
            serialized.FindProperty("footprintHeight").intValue = seed.Height;
            serialized.FindProperty("occupancyCapacity").intValue = seed.OccupancyCapacity;
            serialized.FindProperty("jobCapacity").intValue = seed.JobCapacity;
            serialized.FindProperty("powerDemand").intValue = Math.Max(1, seed.OccupancyCapacity + seed.JobCapacity);
            serialized.FindProperty("presentationKey").stringValue = seed.PresentationKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private readonly struct DefinitionSeed
        {
            public DefinitionSeed(
                string fileName,
                ulong id,
                ZoneType zoneType,
                int width,
                int height,
                int occupancyCapacity,
                int jobCapacity,
                string presentationKey)
            {
                FileName = fileName;
                Id = id;
                ZoneType = zoneType;
                Width = width;
                Height = height;
                OccupancyCapacity = occupancyCapacity;
                JobCapacity = jobCapacity;
                PresentationKey = presentationKey;
            }

            public string FileName { get; }
            public ulong Id { get; }
            public ZoneType ZoneType { get; }
            public int Width { get; }
            public int Height { get; }
            public int OccupancyCapacity { get; }
            public int JobCapacity { get; }
            public string PresentationKey { get; }
        }
    }
}
