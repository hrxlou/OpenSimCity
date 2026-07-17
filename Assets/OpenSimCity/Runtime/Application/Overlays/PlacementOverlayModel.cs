using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.Overlays
{
    /// <summary>
    /// Builds the P2 placement datamaps from the versioned Application read models.
    /// It only reads query values, so an overlay cell always agrees with the cell,
    /// road-accessibility or parcel query for the same coordinate.
    /// </summary>
    public sealed class PlacementOverlayModel
    {
        private readonly RoadAccessibilityReadModel _roads;
        private readonly ZoneReadModel _zones;
        private readonly ParcelReadModel _parcels;
        private readonly PowerReadModel _power;

        public PlacementOverlayModel(CityRuntime runtime)
            : this(
                (runtime ?? throw new ArgumentNullException(nameof(runtime))).RoadAccessibility,
                runtime.ZoneModel,
                runtime.ParcelModel,
                runtime.PowerModel)
        {
        }

        public PlacementOverlayModel(
            RoadAccessibilityReadModel roads,
            ZoneReadModel zones,
            ParcelReadModel parcels,
            PowerReadModel power = null)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
            _power = power;
        }

        public PlacementOverlaySnapshot Build(PlacementOverlayKind kind)
        {
            switch (kind)
            {
                case PlacementOverlayKind.RoadConnectivity:
                    return BuildRoadConnectivity();
                case PlacementOverlayKind.Accessibility:
                    return BuildAccessibility();
                case PlacementOverlayKind.Zone:
                    return BuildZone();
                case PlacementOverlayKind.Parcel:
                    return BuildParcel();
                case PlacementOverlayKind.Power:
                    return BuildPower();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown overlay kind.");
            }
        }

        public PlacementOverlaySnapshot BuildRoadConnectivity()
        {
            _roads.RefreshIfNeeded();
            var cells = new List<PlacementOverlayCell>();
            foreach (var road in _roads.Cells)
            {
                if (!road.HasRoad)
                    continue;

                var category = road.IsReachable
                    ? PlacementOverlayCategory.RoadConnected
                    : PlacementOverlayCategory.RoadIsolated;
                cells.Add(MakeCell(road.Cell, category, string.Empty));
            }

            return new PlacementOverlaySnapshot(
                PlacementOverlayKind.RoadConnectivity,
                "도로 연결",
                cells.AsReadOnly(),
                Legend(
                    PlacementOverlayCategory.RoadConnected,
                    PlacementOverlayCategory.RoadIsolated));
        }

        public PlacementOverlaySnapshot BuildAccessibility()
        {
            _roads.RefreshIfNeeded();
            var cells = new List<PlacementOverlayCell>();
            foreach (var road in _roads.Cells)
            {
                if (!road.HasRoad)
                    continue;

                var category = CategoryForBand(road.DistanceBand);
                var label = road.IsReachable
                    ? road.NetworkDistanceToExternalGate.ToString()
                    : "X";
                cells.Add(MakeCell(road.Cell, category, label));
            }

            return new PlacementOverlaySnapshot(
                PlacementOverlayKind.Accessibility,
                "외부 접근성",
                cells.AsReadOnly(),
                Legend(
                    PlacementOverlayCategory.AccessibilityGate,
                    PlacementOverlayCategory.AccessibilityNear,
                    PlacementOverlayCategory.AccessibilityMedium,
                    PlacementOverlayCategory.AccessibilityFar,
                    PlacementOverlayCategory.AccessibilityUnreachable));
        }

        public PlacementOverlaySnapshot BuildZone()
        {
            _zones.RefreshIfNeeded();
            var cells = new List<PlacementOverlayCell>();
            foreach (var zone in _zones.Cells)
            {
                if (!zone.IsZoned)
                    continue;

                var category = CategoryForZone(zone.ZoneType);
                cells.Add(MakeCell(zone.Cell, category, Descriptor(category).Label));
            }

            return new PlacementOverlaySnapshot(
                PlacementOverlayKind.Zone,
                "구역",
                cells.AsReadOnly(),
                Legend(
                    PlacementOverlayCategory.ZoneResidential,
                    PlacementOverlayCategory.ZoneCommercial,
                    PlacementOverlayCategory.ZoneIndustrial));
        }

        public PlacementOverlaySnapshot BuildParcel()
        {
            _parcels.RefreshIfNeeded();
            var cells = new List<PlacementOverlayCell>();
            foreach (var parcel in _parcels.Parcels)
            {
                var category = CategoryForParcel(parcel);
                var label = ParcelLabel(parcel, category);
                for (var index = 0; index < parcel.Cells.Count; index++)
                    cells.Add(MakeCell(parcel.Cells[index], category, label));
            }

            return new PlacementOverlaySnapshot(
                PlacementOverlayKind.Parcel,
                "필지 개발 자격",
                cells.AsReadOnly(),
                Legend(
                    PlacementOverlayCategory.ParcelEligible,
                    PlacementOverlayCategory.ParcelReachableFar,
                    PlacementOverlayCategory.ParcelNoExternalConnection,
                    PlacementOverlayCategory.ParcelNoFrontage));
        }

        public PlacementOverlaySnapshot BuildPower()
        {
            if (_power == null) throw new InvalidOperationException("A power read model is required for the power overlay.");
            _power.RefreshIfNeeded();
            var cells = new List<PlacementOverlayCell>();
            foreach (var producer in _power.Producers)
                cells.Add(MakeCell(producer.Cell, producer.IsConnected
                    ? PlacementOverlayCategory.PowerProducerConnected
                    : PlacementOverlayCategory.PowerProducerDisconnected, producer.IsConnected ? "+" : "!"));
            foreach (var building in _power.Buildings)
                cells.Add(MakeCell(building.Cell, building.IsPowered
                    ? PlacementOverlayCategory.BuildingPowered
                    : PlacementOverlayCategory.BuildingUnpowered, building.IsPowered ? string.Empty : "X"));
            return new PlacementOverlaySnapshot(PlacementOverlayKind.Power, "전력",
                cells.AsReadOnly(), Legend(PlacementOverlayCategory.PowerProducerConnected,
                    PlacementOverlayCategory.PowerProducerDisconnected, PlacementOverlayCategory.BuildingPowered,
                    PlacementOverlayCategory.BuildingUnpowered));
        }

        /// <summary>
        /// Classifies one parcel with the same public reason and band the parcel
        /// query exposes, so the overlay never disagrees with the query.
        /// </summary>
        public static PlacementOverlayCategory CategoryForParcel(ParcelDevelopmentReadModel parcel)
        {
            if (parcel == null)
                throw new ArgumentNullException(nameof(parcel));

            if (!parcel.IsDevelopmentEligible)
            {
                switch (parcel.FailureReason)
                {
                    case ParcelDevelopmentFailureReason.NoRoadFrontage:
                        return PlacementOverlayCategory.ParcelNoFrontage;
                    default:
                        return PlacementOverlayCategory.ParcelNoExternalConnection;
                }
            }

            return parcel.DistanceBand == ExternalGateDistanceBand.Far
                ? PlacementOverlayCategory.ParcelReachableFar
                : PlacementOverlayCategory.ParcelEligible;
        }

        public static PlacementOverlayCategory CategoryForBand(RoadAccessibilityBand band)
        {
            switch (band)
            {
                case RoadAccessibilityBand.Gate:
                    return PlacementOverlayCategory.AccessibilityGate;
                case RoadAccessibilityBand.Near:
                    return PlacementOverlayCategory.AccessibilityNear;
                case RoadAccessibilityBand.Medium:
                    return PlacementOverlayCategory.AccessibilityMedium;
                case RoadAccessibilityBand.Far:
                    return PlacementOverlayCategory.AccessibilityFar;
                default:
                    return PlacementOverlayCategory.AccessibilityUnreachable;
            }
        }

        public static PlacementOverlayCategory CategoryForZone(ZoneType zoneType)
        {
            switch (zoneType)
            {
                case ZoneType.CommercialLow:
                    return PlacementOverlayCategory.ZoneCommercial;
                case ZoneType.IndustrialLow:
                    return PlacementOverlayCategory.ZoneIndustrial;
                default:
                    return PlacementOverlayCategory.ZoneResidential;
            }
        }

        public static OverlayCategoryDescriptor Descriptor(PlacementOverlayCategory category)
        {
            return Descriptors.TryGetValue(category, out var descriptor)
                ? descriptor
                : new OverlayCategoryDescriptor(
                    OverlayColorRole.Neutral, OverlayPattern.Solid, string.Empty, string.Empty);
        }

        private static PlacementOverlayCell MakeCell(
            Core.Mathematics.CellCoord cell,
            PlacementOverlayCategory category,
            string label)
        {
            var descriptor = Descriptor(category);
            return new PlacementOverlayCell(cell, category, descriptor.ColorRole, descriptor.Pattern, label);
        }

        private static string ParcelLabel(ParcelDevelopmentReadModel parcel, PlacementOverlayCategory category)
        {
            switch (category)
            {
                case PlacementOverlayCategory.ParcelReachableFar:
                    return parcel.NetworkDistanceToExternalGate.ToString();
                case PlacementOverlayCategory.ParcelNoExternalConnection:
                    return "!";
                case PlacementOverlayCategory.ParcelNoFrontage:
                    return "X";
                default:
                    return Descriptor(category).Label;
            }
        }

        private static IReadOnlyList<PlacementOverlayLegendEntry> Legend(
            params PlacementOverlayCategory[] categories)
        {
            var entries = new PlacementOverlayLegendEntry[categories.Length];
            for (var index = 0; index < categories.Length; index++)
            {
                var descriptor = Descriptor(categories[index]);
                entries[index] = new PlacementOverlayLegendEntry(
                    categories[index],
                    descriptor.ColorRole,
                    descriptor.Pattern,
                    descriptor.Label,
                    descriptor.Description);
            }

            return Array.AsReadOnly(entries);
        }

        private static readonly IReadOnlyDictionary<PlacementOverlayCategory, OverlayCategoryDescriptor>
            Descriptors = new Dictionary<PlacementOverlayCategory, OverlayCategoryDescriptor>
            {
                [PlacementOverlayCategory.RoadConnected] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Info, OverlayPattern.Solid, string.Empty, "외부와 연결된 도로"),
                [PlacementOverlayCategory.RoadIsolated] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Danger, OverlayPattern.Hatch, "!", "외부와 끊긴 고립 도로망"),

                [PlacementOverlayCategory.AccessibilityGate] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Accent, OverlayPattern.Solid, "0", "외부 관문 셀"),
                [PlacementOverlayCategory.AccessibilityNear] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Success, OverlayPattern.Dot, string.Empty, "가까움 (1~4)"),
                [PlacementOverlayCategory.AccessibilityMedium] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Info, OverlayPattern.Ring, string.Empty, "보통 (5~12)"),
                [PlacementOverlayCategory.AccessibilityFar] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Warning, OverlayPattern.Hatch, string.Empty, "멀다 (13+)"),
                [PlacementOverlayCategory.AccessibilityUnreachable] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Danger, OverlayPattern.CrossHatch, "X", "도달 불가"),

                [PlacementOverlayCategory.ZoneResidential] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Residential, OverlayPattern.Solid, "R", "저밀도 주거"),
                [PlacementOverlayCategory.ZoneCommercial] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Commercial, OverlayPattern.Solid, "C", "저밀도 상업"),
                [PlacementOverlayCategory.ZoneIndustrial] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Industrial, OverlayPattern.Solid, "I", "저밀도 공업"),

                [PlacementOverlayCategory.ParcelEligible] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Success, OverlayPattern.Solid, string.Empty, "개발 가능"),
                [PlacementOverlayCategory.ParcelReachableFar] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Info, OverlayPattern.Ring, string.Empty, "개발 가능하지만 먼 필지"),
                [PlacementOverlayCategory.ParcelNoExternalConnection] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Warning, OverlayPattern.Hatch, "!", "외부 연결 없음"),
                [PlacementOverlayCategory.ParcelNoFrontage] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Danger, OverlayPattern.CrossHatch, "X", "도로 접면 없음"),
                [PlacementOverlayCategory.PowerProducerConnected] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Special, OverlayPattern.Solid, "+", "도로망에 연결된 발전소"),
                [PlacementOverlayCategory.PowerProducerDisconnected] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Warning, OverlayPattern.Hatch, "!", "도로망에 연결되지 않은 발전소"),
                [PlacementOverlayCategory.BuildingPowered] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Success, OverlayPattern.Solid, string.Empty, "전력 공급됨"),
                [PlacementOverlayCategory.BuildingUnpowered] = new OverlayCategoryDescriptor(
                    OverlayColorRole.Danger, OverlayPattern.CrossHatch, "X", "전력 공급 안 됨")
            };
    }

    public readonly struct OverlayCategoryDescriptor
    {
        public OverlayCategoryDescriptor(
            OverlayColorRole colorRole,
            OverlayPattern pattern,
            string label,
            string description)
        {
            ColorRole = colorRole;
            Pattern = pattern;
            Label = label ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public OverlayColorRole ColorRole { get; }

        public OverlayPattern Pattern { get; }

        public string Label { get; }

        public string Description { get; }
    }
}
