using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class WaterUtilitySystemTests
    {
        [Test]
        public void StableConsumersConserveComponentCapacityAndExposePartialReason()
        {
            var producers = new WaterProducerCollection();
            producers.Add(new WaterProducerState(
                new StableId(0xF720000000000001UL),
                WaterProducerDefinitionIds.Pump,
                new CellCoord(1, 1)));
            var resolver = new FakeResolver(
                new[]
                {
                    Consumer(2, 600, 1),
                    Consumer(1, 600, 1)
                },
                producerComponent: 1);
            var system = new WaterUtilitySystem(
                producers,
                WaterProducerDefinitionCatalog.CreateDefault(),
                resolver);

            system.RunMonth(new SimulationDate(1, 1));

            Assert.That(system.State.RequestedTotal, Is.EqualTo(1200));
            Assert.That(system.State.SuppliedTotal, Is.EqualTo(1000));
            Assert.That(system.State.Supply.Select(row => row.BuildingId.Value), Is.Ordered);
            Assert.That(system.State.Supply[0].Supplied, Is.EqualTo(600));
            Assert.That(system.State.Supply[1].Supplied, Is.EqualTo(400));
            Assert.That(system.State.Supply[1].FailureReason,
                Is.EqualTo(WaterSupplyFailureReason.CapacityExceeded));
            Assert.That(system.State.ProducerUsage.Single().Used, Is.EqualTo(1000));
        }

        [Test]
        public void MissingRoadAndDifferentComponentHaveExplicitReasons()
        {
            var producers = new WaterProducerCollection();
            producers.Add(new WaterProducerState(
                new StableId(0xF720000000000002UL),
                WaterProducerDefinitionIds.Pump,
                new CellCoord(1, 1)));
            var resolver = new FakeResolver(
                new[]
                {
                    Consumer(1, 20, -1),
                    Consumer(2, 20, 2)
                },
                producerComponent: 1);
            var system = new WaterUtilitySystem(
                producers,
                WaterProducerDefinitionCatalog.CreateDefault(),
                resolver);

            system.RunMonth(new SimulationDate(1, 1));

            Assert.That(system.State.Supply[0].FailureReason,
                Is.EqualTo(WaterSupplyFailureReason.NoRoadAccess));
            Assert.That(system.State.Supply[1].FailureReason,
                Is.EqualTo(WaterSupplyFailureReason.DifferentRoadComponent));
            Assert.That(system.State.SuppliedTotal, Is.Zero);
        }

        [Test]
        public void ConsumerInsertionOrderDoesNotChangeChecksum()
        {
            Assert.That(BuildChecksum(false), Is.EqualTo(BuildChecksum(true)));
        }

        private static ulong BuildChecksum(bool reverse)
        {
            var consumers = new List<WaterConsumerNode>
            {
                Consumer(1, 100, 1),
                Consumer(2, 150, 1),
                Consumer(3, 200, 1)
            };
            if (reverse) consumers.Reverse();
            var producers = new WaterProducerCollection();
            producers.Add(new WaterProducerState(
                new StableId(0xF720000000000003UL),
                WaterProducerDefinitionIds.Pump,
                new CellCoord(1, 1)));
            var system = new WaterUtilitySystem(
                producers,
                WaterProducerDefinitionCatalog.CreateDefault(),
                new FakeResolver(consumers, 1));
            system.RunMonth(new SimulationDate(1, 1));
            return system.State.ComputeChecksum();
        }

        private static WaterConsumerNode Consumer(ulong suffix, int demand, int component)
        {
            return new WaterConsumerNode(
                new StableId(0xB702000000000000UL | suffix),
                new CellCoord((int)suffix, 2),
                component,
                demand);
        }

        private sealed class FakeResolver : IWaterNetworkResolver
        {
            private readonly IReadOnlyList<WaterConsumerNode> _consumers;
            private readonly int _producerComponent;
            public FakeResolver(IEnumerable<WaterConsumerNode> consumers, int producerComponent)
            {
                _consumers = new List<WaterConsumerNode>(consumers).AsReadOnly();
                _producerComponent = producerComponent;
            }
            public IReadOnlyList<WaterConsumerNode> GetConsumers() => _consumers;
            public WaterProducerNetworkNode ResolveProducer(
                WaterProducerState producer,
                WaterProducerDefinition definition) =>
                new WaterProducerNetworkNode(producer.Id, _producerComponent, definition.Capacity);
        }
    }
}
