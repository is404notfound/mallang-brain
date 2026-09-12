using Mallang.Gate0.Runtime.Clock;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Input;
using Mallang.Gate0.Runtime.Workload;
using NUnit.Framework;

namespace Mallang.Gate0.Tests.EditMode
{
    // Covers the pieces that wire T4-T6 into a real, measurable run: beat-crossing detection, judgement
    // tallying, and the phase session that ties a scenario phase to both.
    public sealed class BeatSchedulerTests
    {
        [Test] public void ConsumesOneBeatPerCrossingAndCatchesUpAfterAGap()
        {
            var scheduler = new BeatScheduler(beatIntervalUs: 500000, beatCount: 4);
            CollectionAssert.AreEqual(new[] { 0 }, scheduler.ConsumeDueBeats(0)); // beat 0 fires immediately at phase start
            CollectionAssert.IsEmpty(scheduler.ConsumeDueBeats(100000));
            CollectionAssert.IsEmpty(scheduler.ConsumeDueBeats(400000)); // beat 1 not due yet
            // A slow frame that skips two beat boundaries still reports both, in order.
            CollectionAssert.AreEqual(new[] { 1, 2 }, scheduler.ConsumeDueBeats(1_100_000));
            Assert.AreEqual(3, scheduler.NextBeatIndex);
            Assert.IsFalse(scheduler.IsFinished);
            CollectionAssert.AreEqual(new[] { 3 }, scheduler.ConsumeDueBeats(2_000_000));
            Assert.IsTrue(scheduler.IsFinished);
            CollectionAssert.IsEmpty(scheduler.ConsumeDueBeats(10_000_000)); // no beats left to fire
        }
    }

    public sealed class JudgementTallyTests
    {
        [Test] public void TalliesEachBandAndDuplicatesSeparately()
        {
            var tally = new JudgementTally();
            var probe = new RawTimingProbe();
            tally.Add(probe.Judge(0, true, new long[] { 0 }));            // Perfect
            tally.Add(probe.Judge(100000, false, new long[] { 0 }));      // Good (100ms offset from single cue)
            tally.Add(probe.Judge(200000, true, new long[] { 0 }));       // Miss (>120ms)
            tally.Add(probe.Judge(1000, true, new long[] { 0 }));         // duplicate (same hand, inside refractory)

            Assert.AreEqual(1, tally.Perfect);
            Assert.AreEqual(1, tally.Good);
            Assert.AreEqual(1, tally.Miss);
            Assert.AreEqual(1, tally.Duplicate);
            Assert.AreEqual(3, tally.TotalJudged);
        }
    }

    public sealed class PhaseSessionTests
    {
        private static Gate0Scenario.Phase MakePhase(int durationMs, int crowd, string kind) => new Gate0Scenario.Phase
        {
            id = crowd + "-" + kind, durationMs = durationMs, crowdCount = crowd, animatorCount = crowd,
            spriteLayers = 2, audioVoiceCount = 8, uiUpdatesPerBeat = 4,
            vfxSpawnCount = kind == "simultaneous_vfx" ? crowd : 0,
            eliminationCount = kind == "mass_elimination" ? crowd : 0,
            syntheticInputUs = new long[] { 500000 },
        };

        [Test] public void BeatsAndCompletionFollowThePhaseDuration()
        {
            var session = new PhaseSession(MakePhase(1000, 30, "idle")); // 2 beats: 0 and 500000us
            CollectionAssert.AreEqual(new[] { 0 }, session.ConsumeDueBeats(200000));
            Assert.IsFalse(session.IsComplete(200000));
            CollectionAssert.AreEqual(new[] { 1 }, session.ConsumeDueBeats(999999));
            Assert.IsFalse(session.IsComplete(999999));
            Assert.IsTrue(session.IsComplete(1000000));
        }

        [Test] public void JudgeTapFeedsTheTally()
        {
            var session = new PhaseSession(MakePhase(1000, 30, "idle"));
            session.ConsumeDueBeats(1000000); // establish the two beat cues at 0 and 500000
            var result = session.JudgeTap(510000, true); // 10ms after the second beat cue
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(Mallang.Gate0.Runtime.Input.JudgementBand.Perfect, result.Band);
            Assert.AreEqual(1, session.Tally.Perfect);
        }

        [Test] public void RecordFrameFeedsFrameStats()
        {
            var session = new PhaseSession(MakePhase(1000, 30, "idle"));
            session.RecordFrame(1.0 / 60.0);
            session.RecordFrame(1.0 / 60.0);
            Assert.AreEqual(2, session.FrameStats.FrameCount);
        }

        // A clock calibration failure mid-run must stop judging immediately and mark the segment invalid,
        // rather than silently keep using a stale/drifted conversion (test-plan E06/E07/E08 applied at the
        // session level).
        [Test] public void InvalidateStopsJudgingAndRecordsTheReason()
        {
            var session = new PhaseSession(MakePhase(1000, 30, "idle"));
            session.ConsumeDueBeats(1000000);
            Assert.IsTrue(session.JudgeTap(0, true).Accepted);
            Assert.AreEqual(1, session.Tally.TotalJudged);

            session.Invalidate("clock-error");
            Assert.IsTrue(session.Invalidated);
            Assert.AreEqual("clock-error", session.InvalidReason);

            var result = session.JudgeTap(500000, false);
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("segment-invalidated", result.RejectReason);
            Assert.AreEqual(1, session.Tally.TotalJudged); // unchanged: the post-invalidation tap was not counted

            session.Invalidate("clock-error-again"); // idempotent: first reason sticks
            Assert.AreEqual("clock-error", session.InvalidReason);
        }

        // The scenario's fixed synthetic input schedule auto-replays as the phase runs, independent of any
        // human tapping, so the cross-platform band-array comparison (Gate metric) does not require a human
        // to tap continuously for the full three minutes.
        [Test] public void SyntheticInputsAutoReplayAsElapsedTimeAdvances()
        {
            var phase = MakePhase(2000, 30, "idle"); // 4 beats: 0/500000/1000000/1500000
            phase.syntheticInputUs = new long[] { 10000, 505000, 1490000 };
            var session = new PhaseSession(phase);
            session.ConsumeDueBeats(2000000); // establish all four beat cues up front

            session.ConsumeDueSyntheticInputs(400000); // only the first synthetic input (10000) is due
            Assert.AreEqual(1, session.Tally.TotalJudged);

            session.ConsumeDueSyntheticInputs(400000); // calling again with the same elapsed time is a no-op
            Assert.AreEqual(1, session.Tally.TotalJudged);

            session.ConsumeDueSyntheticInputs(2000000); // the remaining two come due together
            Assert.AreEqual(3, session.Tally.TotalJudged);
        }

        [Test] public void SyntheticInputsStopReplayingOnceInvalidated()
        {
            var phase = MakePhase(1000, 30, "idle");
            phase.syntheticInputUs = new long[] { 10000 };
            var session = new PhaseSession(phase);
            session.Invalidate("clock-error");
            session.ConsumeDueSyntheticInputs(1000000);
            Assert.AreEqual(0, session.Tally.TotalJudged);
        }
    }
}
