using System;
using System.Collections.Generic;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Application-owned monthly composition for the private market, growth,
    /// aggregate trip demand, optional post-demand mobility systems, deterministic
    /// baseline routing and the municipal ledger. Registration order is the stable
    /// execution order for post-demand systems.
    /// </summary>
    public sealed class MunicipalFinanceMonthlySystem : IMonthlySimulationSystem
    {
        private readonly MunicipalFinanceState _finance;
        private readonly UrbanEconomySystem _urbanEconomy;
        private readonly GrowthDevelopmentSystem _growth;
        private readonly TripDemandSystem _tripDemand;
        private readonly TrafficRoutingSystem _trafficRouting;
        private readonly BuildingStateCollection _buildings;
        private readonly BuildingDefinitionCatalog _buildingDefinitions;
        private readonly RoadNetworkState _roads;
        private readonly RoadDefinitionCatalog _roadDefinitions;
        private readonly PowerProducerCollection _powerProducers;
        private readonly UtilityDefinitionCatalog _utilityDefinitions;
        private readonly MunicipalFinanceParameters _parameters;
        private readonly List<IMonthlySimulationSystem> _postTripDemandSystems =
            new List<IMonthlySimulationSystem>();

        public MunicipalFinanceMonthlySystem(
            MunicipalFinanceState finance,
            UrbanEconomySystem urbanEconomy,
            GrowthDevelopmentSystem growth,
            TripDemandSystem tripDemand,
            BuildingStateCollection buildings,
            BuildingDefinitionCatalog buildingDefinitions,
            RoadNetworkState roads,
            RoadDefinitionCatalog roadDefinitions,
            PowerProducerCollection powerProducers,
            UtilityDefinitionCatalog utilityDefinitions,
            MunicipalFinanceParameters parameters = null)
            : this(
                finance,
                urbanEconomy,
                growth,
                tripDemand,
                null,
                buildings,
                buildingDefinitions,
                roads,
                roadDefinitions,
                powerProducers,
                utilityDefinitions,
                parameters)
        {
        }

        public MunicipalFinanceMonthlySystem(
            MunicipalFinanceState finance,
            UrbanEconomySystem urbanEconomy,
            GrowthDevelopmentSystem growth,
            TripDemandSystem tripDemand,
            TrafficRoutingSystem trafficRouting,
            BuildingStateCollection buildings,
            BuildingDefinitionCatalog buildingDefinitions,
            RoadNetworkState roads,
            RoadDefinitionCatalog roadDefinitions,
            PowerProducerCollection powerProducers,
            UtilityDefinitionCatalog utilityDefinitions,
            MunicipalFinanceParameters parameters = null)
        {
            _finance = finance ?? throw new ArgumentNullException(nameof(finance));
            _urbanEconomy = urbanEconomy ?? throw new ArgumentNullException(nameof(urbanEconomy));
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
            _tripDemand = tripDemand ?? throw new ArgumentNullException(nameof(tripDemand));
            _trafficRouting = trafficRouting;
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _buildingDefinitions = buildingDefinitions ?? throw new ArgumentNullException(nameof(buildingDefinitions));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _roadDefinitions = roadDefinitions ?? throw new ArgumentNullException(nameof(roadDefinitions));
            _powerProducers = powerProducers ?? throw new ArgumentNullException(nameof(powerProducers));
            _utilityDefinitions = utilityDefinitions ?? throw new ArgumentNullException(nameof(utilityDefinitions));
            _parameters = parameters ?? MunicipalFinanceParameters.Default;
        }

        public IReadOnlyList<IMonthlySimulationSystem> PostTripDemandSystems =>
            _postTripDemandSystems.AsReadOnly();

        /// <summary>
        /// Registers a deterministic monthly extension after aggregate OD exists and
        /// before baseline routing and municipal settlement. Duplicate references are
        /// ignored so runtime composition can be rebuilt safely.
        /// </summary>
        public void RegisterPostTripDemandSystem(IMonthlySimulationSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            for (var index = 0; index < _postTripDemandSystems.Count; index++)
            {
                if (ReferenceEquals(_postTripDemandSystems[index], system))
                    return;
            }
            _postTripDemandSystems.Add(system);
        }

        public void RunMonth(SimulationDate month)
        {
            _finance.ApplyTaxRatesForMonth(month);
            _urbanEconomy.TaxRates = _finance.ActiveTaxRates;
            _urbanEconomy.RunMonth(month);
            _growth.RunMonth(month);
            _tripDemand.RunMonth(month);
            for (var index = 0; index < _postTripDemandSystems.Count; index++)
                _postTripDemandSystems[index].RunMonth(month);
            _trafficRouting?.RunMonth(month);

            TaxIncome(out var residential, out var commercial, out var industrial);
            _finance.SettleMonth(
                month,
                residential,
                commercial,
                industrial,
                RoadMaintenanceExpense(),
                PowerMaintenanceExpense());
        }

        private void TaxIncome(out int residential, out int commercial, out int industrial)
        {
            long residentialBase = 0;
            long commercialBase = 0;
            long industrialBase = 0;
            var source = _buildings.Buildings;
            for (var index = 0; index < source.Count; index++)
            {
                var building = source[index];
                var definition = _buildingDefinitions.Get(building.DefinitionId);
                switch (definition.ZoneType)
                {
                    case ZoneType.ResidentialLow:
                        residentialBase += (long)Math.Min(building.Occupancy, definition.OccupancyCapacity) *
                                           _parameters.ResidentialTaxBasePerResident;
                        break;
                    case ZoneType.CommercialLow:
                        commercialBase += (long)Math.Min(building.FilledJobs, definition.JobCapacity) *
                                          _parameters.CommercialTaxBasePerFilledJob;
                        break;
                    case ZoneType.IndustrialLow:
                        industrialBase += (long)Math.Min(building.FilledJobs, definition.JobCapacity) *
                                          _parameters.IndustrialTaxBasePerFilledJob;
                        break;
                    default:
                        throw new InvalidOperationException($"Building {building.Id} has no municipal tax sector.");
                }
            }

            var rates = _finance.ActiveTaxRates;
            residential = ApplyPermille(residentialBase, rates.ResidentialPermille);
            commercial = ApplyPermille(commercialBase, rates.CommercialPermille);
            industrial = ApplyPermille(industrialBase, rates.IndustrialPermille);
        }

        private int RoadMaintenanceExpense()
        {
            long total = 0;
            var cells = _roads.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                var definitionId = cells[index].DefinitionId;
                if (definitionId.IsValid)
                    total += _roadDefinitions.Get(definitionId).MonthlyMaintenanceCost;
            }

            return ToInt(total, "Road maintenance exceeded Int32 range.");
        }

        private int PowerMaintenanceExpense()
        {
            long total = 0;
            var producers = _powerProducers.Producers;
            for (var index = 0; index < producers.Count; index++)
                total += _utilityDefinitions.Get(producers[index].DefinitionId).MonthlyMaintenanceCost;

            return ToInt(total, "Power maintenance exceeded Int32 range.");
        }

        private static int ApplyPermille(long taxBase, int rate)
        {
            return ToInt(taxBase * rate / UrbanEconomyTaxRates.MaxPermille, "Municipal tax income exceeded Int32 range.");
        }

        private static int ToInt(long value, string overflowMessage)
        {
            if (value < 0 || value > int.MaxValue)
                throw new OverflowException(overflowMessage);
            return (int)value;
        }
    }
}
