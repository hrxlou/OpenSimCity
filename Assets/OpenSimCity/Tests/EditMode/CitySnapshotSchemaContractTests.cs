using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenSimCity.Application.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    /// <summary>
    /// Locks the exact field declaration order used by JsonUtility and the binary
    /// envelope payload. Adding/removing/reordering/changing a field requires an
    /// explicit format-version decision and a migration step before this constant
    /// is intentionally updated.
    /// </summary>
    public sealed class CitySnapshotSchemaContractTests
    {
        private const ulong ExpectedSchemaChecksum = 0x8BEF4A499CE554CCUL;

        private static readonly string[] ExpectedContract =
        {
            "OpenSimCity.Application.Persistence.CitySnapshotDto",
            "  Header:OpenSimCity.Application.Persistence.CitySnapshotHeaderDto",
            "  World:OpenSimCity.Simulation.World.WorldStateDto",
            "  Timeline:OpenSimCity.Application.Persistence.CityTimelineSnapshotDto",
            "  Roads:OpenSimCity.Application.Persistence.RoadNetworkSnapshotDto",
            "  Zones:OpenSimCity.Application.Persistence.ZoneNetworkSnapshotDto",
            "  Parcels:OpenSimCity.Application.Persistence.ParcelNetworkSnapshotDto",
            "  ExternalConnections:OpenSimCity.Application.Persistence.ExternalConnectionCollectionSnapshotDto",
            "  Buildings:OpenSimCity.Simulation.Buildings.BuildingStateCollectionDto",
            "  PowerProducers:OpenSimCity.Simulation.Utilities.PowerProducerCollectionDto",
            "  Growth:OpenSimCity.Simulation.Development.GrowthDevelopmentSystemStateDto",
            "  PopulationEmployment:OpenSimCity.Simulation.Population.PopulationEmploymentMigrationStateDto",
            "  UrbanEconomy:OpenSimCity.Simulation.Economy.UrbanEconomySystemStateDto",
            "  MunicipalFinance:OpenSimCity.Simulation.Economy.MunicipalFinanceStateDto",
            "OpenSimCity.Application.Persistence.CitySnapshotHeaderDto",
            "  Magic:System.String",
            "  FormatVersion:System.Int32",
            "  GameVersion:System.String",
            "  CreatedUtcTicks:System.Int64",
            "  WorldSeed:System.UInt64",
            "  DefaultRoadDefinitionId:System.UInt64",
            "  DefaultPowerPlantDefinitionId:System.UInt64",
            "OpenSimCity.Simulation.World.WorldStateDto",
            "  Width:System.Int32",
            "  Height:System.Int32",
            "  Seed:System.UInt64",
            "OpenSimCity.Application.Persistence.CityTimelineSnapshotDto",
            "  CurrentTick:System.Int64",
            "  NextCommandSequence:System.UInt64",
            "  FixedDeltaSeconds:System.Double",
            "  MaxCatchUpTicks:System.Int32",
            "  SimulationSpeed:System.Int32",
            "  AccumulatedSeconds:System.Double",
            "  WasCatchUpClamped:System.Boolean",
            "  TicksPerMonth:System.Int32",
            "  CompletedMonths:System.Int32",
            "  NextMonthYear:System.Int32",
            "  NextMonth:System.Int32",
            "  NextSchedulerRunTick:System.Int64",
            "OpenSimCity.Application.Persistence.RoadNetworkSnapshotDto",
            "  Cells:OpenSimCity.Application.Persistence.RoadCellSnapshotDto[]",
            "OpenSimCity.Application.Persistence.RoadCellSnapshotDto",
            "  CellIndex:System.Int32",
            "  DefinitionId:System.UInt64",
            "  TravelDirection:System.Int32",
            "OpenSimCity.Application.Persistence.ZoneNetworkSnapshotDto",
            "  Cells:OpenSimCity.Application.Persistence.ZoneCellSnapshotDto[]",
            "OpenSimCity.Application.Persistence.ZoneCellSnapshotDto",
            "  CellIndex:System.Int32",
            "  ZoneType:System.Int32",
            "  DefinitionId:System.UInt64",
            "OpenSimCity.Application.Persistence.ParcelNetworkSnapshotDto",
            "  Parcels:OpenSimCity.Application.Persistence.ParcelSnapshotDto[]",
            "OpenSimCity.Application.Persistence.ParcelSnapshotDto",
            "  Id:System.UInt64",
            "  ZoneType:System.Int32",
            "  AnchorX:System.Int32",
            "  AnchorY:System.Int32",
            "  Width:System.Int32",
            "  Height:System.Int32",
            "OpenSimCity.Application.Persistence.ExternalConnectionCollectionSnapshotDto",
            "  Connections:OpenSimCity.Application.Persistence.ExternalConnectionSnapshotDto[]",
            "OpenSimCity.Application.Persistence.ExternalConnectionSnapshotDto",
            "  Id:System.UInt64",
            "  Kind:System.Int32",
            "  CellX:System.Int32",
            "  CellY:System.Int32",
            "  IsEnabled:System.Boolean",
            "OpenSimCity.Simulation.Buildings.BuildingStateCollectionDto",
            "  NextBuildingId:System.UInt64",
            "  Buildings:OpenSimCity.Simulation.Buildings.BuildingStateDto[]",
            "OpenSimCity.Simulation.Buildings.BuildingStateDto",
            "  Id:System.UInt64",
            "  DefinitionId:System.UInt64",
            "  ParcelId:System.UInt64",
            "  AnchorX:System.Int32",
            "  AnchorY:System.Int32",
            "  IsOperational:System.Boolean",
            "  Occupancy:System.Int32",
            "  FilledJobs:System.Int32",
            "OpenSimCity.Simulation.Utilities.PowerProducerCollectionDto",
            "  Producers:OpenSimCity.Simulation.Utilities.PowerProducerStateDto[]",
            "OpenSimCity.Simulation.Utilities.PowerProducerStateDto",
            "  Id:System.UInt64",
            "  DefinitionId:System.UInt64",
            "  AnchorX:System.Int32",
            "  AnchorY:System.Int32",
            "  CurrentProduction:System.Int32",
            "OpenSimCity.Simulation.Development.GrowthDevelopmentSystemStateDto",
            "  HasProcessedMonth:System.Boolean",
            "  LastProcessedYear:System.Int32",
            "  LastProcessedMonth:System.Int32",
            "  LastObservedParcelVersion:System.Int32",
            "  LastObservedPowerVersion:System.Int32",
            "  LastObservedBuildingVersion:System.Int32",
            "  ConstructionSequence:System.UInt64",
            "  LastEvaluatedParcels:System.Int32",
            "  LastStartedConstructions:System.Int32",
            "  LastCompletedConstructions:System.Int32",
            "  LastSkippedByCap:System.Int32",
            "  Development:OpenSimCity.Simulation.Development.DevelopmentStateDto",
            "  LastEvents:OpenSimCity.Simulation.Development.GrowthEventDto[]",
            "OpenSimCity.Simulation.Development.DevelopmentStateDto",
            "  Sites:OpenSimCity.Simulation.Development.ConstructionSiteDto[]",
            "OpenSimCity.Simulation.Development.ConstructionSiteDto",
            "  BuildingId:System.UInt64",
            "  ParcelId:System.UInt64",
            "  DefinitionId:System.UInt64",
            "  StartedYear:System.Int32",
            "  StartedMonth:System.Int32",
            "  RemainingMonths:System.Int32",
            "OpenSimCity.Simulation.Development.GrowthEventDto",
            "  Kind:System.Int32",
            "  BuildingId:System.UInt64",
            "  ParcelId:System.UInt64",
            "  DefinitionId:System.UInt64",
            "  Year:System.Int32",
            "  Month:System.Int32",
            "OpenSimCity.Simulation.Population.PopulationEmploymentMigrationStateDto",
            "  HasProcessedMonth:System.Boolean",
            "  LastProcessedYear:System.Int32",
            "  LastProcessedMonth:System.Int32",
            "  Population:OpenSimCity.Simulation.Population.PopulationStateDto",
            "  Employment:OpenSimCity.Simulation.Employment.EmploymentStateDto",
            "  Migration:OpenSimCity.Simulation.Population.MigrationStateDto",
            "OpenSimCity.Simulation.Population.PopulationStateDto",
            "  TotalResidents:System.Int32",
            "  TotalCapacity:System.Int32",
            "  Buildings:OpenSimCity.Simulation.Population.BuildingPopulationStateDto[]",
            "OpenSimCity.Simulation.Population.BuildingPopulationStateDto",
            "  BuildingId:System.UInt64",
            "  Residents:System.Int32",
            "  Capacity:System.Int32",
            "OpenSimCity.Simulation.Employment.EmploymentStateDto",
            "  TotalFilledJobs:System.Int32",
            "  TotalCapacity:System.Int32",
            "  Buildings:OpenSimCity.Simulation.Employment.BuildingEmploymentStateDto[]",
            "OpenSimCity.Simulation.Employment.BuildingEmploymentStateDto",
            "  BuildingId:System.UInt64",
            "  FilledJobs:System.Int32",
            "  Capacity:System.Int32",
            "OpenSimCity.Simulation.Population.MigrationStateDto",
            "  ExternalRoadConnectionAvailable:System.Boolean",
            "  PotentialArrivals:System.Int32",
            "  Arrivals:System.Int32",
            "  VacantHousingBefore:System.Int32",
            "  VacantJobsBefore:System.Int32",
            "  VacantHousingAfter:System.Int32",
            "  VacantJobsAfter:System.Int32",
            "OpenSimCity.Simulation.Economy.UrbanEconomySystemStateDto",
            "  HasProcessedMonth:System.Boolean",
            "  LastProcessedYear:System.Int32",
            "  LastProcessedMonth:System.Int32",
            "  ResidentialTaxPermille:System.Int32",
            "  CommercialTaxPermille:System.Int32",
            "  IndustrialTaxPermille:System.Int32",
            "  Market:OpenSimCity.Simulation.Economy.MarketStateDto",
            "  Rci:OpenSimCity.Simulation.Economy.RciDemandStateDto",
            "OpenSimCity.Simulation.Economy.MarketStateDto",
            "  GoodsSupply:System.Int32",
            "  ConsumptionPower:System.Int32",
            "  GoodsSold:System.Int32",
            "  GoodsShortage:System.Int32",
            "  GoodsSurplus:System.Int32",
            "  CommercialThroughputCapacity:System.Int32",
            "  CommercialThroughput:System.Int32",
            "  BusinessUtilizationPermille:System.Int32",
            "OpenSimCity.Simulation.Economy.RciDemandStateDto",
            "  Residential:OpenSimCity.Simulation.Economy.RciSectorDemandDto",
            "  Commercial:OpenSimCity.Simulation.Economy.RciSectorDemandDto",
            "  Industrial:OpenSimCity.Simulation.Economy.RciSectorDemandDto",
            "OpenSimCity.Simulation.Economy.RciSectorDemandDto",
            "  RawDemand:System.Int32",
            "  ClampedDemand:System.Int32",
            "  SmoothedDemand:System.Int32",
            "  Factors:OpenSimCity.Simulation.Economy.RciDemandFactorDto[]",
            "OpenSimCity.Simulation.Economy.RciDemandFactorDto",
            "  Name:System.String",
            "  Contribution:System.Int32",
            "OpenSimCity.Simulation.Economy.MunicipalFinanceStateDto",
            "  Balance:System.Int32",
            "  ActiveResidentialTaxPermille:System.Int32",
            "  ActiveCommercialTaxPermille:System.Int32",
            "  ActiveIndustrialTaxPermille:System.Int32",
            "  PendingResidentialTaxPermille:System.Int32",
            "  PendingCommercialTaxPermille:System.Int32",
            "  PendingIndustrialTaxPermille:System.Int32",
            "  PendingEffectiveYear:System.Int32",
            "  PendingEffectiveMonth:System.Int32",
            "  RoadConstructionExpenseToDate:System.Int32",
            "  UtilityConstructionExpenseToDate:System.Int32",
            "  Entries:OpenSimCity.Simulation.Economy.MunicipalLedgerEntryDto[]",
            "OpenSimCity.Simulation.Economy.MunicipalLedgerEntryDto",
            "  Year:System.Int32",
            "  Month:System.Int32",
            "  OpeningBalance:System.Int32",
            "  ResidentialTaxIncome:System.Int32",
            "  CommercialTaxIncome:System.Int32",
            "  IndustrialTaxIncome:System.Int32",
            "  RoadMaintenanceExpense:System.Int32",
            "  PowerMaintenanceExpense:System.Int32",
            "  ClosingBalance:System.Int32"
        };

        [Test]
        public void SnapshotSchemaDeclarationOrderMatchesVersionOneContract()
        {
            var actual = BuildContract(typeof(CitySnapshotDto));
            Assert.That(actual, Is.EqualTo(ExpectedContract));
            Assert.That(ComputeChecksum(actual), Is.EqualTo(ExpectedSchemaChecksum));
        }

        private static IReadOnlyList<string> BuildContract(Type root)
        {
            var rows = new List<string>();
            Walk(root, new HashSet<Type>(), rows);
            return rows;
        }

        private static void Walk(Type type, ISet<Type> visited, ICollection<string> rows)
        {
            if (IsScalar(type))
                return;
            if (type.IsArray)
            {
                Walk(type.GetElementType(), visited, rows);
                return;
            }
            if (!visited.Add(type))
                return;

            rows.Add(type.FullName);
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].IsStatic)
                    continue;
                rows.Add($"  {fields[index].Name}:{TypeName(fields[index].FieldType)}");
            }
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].IsStatic)
                    continue;
                Walk(fields[index].FieldType, visited, rows);
            }
        }

        private static string TypeName(Type type)
        {
            if (type.IsArray)
                return TypeName(type.GetElementType()) + "[]";
            return type.FullName;
        }

        private static bool IsScalar(Type type)
        {
            return type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(string);
        }

        private static ulong ComputeChecksum(IEnumerable<string> rows)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var row in rows)
            {
                for (var index = 0; index < row.Length; index++)
                {
                    var value = row[index];
                    hash ^= (byte)value;
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }
                hash ^= (byte)'\n';
                hash *= prime;
            }
            return hash;
        }
    }
}
