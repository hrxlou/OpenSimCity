using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// Persistent runtime state for the growth system: the set of buildings currently
    /// under construction. Completed buildings live in
    /// <see cref="OpenSimCity.Simulation.Buildings.BuildingStateCollection"/>; only the
    /// in-progress sites are owned here. Sites are exposed in stable building-ID order so
    /// serialization and iteration are deterministic.
    /// </summary>
    [Serializable]
    public sealed class DevelopmentState
    {
        private readonly Dictionary<StableId, ConstructionSite> _byBuilding =
            new Dictionary<StableId, ConstructionSite>();
        private readonly Dictionary<StableId, ConstructionSite> _byParcel =
            new Dictionary<StableId, ConstructionSite>();
        private readonly List<ConstructionSite> _ordered = new List<ConstructionSite>();
        private readonly IReadOnlyList<ConstructionSite> _readOnlyOrdered;

        public DevelopmentState()
        {
            _readOnlyOrdered = _ordered.AsReadOnly();
        }

        public int Count => _ordered.Count;

        public IReadOnlyList<ConstructionSite> Sites => _readOnlyOrdered;

        public void Add(ConstructionSite site)
        {
            if (site == null)
                throw new ArgumentNullException(nameof(site));
            if (_byBuilding.ContainsKey(site.BuildingId))
                throw new ArgumentException($"Construction site {site.BuildingId} already exists.", nameof(site));
            if (_byParcel.ContainsKey(site.ParcelId))
                throw new ArgumentException($"Parcel {site.ParcelId} is already under construction.", nameof(site));

            _byBuilding.Add(site.BuildingId, site);
            _byParcel.Add(site.ParcelId, site);
            var index = _ordered.BinarySearch(site, SiteComparer.Instance);
            _ordered.Insert(index < 0 ? ~index : index, site);
        }

        public void Replace(ConstructionSite site)
        {
            if (site == null)
                throw new ArgumentNullException(nameof(site));
            if (!_byBuilding.TryGetValue(site.BuildingId, out var existing))
                throw new KeyNotFoundException($"Construction site {site.BuildingId} is not registered.");

            _byBuilding[site.BuildingId] = site;
            _byParcel[site.ParcelId] = site;
            _ordered[_ordered.IndexOf(existing)] = site;
        }

        public bool Remove(StableId buildingId)
        {
            if (!_byBuilding.TryGetValue(buildingId, out var site))
                return false;

            _byBuilding.Remove(buildingId);
            _byParcel.Remove(site.ParcelId);
            _ordered.Remove(site);
            return true;
        }

        public bool TryGet(StableId buildingId, out ConstructionSite site) =>
            _byBuilding.TryGetValue(buildingId, out site);

        public bool TryGetByParcel(StableId parcelId, out ConstructionSite site) =>
            _byParcel.TryGetValue(parcelId, out site);

        public bool IsParcelUnderConstruction(StableId parcelId) => _byParcel.ContainsKey(parcelId);

        public DevelopmentStateDto ToDto()
        {
            var sites = new ConstructionSiteDto[_ordered.Count];
            for (var index = 0; index < _ordered.Count; index++)
                sites[index] = _ordered[index].ToDto();
            return new DevelopmentStateDto { Sites = sites };
        }

        public static DevelopmentState FromDto(DevelopmentStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var state = new DevelopmentState();
            var sites = dto.Sites ?? Array.Empty<ConstructionSiteDto>();
            for (var index = 0; index < sites.Length; index++)
                state.Add(ConstructionSite.FromDto(sites[index]));
            return state;
        }

        private sealed class SiteComparer : IComparer<ConstructionSite>
        {
            public static readonly SiteComparer Instance = new SiteComparer();

            public int Compare(ConstructionSite left, ConstructionSite right) =>
                left.BuildingId.CompareTo(right.BuildingId);
        }
    }

    [Serializable]
    public sealed class DevelopmentStateDto
    {
        public ConstructionSiteDto[] Sites = Array.Empty<ConstructionSiteDto>();
    }
}
