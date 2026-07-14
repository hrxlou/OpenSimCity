using System;
using NUnit.Framework;
using OpenSimCity.Core;
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
            Assert.That(new CellCoord(-2, -1).CompareTo(new CellCoord(-1, -1)), Is.LessThan(0));
            Assert.That(new CellCoord(int.MinValue, int.MaxValue), Is.EqualTo(new CellCoord(int.MinValue, int.MaxValue)));
        }

        [Test]
        public void CellCoordSupportsCheckedArithmeticAndStableChunkMapping()
        {
            Assert.That(new CellCoord(2, 3) + new CellCoord(-1, 4), Is.EqualTo(new CellCoord(1, 7)));
            Assert.That(new CellCoord(2, 3).Offset(-3, 5), Is.EqualTo(new CellCoord(-1, 8)));
            Assert.That(new CellCoord(-1, -17).ToChunkCoord(16), Is.EqualTo(new ChunkCoord(-1, -2)));
            Assert.That(new CellCoord(-16, 16).ToChunkCoord(16), Is.EqualTo(new ChunkCoord(-1, 1)));
            Assert.Throws<OverflowException>(() => new CellCoord(int.MaxValue, 0).Offset(1, 0));
        }

        [Test]
        public void ChunkCoordUsesStableRowMajorOrderAndHashRule()
        {
            var first = new ChunkCoord(-1, 2);
            var same = new ChunkCoord(-1, 2);

            Assert.That(first.CompareTo(new ChunkCoord(0, 3)), Is.LessThan(0));
            Assert.That(first.CompareTo(new ChunkCoord(0, 2)), Is.LessThan(0));
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(ChunkCoord.FromCell(new CellCoord(-17, -1), 16), Is.EqualTo(new ChunkCoord(-2, -1)));
            Assert.That(new ChunkCoord(-2, 3).OriginCell(16), Is.EqualTo(new CellCoord(-32, 48)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkCoord.FromCell(CellCoord.Zero, 0));
        }

        [Test]
        public void StableIdRejectsInvalidValueAndSortsByNumericValue()
        {
            Assert.That(StableId.Invalid.IsValid, Is.False);
            Assert.That(new StableId(2) < new StableId(3), Is.True);
            Assert.That(new StableId(3).CompareTo(new StableId(2)), Is.GreaterThan(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StableId(0));
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
            Assert.That(first < second, Is.True);
        }

        [Test]
        public void TickAndSequenceFailInsteadOfWrappingAtMaximum()
        {
            Assert.That(SimulationTick.MaxValue.CanAdvance, Is.False);
            Assert.That(CommandSequence.MaxValue.CanAdvance, Is.False);
            Assert.Throws<OverflowException>(() => SimulationTick.MaxValue.Next());
            Assert.Throws<OverflowException>(() => CommandSequence.MaxValue.Next());
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

        [Test]
        public void DeterministicRandomUsesFixedSeedVector()
        {
            var random = new DeterministicRandom(42);

            Assert.That(random.NextUInt64(), Is.EqualTo(0x31B0ECE7C4F697A2UL));
            Assert.That(random.NextUInt64(), Is.EqualTo(0x9008A3B1CB686F03UL));
            Assert.That(random.NextUInt64(), Is.EqualTo(0x7C7173ABD97BE16FUL));
        }

        [Test]
        public void DeterministicRandomSeparatesSubsystemStreamsWithFixedVectors()
        {
            const ulong seed = 0x0123456789ABCDEFUL;
            var economy = DeterministicRandom.CreateStream(seed, "economy");
            var traffic = DeterministicRandom.ForSubsystem(seed, "traffic");
            var economyAgain = new DeterministicRandom(seed, "economy");

            Assert.That(economy.NextUInt64(), Is.EqualTo(0x4C9D5872530357ABUL));
            Assert.That(traffic.NextUInt64(), Is.EqualTo(0xBA0AEE2FF6093251UL));
            Assert.That(economyAgain.NextUInt64(), Is.EqualTo(0x4C9D5872530357ABUL));
            Assert.That(DeterministicHash.HashString("economy"), Is.EqualTo(0xA2035BA9438C1277UL));
            Assert.Throws<ArgumentException>(() => DeterministicRandom.CreateStream(seed, string.Empty));
        }
    }
}
