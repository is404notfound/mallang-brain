using System;
using System.Collections.Generic;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Accumulates frame durations and produces the frame-stability metrics Gate 0 records. Pure and
    // deterministic: percentiles use the nearest-rank method so the same samples always yield the same numbers.
    // Frames captured during an interrupted (excluded) segment must not be fed here — the caller gates on
    // LifecycleController state.
    public sealed class FrameStatsCollector
    {
        private readonly List<double> frameMs = new List<double>();

        public void AddFrameSeconds(double deltaSeconds)
        {
            if (deltaSeconds <= 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) return;
            frameMs.Add(deltaSeconds * 1000.0);
        }

        public void Clear() => frameMs.Clear();
        public int FrameCount => frameMs.Count;

        public readonly struct Snapshot
        {
            public readonly int FrameCount;
            public readonly double MeanFps;
            public readonly double P95Ms;
            public readonly double P99Ms;
            public readonly int FramesOver33Ms; // below ~30 FPS
            public readonly int FramesOver50Ms; // below ~20 FPS
            public readonly double LongestStallMs; // longest run of consecutive frames over 50ms
            public Snapshot(int frameCount, double meanFps, double p95, double p99, int over33, int over50, double longestStall)
            { FrameCount = frameCount; MeanFps = meanFps; P95Ms = p95; P99Ms = p99; FramesOver33Ms = over33; FramesOver50Ms = over50; LongestStallMs = longestStall; }
        }

        public Snapshot Compute()
        {
            if (frameMs.Count == 0) return new Snapshot(0, 0, 0, 0, 0, 0, 0);
            var sorted = frameMs.ToArray();
            Array.Sort(sorted);

            double totalMs = 0;
            int over33 = 0, over50 = 0;
            double longestStall = 0, currentStall = 0;
            foreach (var ms in frameMs) // iterate in capture order for the consecutive-stall run
            {
                totalMs += ms;
                if (ms > 33.0) over33++;
                if (ms > 50.0) { over50++; currentStall += ms; longestStall = Math.Max(longestStall, currentStall); }
                else currentStall = 0;
            }
            var meanFps = frameMs.Count / (totalMs / 1000.0);
            return new Snapshot(frameMs.Count, meanFps, Percentile(sorted, 0.95), Percentile(sorted, 0.99), over33, over50, longestStall);
        }

        // Nearest-rank percentile on an ascending array.
        private static double Percentile(double[] sorted, double p)
        {
            var rank = (int)Math.Ceiling(p * sorted.Length);
            if (rank < 1) rank = 1;
            if (rank > sorted.Length) rank = sorted.Length;
            return sorted[rank - 1];
        }
    }
}
