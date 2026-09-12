using Mallang.Gate0.Runtime.Clock;
using Mallang.Gate0.Runtime.Input;
using Mallang.Gate0.Runtime.Measurement;
using NUnit.Framework;

namespace Mallang.Gate0.Tests.EditMode
{
    // Regression coverage for a review finding: a failed recalibration must actually replace the previous
    // (possibly stale/valid) calibration, not silently keep returning the old good value. Before this class
    // existed, Gate0MeasurementController only assigned `calibration = result` inside an `if (result.Valid)`
    // branch, so a failed recalibration left the old value in place and the controller kept converting
    // timestamps with a calibration that had already failed its freshness check.
    public sealed class CalibrationStateTests
    {
        [Test] public void ApplyAlwaysReplacesCurrentRegardlessOfValidity()
        {
            var state = new CalibrationState();
            Assert.IsFalse(state.Current.Valid, "A fresh CalibrationState must start invalid, not silently usable.");

            var calibrator = new ClockCalibrator();
            calibrator.AddSample(1.0, 1.1);
            var good = calibrator.Build();
            Assert.IsTrue(state.Apply(good));
            Assert.IsTrue(state.Current.Valid);
            var offsetAfterGood = state.Current.OffsetSeconds;

            // A calibrator that saw a clock rewind produces an Invalid result; applying it must overwrite the
            // still-valid previous result rather than being ignored.
            var failing = new ClockCalibrator();
            failing.AddSample(2.0, 2.1);
            failing.AddSample(1.5, 2.3); // realtime went backward
            var bad = failing.Build();
            Assert.IsFalse(bad.Valid);

            Assert.IsFalse(state.Apply(bad), "Apply must report the new result's validity, not the old one.");
            Assert.IsFalse(state.Current.Valid, "A failed recalibration must replace the stale valid calibration, not be discarded.");
            Assert.AreEqual("clock-backward", state.Current.InvalidReason);
            Assert.AreNotEqual(offsetAfterGood, state.Current.OffsetSeconds, "Current must actually be the new (invalid) result, not the retained old one.");
        }
    }

    // Covers the fix for a review finding: raw-samples.bin previously just re-serialized the RunRecord summary,
    // which is not raw data. RawMeasurementTrace must capture the individual clock/frame/beat/input
    // observations needed to recompute those summaries independently later.
    public sealed class RawMeasurementTraceTests
    {
        [Test] public void CapturesRealObservationsNotJustASummaryCopy()
        {
            var trace = new RawMeasurementTrace("30-idle");
            Assert.IsNotEmpty(trace.attemptId);
            Assert.AreEqual("30-idle", trace.phaseId);

            trace.AddClock(1.0, 1.1);
            trace.AddFrame(500000, 1.0 / 60.0);
            trace.AddBeat(3, 1500000);

            var probe = new RawTimingProbe();
            var judged = probe.Judge(505000, true, new long[] { 500000 });
            trace.AddInput("device", true, 1.505, 1.605, 505000, judged);

            Assert.AreEqual(1, trace.clockSamples.Count);
            Assert.AreEqual(1.0, trace.clockSamples[0].realtimeSeconds, 1e-9);
            Assert.AreEqual(1.1, trace.clockSamples[0].dspSeconds, 1e-9);

            Assert.AreEqual(1, trace.frameSamples.Count);
            Assert.AreEqual(500000, trace.frameSamples[0].phaseElapsedUs);
            Assert.Greater(trace.frameSamples[0].deltaUs, 0);

            Assert.AreEqual(1, trace.beatSamples.Count);
            Assert.AreEqual(3, trace.beatSamples[0].beatIndex);

            Assert.AreEqual(1, trace.inputSamples.Count);
            var sample = trace.inputSamples[0];
            Assert.AreEqual("device", sample.source);
            Assert.IsTrue(sample.leftHand);
            Assert.AreEqual(505000, sample.phaseInputUs);
            Assert.IsTrue(sample.accepted);
            Assert.AreEqual("Perfect", sample.band);
            Assert.AreEqual(5000, sample.offsetUs);

            // The serialized bytes must contain the actual sample values, not a RunRecord-shaped summary.
            var json = System.Text.Encoding.UTF8.GetString(trace.ToUtf8Json());
            StringAssert.Contains("\"phaseId\": \"30-idle\"", json);
            StringAssert.Contains("\"offsetUs\": 5000", json);
            StringAssert.DoesNotContain("meanFps", json);
            StringAssert.DoesNotContain("perfectCount", json);
        }
    }

    // The one-button "run all nine phases" diagnostic mode (requested follow-up work).
    public sealed class AutoPhaseRunnerTests
    {
        [Test] public void StepsThroughAllPhasesThenStops()
        {
            var runner = new AutoPhaseRunner(3);
            Assert.IsFalse(runner.Active);

            Assert.AreEqual(0, runner.Start());
            Assert.IsTrue(runner.Active);

            Assert.IsTrue(runner.TryAdvance(out var second));
            Assert.AreEqual(1, second);
            Assert.IsTrue(runner.Active);

            Assert.IsTrue(runner.TryAdvance(out var third));
            Assert.AreEqual(2, third);
            Assert.IsTrue(runner.Active);

            Assert.IsFalse(runner.TryAdvance(out var none), "No fourth phase exists in a 3-phase run.");
            Assert.AreEqual(-1, none);
            Assert.IsFalse(runner.Active, "The run must stop itself once the last phase is done.");
        }

        [Test] public void CancelStopsAnInProgressRun()
        {
            var runner = new AutoPhaseRunner(9);
            runner.Start();
            runner.TryAdvance(out _);
            runner.Cancel();
            Assert.IsFalse(runner.Active);
        }
    }
}
