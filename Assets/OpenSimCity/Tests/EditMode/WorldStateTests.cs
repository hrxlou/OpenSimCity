using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class WorldStateTests
    {
        [Test]
        public void DefaultWorldBuildsDeterministic64By64CellAndChunkIndices()
        {
            var state = new WorldState(0x0123456789ABCDEFUL);

            Assert.That(state.Width, Is.EqualTo(64));
            Assert.That(state.Height, Is.EqualTo(64));
            Assert.That(state.ChunkSize, Is.EqualTo(16));
            Assert.That(state.Seed, Is.EqualTo(0x0123456789ABCDEFUL));
            Assert.That(state.CellCount, Is.EqualTo(4096));
            Assert.That(state.Cells.Count, Is.EqualTo(4096));
            Assert.That(state.ChunkColumns, Is.EqualTo(4));
            Assert.That(state.ChunkRows, Is.EqualTo(4));
            Assert.That(state.ChunkCount, Is.EqualTo(16));
            Assert.That(state.Chunks.Count, Is.EqualTo(16));
            Assert.That(state.DirtyChunkCount, Is.EqualTo(0));

            for (var index = 0; index < state.CellCount; index++)
            {
                var coordinate = state.GetCoordinate(index);
                Assert.That(state.GetIndex(coordinate), Is.EqualTo(index));
                Assert.That(state.GetCellAtIndex(index), Is.EqualTo(state.GetCell(coordinate)));
                Assert.That(state.GetCell(coordinate).Index, Is.EqualTo(index));
            }

            Assert.That(state.GetChunk(new ChunkCoord(0, 0)), Is.EqualTo(state.Chunks[0]));
            Assert.That(state.GetChunk(new ChunkCoord(3, 3)), Is.EqualTo(state.Chunks[15]));
        }

        [Test]
        public void CellAndChunkBoundariesUseExpectedRowMajorIndices()
        {
            var state = new WorldState(64, 64, 16, 7UL);

            Assert.That(state.GetIndex(new CellCoord(0, 0)), Is.EqualTo(0));
            Assert.That(state.GetIndex(new CellCoord(15, 0)), Is.EqualTo(15));
            Assert.That(state.GetIndex(new CellCoord(0, 1)), Is.EqualTo(64));
            Assert.That(state.GetIndex(new CellCoord(63, 63)), Is.EqualTo(4095));

            Assert.That(state.GetChunkCoord(new CellCoord(0, 0)), Is.EqualTo(new ChunkCoord(0, 0)));
            Assert.That(state.GetChunkCoord(new CellCoord(15, 15)), Is.EqualTo(new ChunkCoord(0, 0)));
            Assert.That(state.GetChunkCoord(new CellCoord(16, 0)), Is.EqualTo(new ChunkCoord(1, 0)));
            Assert.That(state.GetChunkCoord(new CellCoord(0, 16)), Is.EqualTo(new ChunkCoord(0, 1)));
            Assert.That(state.GetChunkCoord(new CellCoord(63, 63)), Is.EqualTo(new ChunkCoord(3, 3)));

            Assert.That(state.GetChunkIndex(new ChunkCoord(0, 0)), Is.EqualTo(0));
            Assert.That(state.GetChunkIndex(new ChunkCoord(1, 0)), Is.EqualTo(1));
            Assert.That(state.GetChunkIndex(new ChunkCoord(0, 1)), Is.EqualTo(4));
            Assert.That(state.GetChunkIndex(new CellCoord(63, 63)), Is.EqualTo(15));

            var lastChunk = state.GetChunk(new ChunkCoord(3, 3));
            Assert.That(lastChunk.Origin, Is.EqualTo(new CellCoord(48, 48)));
            Assert.That(lastChunk.Width, Is.EqualTo(16));
            Assert.That(lastChunk.Height, Is.EqualTo(16));
            Assert.That(lastChunk.FirstCellIndex, Is.EqualTo(3120));
            Assert.That(lastChunk.CellCount, Is.EqualTo(256));
            Assert.That(lastChunk.Contains(new CellCoord(63, 63)), Is.True);
            Assert.That(lastChunk.Contains(new CellCoord(64, 63)), Is.False);
        }

        [Test]
        public void TryQueriesFailExplicitlyForOutOfRangeCoordinatesAndIndices()
        {
            var state = new WorldState();

            Assert.That(state.TryGetIndex(new CellCoord(-1, 0), out var cellIndex), Is.False);
            Assert.That(cellIndex, Is.EqualTo(-1));
            Assert.That(state.TryGetIndex(new CellCoord(64, 0), out _), Is.False);
            Assert.That(state.TryGetCoordinate(-1, out _), Is.False);
            Assert.That(state.TryGetCoordinate(state.CellCount, out _), Is.False);
            Assert.That(state.TryGetCell(new CellCoord(0, 64), out _), Is.False);
            Assert.That(state.TryGetChunkCoord(new CellCoord(0, -1), out _), Is.False);
            Assert.That(state.TryGetChunkIndex(new ChunkCoord(4, 0), out _), Is.False);
            Assert.That(state.TryGetChunk(new ChunkCoord(-1, 0), out _), Is.False);
            Assert.That(state.TryGetCellAtIndex(state.CellCount, out _), Is.False);
            Assert.That(state.TryGetChunkAtIndex(state.ChunkCount, out _), Is.False);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetIndex(new CellCoord(-1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetCoordinate(state.CellCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetCell(new CellCoord(64, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetChunkIndex(new ChunkCoord(4, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetChunkAtIndex(state.ChunkCount));
        }

        [Test]
        public void DirtyMarksAreChunkScopedIdempotentAndDeterministic()
        {
            var state = new WorldState();

            Assert.That(state.TryMarkChunkDirty(new CellCoord(16, 16)), Is.True);
            Assert.That(state.DirtyChunkCount, Is.EqualTo(1));
            Assert.That(state.IsChunkDirty(new ChunkCoord(1, 1)), Is.True);
            Assert.That(state.IsChunkDirty(new ChunkCoord(0, 0)), Is.False);

            Assert.That(state.TryMarkChunkDirty(new CellCoord(17, 17)), Is.True);
            Assert.That(state.DirtyChunkCount, Is.EqualTo(1));

            Assert.That(state.TryMarkChunkDirty(new ChunkCoord(0, 0)), Is.True);
            Assert.That(state.DirtyChunkCount, Is.EqualTo(2));
            Assert.That(state.DirtyChunks.Select(chunk => chunk.Coordinate).ToArray(), Is.EqualTo(new[]
            {
                new ChunkCoord(0, 0),
                new ChunkCoord(1, 1)
            }));

            Assert.That(state.TryClearChunkDirty(new ChunkCoord(1, 1)), Is.True);
            Assert.That(state.DirtyChunkCount, Is.EqualTo(1));
            Assert.That(state.TryClearChunkDirty(new ChunkCoord(3, 3)), Is.True);
            Assert.That(state.DirtyChunkCount, Is.EqualTo(1));

            state.ClearDirtyChunks();
            Assert.That(state.HasDirtyChunks, Is.False);
            Assert.That(state.DirtyChunks, Is.Empty);
            Assert.That(state.TryMarkChunkDirty(new ChunkCoord(4, 0)), Is.False);
        }

        [Test]
        public void NonDivisibleWorldDimensionsUsePartialEdgeChunks()
        {
            var state = new WorldState(new WorldStateDto(33, 17, 16, 19UL));

            Assert.That(state.CellCount, Is.EqualTo(561));
            Assert.That(state.ChunkColumns, Is.EqualTo(3));
            Assert.That(state.ChunkRows, Is.EqualTo(2));
            Assert.That(state.ChunkCount, Is.EqualTo(6));
            Assert.That(state.GetChunk(new ChunkCoord(2, 1)).Origin, Is.EqualTo(new CellCoord(32, 16)));
            Assert.That(state.GetChunk(new ChunkCoord(2, 1)).Width, Is.EqualTo(1));
            Assert.That(state.GetChunk(new ChunkCoord(2, 1)).Height, Is.EqualTo(1));
            Assert.That(state.GetChunk(new ChunkCoord(2, 1)).CellCount, Is.EqualTo(1));
            Assert.That(state.GetCell(new CellCoord(32, 16)).Chunk, Is.EqualTo(new ChunkCoord(2, 1)));
            Assert.That(state.GetIndex(new CellCoord(32, 16)), Is.EqualTo(560));
        }

        [Test]
        public void DtoAndRepeatedConstructionPreserveTheSameGridContract()
        {
            var dto = new WorldStateDto(64, 64, 16, 42UL);
            var first = new WorldState(dto);
            var second = new WorldState(dto);

            Assert.That(first.ToDto().Width, Is.EqualTo(dto.Width));
            Assert.That(first.ToDto().Height, Is.EqualTo(dto.Height));
            Assert.That(first.ToDto().ChunkSize, Is.EqualTo(dto.ChunkSize));
            Assert.That(first.ToDto().Seed, Is.EqualTo(dto.Seed));
            Assert.That(first.Cells, Is.Not.SameAs(second.Cells));
            Assert.That(first.Cells, Is.EqualTo(second.Cells));
            Assert.That(first.Chunks, Is.EqualTo(second.Chunks));
        }

        [Test]
        public void InvalidWorldDimensionsAreRejectedBeforeStorageIsCreated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldState(0, 64, 16, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldState(64, 0, 16, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldState(64, 64, 0, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldState(-1));
            Assert.Throws<ArgumentNullException>(() => new WorldState(null));
        }
    }
}
