using System;
using Mallang.Gate0.Runtime.Configuration;

namespace Mallang.Gate0.Runtime.Workload
{
    // Deterministic per-beat plan derived from a frozen Gate0Scenario.Phase. Pure and platform-independent so
    // the same scenario drives an identical crowd/VFX/elimination load on Android and WebGL, and so the counts
    // can be verified in Edit Mode without spawning GameObjects. The measured worst case is the upper-bound
    // proxy load described in the plan: each simultaneous_vfx beat spawns one VFX per crowd member, and each
    // mass_elimination beat eliminates and respawns the whole crowd.
    public sealed class WorkloadSchedule
    {
        // Beat cadence matches the scenario's synthetic input generator (500ms → 120 BPM).
        public const long BeatIntervalUs = 500000;

        public string PhaseId { get; }
        public int CrowdCount { get; }
        public int BeatCount { get; }
        private readonly int vfxPerBeat;
        private readonly int eliminationsPerBeat;

        public WorkloadSchedule(Gate0Scenario.Phase phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            var durationUs = (long)phase.durationMs * 1000;
            if (durationUs <= 0 || durationUs % BeatIntervalUs != 0)
                throw new InvalidOperationException("Phase duration must be a positive whole number of beats.");
            PhaseId = phase.id;
            CrowdCount = phase.crowdCount;
            BeatCount = (int)(durationUs / BeatIntervalUs);
            // Fields come straight from the frozen scenario: vfxSpawnCount/eliminationCount are crowd-sized only
            // for their respective phase kind, 0 otherwise.
            vfxPerBeat = phase.vfxSpawnCount;
            eliminationsPerBeat = phase.eliminationCount;
        }

        public long BeatTimeUs(int beat) => (long)beat * BeatIntervalUs;
        public int VfxOnBeat(int beat) => InRange(beat) ? vfxPerBeat : 0;
        public int EliminationsOnBeat(int beat) => InRange(beat) ? eliminationsPerBeat : 0;

        public long TotalVfx => (long)vfxPerBeat * BeatCount;
        public long TotalEliminations => (long)eliminationsPerBeat * BeatCount;

        private bool InRange(int beat) => beat >= 0 && beat < BeatCount;
    }
}
