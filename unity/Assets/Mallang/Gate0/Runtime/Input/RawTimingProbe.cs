using System;

namespace Mallang.Gate0.Runtime.Input
{
    // Technical-validation bands only. This is NOT the game's scoring. Gate 1 builds score/combo/hold/fail.
    public enum JudgementBand { Perfect, Good, Miss }

    // Classifies a converted input timestamp against a fixed cue schedule and records only the raw offset band.
    // Works in integer microseconds so the same input+cue arrays produce a byte-identical band array on every
    // platform (Gate metric: cross-platform band match must be 100%). Clock→DSP conversion happens upstream in
    // seconds; the caller quantizes to microseconds before judging.
    public sealed class RawTimingProbe
    {
        public const long DefaultPerfectUs = 60000;    // ±60ms  → Perfect
        public const long DefaultGoodUs = 120000;      // ±120ms → Good
        public const long DefaultRefractoryUs = 50000; // same-hand inputs closer than this are duplicates

        private readonly long perfectUs, goodUs, refractoryUs;
        private long lastLeftUs = long.MinValue, lastRightUs = long.MinValue;

        public RawTimingProbe(long perfectUs = DefaultPerfectUs, long goodUs = DefaultGoodUs, long refractoryUs = DefaultRefractoryUs)
        {
            if (perfectUs < 0 || goodUs < perfectUs || refractoryUs < 0)
                throw new ArgumentException("Probe windows must be non-negative and Good must be no tighter than Perfect.");
            this.perfectUs = perfectUs; this.goodUs = goodUs; this.refractoryUs = refractoryUs;
        }

        public readonly struct Result
        {
            public readonly bool Accepted;      // false → duplicate input: no VFX, no sample
            public readonly JudgementBand Band;
            public readonly long OffsetUs;      // signed: input - nearest cue
            public readonly int CueIndex;       // -1 when no cue exists
            public readonly string RejectReason;
            public Result(bool accepted, JudgementBand band, long offsetUs, int cueIndex, string rejectReason)
            { Accepted = accepted; Band = band; OffsetUs = offsetUs; CueIndex = cueIndex; RejectReason = rejectReason; }
        }

        public void Reset() { lastLeftUs = long.MinValue; lastRightUs = long.MinValue; }

        // cuesUs must be sorted ascending. leftHand selects the per-hand duplicate filter.
        public Result Judge(long inputUs, bool leftHand, long[] cuesUs)
        {
            var last = leftHand ? lastLeftUs : lastRightUs;
            if (last != long.MinValue && inputUs - last < refractoryUs)
                return new Result(false, JudgementBand.Miss, 0, -1, "duplicate");
            if (leftHand) lastLeftUs = inputUs; else lastRightUs = inputUs;

            if (cuesUs == null || cuesUs.Length == 0)
                return new Result(true, JudgementBand.Miss, 0, -1, null);

            var index = NearestCue(cuesUs, inputUs);
            var offset = inputUs - cuesUs[index];
            var abs = Math.Abs(offset);
            var band = abs <= perfectUs ? JudgementBand.Perfect : abs <= goodUs ? JudgementBand.Good : JudgementBand.Miss;
            return new Result(true, band, offset, index, null);
        }

        private static int NearestCue(long[] cues, long value)
        {
            var lo = Array.BinarySearch(cues, value);
            if (lo >= 0) return lo;
            lo = ~lo; // insertion point
            if (lo == 0) return 0;
            if (lo == cues.Length) return cues.Length - 1;
            // Tie on equal distance resolves to the earlier cue for a deterministic band array.
            return value - cues[lo - 1] <= cues[lo] - value ? lo - 1 : lo;
        }
    }
}
