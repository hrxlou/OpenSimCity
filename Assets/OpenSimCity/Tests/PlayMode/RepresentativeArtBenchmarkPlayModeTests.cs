using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Verification;
using OpenSimCity.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class RepresentativeArtBenchmarkPlayModeTests
    {
        private static IEnumerator LoadBenchmark()
        {
            yield return SceneManager.LoadSceneAsync("Benchmark", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaceholderAndProductionLikeModesRecordSeparateBaselines()
        {
            yield return LoadBenchmark();
            var fixture =
                Object.FindAnyObjectByType<RepresentativeArtBenchmarkFixture>();
            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.CurrentMode, Is.EqualTo(BenchmarkArtMode.ProductionLike));
            Assert.That(fixture.InstanceCount, Is.EqualTo(24));

            using var productionDrawCalls = new DrawCallRecorders();
            yield return null;
            yield return null;
            var productionDrawCallCount = productionDrawCalls.TotalLastValue;
            var productionDrawCallBreakdown = productionDrawCalls.ToString();
            var production = RepresentativeArtMetrics.Capture(
                fixture.InstancesRoot.gameObject);

            fixture.Rebuild(BenchmarkArtMode.Placeholder);
            yield return null;
            using var placeholderDrawCalls = new DrawCallRecorders();
            yield return null;
            yield return null;
            var placeholderDrawCallCount = placeholderDrawCalls.TotalLastValue;
            var placeholderDrawCallBreakdown = placeholderDrawCalls.ToString();
            var placeholder = RepresentativeArtMetrics.Capture(
                fixture.InstancesRoot.gameObject);

            TestContext.WriteLine($"P3-08 measured production-like: {production}");
            TestContext.WriteLine($"P3-08 measured authored fallback: {placeholder}");

            Assert.That(production.RendererCount, Is.EqualTo(24));
            Assert.That(production.MaterialSlotCount, Is.EqualTo(172));
            Assert.That(production.UniqueMaterialCount, Is.EqualTo(17));
            Assert.That(production.TriangleCount, Is.EqualTo(355648));
            Assert.That(production.UniqueMeshCount, Is.EqualTo(6));
            Assert.That(production.UniqueTextureCount, Is.EqualTo(17));
            Assert.That(production.TextureMemoryBytes, Is.GreaterThan(0));

            // Authored fallback includes the inactive Korean crosswalk approach
            // modules so intersection topology can enable them without allocation.
            Assert.That(placeholder.RendererCount, Is.EqualTo(188));
            Assert.That(placeholder.MaterialSlotCount, Is.EqualTo(204));
            Assert.That(placeholder.UniqueMaterialCount, Is.EqualTo(13));
            Assert.That(placeholder.TriangleCount, Is.EqualTo(14912));
            Assert.That(placeholder.UniqueMeshCount, Is.EqualTo(3));
            // Source roughness maps now contribute three linear smoothness textures.
            Assert.That(placeholder.UniqueTextureCount, Is.EqualTo(6));
            Assert.That(production.TriangleCount, Is.GreaterThan(placeholder.TriangleCount));
            if (!UnityEngine.Application.isEditor &&
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.That(productionDrawCallCount, Is.GreaterThan(0));
                Assert.That(placeholderDrawCallCount, Is.GreaterThan(0));
                Assert.That(
                    productionDrawCallCount,
                    Is.GreaterThan(placeholderDrawCallCount));
            }
            TestContext.WriteLine(
                $"P3-08 production-like: {production}, " +
                $"drawCalls={productionDrawCallCount}, " +
                $"breakdown=[{productionDrawCallBreakdown}]");
            TestContext.WriteLine(
                $"P3-08 placeholder: {placeholder}, " +
                $"drawCalls={placeholderDrawCallCount}, " +
                $"breakdown=[{placeholderDrawCallBreakdown}]");
        }

        [UnityTest]
        public IEnumerator RepresentativeViewToggleDoesNotChangeSimulationChecksum()
        {
            yield return LoadBenchmark();
            var fixture =
                Object.FindAnyObjectByType<RepresentativeArtBenchmarkFixture>();
            Assert.That(fixture, Is.Not.Null);

            fixture.Rebuild(BenchmarkArtMode.ProductionLike);
            yield return null;
            var visible = MvpSoakFixture.Run(
                MvpRoadLayout.SimpleGrid,
                observePresentationReadModels: true);

            fixture.Rebuild(BenchmarkArtMode.Placeholder);
            yield return null;
            var hidden = MvpSoakFixture.Run(
                MvpRoadLayout.SimpleGrid,
                observePresentationReadModels: false);

            Assert.That(
                visible.Months.Select(month => month.Checksum),
                Is.EqualTo(hidden.Months.Select(month => month.Checksum)));
            TestContext.WriteLine(
                $"P3-08 representative toggle checksum: {visible.FinalChecksum:X16}");
        }

        private sealed class DrawCallRecorders : IDisposable
        {
            // Unity 6.5 exposes draw-call counters per rendering path instead of
            // exposing the documented aggregate "Draw Calls Count" in the
            // Editor/player counter list. Their sum is the frame baseline.
            private static readonly string[] CounterNames =
            {
                "Standard Draw Calls Count",
                "Standard Indirect Draw Calls Count",
                "Standard Instanced Draw Calls Count",
                "SRP Batcher Draw Calls Count",
                "BRG Draw Calls Count",
                "BRG Indirect Draw Calls Count",
                "Null Geometry Draw Calls Count",
                "Null Geometry Indirect Draw Calls Count"
            };

            private readonly List<(string Name, ProfilerRecorder Recorder)> recorders =
                new List<(string, ProfilerRecorder)>();

            public DrawCallRecorders()
            {
                for (var index = 0; index < CounterNames.Length; index++)
                {
                    var recorder = ProfilerRecorder.StartNew(
                        ProfilerCategory.Render,
                        CounterNames[index],
                        16);
                    if (recorder.Valid)
                        recorders.Add((CounterNames[index], recorder));
                    else
                        recorder.Dispose();
                }
            }

            public long TotalLastValue =>
                recorders.Count == 0
                    ? -1
                    : recorders.Sum(entry => entry.Recorder.LastValue);

            public void Dispose()
            {
                for (var index = 0; index < recorders.Count; index++)
                    recorders[index].Recorder.Dispose();
            }

            public override string ToString()
            {
                return recorders.Count == 0
                    ? "unavailable"
                    : string.Join(
                        ", ",
                        recorders.Select(entry =>
                            $"{entry.Name}={entry.Recorder.LastValue}"));
            }
        }
    }
}
