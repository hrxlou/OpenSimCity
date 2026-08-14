using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Spatial
{
    public enum SpatialFieldKind : byte
    {
        LandValue,
        Pollution,
        Noise,
        Crime,
        ParkAmenity
    }

    [Serializable]
    public readonly struct SpatialFieldCellValue : IEquatable<SpatialFieldCellValue>
    {
        public SpatialFieldCellValue(
            int landValue,
            int pollution,
            int noise,
            int crime,
            int parkAmenity)
        {
            LandValue = landValue;
            Pollution = pollution;
            Noise = noise;
            Crime = crime;
            ParkAmenity = parkAmenity;
        }

        public int LandValue { get; }
        public int Pollution { get; }
        public int Noise { get; }
        public int Crime { get; }
        public int ParkAmenity { get; }

        public int Get(SpatialFieldKind kind)
        {
            switch (kind)
            {
                case SpatialFieldKind.LandValue: return LandValue;
                case SpatialFieldKind.Pollution: return Pollution;
                case SpatialFieldKind.Noise: return Noise;
                case SpatialFieldKind.Crime: return Crime;
                case SpatialFieldKind.ParkAmenity: return ParkAmenity;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public bool Equals(SpatialFieldCellValue other) =>
            LandValue == other.LandValue &&
            Pollution == other.Pollution &&
            Noise == other.Noise &&
            Crime == other.Crime &&
            ParkAmenity == other.ParkAmenity;
        public override bool Equals(object obj) => obj is SpatialFieldCellValue other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = LandValue;
                hash = (hash * 397) ^ Pollution;
                hash = (hash * 397) ^ Noise;
                hash = (hash * 397) ^ Crime;
                return (hash * 397) ^ ParkAmenity;
            }
        }
        public static bool operator ==(SpatialFieldCellValue left, SpatialFieldCellValue right) => left.Equals(right);
        public static bool operator !=(SpatialFieldCellValue left, SpatialFieldCellValue right) => !left.Equals(right);
    }

    [Serializable]
    public sealed class SpatialSourceDefinition
    {
        public SpatialSourceDefinition(
            StableId id,
            string name,
            int radius,
            int landValueDelta,
            int pollution,
            int noise,
            int crime,
            int parkAmenity)
        {
            if (!id.IsValid)
                throw new ArgumentException("A spatial source definition requires a stable ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A spatial source definition requires a name.", nameof(name));
            if (radius < 0 || radius > 64)
                throw new ArgumentOutOfRangeException(nameof(radius));
            ValidateContribution(landValueDelta, nameof(landValueDelta));
            ValidateContribution(pollution, nameof(pollution));
            ValidateContribution(noise, nameof(noise));
            ValidateContribution(crime, nameof(crime));
            ValidateContribution(parkAmenity, nameof(parkAmenity));

            Id = id;
            Name = name.Trim();
            Radius = radius;
            LandValueDelta = landValueDelta;
            Pollution = pollution;
            Noise = noise;
            Crime = crime;
            ParkAmenity = parkAmenity;
        }

        public StableId Id { get; }
        public string Name { get; }
        public int Radius { get; }
        public int LandValueDelta { get; }
        public int Pollution { get; }
        public int Noise { get; }
        public int Crime { get; }
        public int ParkAmenity { get; }

        public int Contribution(int peak, int manhattanDistance)
        {
            if (manhattanDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(manhattanDistance));
            if (manhattanDistance > Radius || peak == 0)
                return 0;
            return checked(peak * (Radius + 1 - manhattanDistance) / (Radius + 1));
        }

        private static void ValidateContribution(int value, string name)
        {
            if (value < -2000 || value > 2000)
                throw new ArgumentOutOfRangeException(name, "Spatial contributions must be -2000..2000.");
        }
    }

    public static class SpatialSourceDefinitionIds
    {
        private const ulong Namespace = 0xF601_0000_0000_0000UL;
        public static readonly StableId Park = new StableId(Namespace | 0x01UL);
        public static readonly StableId IndustrialEmitter = new StableId(Namespace | 0x02UL);
        public static readonly StableId TrafficNoise = new StableId(Namespace | 0x03UL);
        public static readonly StableId CrimePressure = new StableId(Namespace | 0x04UL);
        public static readonly StableId CivicImprovement = new StableId(Namespace | 0x05UL);
        public static readonly StableId SmallRoadNoise = new StableId(Namespace | 0x06UL);
        public static readonly StableId StreetRoadNoise = new StableId(Namespace | 0x07UL);
        public static readonly StableId AvenueRoadNoise = new StableId(Namespace | 0x08UL);
    }

    public sealed class SpatialSourceDefinitionCatalog
    {
        private readonly SortedDictionary<StableId, SpatialSourceDefinition> _definitions =
            new SortedDictionary<StableId, SpatialSourceDefinition>();

        public SpatialSourceDefinitionCatalog(IEnumerable<SpatialSourceDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions)
            {
                if (definition == null || _definitions.ContainsKey(definition.Id))
                    throw new ArgumentException("Spatial source definitions must be non-null with unique IDs.", nameof(definitions));
                _definitions.Add(definition.Id, definition);
            }
            if (_definitions.Count == 0)
                throw new ArgumentException("At least one spatial source definition is required.", nameof(definitions));
        }

        public IReadOnlyList<SpatialSourceDefinition> Definitions =>
            new List<SpatialSourceDefinition>(_definitions.Values).AsReadOnly();
        public SpatialSourceDefinition Get(StableId id) =>
            _definitions.TryGetValue(id, out var value)
                ? value
                : throw new KeyNotFoundException($"Unknown spatial source definition {id.Value}.");
        public bool TryGet(StableId id, out SpatialSourceDefinition definition) =>
            _definitions.TryGetValue(id, out definition);

        public static SpatialSourceDefinitionCatalog CreateDefault()
        {
            return new SpatialSourceDefinitionCatalog(new[]
            {
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.Park, "Park", 4,
                    landValueDelta: 80, pollution: -40, noise: -30, crime: -20, parkAmenity: 400),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.IndustrialEmitter, "Industrial emitter", 5,
                    landValueDelta: -50, pollution: 500, noise: 250, crime: 40, parkAmenity: 0),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.TrafficNoise, "Traffic noise", 3,
                    landValueDelta: -20, pollution: 80, noise: 400, crime: 0, parkAmenity: 0),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.CrimePressure, "Crime pressure", 4,
                    landValueDelta: -60, pollution: 0, noise: 30, crime: 450, parkAmenity: 0),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.CivicImprovement, "Civic improvement", 4,
                    landValueDelta: 100, pollution: -20, noise: -20, crime: -300, parkAmenity: 50),
                // Derived road sources use the road hierarchy's authoritative noise-emission
                // scale (15/30/70) instead of the generic 400-point traffic fixture source.
                // These IDs are rebuildable and never persisted in capability snapshots.
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.SmallRoadNoise, "Small-road noise", 3,
                    landValueDelta: 0, pollution: 0, noise: 15, crime: 0, parkAmenity: 0),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.StreetRoadNoise, "Street-road noise", 3,
                    landValueDelta: 0, pollution: 0, noise: 30, crime: 0, parkAmenity: 0),
                new SpatialSourceDefinition(
                    SpatialSourceDefinitionIds.AvenueRoadNoise, "Avenue-road noise", 3,
                    landValueDelta: 0, pollution: 0, noise: 70, crime: 0, parkAmenity: 0)
            });
        }
    }

    [Serializable]
    public sealed class SpatialFieldSourceState
    {
        public SpatialFieldSourceState(StableId id, StableId definitionId, CellCoord cell, bool isEnabled = true)
        {
            if (!id.IsValid)
                throw new ArgumentException("A spatial source requires a stable ID.", nameof(id));
            if (!definitionId.IsValid)
                throw new ArgumentException("A spatial source requires a definition ID.", nameof(definitionId));
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
            IsEnabled = isEnabled;
        }
        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
        public bool IsEnabled { get; }
    }

    [Serializable]
    public sealed class SpatialFieldSourceStateDto
    {
        public ulong Id;
        public ulong DefinitionId;
        public int CellX;
        public int CellY;
        public bool IsEnabled;
    }

    [Serializable]
    public sealed class SpatialFieldRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public SpatialFieldSourceStateDto[] Sources = Array.Empty<SpatialFieldSourceStateDto>();
    }
}
