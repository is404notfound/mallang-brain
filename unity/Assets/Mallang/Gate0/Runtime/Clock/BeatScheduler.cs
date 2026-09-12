using System.Collections.Generic;

namespace Mallang.Gate0.Runtime.Clock
{
    // Pure beat-crossing detector. Given elapsed time since a phase (or count-in) started, reports which beat
    // indices have newly come due, so the caller drives the crowd load and the metronome click/flash exactly
    // once per beat regardless of how many Update frames land between two beats (or several beats on one slow
    // frame). Does not schedule audio itself; AudioSource.PlayScheduled look-ahead is handled by the caller
    // against AudioSettings.dspTime directly, since that needs sub-frame precision this detector cannot give.
    public sealed class BeatScheduler
    {
        private readonly long beatIntervalUs;
        private readonly int beatCount;
        private int nextBeat;

        public BeatScheduler(long beatIntervalUs, int beatCount)
        {
            this.beatIntervalUs = beatIntervalUs;
            this.beatCount = beatCount;
        }

        public int NextBeatIndex => nextBeat;
        public bool IsFinished => nextBeat >= beatCount;

        // elapsedUs must be non-decreasing across calls on the same instance.
        public List<int> ConsumeDueBeats(long elapsedUs)
        {
            var due = new List<int>();
            while (nextBeat < beatCount && (long)nextBeat * beatIntervalUs <= elapsedUs)
            {
                due.Add(nextBeat);
                nextBeat++;
            }
            return due;
        }
    }
}
