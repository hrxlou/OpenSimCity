using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// A discrete growth event emitted during a month boundary. Views consume the
    /// ordered event stream to create/recycle placeholders; the event carries stable
    /// IDs and value data only, so presentation never drives simulation.
    /// </summary>
    public readonly struct GrowthEvent : IEquatable<GrowthEvent>
    {
        public GrowthEvent(
            GrowthEventKind kind,
            StableId buildingId,
            StableId definitionId,
            StableId parcelId,
            CellCoord anchor,
            int monthsRemaining)
        {
            Kind = kind;
            BuildingId = buildingId;
            DefinitionId = definitionId;
            ParcelId = parcelId;
            Anchor = anchor;
            MonthsRemaining = monthsRemaining;
        }

        public GrowthEventKind Kind { get; }

        public StableId BuildingId { get; }

        public StableId DefinitionId { get; }

        public StableId ParcelId { get; }

        public CellCoord Anchor { get; }

        /// <summary>Construction months still remaining after this event (0 once complete).</summary>
        public int MonthsRemaining { get; }

        public GrowthEventDto ToDto() => new GrowthEventDto
        {
            Kind = (int)Kind,
            BuildingId = BuildingId.Value,
            DefinitionId = DefinitionId.Value,
            ParcelId = ParcelId.Value,
            AnchorX = Anchor.X,
            AnchorY = Anchor.Y,
            MonthsRemaining = MonthsRemaining
        };

        public bool Equals(GrowthEvent other)
        {
            return Kind == other.Kind && BuildingId == other.BuildingId &&
                   DefinitionId == other.DefinitionId && ParcelId == other.ParcelId &&
                   Anchor == other.Anchor && MonthsRemaining == other.MonthsRemaining;
        }

        public override bool Equals(object obj) => obj is GrowthEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ BuildingId.GetHashCode();
                hashCode = (hashCode * 397) ^ ParcelId.GetHashCode();
                hashCode = (hashCode * 397) ^ Anchor.GetHashCode();
                return (hashCode * 397) ^ MonthsRemaining;
            }
        }
    }

    [Serializable]
    public sealed class GrowthEventDto
    {
        public int Kind;
        public ulong BuildingId;
        public ulong DefinitionId;
        public ulong ParcelId;
        public int AnchorX;
        public int AnchorY;
        public int MonthsRemaining;
    }
}
