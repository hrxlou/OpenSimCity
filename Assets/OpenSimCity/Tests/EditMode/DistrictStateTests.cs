using System;
using NUnit.Framework;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class DistrictStateTests
    {
        [Test]
        public void PaintRejectsOverlapAtomicallyAndKeepsStableCellOrder()
        {
            var districts = new DistrictCollection();
            var first = new StableId(0xD760000000000001UL);
            var second = new StableId(0xD760000000000002UL);
            Assert.That(districts.Create(first, "A", 0xFFFFFFFFu), Is.True);
            Assert.That(districts.Create(second, "B", 0xFFFFFFFFu), Is.True);
            Assert.That(districts.Paint(first, new[] { new CellCoord(2, 1), new CellCoord(1, 1) }), Is.True);
            Assert.That(districts.Paint(second, new[] { new CellCoord(3, 1), new CellCoord(2, 1) }), Is.False);
            Assert.That(districts.TryGet(second, out var secondState), Is.True);
            Assert.That(secondState.CellCount, Is.Zero);
            districts.TryGet(first, out var firstState);
            Assert.That(firstState.Cells[0], Is.EqualTo(new CellCoord(1, 1)));
            Assert.That(firstState.Cells[1], Is.EqualTo(new CellCoord(2, 1)));
        }

        [Test]
        public void DeleteClearsMembershipAndAllowsRepaint()
        {
            var districts = new DistrictCollection();
            var first = new StableId(0xD760000000000001UL);
            var second = new StableId(0xD760000000000002UL);
            var cell = new CellCoord(4, 5);
            districts.Create(first, "A", 1u); districts.Create(second, "B", 2u); districts.Paint(first, new[] { cell });
            Assert.That(districts.Delete(first), Is.True);
            Assert.That(districts.TryGetDistrictAt(cell, out _), Is.False);
            Assert.That(districts.Paint(second, new[] { cell }), Is.True);
        }

        [Test]
        public void SaveRestorePreservesMembershipAndRejectsOutOfBoundsBeforeReplace()
        {
            var runtime = new CityRuntime();
            var module = DistrictRuntimeRegistry.GetOrCreate(runtime);
            var id = module.NextDistrictId();
            Assert.That(module.Execute(new CreateDistrictCommand(id, "중심지", 0x4C9AFFCCu)).IsApplied, Is.True);
            Assert.That(module.Execute(new PaintDistrictCommand(id, new[] { new CellCoord(1, 1), new CellCoord(2, 1) })).IsApplied, Is.True);
            var saved = module.Capture();

            var otherRuntime = new CityRuntime();
            var restored = DistrictRuntimeRegistry.GetOrCreate(otherRuntime);
            restored.Restore(saved);
            restored.ReadModel.RefreshIfNeeded();
            Assert.That(restored.ReadModel.Snapshot.Districts.Count, Is.EqualTo(1));
            Assert.That(restored.ReadModel.Snapshot.Districts[0].Cells.Count, Is.EqualTo(2));

            saved.Items[0].Cells[0].X = otherRuntime.Width + 1;
            Assert.Throws<ArgumentException>(() => restored.Restore(saved));
            Assert.That(restored.ReadModel.Snapshot.Districts.Count, Is.EqualTo(1));
        }
    }
}
