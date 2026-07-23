using System;
using System.Collections.Generic;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;

namespace OpenSimCity.Application.Queries
{
    public enum BuildingViewPhase : byte
    {
        Construction = 0,
        Complete = 1
    }

    /// <summary>
    /// Unity-free presentation row for one construction site or completed building.
    /// Presentation keys remain static definition data; runtime progress stays here.
    /// </summary>
    public readonly struct BuildingViewData
    {
        public BuildingViewData(
            StableId id,
            string presentationKey,
            CellCoord anchor,
            int footprintWidth,
            int footprintHeight,
            BuildingViewPhase phase,
            int progressPermille)
        {
            Id = id;
            PresentationKey = presentationKey ?? string.Empty;
            Anchor = anchor;
            FootprintWidth = footprintWidth;
            FootprintHeight = footprintHeight;
            Phase = phase;
            ProgressPermille = progressPermille;
        }

        public StableId Id { get; }
        public string PresentationKey { get; }
        public CellCoord Anchor { get; }
        public int FootprintWidth { get; }
        public int FootprintHeight { get; }
        public BuildingViewPhase Phase { get; }
        public int ProgressPermille { get; }
    }

    /// <summary>
    /// Central snapshot used by the pooled building renderer. Reading or not reading
    /// this model cannot mutate simulation state or consume deterministic RNG.
    /// </summary>
    public sealed class BuildingViewReadModel
    {
        public const string ConstructionPresentationKey = "building.construction.placeholder";

        private readonly BuildingStateCollection _buildings;
        private readonly BuildingDefinitionCatalog _definitions;
        private readonly GrowthDevelopmentSystem _growth;
        private IReadOnlyList<BuildingViewData> _views =
            Array.AsReadOnly(Array.Empty<BuildingViewData>());
        private int _buildingVersion = -1;
        private int _growthVersion = -1;
        private int _version;

        public BuildingViewReadModel(
            BuildingStateCollection buildings,
            BuildingDefinitionCatalog definitions,
            GrowthDevelopmentSystem growth)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
            RefreshIfNeeded();
        }

        public int Version
        {
            get
            {
                RefreshIfNeeded();
                return _version;
            }
        }

        public IReadOnlyList<BuildingViewData> Views
        {
            get
            {
                RefreshIfNeeded();
                return _views;
            }
        }

        public bool RefreshIfNeeded()
        {
            if (_buildingVersion == _buildings.Version && _growthVersion == _growth.Version)
                return false;

            var rows = new List<BuildingViewData>(_buildings.Count + _growth.State.Count);
            var sites = _growth.State.Sites;
            for (var index = 0; index < sites.Count; index++)
            {
                var site = sites[index];
                var definition = _definitions.Get(site.DefinitionId);
                var completedMonths = site.MonthsRequired - site.MonthsRemaining;
                var progress = site.MonthsRequired == 0
                    ? 0
                    : completedMonths * 1000 / site.MonthsRequired;
                rows.Add(new BuildingViewData(
                    site.BuildingId,
                    ConstructionPresentationKey,
                    site.Anchor,
                    definition.Footprint.Width,
                    definition.Footprint.Height,
                    BuildingViewPhase.Construction,
                    progress));
            }

            var buildings = _buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                var definition = _definitions.Get(building.DefinitionId);
                rows.Add(new BuildingViewData(
                    building.Id,
                    definition.PresentationKey,
                    building.Anchor,
                    definition.Footprint.Width,
                    definition.Footprint.Height,
                    BuildingViewPhase.Complete,
                    1000));
            }

            rows.Sort((left, right) => left.Id.CompareTo(right.Id));
            _views = rows.AsReadOnly();
            _buildingVersion = _buildings.Version;
            _growthVersion = _growth.Version;
            _version = checked(_version + 1);
            return true;
        }
    }
}
