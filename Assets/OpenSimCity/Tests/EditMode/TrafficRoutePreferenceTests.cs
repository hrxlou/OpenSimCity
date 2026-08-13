using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class TrafficRoutePreferenceTests
    {
        private static readonly StableId RoadId = new StableId(0x701UL);
        private static readonly StableId ResidentialDefinition = new StableId(0x702UL);
        private static readonly StableId CommercialDefinition = new StableId(0x703UL);
        private static readonly StableId IndustrialDefinition = new StableId(0x704UL);

        [Test]
        public void GoodsAvoidHigherPreferenceTierWhenEqualCostAlternativeExists()
        {
            var roads = new RoadNetworkState(3, 3);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0,1), new CellCoord(0,0), new CellCoord(1,0), new CellCoord(2,0), new CellCoord(2,1),
                new CellCoord(0,2), new CellCoord(1,2), new CellCoord(2,2)
            }, RoadId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var buildings = new BuildingStateCollection();
            var residentialId = new StableId(0x7101UL); var commercialId = new StableId(0x7102UL); var industrialId = new StableId(0x7103UL);
            buildings.Add(new BuildingState(residentialId, ResidentialDefinition, new StableId(0x7201UL), new CellCoord(0,1), occupancy: 4));
            buildings.Add(new BuildingState(commercialId, CommercialDefinition, new StableId(0x7202UL), new CellCoord(2,1), filledJobs: 4));
            buildings.Add(new BuildingState(industrialId, IndustrialDefinition, new StableId(0x7203UL), new CellCoord(0,1), filledJobs: 4));
            var profiles = new Profiles(
                new UrbanEconomyProfile(ResidentialDefinition, EconomicSector.Residential, 4, 0),
                new UrbanEconomyProfile(CommercialDefinition, EconomicSector.Commercial, 0, 4),
                new UrbanEconomyProfile(IndustrialDefinition, EconomicSector.Industrial, 0, 4));
            var access = new Access(new Dictionary<StableId, TripEndpoint>
            {
                [residentialId] = Endpoint(residentialId, new CellCoord(0,1)),
                [commercialId] = Endpoint(commercialId, new CellCoord(2,1)),
                [industrialId] = Endpoint(industrialId, new CellCoord(0,1))
            });
            var economy = new UrbanEconomySystem(buildings, profiles);
            var demand = new TripDemandSystem(buildings, profiles, access, economy);
            var definitions = new RoadDefinitionCatalog(new[] { new RoadDefinition(RoadId, 0, "test", 0, 100, 30) });
            var routing = new TrafficRoutingSystem(roads, graph, definitions, demand, new TrafficRoutingParameters(3));
            var month = new SimulationDate(1,1);
            economy.RunMonth(month); demand.RunMonth(month); routing.RunMonth(month);
            Assert.That(routing.State.Assignments.Count(row => row.Kind == TripDemandKind.Goods), Is.GreaterThan(1));

            routing.RoutePolicy = new UpperPathPenalty();
            Assert.That(routing.RefreshIfNeeded(), Is.True);
            var goods = routing.State.Assignments.Where(row => row.Kind == TripDemandKind.Goods).ToArray();
            Assert.That(goods, Is.Not.Empty);
            Assert.That(goods.All(row => !row.Route.Contains(new CellCoord(1,0))), Is.True);
            Assert.That(goods.Sum(row => row.Amount), Is.EqualTo(demand.State.GoodsMatched));
        }

        private sealed class UpperPathPenalty : ITrafficRoutePolicy
        {
            public int Version => 1;
            public int RoutePreferencePermille(TripDemandKind kind, TrafficRoute route) =>
                kind == TripDemandKind.Goods && route.Contains(new CellCoord(1,0)) ? 1600 : 1000;
        }

        private static TripEndpoint Endpoint(StableId id, CellCoord cell) => new TripEndpoint(TripEndpointKind.Building, id, cell, 0);

        private sealed class Profiles : IUrbanEconomyProfileResolver
        {
            private readonly Dictionary<StableId, UrbanEconomyProfile> _values = new Dictionary<StableId, UrbanEconomyProfile>();
            public Profiles(params UrbanEconomyProfile[] values) { foreach (var value in values) _values.Add(value.DefinitionId, value); }
            public bool TryGetUrbanEconomyProfile(StableId definitionId, out UrbanEconomyProfile profile) => _values.TryGetValue(definitionId, out profile);
        }

        private sealed class Access : ITripAccessResolver
        {
            private readonly Dictionary<StableId, TripEndpoint> _values;
            public Access(Dictionary<StableId, TripEndpoint> values) { _values = values; }
            public TripEndpoint ResolveBuilding(BuildingState building) => _values[building.Id];
            public IReadOnlyList<TripEndpoint> GetEnabledExternalRoadGates() => Array.AsReadOnly(Array.Empty<TripEndpoint>());
        }
    }
}
