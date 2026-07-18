using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Development;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Versioned growth snapshot for overlays and future HUD consumers. It contains no
    /// Unity or presentation objects and never exposes simulation arrays. Views drive
    /// creation/recycling from the ordered <see cref="GrowthDevelopmentSnapshotDto.Events"/>
    /// stream and read per-parcel feasibility through <see cref="TryGetFeasibility"/>.
    /// </summary>
    public sealed class GrowthDevelopmentReadModel
    {
        private readonly GrowthDevelopmentSystem _system;
        private int _sourceVersion = -1;
        private GrowthDevelopmentSnapshotDto _snapshot;

        public GrowthDevelopmentReadModel(GrowthDevelopmentSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public int Version
        {
            get
            {
                RefreshIfNeeded();
                return _sourceVersion;
            }
        }

        public bool HasSnapshot
        {
            get
            {
                RefreshIfNeeded();
                return _snapshot != null;
            }
        }

        public GrowthDevelopmentSnapshotDto Snapshot
        {
            get
            {
                RefreshIfNeeded();
                if (_snapshot == null)
                    throw new InvalidOperationException("No monthly growth snapshot has been produced yet.");
                return _snapshot;
            }
        }

        /// <summary>Live per-parcel development feasibility for the public query.</summary>
        public bool TryGetFeasibility(StableId parcelId, out DevelopmentFeasibility feasibility)
        {
            return _system.TryGetFeasibility(parcelId, out feasibility);
        }

        public bool RefreshIfNeeded()
        {
            if (_sourceVersion == _system.Version)
                return false;

            _sourceVersion = _system.Version;
            if (!_system.HasProcessedMonth)
            {
                _snapshot = null;
                return true;
            }

            var sites = _system.State.Sites;
            var siteDtos = new ConstructionSiteDto[sites.Count];
            for (var index = 0; index < sites.Count; index++)
                siteDtos[index] = sites[index].ToDto();

            var events = _system.LastEvents;
            var eventDtos = new GrowthEventDto[events.Count];
            for (var index = 0; index < events.Count; index++)
                eventDtos[index] = events[index].ToDto();

            var summaries = _system.SectorSummaries;
            var summaryDtos = new DevelopmentSectorSummaryDto[summaries.Count];
            for (var index = 0; index < summaries.Count; index++)
                summaryDtos[index] = summaries[index].ToDto();

            _snapshot = new GrowthDevelopmentSnapshotDto
            {
                Year = _system.LastProcessedMonth.Year,
                Month = _system.LastProcessedMonth.Month,
                Sites = siteDtos,
                Events = eventDtos,
                Sectors = summaryDtos
            };
            return true;
        }
    }

    [Serializable]
    public sealed class GrowthDevelopmentSnapshotDto
    {
        public int Year;
        public int Month;
        public ConstructionSiteDto[] Sites = Array.Empty<ConstructionSiteDto>();
        public GrowthEventDto[] Events = Array.Empty<GrowthEventDto>();
        public DevelopmentSectorSummaryDto[] Sectors = Array.Empty<DevelopmentSectorSummaryDto>();

        public SimulationDate Date => new SimulationDate(Year, Month);
    }
}
