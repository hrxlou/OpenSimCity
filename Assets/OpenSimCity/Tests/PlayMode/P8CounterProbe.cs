using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Application.Verification;
using Unity.Profiling;

namespace OpenSimCity.Tests.PlayMode
{
    internal sealed class P8CounterProbe : IDisposable
    {
        private static readonly string[] DrawNames =
        {
            "Standard Draw Calls Count", "Standard Indirect Draw Calls Count",
            "Standard Instanced Draw Calls Count", "SRP Batcher Draw Calls Count",
            "BRG Draw Calls Count", "BRG Indirect Draw Calls Count",
            "Null Geometry Draw Calls Count", "Null Geometry Indirect Draw Calls Count"
        };
        private readonly ProfilerRecorder cpu;
        private readonly ProfilerRecorder gpu;
        private readonly ProfilerRecorder gc;
        private readonly ProfilerRecorder memory;
        private readonly List<ProfilerRecorder> draws = new List<ProfilerRecorder>();

        public P8CounterProbe(int capacity = 600)
        {
            cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", capacity);
            gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", capacity);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", capacity);
            memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", capacity);
            for (var i = 0; i < DrawNames.Length; i++)
            {
                var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, DrawNames[i], capacity);
                if (recorder.Valid) draws.Add(recorder); else recorder.Dispose();
            }
        }

        public P8BenchmarkMetricValue CpuFrame() => Read(P8BenchmarkMetric.CpuFrameMicroseconds, cpu, true);
        public P8BenchmarkMetricValue GpuFrame() => Read(P8BenchmarkMetric.GpuFrameMicroseconds, gpu, true);
        public P8BenchmarkMetricValue GcBytes() => Read(P8BenchmarkMetric.GcBytesPerFrame, gc, false);
        public P8BenchmarkMetricValue TotalMemory() => Read(P8BenchmarkMetric.TotalMemoryBytes, memory, false);
        public P8BenchmarkMetricValue DrawCalls()
        {
            if (draws.Count == 0) return Missing(P8BenchmarkMetric.DrawCalls);
            var values = new List<long>();
            var count = draws.Min(entry => (int)entry.Count);
            for (var sample = 0; sample < count; sample++)
            {
                long sum = 0;
                for (var i = 0; i < draws.Count; i++) sum += draws[i].GetSample(sample).Value;
                values.Add(sum);
            }
            return Values(P8BenchmarkMetric.DrawCalls, values, false);
        }

        public void Dispose()
        {
            cpu.Dispose(); gpu.Dispose(); gc.Dispose(); memory.Dispose();
            for (var i = 0; i < draws.Count; i++) draws[i].Dispose();
        }

        private static P8BenchmarkMetricValue Read(P8BenchmarkMetric metric, ProfilerRecorder recorder, bool nanoToMicro)
        {
            if (!recorder.Valid || recorder.Count == 0) return Missing(metric);
            var values = new List<long>((int)recorder.Count);
            for (var i = 0; i < recorder.Count; i++) values.Add(recorder.GetSample(i).Value);
            return Values(metric, values, nanoToMicro);
        }

        private static P8BenchmarkMetricValue Values(P8BenchmarkMetric metric, List<long> values, bool nanoToMicro)
        {
            if (values.Count == 0) return Missing(metric);
            values.Sort();
            long Convert(long value) => nanoToMicro ? value / 1000L : value;
            return new P8BenchmarkMetricValue
            {
                Metric = metric, IsMeasured = true,
                Minimum = Convert(values[0]), Median = Convert(values[values.Count / 2]), Maximum = Convert(values[values.Count - 1])
            };
        }

        private static P8BenchmarkMetricValue Missing(P8BenchmarkMetric metric) =>
            new P8BenchmarkMetricValue { Metric = metric, IsMeasured = false };
    }
}
