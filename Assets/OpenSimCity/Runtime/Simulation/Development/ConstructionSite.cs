using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// Serializable runtime state for one building under construction. Static footprint,
    /// capacity and power demand are resolved through <see cref="DefinitionId"/> and are
    /// not duplicated here. The pre-allocated <see cref="BuildingId"/> becomes the
    /// building's stable ID when construction completes.
    /// </summary>
    [Serializable]
    public sealed class ConstructionSite : IEquatable<ConstructionSite>
    {
        public ConstructionSite(
            StableId buildingId,
            StableId definitionId,
            StableId parcelId,
            CellCoord anchor,
            int monthsRequired,
            int monthsRemaining)
        {
            if (!buildingId.IsValid)
                throw new ArgumentException("A construction site requires a valid building ID.", nameof(buildingId));
            if (!definitionId.IsValid)
                throw new ArgumentException("A construction site requires a valid definition ID.", nameof(definitionId));
            if (!parcelId.IsValid)
                throw new ArgumentException("A construction site requires a valid parcel ID.", nameof(parcelId));
            if (monthsRequired <= 0)
                throw new ArgumentOutOfRangeException(nameof(monthsRequired), "Construction must take at least one month.");
            if (monthsRemaining <= 0 || monthsRemaining > monthsRequired)
                throw new ArgumentOutOfRangeException(nameof(monthsRemaining), "Remaining months must be in (0, required].");

            BuildingId = buildingId;
            DefinitionId = definitionId;
            ParcelId = parcelId;
            Anchor = anchor;
            MonthsRequired = monthsRequired;
            MonthsRemaining = monthsRemaining;
        }

        public StableId BuildingId { get; }

        public StableId DefinitionId { get; }

        public StableId ParcelId { get; }

        public CellCoord Anchor { get; }

        public int MonthsRequired { get; }

        public int MonthsRemaining { get; }

        /// <summary>True when advancing one more month would finish construction.</summary>
        public bool CompletesNextMonth => MonthsRemaining <= 1;

        /// <summary>
        /// Returns the site with one construction month consumed. Only valid while more
        /// than one month remains; the system completes a site instead of advancing it
        /// on its final month.
        /// </summary>
        public ConstructionSite Advanced()
        {
            if (MonthsRemaining <= 1)
                throw new InvalidOperationException("A site on its final month completes instead of advancing.");

            return new ConstructionSite(
                BuildingId,
                DefinitionId,
                ParcelId,
                Anchor,
                MonthsRequired,
                MonthsRemaining - 1);
        }

        public ConstructionSiteDto ToDto() => new ConstructionSiteDto
        {
            BuildingId = BuildingId.Value,
            DefinitionId = DefinitionId.Value,
            ParcelId = ParcelId.Value,
            AnchorX = Anchor.X,
            AnchorY = Anchor.Y,
            MonthsRequired = MonthsRequired,
            MonthsRemaining = MonthsRemaining
        };

        public static ConstructionSite FromDto(ConstructionSiteDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return new ConstructionSite(
                new StableId(dto.BuildingId),
                new StableId(dto.DefinitionId),
                new StableId(dto.ParcelId),
                new CellCoord(dto.AnchorX, dto.AnchorY),
                dto.MonthsRequired,
                dto.MonthsRemaining);
        }

        public bool Equals(ConstructionSite other)
        {
            return other != null && BuildingId == other.BuildingId &&
                   DefinitionId == other.DefinitionId && ParcelId == other.ParcelId &&
                   Anchor == other.Anchor && MonthsRequired == other.MonthsRequired &&
                   MonthsRemaining == other.MonthsRemaining;
        }

        public override bool Equals(object obj) => Equals(obj as ConstructionSite);

        public override int GetHashCode() => BuildingId.GetHashCode();
    }

    [Serializable]
    public sealed class ConstructionSiteDto
    {
        public ulong BuildingId;
        public ulong DefinitionId;
        public ulong ParcelId;
        public int AnchorX;
        public int AnchorY;
        public int MonthsRequired;
        public int MonthsRemaining;
    }
}
