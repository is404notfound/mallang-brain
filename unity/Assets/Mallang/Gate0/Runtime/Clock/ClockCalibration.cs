using System;
using System.Collections.Generic;

namespace Mallang.Gate0.Runtime.Clock
{
    // Result of pairing realtime and DSP samples. offset converts realtime → DSP: dsp = realtime + OffsetSeconds.
    // An invalid calibration must never be used to judge; the segment is dropped and its raw data preserved.
    public readonly struct ClockCalibration
    {
        public readonly bool Valid;
        public readonly string InvalidReason;
        public readonly double OffsetSeconds;   // median(dsp - realtime)
        public readonly double ErrorSeconds;    // max |sample offset - median|
        public readonly double SampledAtRealtime;
        public readonly int SampleCount;

        public ClockCalibration(double offset, double error, double sampledAt, int count)
        {
            Valid = true; InvalidReason = null;
            OffsetSeconds = offset; ErrorSeconds = error; SampledAtRealtime = sampledAt; SampleCount = count;
        }
        private ClockCalibration(string reason, int count)
        {
            Valid = false; InvalidReason = reason;
            OffsetSeconds = 0; ErrorSeconds = double.PositiveInfinity; SampledAtRealtime = 0; SampleCount = count;
        }
        public static ClockCalibration Invalid(string reason, int count) => new ClockCalibration(reason, count);

        // Convert an input timestamp (realtime timeline) into the DSP timeline. Guarded by Valid upstream.
        public double ToDspSeconds(double realtimeSeconds) => realtimeSeconds + OffsetSeconds;
    }

    // Accumulates paired (realtime, dsp) samples and produces a ClockCalibration.
    // Recalibrated at segment start, every 30s, on resume, and after AudioSettings.OnAudioConfigurationChanged.
    // Fails closed on a rewound clock, a non-monotonic timeline, or a calibration error above the limit.
    public sealed class ClockCalibrator
    {
        public const double DefaultMaxErrorSeconds = 0.005; // Gate 0 policy: clock_calibration_error_ms = 5
        // A recalibration evaluates only the most recent samples, not everything accumulated since the last
        // finalize. An open-ended window mixes genuine long-term drift detection with ordinary short-term
        // scheduler jitter — measured in batch-mode Unity, 130+ samples spread over ~2s of otherwise healthy
        // Playing time exceeded the 5ms error bound on their own, which would falsely invalidate a normal
        // resume. A short rolling burst still catches a real rewind/jump (see AddSample) while tolerating that
        // jitter.
        public const int DefaultMaxSamples = 8;

        private readonly double maxErrorSeconds;
        private readonly int maxSamples;
        private readonly List<double> offsets = new List<double>();
        private double lastRealtime = double.NegativeInfinity;
        private double lastDsp = double.NegativeInfinity;
        private double lastSampledRealtime;
        private string faultReason;

        public ClockCalibrator(double maxErrorSeconds = DefaultMaxErrorSeconds, int maxSamples = DefaultMaxSamples)
        {
            this.maxErrorSeconds = maxErrorSeconds;
            this.maxSamples = maxSamples;
        }

        public int SampleCount => offsets.Count;

        public void Reset() { offsets.Clear(); lastRealtime = double.NegativeInfinity; lastDsp = double.NegativeInfinity; faultReason = null; }

        // Add one paired reading. Both timelines must move forward; a rewound reading marks the calibration
        // invalid. Once maxSamples is reached, the oldest reading is dropped so Build() always reflects a
        // short, recent window rather than the whole accumulation period.
        public void AddSample(double realtimeSeconds, double dspSeconds)
        {
            if (faultReason != null) return; // already invalid; keep raw but stop mixing new samples
            if (double.IsNaN(realtimeSeconds) || double.IsNaN(dspSeconds) || double.IsInfinity(realtimeSeconds) || double.IsInfinity(dspSeconds))
            { faultReason = "clock-error"; return; }
            if (offsets.Count > 0)
            {
                if (realtimeSeconds < lastRealtime || dspSeconds < lastDsp) { faultReason = "clock-backward"; return; }
                if (realtimeSeconds == lastRealtime) { faultReason = "clock-non-monotonic"; return; }
            }
            lastRealtime = realtimeSeconds; lastDsp = dspSeconds; lastSampledRealtime = realtimeSeconds;
            offsets.Add(dspSeconds - realtimeSeconds);
            if (offsets.Count > maxSamples) offsets.RemoveAt(0);
        }

        public void AddSample(IRhythmClock clock) => AddSample(clock.RealtimeSeconds, clock.DspSeconds);

        public ClockCalibration Build()
        {
            if (faultReason != null) return ClockCalibration.Invalid(faultReason, offsets.Count);
            if (offsets.Count == 0) return ClockCalibration.Invalid("no-samples", 0);
            var sorted = offsets.ToArray();
            Array.Sort(sorted);
            var mid = sorted.Length / 2;
            var median = (sorted.Length & 1) == 1 ? sorted[mid] : 0.5 * (sorted[mid - 1] + sorted[mid]);
            var error = 0.0;
            foreach (var o in sorted) error = Math.Max(error, Math.Abs(o - median));
            // A forward jump or drift on one timeline shows up as an outlier offset; over the limit the segment is dropped.
            if (error > maxErrorSeconds) return ClockCalibration.Invalid("clock-error", offsets.Count);
            return new ClockCalibration(median, error, lastSampledRealtime, offsets.Count);
        }
    }
}
