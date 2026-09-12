using System;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Small deterministic state holder for the one-button nine-phase diagnostic run.
    public sealed class AutoPhaseRunner
    {
        public bool Active { get; private set; }
        public int PhaseIndex { get; private set; } = -1;
        public int PhaseCount { get; }

        public AutoPhaseRunner(int phaseCount)
        {
            if (phaseCount <= 0) throw new ArgumentOutOfRangeException(nameof(phaseCount));
            PhaseCount = phaseCount;
        }

        public int Start()
        {
            Active = true;
            PhaseIndex = 0;
            return PhaseIndex;
        }

        public bool TryAdvance(out int nextPhaseIndex)
        {
            if (!Active || PhaseIndex + 1 >= PhaseCount)
            {
                Active = false;
                nextPhaseIndex = -1;
                return false;
            }
            nextPhaseIndex = ++PhaseIndex;
            return true;
        }

        public void Cancel() => Active = false;
    }
}
