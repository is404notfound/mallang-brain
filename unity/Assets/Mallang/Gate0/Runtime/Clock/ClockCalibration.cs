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
        public readonly double ErrorSeconds;    // |median(first half) - median(second half)|
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
    //
    // Design note (found by pulling real RawMeasurementTrace clock samples off a Galaxy device): a single
    // per-frame (realtime, dsp) reading carries up to about one audio-buffer-length of quantization jitter,
    // because AudioSettings.dspTime only advances once per audio callback (observed: 21.33ms steps, a 1024
    // sample buffer at 48kHz) while realtimeSinceStartup advances every video frame (~16.7ms at 60fps). Those
    // two step sizes are unrelated, so comparing an individual sample to the overall median (the previous
    // approach) shows 8-20ms of sawtooth "error" from quantization alone, on any device, regardless of whether
    // the clocks are actually in sync. That doesn't mean the calibration is bad — it means comparing single
    // samples is the wrong statistic.
    //
    // The median of enough samples is a robust, low-noise estimate of the true offset. To check whether that
    // *estimate* is stable (as opposed to whether a single reading matches it), this class splits the window in
    // half chronologically and compares the two halves' medians — quantization jitter on each side averages
    // out, while a genuine jump/drift during the window still shows up as disagreement between the halves.
    // Empirically (same real-device trace), this needed at least ~96 samples to reliably clear the 5ms policy
    // bound; RequiredSamples defaults to 160 (~2.7s at 60fps) for comfortable margin (observed max error 1.9ms
    // at that count, vs. the 5ms bound).
    public sealed class ClockCalibrator
    {
        public const double DefaultMaxErrorSeconds = 0.005; // Gate 0 policy: clock_calibration_error_ms = 5
        public const int DefaultRequiredSamples = 160;

        private readonly double maxErrorSeconds;
        private readonly int requiredSamples;
        private readonly List<double> offsets = new List<double>();
        private double lastRealtime = double.NegativeInfinity;
        private double lastDsp = double.NegativeInfinity;
        private double lastSampledRealtime;
        private string faultReason;

        public ClockCalibrator(double maxErrorSeconds = DefaultMaxErrorSeconds, int requiredSamples = DefaultRequiredSamples)
        {
            if (requiredSamples < 2) throw new ArgumentException("requiredSamples must be at least 2 to split into two halves.", nameof(requiredSamples));
            this.maxErrorSeconds = maxErrorSeconds;
            this.requiredSamples = requiredSamples;
        }

        public int SampleCount => offsets.Count;
        public int RequiredSamples => requiredSamples;

        public void Reset() { offsets.Clear(); lastRealtime = double.NegativeInfinity; lastDsp = double.NegativeInfinity; faultReason = null; }

        // Add one paired reading. Both timelines must move forward; a rewound reading marks the calibration
        // invalid. Once requiredSamples is reached, the oldest reading is dropped so Build() always reflects a
        // recent rolling window rather than the whole accumulation period.
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
            if (offsets.Count > requiredSamples) offsets.RemoveAt(0);
        }

        public void AddSample(IRhythmClock clock) => AddSample(clock.RealtimeSeconds, clock.DspSeconds);

        public ClockCalibration Build()
        {
            if (faultReason != null) return ClockCalibration.Invalid(faultReason, offsets.Count);
            if (offsets.Count < requiredSamples) return ClockCalibration.Invalid("insufficient-samples", offsets.Count);

            var overallMedian = Median(offsets, 0, offsets.Count);
            var half = offsets.Count / 2;
            var firstHalfMedian = Median(offsets, 0, half);
            var secondHalfMedian = Median(offsets, half, offsets.Count - half);
            var error = Math.Abs(firstHalfMedian - secondHalfMedian);

            // Disagreement between the two halves' medians reflects genuine drift/instability across the
            // window, not per-sample quantization noise (which the median already averages out on each side).
            if (error > maxErrorSeconds) return ClockCalibration.Invalid("clock-error", offsets.Count);
            return new ClockCalibration(overallMedian, error, lastSampledRealtime, offsets.Count);
        }

        // Median of offsets[start .. start+count), taken in chronological (insertion) order, not sorted order —
        // a chronological split is what lets the two halves catch a drift that happens *during* the window.
        private static double Median(List<double> source, int start, int count)
        {
            var slice = new double[count];
            source.CopyTo(start, slice, 0, count);
            Array.Sort(slice);
            var mid = count / 2;
            return (count & 1) == 1 ? slice[mid] : 0.5 * (slice[mid - 1] + slice[mid]);
        }
    }
}
