using System;
using Mallang.RhythmCore;

namespace Mallang.Gate1.Runtime
{
    public enum MiniContestPhase { Calibrating, Ready, CountIn, Playing, StageResult, Result }

    // Small state machine shared by the screen and tests. Timing and presentation stay outside it.
    public sealed class MiniContestFlow
    {
        private bool finalResultPending;

        public MiniContestPhase Phase { get; private set; } = MiniContestPhase.Calibrating;

        public void BeginCalibration()
        {
            finalResultPending = false;
            Phase = MiniContestPhase.Calibrating;
        }

        public void CompleteCalibration()
        {
            Require(MiniContestPhase.Calibrating);
            Phase = MiniContestPhase.Ready;
        }

        public void StartRun()
        {
            Require(MiniContestPhase.Ready, MiniContestPhase.Result);
            finalResultPending = false;
            Phase = MiniContestPhase.CountIn;
        }

        public void StartPlaying()
        {
            Require(MiniContestPhase.CountIn);
            Phase = MiniContestPhase.Playing;
        }

        public void ResolveStage(RunSnapshot snapshot)
        {
            Require(MiniContestPhase.Playing);
            if (snapshot.EndReason == RunEndReason.Failed || snapshot.EndReason == RunEndReason.Invalid)
            {
                Phase = MiniContestPhase.Result;
                finalResultPending = false;
                return;
            }

            finalResultPending = snapshot.EndReason == RunEndReason.Completed;
            Phase = MiniContestPhase.StageResult;
        }

        public void CompleteStageResult()
        {
            Require(MiniContestPhase.StageResult);
            Phase = finalResultPending ? MiniContestPhase.Result : MiniContestPhase.CountIn;
            finalResultPending = false;
        }

        private void Require(params MiniContestPhase[] allowed)
        {
            foreach (var candidate in allowed) if (Phase == candidate) return;
            throw new InvalidOperationException("Invalid mini contest transition from " + Phase + ".");
        }
    }
}
