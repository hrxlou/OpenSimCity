using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Definitions.Utilities;
using OpenSimCity.Application.World;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    public static class UtilityDefinitionSetup
    {
        public const string Folder = "Assets/OpenSimCity/Content/Definitions/Utilities";
        public const string PowerPlantPath = Folder + "/PowerPlant.asset";

        [MenuItem("OpenSimCity/Setup/Configure Utility Definitions")]
        public static void ConfigureDefinitions()
        {
            EnsureFolder(Folder);
            var definition = AssetDatabase.LoadAssetAtPath<UtilityDefinition>(PowerPlantPath);
            if (definition == null)
            { definition = ScriptableObject.CreateInstance<UtilityDefinition>(); definition.name = "PowerPlant"; AssetDatabase.CreateAsset(definition, PowerPlantPath); }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").ulongValue = CityRuntimeOptions.DefaultPowerPlantDefinitionId.Value;
            serialized.FindProperty("powerCapacity").intValue = 100;
            serialized.FindProperty("constructionCost").intValue = CityRuntimeOptions.DefaultPowerPlantConstructionCost;
            serialized.FindProperty("monthlyMaintenanceCost").intValue = CityRuntimeOptions.DefaultPowerPlantMonthlyMaintenanceCost;
            serialized.FindProperty("presentationKey").stringValue = "utility.power.plant.placeholder";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var definitions = AssetDatabase.FindAssets("t:UtilityDefinition", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<UtilityDefinition>).Where(value => value != null).ToArray();
            if (definitions.Length == 0) { issues.Add("A power UtilityDefinition asset is required."); return issues; }
            var ids = new HashSet<ulong>();
            foreach (var definition in definitions)
            {
                if (!definition.DefinitionId.IsValid) issues.Add($"Utility definition '{definition.name}' has no stable ID.");
                else if (!ids.Add(definition.DefinitionId.Value)) issues.Add($"Utility definition ID {definition.DefinitionId} is duplicated.");
                if (definition.PowerCapacity <= 0) issues.Add($"Utility definition '{definition.name}' requires positive capacity.");
                if (string.IsNullOrWhiteSpace(definition.PresentationKey)) issues.Add($"Utility definition '{definition.name}' requires a presentation key.");
            }
            if (issues.Count == 0) _ = new UtilityDefinitionCatalog(definitions.Select(value => value.ToData()));
            return issues;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
    }
}
