using System;
using System.Collections.Generic;

namespace Mallang.Gate0.Runtime.Lifecycle
{
    // One state machine for both platforms. Android pause/focus and WebGL visibility/AudioContext signals are
    // coalesced by the adapters into a single Interrupt/Resume before reaching this controller.
    //
    // Transition table (kept in sync with the test file header and the implementation plan diagram):
    //
    //   Boot        --BootSucceeded-->      Idle
    //   Boot        --BootFailed-->         Error
    //   Idle        --Begin-->              CountIn
    //   CountIn     --CountInComplete-->    Playing
    //   Playing     --Finish-->             Completed
    //   Idle        --Interrupt-->          Interrupted
    //   CountIn     --Interrupt-->          Interrupted
    //   Playing     --Interrupt-->          Interrupted
    //   Interrupted --Interrupt-->          Interrupted   (repeated/continuous interrupt, no double count)
    //   Interrupted --Resume-->             Recalibrating
    //   Recalibrating --Interrupt-->        Interrupted   (re-interrupt during resume; previous resume cancelled)
    //   Recalibrating --RecalibrateSucceeded--> CountIn   (fresh 4-beat count-in)
    //   Recalibrating --RecalibrateFailed-->    Error
    //   Error       --Retry-->              Recalibrating
    //   Completed   --Begin-->              CountIn       (replay next segment)
    //
    // Any transition outside this table is rejected (fail closed) so undefined lifecycle paths cannot pass silently.
    public enum GameState { Boot, Idle, CountIn, Playing, Completed, Interrupted, Recalibrating, Error }

    public enum LifecycleTrigger { BootSucceeded, BootFailed, Begin, CountInComplete, Finish, Interrupt, Resume, RecalibrateSucceeded, RecalibrateFailed, Retry }

    public sealed class LifecycleController
    {
        public const int CountInBeats = 4;

        private static readonly Dictionary<(GameState, LifecycleTrigger), GameState> Table = new Dictionary<(GameState, LifecycleTrigger), GameState>
        {
            { (GameState.Boot, LifecycleTrigger.BootSucceeded), GameState.Idle },
            { (GameState.Boot, LifecycleTrigger.BootFailed), GameState.Error },
            { (GameState.Idle, LifecycleTrigger.Begin), GameState.CountIn },
            { (GameState.CountIn, LifecycleTrigger.CountInComplete), GameState.Playing },
            { (GameState.Playing, LifecycleTrigger.Finish), GameState.Completed },
            { (GameState.Idle, LifecycleTrigger.Interrupt), GameState.Interrupted },
            { (GameState.CountIn, LifecycleTrigger.Interrupt), GameState.Interrupted },
            { (GameState.Playing, LifecycleTrigger.Interrupt), GameState.Interrupted },
            { (GameState.Interrupted, LifecycleTrigger.Interrupt), GameState.Interrupted },
            { (GameState.Interrupted, LifecycleTrigger.Resume), GameState.Recalibrating },
            { (GameState.Recalibrating, LifecycleTrigger.Interrupt), GameState.Interrupted },
            { (GameState.Recalibrating, LifecycleTrigger.RecalibrateSucceeded), GameState.CountIn },
            { (GameState.Recalibrating, LifecycleTrigger.RecalibrateFailed), GameState.Error },
            { (GameState.Error, LifecycleTrigger.Retry), GameState.Recalibrating },
            { (GameState.Completed, LifecycleTrigger.Begin), GameState.CountIn },
        };

        public GameState State { get; private set; } = GameState.Boot;
        public int BeatsRemaining { get; private set; }

        // A segment is excluded from Gate aggregation whenever it is interrupted mid-flight.
        public int ExcludedSegments { get; private set; }

        // Game input is only live while Playing. Count-in taps are ignored (test-plan P02).
        public bool AcceptsGameInput => State == GameState.Playing;

        public event Action<GameState, GameState> Transitioned;
        // Raised when entering Interrupted: caller stops input collection and scheduled audio, and flushes
        // active holds and pending inputs.
        public event Action FlushRequested;

        public bool IsDefined(LifecycleTrigger trigger) => Table.ContainsKey((State, trigger));

        // Attempt a transition. Returns false for triggers that are valid to ignore in the current state
        // (used by adapters that may deliver a Resume with no prior Interrupt). Throws only for a genuinely
        // undefined state/trigger pair so undefined paths fail loudly.
        public bool TryFire(LifecycleTrigger trigger)
        {
            if (!Table.ContainsKey((State, trigger))) return false;
            Fire(trigger);
            return true;
        }

        public void Fire(LifecycleTrigger trigger)
        {
            if (!Table.TryGetValue((State, trigger), out var next))
                throw new InvalidOperationException($"Undefined lifecycle transition: {State} --{trigger}-->");
            var previous = State;

            // A running segment that gets interrupted is dropped from Gate aggregation. Entering Interrupted from
            // Interrupted (repeated interrupt) does not double-count.
            if (next == GameState.Interrupted && previous != GameState.Interrupted)
            {
                ExcludedSegments++;
                FlushRequested?.Invoke();
            }
            // Every entry into count-in restarts the four-beat count, including the fresh count after a resume.
            if (next == GameState.CountIn) BeatsRemaining = CountInBeats;

            State = next;
            Transitioned?.Invoke(previous, next);
        }

        // Called once per beat during CountIn. Completes the count-in when the fourth beat elapses.
        public void AdvanceCountInBeat()
        {
            if (State != GameState.CountIn) throw new InvalidOperationException("AdvanceCountInBeat is only valid during CountIn.");
            if (BeatsRemaining > 0) BeatsRemaining--;
            if (BeatsRemaining == 0) Fire(LifecycleTrigger.CountInComplete);
        }
    }
}
