using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Definitions.Buildings
{
    public enum BuildingDefinitionIssueKind
    {
        MissingDefinition = 1,
        MissingId = 2,
        DuplicateId = 3,
        UnsupportedZone = 4,
        InvalidFootprint = 5,
        InvalidCapacity = 6,
        MissingPresentationKey = 7
    }

    public readonly struct BuildingDefinitionIssue
    {
        public BuildingDefinitionIssue(
            BuildingDefinitionIssueKind kind,
            string assetName,
            StableId definitionId,
            string message)
        {
            Kind = kind;
            AssetName = assetName ?? string.Empty;
            DefinitionId = definitionId;
            Message = message ?? string.Empty;
        }

        public BuildingDefinitionIssueKind Kind { get; }

        public string AssetName { get; }

        public StableId DefinitionId { get; }

        public string Message { get; }

        public override string ToString() => Message;
    }

    public static class BuildingDefinitionValidator
    {
        public static IReadOnlyList<BuildingDefinitionIssue> Validate(
            IEnumerable<BuildingDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            var issues = new List<BuildingDefinitionIssue>();
            var ids = new Dictionary<StableId, string>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.MissingDefinition,
                        string.Empty,
                        StableId.Invalid,
                        "A building definition reference is missing."));
                    continue;
                }

                var assetName = definition.name;
                var id = definition.DefinitionId;
                if (!id.IsValid)
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.MissingId,
                        assetName,
                        StableId.Invalid,
                        $"Building definition '{assetName}' has no stable definition ID."));
                }
                else if (ids.TryGetValue(id, out var firstAssetName))
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.DuplicateId,
                        assetName,
                        id,
                        $"Building definitions '{firstAssetName}' and '{assetName}' share stable ID {id}."));
                }
                else
                {
                    ids.Add(id, assetName);
                }

                if (!definition.ZoneType.IsPaintable())
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.UnsupportedZone,
                        assetName,
                        id,
                        $"Building definition '{assetName}' must use a low-density R/C/I zone."));
                }

                if (definition.FootprintWidth <= 0 || definition.FootprintHeight <= 0 ||
                    definition.FootprintWidth > ParcelNetworkState.MaximumParcelWidth ||
                    definition.FootprintHeight > ParcelNetworkState.MaximumParcelHeight)
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.InvalidFootprint,
                        assetName,
                        id,
                        $"Building definition '{assetName}' footprint must fit the current 1x1 to " +
                        $"{ParcelNetworkState.MaximumParcelWidth}x{ParcelNetworkState.MaximumParcelHeight} parcel contract."));
                }

                ValidateCapacity(definition, assetName, id, issues);

                if (string.IsNullOrWhiteSpace(definition.PresentationKey))
                {
                    issues.Add(new BuildingDefinitionIssue(
                        BuildingDefinitionIssueKind.MissingPresentationKey,
                        assetName,
                        id,
                        $"Building definition '{assetName}' has no presentation key."));
                }
            }

            return issues.AsReadOnly();
        }

        private static void ValidateCapacity(
            BuildingDefinition definition,
            string assetName,
            StableId id,
            ICollection<BuildingDefinitionIssue> issues)
        {
            var valid = definition.ZoneType == ZoneType.ResidentialLow
                ? definition.OccupancyCapacity > 0 && definition.JobCapacity == 0
                : (definition.ZoneType == ZoneType.CommercialLow || definition.ZoneType == ZoneType.IndustrialLow) &&
                  definition.OccupancyCapacity == 0 && definition.JobCapacity > 0;
            if (valid)
                return;

            issues.Add(new BuildingDefinitionIssue(
                BuildingDefinitionIssueKind.InvalidCapacity,
                assetName,
                id,
                $"Building definition '{assetName}' must provide residential occupancy capacity or " +
                "commercial/industrial job capacity for its zone, without mixed-use capacity."));
        }
    }
}
