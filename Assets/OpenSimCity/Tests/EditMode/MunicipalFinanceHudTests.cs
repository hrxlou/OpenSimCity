using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class MunicipalFinanceHudTests
    {
        [Test]
        public void MonthlyLedgerPreservesOpeningPlusNetEqualsClosingAndSerializes()
        {
            var finance = new MunicipalFinanceState(1_000);
            Assert.That(finance.TrySpend(120), Is.True);
            finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, 120);
            Assert.That(finance.TrySpend(80), Is.True);
            finance.RecordConstructionExpense(MunicipalConstructionCategory.PowerProducer, 80);

            var entry = finance.SettleMonth(
                new SimulationDate(1, 1),
                residentialTaxIncome: 40,
                commercialTaxIncome: 50,
                industrialTaxIncome: 60,
                roadMaintenanceExpense: 10,
                powerMaintenanceExpense: 20);

            Assert.That(entry.OpeningBalance + entry.NetChange, Is.EqualTo(entry.ClosingBalance));
            Assert.That(entry.ClosingBalance, Is.EqualTo(920));
            Assert.That(finance.Balance, Is.EqualTo(entry.ClosingBalance));

            var json = JsonUtility.ToJson(finance.ToDto());
            var restored = JsonUtility.FromJson<MunicipalFinanceStateDto>(json);
            Assert.That(restored.Balance, Is.EqualTo(920));
            Assert.That(restored.Ledger, Has.Length.EqualTo(1));
            Assert.That(restored.Ledger[0].RoadConstructionExpense, Is.EqualTo(120));
            Assert.That(restored.Ledger[0].PowerMaintenanceExpense, Is.EqualTo(20));
        }

        [Test]
        public void TaxCommandAppliesOnlyAtFollowingMonthlyPeriod()
        {
            var runtime = CreateRuntime();
            var execution = runtime.Execute(new SetMunicipalTaxRatesCommand(100, 200, 300));

            Assert.That(execution.IsApplied, Is.True);
            Assert.That(runtime.Finance.HasPendingTaxRateChange, Is.True);
            Assert.That(runtime.Finance.PendingTaxEffectiveMonth, Is.EqualTo(new SimulationDate(1, 2)));
            Assert.That(runtime.Finance.ActiveTaxRates, Is.EqualTo(UrbanEconomyTaxRates.Zero));

            runtime.ProcessSimulationTick(SimulationTick.Zero);

            Assert.That(runtime.Finance.ActiveTaxRates, Is.EqualTo(UrbanEconomyTaxRates.Zero));
            Assert.That(runtime.UrbanEconomySystem.TaxRates, Is.EqualTo(UrbanEconomyTaxRates.Zero));
            Assert.That(runtime.Finance.LatestLedgerEntry.Month, Is.EqualTo(new SimulationDate(1, 1)));

            runtime.ProcessSimulationTick(new SimulationTick(1));

            Assert.That(runtime.Finance.ActiveTaxRates.ResidentialPermille, Is.EqualTo(100));
            Assert.That(runtime.Finance.ActiveTaxRates.CommercialPermille, Is.EqualTo(200));
            Assert.That(runtime.Finance.ActiveTaxRates.IndustrialPermille, Is.EqualTo(300));
            Assert.That(runtime.UrbanEconomySystem.TaxRates.ResidentialPermille, Is.EqualTo(100));
            Assert.That(runtime.Finance.LatestLedgerEntry.Month, Is.EqualTo(new SimulationDate(1, 2)));
        }

        [Test]
        public void RoadAndPowerCommandsAccrueConstructionAndMonthlyMaintenanceInSameLedger()
        {
            var runtime = CreateRuntime(initialFunds: 1_000);
            var gateRoad = new CellCoord(0, runtime.Height / 2);
            var plantCell = new CellCoord(0, runtime.Height / 2 - 1);

            Assert.That(runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, gateRoad, gateRoad)).IsApplied, Is.True);
            Assert.That(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(plantCell), runtime.DefaultPowerPlantDefinitionId, plantCell)).IsApplied, Is.True);
            Assert.That(runtime.Funds, Is.EqualTo(890));

            runtime.ProcessSimulationTick(SimulationTick.Zero);
            var ledger = runtime.Finance.LatestLedgerEntry;

            Assert.That(ledger.RoadConstructionExpense, Is.EqualTo(CityRuntimeOptions.DefaultRoadBuildCost));
            Assert.That(ledger.PowerConstructionExpense, Is.EqualTo(CityRuntimeOptions.DefaultPowerPlantConstructionCost));
            Assert.That(ledger.RoadMaintenanceExpense, Is.EqualTo(CityRuntimeOptions.DefaultRoadMonthlyMaintenanceCost));
            Assert.That(ledger.PowerMaintenanceExpense, Is.EqualTo(CityRuntimeOptions.DefaultPowerPlantMonthlyMaintenanceCost));
            Assert.That(ledger.ClosingBalance, Is.EqualTo(884));
        }

        [Test]
        public void DevelopmentStatusReportsPowerBlockThenNextPeriodRecoveryWithSamePeriodChanges()
        {
            var runtime = CreateRuntime(width: 12, height: 8);
            var roadY = runtime.Height / 2;
            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(5, roadY))).IsApplied, Is.True);
            Assert.That(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(2, roadY - 2),
                new CellCoord(3, roadY - 1))).IsApplied, Is.True);

            var commercial = runtime.BuildingDefinitions.Definitions[1];
            runtime.Buildings.Create(
                new StableId(0xCAFE),
                commercial.Id,
                new StableId(0xBEEF),
                new CellCoord(8, 6));

            runtime.ProcessSimulationTick(SimulationTick.Zero);
            var blocked = runtime.HudModel.Snapshot;

            Assert.That(blocked.HasDevelopmentStatus, Is.True);
            Assert.That(blocked.DevelopmentStatus, Is.EqualTo(DevelopmentAvailabilityStatus.DemandButNoFeasibleParcels));
            Assert.That(blocked.TotalFeasibleParcelCount, Is.Zero);
            Assert.That(blocked.TopBlockReasons[0].Code, Is.EqualTo("NoPowerCapacity"));
            Assert.That(blocked.HasConnectedPowerProducer, Is.False);
            Assert.That(blocked.LedgerYear, Is.EqualTo(1));
            Assert.That(blocked.LedgerMonth, Is.EqualTo(1));

            var plantCell = new CellCoord(0, roadY - 1);
            Assert.That(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(plantCell), runtime.DefaultPowerPlantDefinitionId, plantCell)).IsApplied, Is.True);
            runtime.ProcessSimulationTick(new SimulationTick(1));
            var recovered = runtime.HudModel.Snapshot;

            Assert.That(recovered.DevelopmentStatus, Is.EqualTo(DevelopmentAvailabilityStatus.DemandAndFeasible));
            Assert.That(recovered.TotalFeasibleParcelCount, Is.GreaterThan(0));
            Assert.That(recovered.PowerConnectionChanged, Is.True);
            Assert.That(recovered.FeasibleParcelChange, Is.GreaterThan(0));
            Assert.That(recovered.LedgerMonth, Is.EqualTo(2));
        }

        private static CityRuntime CreateRuntime(int width = 8, int height = 8, int initialFunds = 10_000)
        {
            return new CityRuntime(new CityRuntimeOptions
            {
                Width = width,
                Height = height,
                InitialFunds = initialFunds,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(1, 1)
            });
        }
    }
}
