using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Zones
{
    /// <summary>
    /// Stable low-density zoning values. The numeric values are part of the
    /// runtime/save contract; presentation colors and materials are not.
    /// </summary>
    public enum ZoneType : byte
    {
        None = 0,
        Unzoned = None,
        Clear = None,

        ResidentialLow = 1,
        LowDensityResidential = ResidentialLow,
        ResidentialLowDensity = ResidentialLow,
        Residential = ResidentialLow,
        R = ResidentialLow,

        CommercialLow = 2,
        LowDensityCommercial = CommercialLow,
        CommercialLowDensity = CommercialLow,
        Commercial = CommercialLow,
        C = CommercialLow,

        IndustrialLow = 3,
        LowDensityIndustrial = IndustrialLow,
        IndustrialLowDensity = IndustrialLow,
        Industrial = IndustrialLow,
        I = IndustrialLow
    }

    public static class ZoneTypeExtensions
    {
        public static bool IsPaintable(this ZoneType zoneType)
        {
            return zoneType == ZoneType.ResidentialLow ||
                   zoneType == ZoneType.CommercialLow ||
                   zoneType == ZoneType.IndustrialLow;
        }

        public static bool IsClear(this ZoneType zoneType)
        {
            return zoneType == ZoneType.None;
        }
    }

    /// <summary>
    /// Built-in stable definition IDs for the first low-density zoning slice.
    /// These IDs are category-local and intentionally independent of artwork.
    /// </summary>
    public static class ZoneDefinitionIds
    {
        public static readonly StableId ResidentialLow = new StableId(1UL);
        public static readonly StableId CommercialLow = new StableId(2UL);
        public static readonly StableId IndustrialLow = new StableId(3UL);

        public static StableId For(ZoneType zoneType)
        {
            switch (zoneType)
            {
                case ZoneType.ResidentialLow:
                    return ResidentialLow;
                case ZoneType.CommercialLow:
                    return CommercialLow;
                case ZoneType.IndustrialLow:
                    return IndustrialLow;
                default:
                    return StableId.Invalid;
            }
        }
    }

    /// <summary>
    /// Static data for one zone type. Runtime cells retain the stable ID and
    /// enum value, while presentationKey remains an optional presentation hint.
    /// </summary>
    [Serializable]
    public sealed class ZoneDefinition : IEquatable<ZoneDefinition>
    {
        public ZoneDefinition(StableId id, ZoneType zoneType)
            : this(id, zoneType, 0, string.Empty)
        {
        }

        public ZoneDefinition(StableId id, ZoneType zoneType, string presentationKey)
            : this(id, zoneType, 0, presentationKey)
        {
        }

        public ZoneDefinition(StableId id, ZoneType zoneType, int paintCost)
            : this(id, zoneType, paintCost, string.Empty)
        {
        }

        public ZoneDefinition(
            StableId id,
            ZoneType zoneType,
            int paintCost,
            string presentationKey)
        {
            if (!id.IsValid)
                throw new ArgumentException("A zone definition must have a valid stable ID.", nameof(id));
            if (!zoneType.IsPaintable())
                throw new ArgumentException("A zone definition must use a paintable zone type.", nameof(zoneType));
            if (paintCost < 0)
                throw new ArgumentOutOfRangeException(nameof(paintCost), "Zone paint cost cannot be negative.");

            Id = id;
            ZoneType = zoneType;
            PaintCost = paintCost;
            PresentationKey = presentationKey ?? string.Empty;
        }

        public ZoneDefinition(ulong id, ZoneType zoneType, string presentationKey = null)
            : this(new StableId(id), zoneType, 0, presentationKey)
        {
        }

        public StableId Id { get; }

        public StableId DefinitionId => Id;

        public ZoneType ZoneType { get; }

        public ZoneType Type => ZoneType;

        public int PaintCost { get; }

        public int BuildCost => PaintCost;

        public int ConstructionCost => PaintCost;

        public string PresentationKey { get; }

        public bool Equals(ZoneDefinition other)
        {
            return other != null &&
                   Id == other.Id &&
                   ZoneType == other.ZoneType &&
                   PaintCost == other.PaintCost &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as ZoneDefinition);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id.GetHashCode() * 397) ^ (int)ZoneType) * 397 ^
                       PaintCost * 397 ^ StringComparer.Ordinal.GetHashCode(PresentationKey);
            }
        }

        public override string ToString()
        {
            return $"{Id} [{ZoneType}, paintCost={PaintCost}, key={PresentationKey}]";
        }
    }

    /// <summary>
    /// Deterministic lookup for static zone definitions.
    /// </summary>
    public sealed class ZoneDefinitionCatalog
    {
        private readonly Dictionary<StableId, ZoneDefinition> _byId;
        private readonly Dictionary<ZoneType, ZoneDefinition> _byType;
        private readonly IReadOnlyList<ZoneDefinition> _orderedDefinitions;

        public ZoneDefinitionCatalog()
            : this(CreateDefaultDefinitions())
        {
        }

        public ZoneDefinitionCatalog(IEnumerable<ZoneDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<StableId, ZoneDefinition>();
            _byType = new Dictionary<ZoneType, ZoneDefinition>();
            var ordered = new List<ZoneDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("A zone definition cannot be null.", nameof(definitions));
                if (_byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        $"Zone definition {definition.Id} is registered more than once.",
                        nameof(definitions));
                }
                if (_byType.ContainsKey(definition.ZoneType))
                {
                    throw new ArgumentException(
                        $"Zone type {definition.ZoneType} is registered more than once.",
                        nameof(definitions));
                }

                _byId.Add(definition.Id, definition);
                _byType.Add(definition.ZoneType, definition);
                ordered.Add(definition);
            }

            ordered.Sort((left, right) => left.Id.CompareTo(right.Id));
            _orderedDefinitions = ordered.AsReadOnly();
        }

        public int Count => _byId.Count;

        public IReadOnlyList<ZoneDefinition> Definitions => _orderedDefinitions;

        public bool TryGet(StableId id, out ZoneDefinition definition)
        {
            return _byId.TryGetValue(id, out definition);
        }

        public bool TryGet(ZoneType zoneType, out ZoneDefinition definition)
        {
            return _byType.TryGetValue(zoneType, out definition);
        }

        public ZoneDefinition Get(StableId id)
        {
            if (!TryGet(id, out var definition))
                throw new KeyNotFoundException($"Zone definition {id} is not registered.");

            return definition;
        }

        public ZoneDefinition Get(ZoneType zoneType)
        {
            if (!TryGet(zoneType, out var definition))
                throw new KeyNotFoundException($"Zone type {zoneType} is not registered.");

            return definition;
        }

        public static ZoneDefinitionCatalog CreateDefault()
        {
            return new ZoneDefinitionCatalog(CreateDefaultDefinitions());
        }

        private static IEnumerable<ZoneDefinition> CreateDefaultDefinitions()
        {
            return new[]
            {
                new ZoneDefinition(
                    ZoneDefinitionIds.ResidentialLow,
                    ZoneType.ResidentialLow,
                    0,
                    "zone.residential.low"),
                new ZoneDefinition(
                    ZoneDefinitionIds.CommercialLow,
                    ZoneType.CommercialLow,
                    0,
                    "zone.commercial.low"),
                new ZoneDefinition(
                    ZoneDefinitionIds.IndustrialLow,
                    ZoneType.IndustrialLow,
                    0,
                    "zone.industrial.low")
            };
        }
    }

    [Serializable]
    public readonly struct ZoneCellState : IEquatable<ZoneCellState>
    {
        public ZoneCellState(CellCoord coordinate, int index, ZoneType zoneType)
            : this(coordinate, index, zoneType, ZoneDefinitionIds.For(zoneType))
        {
        }

        public ZoneCellState(
            CellCoord coordinate,
            int index,
            ZoneType zoneType,
            StableId definitionId)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "A cell index cannot be negative.");
            if (zoneType != ZoneType.None && !zoneType.IsPaintable())
                throw new ArgumentException("A cell must use a known zone type.", nameof(zoneType));
            if (zoneType == ZoneType.None && definitionId.IsValid)
                throw new ArgumentException("An unzoned cell cannot have a definition ID.", nameof(definitionId));
            if (zoneType.IsPaintable() && !definitionId.IsValid)
                throw new ArgumentException("A zoned cell requires a definition ID.", nameof(definitionId));

            Coordinate = coordinate;
            Index = index;
            ZoneType = zoneType;
            DefinitionId = definitionId;
        }

        public CellCoord Coordinate { get; }

        public CellCoord Coord => Coordinate;

        public int Index { get; }

        public ZoneType ZoneType { get; }

        public ZoneType Type => ZoneType;

        public StableId DefinitionId { get; }

        public StableId ZoneDefinitionId => DefinitionId;

        public bool IsZoned => ZoneType.IsPaintable();

        public bool IsEmpty => !IsZoned;

        public bool Equals(ZoneCellState other)
        {
            return Coordinate == other.Coordinate &&
                   Index == other.Index &&
                   ZoneType == other.ZoneType &&
                   DefinitionId == other.DefinitionId;
        }

        public override bool Equals(object obj) => obj is ZoneCellState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Coordinate.GetHashCode() * 397) ^ Index) * 397 ^
                       ((int)ZoneType * 397) ^ DefinitionId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Coordinate} [index={Index}, zone={ZoneType}, definition={DefinitionId}]";
        }

        public static bool operator ==(ZoneCellState left, ZoneCellState right) => left.Equals(right);

        public static bool operator !=(ZoneCellState left, ZoneCellState right) => !left.Equals(right);
    }
}
