using System.Collections.Generic;
using Mallang.Gate0.Runtime.Clock;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Input;
using Mallang.Gate0.Runtime.Measurement;

namespace Mallang.Gate0.Runtime.Workload
{
    // Ties one Gate0Scenario phase to the beat scheduler, timing probe, and frame/tally collectors. Pure C#:
    // a MonoBehaviour drives it with real elapsed time and forwards beat/tap events to the actual scene
    // (crowd load, audio click, screen flash); this class only holds the deterministic judgement/measurement
    // state, so it is fully testable without Unity's audio or input APIs.
    public sealed class PhaseSession
    {
        public Gate0Scenario.Phase Phase { get; }
        public WorkloadSchedule Schedule { get; }
        public JudgementTally Tally { get; } = new JudgementTally();
        public FrameStatsCollector FrameStats { get; } = new FrameStatsCollector();

        private readonly RawTimingProbe probe = new RawTimingProbe();
        private readonly BeatScheduler beatScheduler;
        private readonly long[] cuesUs;
        private int nextSyntheticIndex;

        // Set once a clock calibration failure is detected mid-run. Judging stops immediately; the run's
        // RunRecord must be written with status "invalid" and this reason, never silently treated as valid.
        public bool Invalidated { get; private set; }
        public string InvalidReason { get; private set; }

        public PhaseSession(Gate0Scenario.Phase phase)
        {
            Phase = phase;
            Schedule = new WorkloadSchedule(phase);
            beatScheduler = new BeatScheduler(WorkloadSchedule.BeatIntervalUs, Schedule.BeatCount);
            cuesUs = new long[Schedule.BeatCount];
            for (var i = 0; i < cuesUs.Length; i++) cuesUs[i] = Schedule.BeatTimeUs(i);
        }

        public long DurationUs => (long)Phase.durationMs * 1000;
        public bool IsComplete(long elapsedUs) => elapsedUs >= DurationUs;

        public void Invalidate(string reason)
        {
            if (Invalidated) return;
            Invalidated = true;
            InvalidReason = reason;
        }

        // Beats newly due since the last call, in phase-relative microseconds. The caller drives
        // LoadDriver.Tick and the click/flash for each one returned.
        public List<int> ConsumeDueBeats(long elapsedUs) => beatScheduler.ConsumeDueBeats(elapsedUs);

        public void RecordFrame(double deltaSeconds) => FrameStats.AddFrameSeconds(deltaSeconds);

        // inputUs must already be converted into the same phase-relative DSP domain as the beat cues.
        public RawTimingProbe.Result JudgeTap(long inputUs, bool leftHand)
        {
            if (Invalidated) return new RawTimingProbe.Result(false, JudgementBand.Miss, 0, -1, "segment-invalidated");
            var result = probe.Judge(inputUs, leftHand, cuesUs);
            Tally.Add(result);
            return result;
        }

        // Automatically judges scenario-defined synthetic inputs as they come due. This drives the
        // cross-platform band-array comparison (Gate metric: same seed -> same bands on both platforms)
        // deterministically on every run, independent of whether a human is also tapping in parallel.
        public void ConsumeDueSyntheticInputs(long elapsedUs, System.Action<long, bool, RawTimingProbe.Result> onJudged = null)
        {
            if (Invalidated) return;
            var inputs = Phase.syntheticInputUs;
            if (inputs == null) return;
            while (nextSyntheticIndex < inputs.Length && inputs[nextSyntheticIndex] <= elapsedUs)
            {
                var inputUs = inputs[nextSyntheticIndex];
                var leftHand = nextSyntheticIndex % 2 == 0;
                var result = JudgeTap(inputUs, leftHand);
                onJudged?.Invoke(inputUs, leftHand, result);
                nextSyntheticIndex++;
            }
        }
    }
}
