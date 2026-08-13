using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.WaterDensity;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class WaterDensityRuntimeTests
    {
        [Test]
        public void PumpPreviewDoesNotSpendAndCommitCreatesStableProducer()
        {
            var runtime = CreateRuntimeWithRoad(out _);
            var module = WaterDensityRuntimeRegistry.GetOrCreate(runtime);
            var cell = new CellCoord(2, 3);
            var producerId = module.ProducerIdFor(cell);
            var command = new PlaceWaterProducerCommand(producerId, WaterDensityDefinitionIds.Pump, cell);
            var preview = module.Commands.Preview(command);
            var funds = runtime.Finance.Funds;
            Assert.That(preview.IsValid, Is.True, preview.Message);
            Assert.That(runtime.Finance.Funds, Is.EqualTo(funds));
            Assert.That(module.WaterProducers.Producers, Is.Empty);
            var result = module.Execute(command);
            Assert.That(result.IsApplied, Is.True, result.Message);
            Assert.That(module.WaterProducers.Producers.Single().Id, Is.EqualTo(producerId));
            Assert.That(runtime.Finance.Funds, Is.EqualTo(funds - module.WaterDefinitions.Get(WaterDensityDefinitionIds.Pump).ConstructionCost));
        }

        [Test]
        public void RestoredMediumStageComposesExpandedCapacityWithConditionResolver()
        {
            var runtime = CreateRuntimeWithRoad(out var building);
            var module = WaterDensityRuntimeRegistry.GetOrCreate(runtime);
            module.Restore(new WaterDensityRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Water = new WaterRuntimeSnapshotDto { SchemaVersion = 1, Producers = Array.Empty<WaterProducerStateDto>() },
                Density = new DensityRuntimeSnapshotDto
                {
                    SchemaVersion = 1,
                    HasProcessedMonth = true,
                    LastProcessedYear = 1,
                    LastProcessedMonth = 1,
                    Buildings = new[]
                    {
                        new DensityBuildingStateDto
                        {
                            BuildingId = building.Id.Value,
                            Stage = (int)DensityStage.Medium,
                            TargetStage = (int)DensityStage.Medium,
                            UpgradeMonthsRemaining = 0,
                            StageAgeMonths = 1,
                            TopReason = (int)DensityUpgradeBlockReason.None,
                            Factors = Array.Empty<DensityUpgradeFactorDto>()
                        }
                    }
                }
            });
            var definition = runtime.BuildingDefinitions.Get(building.DefinitionId);
            Assert.That(runtime.PopulationSystem.CapacityResolver.ResolveHousingCapacity(building, definition.OccupancyCapacity), Is.EqualTo(definition.OccupancyCapacity * 2));
            module.ReadModel.RefreshIfNeeded();
            var row = module.ReadModel.Snapshot.Buildings.Single();
            Assert.That(row.Stage, Is.EqualTo(DensityStageView.Medium));
            Assert.That(row.EffectiveHousingCapacity, Is.EqualTo(definition.OccupancyCapacity * 2));
            Assert.That(row.BuildingId, Is.EqualTo(building.Id));
        }

        [Test]
        public void DensityTaxCompatibilityDoesNotInjectSamePeriodBonus()
        {
            var finance = new MunicipalFinanceState(10_000, new UrbanEconomyTaxRates(100, 100, 100));
#pragma warning disable CS0618
            finance.RecordSupplementalTaxIncome(EconomicSector.Residential, 25);
            finance.RecordSupplementalTaxIncome(EconomicSector.Commercial, 15);
            finance.RecordSupplementalTaxIncome(EconomicSector.Industrial, 10);
#pragma warning restore CS0618
            var row = finance.SettleMonth(new SimulationDate(1, 1), 100, 80, 60, 0, 0);
            Assert.That(row.ResidentialTaxIncome, Is.EqualTo(100));
            Assert.That(row.CommercialTaxIncome, Is.EqualTo(80));
            Assert.That(row.IndustrialTaxIncome, Is.EqualTo(60));
            Assert.That(row.TaxIncome, Is.EqualTo(240));
            Assert.That(row.ClosingBalance, Is.EqualTo(10_240));
        }

        [Test]
        public void UnknownDensityBuildingReferenceIsRejectedBeforeRestore()
        {
            var runtime = CreateRuntimeWithRoad(out _);
            var module = WaterDensityRuntimeRegistry.GetOrCreate(runtime);
            var snapshot = new WaterDensityRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Water = new WaterRuntimeSnapshotDto { SchemaVersion = 1, Producers = Array.Empty<WaterProducerStateDto>() },
                Density = new DensityRuntimeSnapshotDto
                {
                    SchemaVersion = 1,
                    Buildings = new[]
                    {
                        new DensityBuildingStateDto
                        {
                            BuildingId = 0xB7FF000000000001UL,
                            Stage = (int)DensityStage.Low,
                            TargetStage = (int)DensityStage.Low,
                            Factors = Array.Empty<DensityUpgradeFactorDto>()
                        }
                    }
                }
            };
            Assert.Throws<ArgumentException>(() => module.Restore(snapshot));
            Assert.That(module.WaterProducers.Producers, Is.Empty);
        }

        private static CityRuntime CreateRuntimeWithRoad(out BuildingState building)
        {
            var runtime = new CityRuntime(new CityRuntimeOptions { Width = 8, Height = 8, Seed = 0x702UL, InitialFunds = 20_000 });
            var road = runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 4), new CellCoord(6, 4)));
            Assert.That(road.IsApplied, Is.True, road.Message);
            var definition = runtime.BuildingDefinitions.Definitions.Single(item => item.ZoneType == ZoneType.ResidentialLow);
            building = new BuildingState(new StableId(0xB702000000000101UL), definition.Id, new StableId(0xA702000000000101UL), new CellCoord(2, 2), definition.Footprint, definition.OccupancyCapacity, 0);
            runtime.Buildings.Add(building);
            return runtime;
        }
    }
}
