using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Services;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PublicServiceSystemTests
    {
        [Test]
        public void StableDemandOrderPreservesRequestedAndCapacityTotals()
        {
            var definitions = ServiceDefinitionCatalog.CreateDefault();
            var facilities = new ServiceFacilityCollection();
            facilities.Add(new ServiceFacilityState(
                new StableId(0xF711000000000001UL),
                ServiceDefinitionIds.Police,
                new CellCoord(1, 1)));
            var demand = new FakeDemandProvider(new[]
            {
                Node(2, 100),
                Node(1, 100)
            });
            var system = new PublicServiceSystem(
                facilities,
                definitions,
                demand,
                new FixedCoverageResolver(10));

            system.RunMonth(new SimulationDate(1, 1));

            Assert.That(system.State.RequestedTotal, Is.EqualTo(200));
            Assert.That(system.State.ServedTotal, Is.EqualTo(180));
            Assert.That(system.State.Coverage.Select(row => row.BuildingId.Value),
                Is.Ordered);
            Assert.That(system.State.Coverage[0].Served, Is.EqualTo(100));
            Assert.That(system.State.Coverage[1].Served, Is.EqualTo(80));
            Assert.That(system.State.Coverage[1].FailureReason,
                Is.EqualTo(ServiceCoverageFailureReason.CapacityExceeded));
        }

        [Test]
        public void BudgetAppliesAtPeriodBoundaryAndScalesCapacityRangeAndCost()
        {
            var definitions = ServiceDefinitionCatalog.CreateDefault();
            var facilities = new ServiceFacilityCollection();
            var facility = new ServiceFacilityState(
                new StableId(0xF711000000000002UL),
                ServiceDefinitionIds.Police,
                new CellCoord(1, 1));
            facilities.Add(facility);
            var system = new PublicServiceSystem(
                facilities,
                definitions,
                new FakeDemandProvider(new[] { Node(1, 200) }),
                new FixedCoverageResolver(500));

            facility.ScheduleBudget(500, new SimulationDate(1, 2));
            system.RunMonth(new SimulationDate(1, 1));
            var full = system.State.FacilityUsage.Single();
            Assert.That(full.EffectiveBudgetPermille, Is.EqualTo(1000));
            Assert.That(full.EffectiveCapacity, Is.EqualTo(180));
            Assert.That(full.EffectiveMaximumCost, Is.EqualTo(1200));
            Assert.That(full.MonthlyCost, Is.EqualTo(120));

            system.RunMonth(new SimulationDate(1, 2));
            var half = system.State.FacilityUsage.Single();
            Assert.That(half.EffectiveBudgetPermille, Is.EqualTo(500));
            Assert.That(half.EffectiveCapacity, Is.EqualTo(90));
            Assert.That(half.EffectiveMaximumCost, Is.EqualTo(600));
            Assert.That(half.MonthlyCost, Is.EqualTo(60));
            Assert.That(system.State.Coverage.Single().Served, Is.EqualTo(90));
        }

        [Test]
        public void ZeroAvailableFundingProducesExplicitBudgetReasonWithoutBinaryStateMutation()
        {
            var definitions = ServiceDefinitionCatalog.CreateDefault();
            var facilities = new ServiceFacilityCollection();
            var facility = new ServiceFacilityState(
                new StableId(0xF711000000000003UL),
                ServiceDefinitionIds.Fire,
                new CellCoord(1, 1));
            facilities.Add(facility);
            var system = new PublicServiceSystem(
                facilities,
                definitions,
                new FakeDemandProvider(new[] { Node(1, 40) }, ServiceKind.Fire),
                new FixedCoverageResolver(10))
            {
                AvailableFundingPermille = 0
            };

            system.RunMonth(new SimulationDate(1, 1));

            Assert.That(facility.ActiveBudgetPermille, Is.EqualTo(1000));
            Assert.That(system.State.Coverage.Single().FailureReason,
                Is.EqualTo(ServiceCoverageFailureReason.BudgetTooLow));
            Assert.That(system.State.FacilityUsage.Single().EffectiveCapacity, Is.Zero);
            Assert.That(system.State.MonthlyOperatingCost, Is.Zero);
        }

        [Test]
        public void RoadFailureAndInsertionOrderHaveDeterministicOutput()
        {
            var first = BuildChecksum(reverseDemand: false);
            var second = BuildChecksum(reverseDemand: true);
            Assert.That(second, Is.EqualTo(first));
        }

        private static ulong BuildChecksum(bool reverseDemand)
        {
            var nodes = new List<ServiceDemandNode> { Node(1, 30), Node(2, 40) };
            if (reverseDemand) nodes.Reverse();
            var facilities = new ServiceFacilityCollection();
            facilities.Add(new ServiceFacilityState(
                new StableId(0xF711000000000004UL),
                ServiceDefinitionIds.Healthcare,
                new CellCoord(1, 1)));
            var system = new PublicServiceSystem(
                facilities,
                ServiceDefinitionCatalog.CreateDefault(),
                new FakeDemandProvider(nodes, ServiceKind.Healthcare),
                new FailedCoverageResolver(ServiceCoverageFailureReason.NoRoadAccess));
            system.RunMonth(new SimulationDate(1, 1));
            Assert.That(system.State.Coverage.All(row =>
                row.FailureReason == ServiceCoverageFailureReason.NoRoadAccess), Is.True);
            return system.State.ComputeChecksum();
        }

        private static ServiceDemandNode Node(ulong id, int amount)
        {
            return new ServiceDemandNode(
                new StableId(0xB700000000000000UL | id),
                new CellCoord((int)id, 2),
                new CellCoord((int)id, 1),
                1,
                amount);
        }

        private sealed class FakeDemandProvider : IServiceDemandProvider
        {
            private readonly IReadOnlyList<ServiceDemandNode> _nodes;
            private readonly ServiceKind _kind;
            public FakeDemandProvider(IEnumerable<ServiceDemandNode> nodes, ServiceKind kind = ServiceKind.Police)
            {
                _nodes = new List<ServiceDemandNode>(nodes).AsReadOnly();
                _kind = kind;
            }
            public IReadOnlyList<ServiceDemandNode> GetDemand(ServiceKind kind) =>
                kind == _kind ? _nodes : Array.AsReadOnly(Array.Empty<ServiceDemandNode>());
        }

        private sealed class FixedCoverageResolver : IServiceCoverageCostResolver
        {
            private readonly int _cost;
            public FixedCoverageResolver(int cost) { _cost = cost; }
            public ServiceCoverageCostResult Resolve(
                ServiceFacilityState facility,
                ServiceDemandNode demand,
                ServiceCoverageStrategy strategy) => ServiceCoverageCostResult.Reachable(_cost);
        }

        private sealed class FailedCoverageResolver : IServiceCoverageCostResolver
        {
            private readonly ServiceCoverageFailureReason _reason;
            public FailedCoverageResolver(ServiceCoverageFailureReason reason) { _reason = reason; }
            public ServiceCoverageCostResult Resolve(
                ServiceFacilityState facility,
                ServiceDemandNode demand,
                ServiceCoverageStrategy strategy) => ServiceCoverageCostResult.Unreachable(_reason);
        }
    }
}
