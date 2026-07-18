using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Simulation.Development
{
    /// <summary>
    /// Monthly building growth. A parcel develops only when every requirement is met —
    /// road/external eligibility (P2), remaining power capacity (P3-02) and positive
    /// sector demand (P3-04) — after which a definition is sampled and a construction
    /// site is started, respecting a monthly cap and a construction time.
    /// <para>
    /// Determinism: the candidate pool is built in stable parcel-ID order and every draw
    /// comes from a private RNG stream keyed to the "growth" subsystem, so adding calls
    /// in other systems never shifts growth results. Accessibility bands weight sampling
    /// but never remove an eligible parcel, so no single layout is forced. View state and
    /// prefab existence never reach this system.
    /// </para>
    /// </summary>
    public sealed class GrowthDevelopmentSystem : IMonthlySimulationSystem
    {
        private const ulong BuildingIdNamespace = 0x6100_0000_0000_0000UL;
        private const string RngSubsystemKey = "growth";

        private readonly BuildingStateCollection _buildings;
        private readonly ParcelNetworkState _parcels;
        private readonly PowerNetwork _power;
        private readonly IRciDemandProvider _demand;
        private readonly IDevelopmentDefinitionResolver _definitions;
        private readonly GrowthDevelopmentParameters _parameters;
        private readonly int _worldWidth;

        private DeterministicRandom _rng;
        private GrowthEvent[] _lastEvents = Array.Empty<GrowthEvent>();
        private DevelopmentSectorSummary[] _summaries = Array.Empty<DevelopmentSectorSummary>();

        public GrowthDevelopmentSystem(
            BuildingStateCollection buildings,
            ParcelNetworkState parcels,
            PowerNetwork power,
            IRciDemandProvider demand,
            IDevelopmentDefinitionResolver definitions,
            GrowthDevelopmentParameters parameters = null,
            ulong seed = 0)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
            _power = power ?? throw new ArgumentNullException(nameof(power));
            _demand = demand ?? throw new ArgumentNullException(nameof(demand));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _parameters = parameters ?? GrowthDevelopmentParameters.Default;
            _worldWidth = parcels.Width;
            _rng = DeterministicRandom.ForSubsystem(seed, RngSubsystemKey);
        }

        public DevelopmentState State { get; } = new DevelopmentState();

        public GrowthDevelopmentParameters Parameters => _parameters;

        public IReadOnlyList<GrowthEvent> LastEvents => _lastEvents;

        public IReadOnlyList<DevelopmentSectorSummary> SectorSummaries => _summaries;

        public SimulationDate LastProcessedMonth { get; private set; }

        public bool HasProcessedMonth { get; private set; }

        public int Version { get; private set; }

        public void RunMonth(SimulationDate month)
        {
            if (HasProcessedMonth && month.CompareTo(LastProcessedMonth) <= 0)
                throw new InvalidOperationException("Monthly growth requires a strictly increasing month.");

            var events = new List<GrowthEvent>();

            AdvanceConstruction(events);

            _parcels.RefreshIfNeeded();
            _power.RefreshIfNeeded();

            var reserved = BuildReservation();
            var developedParcels = CollectDevelopedParcels();
            var candidates = new List<Candidate>();
            var summaries = InitializeSummaries();
            GatherCandidates(candidates, summaries, reserved, developedParcels);
            Develop(candidates, summaries, reserved, events);
            FinalizeSummaries(summaries);

            _lastEvents = events.ToArray();
            _summaries = summaries;
            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        /// <summary>
        /// Live per-parcel feasibility for the public development query. Recomputes
        /// against the current parcel/power/demand state and active reservations.
        /// </summary>
        public bool TryGetFeasibility(StableId parcelId, out DevelopmentFeasibility feasibility)
        {
            _parcels.RefreshIfNeeded();
            _power.RefreshIfNeeded();

            if (!_parcels.TryGet(parcelId, out var parcel))
            {
                feasibility = new DevelopmentFeasibility(
                    parcelId, default, false, DevelopmentBlockReason.UnknownParcel,
                    RoadConnectivityGraph.UnreachableDistance, ExternalGateDistanceBand.Unreachable, 0);
                return false;
            }

            var reserved = BuildReservation();
            var developedParcels = CollectDevelopedParcels();
            feasibility = Evaluate(parcel, reserved, developedParcels);
            return true;
        }

        private void AdvanceConstruction(List<GrowthEvent> events)
        {
            if (State.Count == 0)
                return;

            var snapshot = new List<ConstructionSite>(State.Sites);
            for (var index = 0; index < snapshot.Count; index++)
            {
                var site = snapshot[index];
                if (site.CompletesNextMonth)
                {
                    State.Remove(site.BuildingId);
                    _buildings.Create(site.BuildingId, site.DefinitionId, site.ParcelId, site.Anchor);
                    events.Add(new GrowthEvent(
                        GrowthEventKind.ConstructionCompleted,
                        site.BuildingId, site.DefinitionId, site.ParcelId, site.Anchor, 0));
                }
                else
                {
                    var advanced = site.Advanced();
                    State.Replace(advanced);
                    events.Add(new GrowthEvent(
                        GrowthEventKind.ConstructionProgressed,
                        advanced.BuildingId, advanced.DefinitionId, advanced.ParcelId, advanced.Anchor,
                        advanced.MonthsRemaining));
                }
            }
        }

        private Dictionary<int, int> BuildReservation()
        {
            var reserved = new Dictionary<int, int>();
            var sites = State.Sites;
            for (var index = 0; index < sites.Count; index++)
            {
                var site = sites[index];
                if (!_definitions.TryGetProfile(site.DefinitionId, out var profile))
                    continue;
                var component = ComponentFor(site.Anchor, profile);
                if (component == PowerNetwork.NoComponent)
                    continue;
                reserved.TryGetValue(component, out var current);
                reserved[component] = checked(current + profile.PowerDemand);
            }

            return reserved;
        }

        private HashSet<StableId> CollectDevelopedParcels()
        {
            var developed = new HashSet<StableId>();
            var buildings = _buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
                developed.Add(buildings[index].ParcelId);
            return developed;
        }

        private DevelopmentSectorSummary[] InitializeSummaries()
        {
            return new[]
            {
                new DevelopmentSectorSummary(EconomicSector.Residential, _demand.SmoothedDemand(EconomicSector.Residential), 0, 0, false),
                new DevelopmentSectorSummary(EconomicSector.Commercial, _demand.SmoothedDemand(EconomicSector.Commercial), 0, 0, false),
                new DevelopmentSectorSummary(EconomicSector.Industrial, _demand.SmoothedDemand(EconomicSector.Industrial), 0, 0, false)
            };
        }

        private void GatherCandidates(
            List<Candidate> candidates,
            DevelopmentSectorSummary[] summaries,
            Dictionary<int, int> reserved,
            HashSet<StableId> developedParcels)
        {
            var parcels = _parcels.Parcels;
            for (var index = 0; index < parcels.Count; index++)
            {
                var feasibility = Evaluate(parcels[index], reserved, developedParcels);
                if (!feasibility.IsFeasible)
                    continue;

                candidates.Add(new Candidate(parcels[index], feasibility.Sector, feasibility.SuitabilityFactor));
                var sectorIndex = SectorIndex(feasibility.Sector);
                var summary = summaries[sectorIndex];
                summaries[sectorIndex] = new DevelopmentSectorSummary(
                    summary.Sector, summary.SmoothedDemand, summary.FeasibleParcelCount + 1, 0, false);
            }
        }

        private void Develop(
            List<Candidate> candidates,
            DevelopmentSectorSummary[] summaries,
            Dictionary<int, int> reserved,
            List<GrowthEvent> events)
        {
            var remaining = _parameters.MaxDevelopmentsPerMonth;
            while (remaining > 0 && candidates.Count > 0)
            {
                var totalWeight = 0;
                for (var index = 0; index < candidates.Count; index++)
                    totalWeight = checked(totalWeight + candidates[index].Suitability);

                var pick = WeightedPick(candidates, totalWeight);
                var candidate = candidates[pick];

                var feasibleDefs = FeasibleDefinitions(candidate, reserved);
                if (feasibleDefs.Count == 0)
                {
                    candidates.RemoveAt(pick);
                    continue;
                }

                var definition = feasibleDefs[_rng.NextInt(feasibleDefs.Count)];
                var buildingId = BuildingIdFor(candidate.Parcel.Anchor);
                var site = new ConstructionSite(
                    buildingId,
                    definition.Profile.DefinitionId,
                    candidate.Parcel.Id,
                    candidate.Parcel.Anchor,
                    _parameters.ConstructionMonths,
                    _parameters.ConstructionMonths);
                State.Add(site);

                reserved.TryGetValue(definition.Component, out var current);
                reserved[definition.Component] = checked(current + definition.Profile.PowerDemand);

                events.Add(new GrowthEvent(
                    GrowthEventKind.ConstructionStarted,
                    buildingId, definition.Profile.DefinitionId, candidate.Parcel.Id,
                    candidate.Parcel.Anchor, _parameters.ConstructionMonths));

                var sectorIndex = SectorIndex(candidate.Sector);
                var summary = summaries[sectorIndex];
                summaries[sectorIndex] = new DevelopmentSectorSummary(
                    summary.Sector, summary.SmoothedDemand, summary.FeasibleParcelCount,
                    summary.StartedThisMonth + 1, false);

                candidates.RemoveAt(pick);
                remaining--;
            }
        }

        private void FinalizeSummaries(DevelopmentSectorSummary[] summaries)
        {
            for (var index = 0; index < summaries.Length; index++)
            {
                var summary = summaries[index];
                var hasUnmetDemand = summary.SmoothedDemand >= _parameters.DemandThreshold &&
                                     summary.FeasibleParcelCount == 0;
                summaries[index] = new DevelopmentSectorSummary(
                    summary.Sector, summary.SmoothedDemand, summary.FeasibleParcelCount,
                    summary.StartedThisMonth, hasUnmetDemand);
            }
        }

        private DevelopmentFeasibility Evaluate(
            ParcelState parcel,
            Dictionary<int, int> reserved,
            HashSet<StableId> developedParcels)
        {
            if (!DevelopmentSectors.TryFromZoneType(parcel.ZoneType, out var sector))
            {
                return new DevelopmentFeasibility(
                    parcel.Id, default, false, DevelopmentBlockReason.NoMatchingDefinition,
                    parcel.NetworkDistanceToExternalGate, parcel.DistanceBand, 0);
            }

            var suitability = _parameters.SuitabilityFactor(sector, parcel.DistanceBand);

            DevelopmentBlockReason reason;
            if (State.IsParcelUnderConstruction(parcel.Id))
                reason = DevelopmentBlockReason.UnderConstruction;
            else if (developedParcels.Contains(parcel.Id))
                reason = DevelopmentBlockReason.AlreadyDeveloped;
            else if (!parcel.IsDevelopmentEligible)
                reason = DevelopmentBlockReason.RoadIneligible;
            else if (_demand.SmoothedDemand(sector) < _parameters.DemandThreshold)
                reason = DevelopmentBlockReason.NoSectorDemand;
            else
                reason = PowerAndDefinitionReason(parcel, sector, reserved);

            return new DevelopmentFeasibility(
                parcel.Id, sector, reason == DevelopmentBlockReason.None, reason,
                parcel.NetworkDistanceToExternalGate, parcel.DistanceBand, suitability);
        }

        private DevelopmentBlockReason PowerAndDefinitionReason(
            ParcelState parcel,
            EconomicSector sector,
            Dictionary<int, int> reserved)
        {
            var fitsFootprint = false;
            var definitions = _definitions.DefinitionsForSector(sector);
            for (var index = 0; index < definitions.Count; index++)
            {
                var profile = definitions[index];
                if (!FootprintFits(profile.Footprint, parcel))
                    continue;
                fitsFootprint = true;
                if (PowerFeasible(parcel.Anchor, profile, reserved, out _))
                    return DevelopmentBlockReason.None;
            }

            return fitsFootprint
                ? DevelopmentBlockReason.NoPowerCapacity
                : DevelopmentBlockReason.NoMatchingDefinition;
        }

        private List<FeasibleDefinition> FeasibleDefinitions(Candidate candidate, Dictionary<int, int> reserved)
        {
            var results = new List<FeasibleDefinition>();
            var definitions = _definitions.DefinitionsForSector(candidate.Sector);
            for (var index = 0; index < definitions.Count; index++)
            {
                var profile = definitions[index];
                if (!FootprintFits(profile.Footprint, candidate.Parcel))
                    continue;
                if (PowerFeasible(candidate.Parcel.Anchor, profile, reserved, out var component))
                    results.Add(new FeasibleDefinition(profile, component));
            }

            return results;
        }

        private bool PowerFeasible(
            CellCoord anchor,
            DevelopmentBuildingProfile profile,
            Dictionary<int, int> reserved,
            out int component)
        {
            var eligibility = _power.QueryEligibility(
                anchor, new BuildingPowerProfile(profile.DefinitionId, profile.Footprint, profile.PowerDemand));
            component = eligibility.ComponentId;
            if (component == PowerNetwork.NoComponent)
                return false;
            if (eligibility.FailureReason == PowerSupplyFailureReason.NoPowerProducer)
                return false;

            reserved.TryGetValue(component, out var reservedDemand);
            return eligibility.AvailableCapacity - reservedDemand >= profile.PowerDemand;
        }

        private int ComponentFor(CellCoord anchor, DevelopmentBuildingProfile profile)
        {
            var eligibility = _power.QueryEligibility(
                anchor, new BuildingPowerProfile(profile.DefinitionId, profile.Footprint, profile.PowerDemand));
            return eligibility.ComponentId;
        }

        private int WeightedPick(List<Candidate> candidates, int totalWeight)
        {
            var roll = _rng.NextInt(totalWeight);
            var cumulative = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                cumulative += candidates[index].Suitability;
                if (roll < cumulative)
                    return index;
            }

            return candidates.Count - 1;
        }

        private StableId BuildingIdFor(CellCoord anchor)
        {
            var index = checked(anchor.Y * _worldWidth + anchor.X);
            return new StableId(BuildingIdNamespace | (ulong)(uint)(index + 1));
        }

        private static bool FootprintFits(BuildingFootprint footprint, ParcelState parcel)
        {
            return footprint.Width <= parcel.Width && footprint.Height <= parcel.Height;
        }

        private static int SectorIndex(EconomicSector sector)
        {
            switch (sector)
            {
                case EconomicSector.Residential:
                    return 0;
                case EconomicSector.Commercial:
                    return 1;
                case EconomicSector.Industrial:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }

        private readonly struct Candidate
        {
            public Candidate(ParcelState parcel, EconomicSector sector, int suitability)
            {
                Parcel = parcel;
                Sector = sector;
                Suitability = suitability;
            }

            public ParcelState Parcel { get; }
            public EconomicSector Sector { get; }
            public int Suitability { get; }
        }

        private readonly struct FeasibleDefinition
        {
            public FeasibleDefinition(DevelopmentBuildingProfile profile, int component)
            {
                Profile = profile;
                Component = component;
            }

            public DevelopmentBuildingProfile Profile { get; }
            public int Component { get; }
        }
    }
}
