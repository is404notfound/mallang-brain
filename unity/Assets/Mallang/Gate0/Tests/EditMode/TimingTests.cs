using Mallang.Gate0.Runtime.Clock;
using Mallang.Gate0.Runtime.Input;
using NUnit.Framework;

namespace Mallang.Gate0.Tests.EditMode
{
    // Covers Gate 0 test-plan paths E05–E11: clock calibration and the raw timing probe.
    public sealed class TimingTests
    {
        // E05 — median offset from clean samples.
        [Test] public void CalibrationTakesMedianOffsetFromCleanSamples()
        {
            var clock = new ManualRhythmClock(realtime: 10.0, dsp: 10.100); // fixed +100ms offset
            var cal = new ClockCalibrator(requiredSamples: 8);
            for (var i = 0; i < 8; i++) { cal.AddSample(clock); clock.Advance(0.030); }
            var result = cal.Build();
            Assert.IsTrue(result.Valid);
            Assert.AreEqual(8, result.SampleCount);
            Assert.AreEqual(0.100, result.OffsetSeconds, 1e-9);
            Assert.AreEqual(0.0, result.ErrorSeconds, 1e-9);
            Assert.AreEqual(10.100, result.ToDspSeconds(10.0), 1e-9);
        }

        // Build() must not produce a result at all before enough samples exist to compute a meaningful
        // split-half comparison — a single sample (or too few) is not evidence either way.
        [Test] public void TooFewSamplesIsInsufficientNotValid()
        {
            var cal = new ClockCalibrator(requiredSamples: 8);
            cal.AddSample(1.0, 1.1);
            cal.AddSample(2.0, 2.1);
            var result = cal.Build();
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("insufficient-samples", result.InvalidReason);
        }

        // E06 — a rewound clock invalidates the segment and blocks a false pass.
        [Test] public void RewoundClockInvalidatesSegment()
        {
            var cal = new ClockCalibrator();
            cal.AddSample(10.0, 10.1);
            cal.AddSample(10.03, 10.13);
            cal.AddSample(10.02, 10.14); // realtime moved backward
            var result = cal.Build();
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("clock-backward", result.InvalidReason);
        }

        // E07 — a sudden jump on one timeline shows up as disagreement between the window's two halves.
        [Test] public void ClockJumpInvalidatesSegment()
        {
            var cal = new ClockCalibrator(requiredSamples: 3);
            cal.AddSample(10.00, 10.100);
            cal.AddSample(10.03, 10.130);
            cal.AddSample(10.06, 10.180); // dsp jumped +20ms relative to realtime
            var result = cal.Build();
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("clock-error", result.InvalidReason);
        }

        // E08 — calibration error (disagreement between the first and second half of the window) above 5ms
        // invalidates the segment; at/under the limit stays valid. Uses requiredSamples:2 so each "half" is a
        // single raw sample, making the boundary numbers exact and easy to reason about.
        [Test] public void CalibrationErrorAboveFiveMillisecondsIsRejected()
        {
            var over = new ClockCalibrator(requiredSamples: 2);
            over.AddSample(1.0, 1.100);
            over.AddSample(2.0, 2.106); // second offset 0.106 vs first 0.100 → 6ms disagreement
            Assert.IsFalse(over.Build().Valid);

            var atLimit = new ClockCalibrator(requiredSamples: 2);
            atLimit.AddSample(1.0, 1.100);
            atLimit.AddSample(2.0, 2.105); // second offset 0.105 vs first 0.100 → exactly 5ms
            Assert.IsTrue(atLimit.Build().Valid);
        }

        // Only the most recent requiredSamples readings are evaluated (ring-buffer eviction), so an old,
        // wildly-different reading ages out rather than permanently poisoning every later recalibration.
        [Test] public void OnlyTheMostRecentSamplesAreEvaluated()
        {
            var cal = new ClockCalibrator(requiredSamples: 4);
            // An old, wildly out-of-band sample that would fail the error check on its own.
            cal.AddSample(0.0, 0.0);
            cal.AddSample(1.0, 1.100);
            cal.AddSample(2.0, 2.200);
            cal.AddSample(3.0, 3.300);
            // Four fresh, mutually consistent samples pushed after the cap: the stale first sample must have
            // aged out by the time Build() runs.
            cal.AddSample(4.0, 4.100);
            cal.AddSample(5.0, 5.100);
            cal.AddSample(6.0, 6.100);
            cal.AddSample(7.0, 7.100);

            var result = cal.Build();
            Assert.IsTrue(result.Valid);
            Assert.AreEqual(4, result.SampleCount);
            Assert.AreEqual(0.100, result.OffsetSeconds, 1e-9);
        }

        // The core regression finding (from pulling real device clock traces): comparing a single raw sample
        // to the overall median produces ~one-audio-buffer-length of "error" purely from quantization, on any
        // device, regardless of real sync. Splitting the window in half and comparing medians must tolerate
        // that per-sample jitter while still catching a real mid-window shift.
        [Test] public void PerSampleQuantizationJitterDoesNotFailCalibrationButARealShiftDoes()
        {
            // realtime ticks smoothly (10ms/sample); each sample's offset (dsp - realtime) wobbles by up to
            // ±8ms around a stable 100ms, the way real device readings do when dspTime only advances once per
            // audio buffer while realtime advances every video frame. The two halves' medians still agree.
            var jittery = new ClockCalibrator(requiredSamples: 8);
            var jitteryDsp = new[] { 1.100, 1.105, 1.125, 1.130, 1.132, 1.158, 1.160, 1.162 };
            for (var i = 0; i < 8; i++) jittery.AddSample(1.0 + i * 0.01, jitteryDsp[i]);
            var jitteryResult = jittery.Build();
            Assert.IsTrue(jitteryResult.Valid, "Per-sample quantization jitter alone must not fail calibration.");
            Assert.Less(jitteryResult.ErrorSeconds, 0.005);

            // Same style of per-sample jitter, but the offset genuinely shifts from ~100ms to ~130ms partway
            // through the window — a real change, not quantization noise, and must still be rejected.
            var shifted = new ClockCalibrator(requiredSamples: 8);
            var shiftedDsp = new[] { 1.100, 1.105, 1.125, 1.130, 1.170, 1.175, 1.195, 1.200 };
            for (var i = 0; i < 8; i++) shifted.AddSample(1.0 + i * 0.01, shiftedDsp[i]);
            Assert.IsFalse(shifted.Build().Valid, "A genuine mid-window shift must still be rejected.");
        }

        // E09 — judgement boundaries. Cue at 0µs; input offset in microseconds.
        [TestCase(-121000, JudgementBand.Miss)]
        [TestCase(-120000, JudgementBand.Good)]
        [TestCase(-60000, JudgementBand.Perfect)]
        [TestCase(0, JudgementBand.Perfect)]
        [TestCase(60000, JudgementBand.Perfect)]
        [TestCase(120000, JudgementBand.Good)]
        [TestCase(121000, JudgementBand.Miss)]
        public void JudgementBandsRespectExactBoundaries(long inputUs, JudgementBand expected)
        {
            var probe = new RawTimingProbe();
            var result = probe.Judge(inputUs, leftHand: true, new long[] { 0 });
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(expected, result.Band);
        }

        // E10 — a fixed input+cue array yields an identical band sequence every time (cross-platform stability).
        [Test] public void FixedTimestampArrayProducesDeterministicBands()
        {
            var cues = new long[] { 500000, 1000000, 1500000, 2000000 };
            var inputs = new long[] { 505000, 1080000, 1210000, 1999000 };
            var expected = new[] { JudgementBand.Perfect, JudgementBand.Good, JudgementBand.Miss, JudgementBand.Perfect };

            var first = Bands(inputs, cues);
            var second = Bands(inputs, cues);
            CollectionAssert.AreEqual(expected, first);
            CollectionAssert.AreEqual(first, second);
        }

        // E11 — a duplicate same-hand input inside the refractory window is dropped: no band, no sample.
        [Test] public void DuplicateSameHandInputIsRejected()
        {
            var probe = new RawTimingProbe();
            var cues = new long[] { 1000000 };
            Assert.IsTrue(probe.Judge(1000000, true, cues).Accepted);
            var dup = probe.Judge(1010000, true, cues); // +10ms, inside 50ms refractory
            Assert.IsFalse(dup.Accepted);
            Assert.AreEqual("duplicate", dup.RejectReason);
            // The other hand is independent.
            Assert.IsTrue(probe.Judge(1010000, false, cues).Accepted);
            // Same hand after the refractory window is accepted again.
            Assert.IsTrue(probe.Judge(1060000, true, cues).Accepted);
        }

        private static JudgementBand[] Bands(long[] inputs, long[] cues)
        {
            var probe = new RawTimingProbe();
            var bands = new JudgementBand[inputs.Length];
            for (var i = 0; i < inputs.Length; i++)
                bands[i] = probe.Judge(inputs[i], leftHand: i % 2 == 0, cues).Band;
            return bands;
        }
    }
}
