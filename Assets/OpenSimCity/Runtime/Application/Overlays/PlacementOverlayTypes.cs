using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Overlays
{
    /// <summary>
    /// The player-facing datamaps P2 exposes. Each kind is a separate toggle and
    /// legend; only maps for implemented systems appear (Docs/DESIGN.md 3.1).
    /// </summary>
    public enum PlacementOverlayKind
    {
        RoadConnectivity,
        Accessibility,
        Zone,
        Parcel
    }

    /// <summary>
    /// Semantic color role for an overlay category. Presentation resolves each
    /// role to a Docs/DESIGN.md token; the Application layer never names a hex
    /// color so overlay color stays a presentation setting.
    /// </summary>
    public enum OverlayColorRole
    {
        Neutral,
        Info,
        Success,
        Warning,
        Danger,
        Accent,
        Special,
        Residential,
        Commercial,
        Industrial
    }

    /// <summary>
    /// The non-color channel every overlay cell carries in addition to its role,
    /// so success/warning/failure and accessibility bands are distinguishable
    /// without relying on hue (Docs/DESIGN.md 3.1, 3.2).
    /// </summary>
    public enum OverlayPattern
    {
        Solid,
        Hatch,
        CrossHatch,
        Dot,
        Ring,
        Outline
    }

    public enum PlacementOverlayCategory
    {
        None,
        RoadConnected,
        RoadIsolated,
        AccessibilityGate,
        AccessibilityNear,
        AccessibilityMedium,
        AccessibilityFar,
        AccessibilityUnreachable,
        ZoneResidential,
        ZoneCommercial,
        ZoneIndustrial,
        ParcelEligible,
        ParcelReachableFar,
        ParcelNoExternalConnection,
        ParcelNoFrontage
    }

    public readonly struct PlacementOverlayCell : IEquatable<PlacementOverlayCell>
    {
        public PlacementOverlayCell(
            CellCoord cell,
            PlacementOverlayCategory category,
            OverlayColorRole colorRole,
            OverlayPattern pattern,
            string label)
        {
            Cell = cell;
            Category = category;
            ColorRole = colorRole;
            Pattern = pattern;
            Label = label ?? string.Empty;
        }

        public CellCoord Cell { get; }

        public PlacementOverlayCategory Category { get; }

        public OverlayColorRole ColorRole { get; }

        public OverlayPattern Pattern { get; }

        /// <summary>Short per-cell text marker used as the non-color channel.</summary>
        public string Label { get; }

        public bool Equals(PlacementOverlayCell other)
        {
            return Cell == other.Cell &&
                   Category == other.Category &&
                   ColorRole == other.ColorRole &&
                   Pattern == other.Pattern &&
                   string.Equals(Label, other.Label, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PlacementOverlayCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Cell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Category;
                hashCode = (hashCode * 397) ^ (int)ColorRole;
                hashCode = (hashCode * 397) ^ (int)Pattern;
                return (hashCode * 397) ^ (Label?.GetHashCode() ?? 0);
            }
        }
    }

    public sealed class PlacementOverlayLegendEntry
    {
        public PlacementOverlayLegendEntry(
            PlacementOverlayCategory category,
            OverlayColorRole colorRole,
            OverlayPattern pattern,
            string label,
            string description)
        {
            Category = category;
            ColorRole = colorRole;
            Pattern = pattern;
            Label = label ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public PlacementOverlayCategory Category { get; }

        public OverlayColorRole ColorRole { get; }

        public OverlayPattern Pattern { get; }

        public string Label { get; }

        public string Description { get; }
    }

    /// <summary>
    /// Immutable overlay result: the cells to draw and the legend that names them.
    /// Overlays consume this snapshot and never recompute simulation values.
    /// </summary>
    public sealed class PlacementOverlaySnapshot
    {
        private static readonly IReadOnlyList<PlacementOverlayCell> EmptyCells =
            Array.AsReadOnly(Array.Empty<PlacementOverlayCell>());

        private static readonly IReadOnlyList<PlacementOverlayLegendEntry> EmptyLegend =
            Array.AsReadOnly(Array.Empty<PlacementOverlayLegendEntry>());

        public PlacementOverlaySnapshot(
            PlacementOverlayKind kind,
            string title,
            IReadOnlyList<PlacementOverlayCell> cells,
            IReadOnlyList<PlacementOverlayLegendEntry> legend)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Cells = cells ?? EmptyCells;
            Legend = legend ?? EmptyLegend;
        }

        public PlacementOverlayKind Kind { get; }

        public string Title { get; }

        public IReadOnlyList<PlacementOverlayCell> Cells { get; }

        public IReadOnlyList<PlacementOverlayLegendEntry> Legend { get; }

        public int CellCount => Cells.Count;
    }
}
