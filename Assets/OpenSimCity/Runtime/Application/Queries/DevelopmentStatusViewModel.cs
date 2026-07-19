using System;
using System.Collections.Generic;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Queries
{
    public enum DevelopmentAvailabilityStatus : byte
    {
        NoDemand = 1,
        DemandAndFeasible = 2,
        DemandButNoFeasibleParcels = 3
    }

    [Serializable]
    public sealed class DevelopmentBlockReasonSummaryDto
    {
        public DevelopmentBlockReason Reason;
        public int ParcelCount;
    }

    [Serializable]
    public sealed class DevelopmentSectorStatusDto
    {
        public EconomicSector Sector;
        public int SmoothedDemand;
        public int FeasibleParcelCount;
        public DevelopmentAvailabilityStatus Status;
    }

    /// <summary>
    /// Immutable HUD-oriented monthly diagnostic. The order of block reasons is
    /// deterministic (count descending, stable enum code ascending), and the UI
    /// must display it without re-ranking or recomputing parcel factors.
    /// </summary>
    [Serializable]
    public sealed class DevelopmentStatusSnapshotDto
    {
        public int Year;
        public int Month;
        public DevelopmentAvailabilityStatus OverallStatus;
        public int TotalFeasibleParcelCount;
        public int AverageAccessibilityFactor;
        public bool HasReachableExternalRoad;
        public bool HasConnectedPowerProducer;
        public DevelopmentSectorStatusDto[] Sectors = Array.Empty<DevelopmentSectorStatusDto>();
        public DevelopmentBlockReasonSummaryDto[] TopBlockReasons = Array.Empty<DevelopmentBlockReasonSummaryDto>();
        public int ResidentialDemandChange;
        public int CommercialDemandChange;
        public int IndustrialDemandChange;
        public int FeasibleParcelChange;
        public bool ExternalRoadChanged;
        public bool PowerConnectionChanged;

        public SimulationDate Date => new SimulationDate(Year, Month);
    }

    /// <summary>
    /// Application ViewModel for the P3 HUD. It consumes read models only and
    /// copies the current monthly diagnostic into a DTO; no Unity view observes
    /// simulation arrays or calculates reason priority on its own.
    /// </summary>
    public sealed class DevelopmentStatusViewModel
    {
        private readonly GrowthDevelopmentReadModel _growth;
        private readonly ParcelReadModel _parcels;
        private readonly RoadAccessibilityReadModel _roads;
        private readonly PowerReadModel _power;

        private int _growthVersion = -1;
        private DevelopmentStatusSnapshotDto _snapshot;

        public DevelopmentStatusViewModel(
            GrowthDevelopmentReadModel growth,
            ParcelReadModel parcels,
            RoadAccessibilityReadModel roads,
            PowerReadModel power)
        {
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
            _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _power = power ?? throw new ArgumentNullException(nameof(power));
        }

        public bool HasSnapshot
        {
            get { RefreshIfNeeded(); return _snapshot != null; }
        }

        public DevelopmentStatusSnapshotDto Snapshot
        {
            get
            {
                RefreshIfNeeded();
                if (_snapshot == null)
                    throw new InvalidOperationException("No monthly development-status snapshot has been produced yet.");
                return _snapshot;
            }
        }

        public bool RefreshIfNeeded()
        {
            var version = _growth.Version;
            if (_growthVersion == version)
                return false;

            _growthVersion = version;
            if (!_growth.HasSnapshot)
            {
                _snapshot = null;
                return true;
            }

            var next = BuildSnapshot(_growth.Snapshot);
            ApplyPeriodChanges(next, _snapshot);
            _snapshot = next;
            return true;
        }

        private DevelopmentStatusSnapshotDto BuildSnapshot(GrowthDevelopmentSnapshotDto growthSnapshot)
        {
            var sectors = BuildSectors(growthSnapshot.Sectors, out var hasDemand, out var totalFeasible);
            var blockCounts = new Dictionary<DevelopmentBlockReason, int>();
            var suitabilityTotal = 0;
            var suitabilityCount = 0;

            var parcelRows = _parcels.Parcels;
            for (var index = 0; index < parcelRows.Count; index++)
            {
                var parcel = parcelRows[index];
                if (!_growth.TryGetFeasibility(parcel.Id, out var feasibility))
                    continue;
                if (!HasDemandFor(sectors, feasibility.Sector))
                    continue;

                suitabilityTotal = checked(suitabilityTotal + feasibility.SuitabilityFactor);
                suitabilityCount = checked(suitabilityCount + 1);
                if (IsActionableBlock(feasibility.BlockReason))
                {
                    blockCounts.TryGetValue(feasibility.BlockReason, out var count);
                    blockCounts[feasibility.BlockReason] = checked(count + 1);
                }
            }

            var hasReachableExternalRoad = false;
            var roadCells = _roads.Cells;
            for (var index = 0; index < roadCells.Count; index++)
            {
                if (roadCells[index].HasRoad && roadCells[index].IsReachable)
                {
                    hasReachableExternalRoad = true;
                    break;
                }
            }

            var hasConnectedPowerProducer = false;
            var producers = _power.Producers;
            for (var index = 0; index < producers.Count; index++)
            {
                if (producers[index].IsConnected)
                {
                    hasConnectedPowerProducer = true;
                    break;
                }
            }

            return new DevelopmentStatusSnapshotDto
            {
                Year = growthSnapshot.Year,
                Month = growthSnapshot.Month,
                OverallStatus = !hasDemand
                    ? DevelopmentAvailabilityStatus.NoDemand
                    : totalFeasible > 0
                        ? DevelopmentAvailabilityStatus.DemandAndFeasible
                        : DevelopmentAvailabilityStatus.DemandButNoFeasibleParcels,
                TotalFeasibleParcelCount = totalFeasible,
                AverageAccessibilityFactor = suitabilityCount == 0 ? 0 : suitabilityTotal / suitabilityCount,
                HasReachableExternalRoad = hasReachableExternalRoad,
                HasConnectedPowerProducer = hasConnectedPowerProducer,
                Sectors = sectors,
                TopBlockReasons = BuildTopReasons(blockCounts)
            };
        }

        private static DevelopmentSectorStatusDto[] BuildSectors(
            DevelopmentSectorSummaryDto[] source,
            out bool hasDemand,
            out int totalFeasible)
        {
            var rows = new DevelopmentSectorStatusDto[source?.Length ?? 0];
            hasDemand = false;
            totalFeasible = 0;
            for (var index = 0; index < rows.Length; index++)
            {
                var summary = source[index];
                var demand = summary.SmoothedDemand > 0;
                hasDemand |= demand;
                if (demand)
                    totalFeasible = checked(totalFeasible + summary.FeasibleParcelCount);
                rows[index] = new DevelopmentSectorStatusDto
                {
                    Sector = (EconomicSector)summary.Sector,
                    SmoothedDemand = summary.SmoothedDemand,
                    FeasibleParcelCount = summary.FeasibleParcelCount,
                    Status = !demand
                        ? DevelopmentAvailabilityStatus.NoDemand
                        : summary.FeasibleParcelCount > 0
                            ? DevelopmentAvailabilityStatus.DemandAndFeasible
                            : DevelopmentAvailabilityStatus.DemandButNoFeasibleParcels
                };
            }

            return rows;
        }

        private static bool HasDemandFor(DevelopmentSectorStatusDto[] sectors, EconomicSector sector)
        {
            for (var index = 0; index < sectors.Length; index++)
            {
                if (sectors[index].Sector == sector)
                    return sectors[index].SmoothedDemand > 0;
            }

            return false;
        }

        private static bool IsActionableBlock(DevelopmentBlockReason reason)
        {
            return reason == DevelopmentBlockReason.RoadIneligible ||
                   reason == DevelopmentBlockReason.NoPowerCapacity ||
                   reason == DevelopmentBlockReason.NoMatchingDefinition;
        }

        private static DevelopmentBlockReasonSummaryDto[] BuildTopReasons(
            Dictionary<DevelopmentBlockReason, int> counts)
        {
            var rows = new List<DevelopmentBlockReasonSummaryDto>(counts.Count);
            foreach (var pair in counts)
                rows.Add(new DevelopmentBlockReasonSummaryDto { Reason = pair.Key, ParcelCount = pair.Value });

            rows.Sort((left, right) =>
            {
                var countOrder = right.ParcelCount.CompareTo(left.ParcelCount);
                return countOrder != 0 ? countOrder : left.Reason.CompareTo(right.Reason);
            });

            if (rows.Count > 3)
                rows.RemoveRange(3, rows.Count - 3);
            return rows.ToArray();
        }

        private static void ApplyPeriodChanges(
            DevelopmentStatusSnapshotDto current,
            DevelopmentStatusSnapshotDto previous)
        {
            if (previous == null)
                return;

            current.ResidentialDemandChange = SectorDemand(current, EconomicSector.Residential) -
                                              SectorDemand(previous, EconomicSector.Residential);
            current.CommercialDemandChange = SectorDemand(current, EconomicSector.Commercial) -
                                             SectorDemand(previous, EconomicSector.Commercial);
            current.IndustrialDemandChange = SectorDemand(current, EconomicSector.Industrial) -
                                             SectorDemand(previous, EconomicSector.Industrial);
            current.FeasibleParcelChange = current.TotalFeasibleParcelCount - previous.TotalFeasibleParcelCount;
            current.ExternalRoadChanged = current.HasReachableExternalRoad != previous.HasReachableExternalRoad;
            current.PowerConnectionChanged = current.HasConnectedPowerProducer != previous.HasConnectedPowerProducer;
        }

        private static int SectorDemand(DevelopmentStatusSnapshotDto snapshot, EconomicSector sector)
        {
            for (var index = 0; index < snapshot.Sectors.Length; index++)
            {
                if (snapshot.Sectors[index].Sector == sector)
                    return snapshot.Sectors[index].SmoothedDemand;
            }

            return 0;
        }
    }
}
