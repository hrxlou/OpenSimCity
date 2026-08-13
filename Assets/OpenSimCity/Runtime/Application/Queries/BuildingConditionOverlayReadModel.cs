using System;
using System.Collections.Generic;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Queries
{
    public enum BuildingConditionOverlayPattern : byte
    {
        Solid,
        Dots,
        Hatch,
        CrossHatch,
        Pulse
    }

    [Serializable]
    public readonly struct BuildingConditionOverlayCell
    {
        public BuildingConditionOverlayCell(
            CellCoord cell,
            StableId buildingId,
            BuildingConditionStageView stage,
            BuildingConditionReasonView topReason,
            BuildingConditionOverlayPattern pattern,
            string label)
        {
            Cell = cell;
            BuildingId = buildingId;
            Stage = stage;
            TopReason = topReason;
            Pattern = pattern;
            Label = label ?? string.Empty;
        }
        public CellCoord Cell { get; }
        public StableId BuildingId { get; }
        public BuildingConditionStageView Stage { get; }
        public BuildingConditionReasonView TopReason { get; }
        public BuildingConditionOverlayPattern Pattern { get; }
        public string Label { get; }
    }

    public sealed class BuildingConditionOverlayReadModel
    {
        private readonly BuildingConditionRuntimeModule _module;
        private int _sourceVersion = -1;
        private IReadOnlyList<BuildingConditionOverlayCell> _cells =
            Array.AsReadOnly(Array.Empty<BuildingConditionOverlayCell>());

        public BuildingConditionOverlayReadModel(BuildingConditionRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
        }

        public int Version { get; private set; }
        public IReadOnlyList<BuildingConditionOverlayCell> Cells
        {
            get
            {
                RefreshIfNeeded();
                return _cells;
            }
        }

        public bool RefreshIfNeeded()
        {
            _module.ReadModel.RefreshIfNeeded();
            var snapshot = _module.ReadModel.Snapshot;
            if (_sourceVersion == snapshot.Version)
                return false;
            var cells = new List<BuildingConditionOverlayCell>();
            for (var index = 0; index < snapshot.Buildings.Count; index++)
            {
                var building = snapshot.Buildings[index];
                var pattern = PatternFor(building.Stage);
                var label = LabelFor(building.Stage, building.TopReason);
                for (var y = 0; y < building.FootprintHeight; y++)
                {
                    for (var x = 0; x < building.FootprintWidth; x++)
                    {
                        cells.Add(new BuildingConditionOverlayCell(
                            new CellCoord(building.Anchor.X + x, building.Anchor.Y + y),
                            building.BuildingId,
                            building.Stage,
                            building.TopReason,
                            pattern,
                            label));
                    }
                }
            }
            cells.Sort((left, right) => left.Cell.CompareTo(right.Cell));
            _cells = cells.AsReadOnly();
            _sourceVersion = snapshot.Version;
            Version = checked(Version + 1);
            return true;
        }

        private static BuildingConditionOverlayPattern PatternFor(BuildingConditionStageView stage)
        {
            switch (stage)
            {
                case BuildingConditionStageView.Healthy: return BuildingConditionOverlayPattern.Solid;
                case BuildingConditionStageView.Stressed: return BuildingConditionOverlayPattern.Dots;
                case BuildingConditionStageView.Declining: return BuildingConditionOverlayPattern.Hatch;
                case BuildingConditionStageView.Abandoned: return BuildingConditionOverlayPattern.CrossHatch;
                default: return BuildingConditionOverlayPattern.Pulse;
            }
        }

        private static string LabelFor(
            BuildingConditionStageView stage,
            BuildingConditionReasonView reason)
        {
            if (stage == BuildingConditionStageView.Healthy)
                return "정상";
            if (stage == BuildingConditionStageView.Redeveloping)
                return "재개발";
            return $"{stage} · {reason}";
        }
    }
}
