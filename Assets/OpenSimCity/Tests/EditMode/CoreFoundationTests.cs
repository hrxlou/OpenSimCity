using NUnit.Framework;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CoreFoundationTests
    {
        [Test]
        public void CellCoordComparisonUsesStableRowMajorOrder()
        {
            Assert.That(new CellCoord(9, 1).CompareTo(new CellCoord(0, 2)), Is.LessThan(0));
            Assert.That(new CellCoord(2, 3).CompareTo(new CellCoord(4, 3)), Is.LessThan(0));
        }

        [Test]
        public void SimulationDateAddsMonthsAcrossYearBoundary()
        {
            Assert.That(new SimulationDate(1, 12).AddMonths(2), Is.EqualTo(new SimulationDate(2, 2)));
        }

        [Test]
        public void CommandStampOrdersByTickThenSequence()
        {
            var first = new CommandStamp(new SimulationTick(10), new CommandSequence(2));
            var second = new CommandStamp(new SimulationTick(10), new CommandSequence(3));
            var laterTick = new CommandStamp(new SimulationTick(11), CommandSequence.Zero);

            Assert.That(first.CompareTo(second), Is.LessThan(0));
            Assert.That(second.CompareTo(laterTick), Is.LessThan(0));
        }

        [Test]
        public void DeterministicRandomReplaysFromSeedAndSavedState()
        {
            var first = new DeterministicRandom(42);
            var second = new DeterministicRandom(42);

            Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));

            var savedState = first.State;
            var expected = first.NextUInt64();
            var restored = DeterministicRandom.Restore(savedState);

            Assert.That(restored.NextUInt64(), Is.EqualTo(expected));
        }
    }
}
