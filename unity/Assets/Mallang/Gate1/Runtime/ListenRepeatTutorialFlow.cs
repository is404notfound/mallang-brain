using System;
using System.Collections.Generic;
using Mallang.RhythmCore;

namespace Mallang.Gate1.Runtime
{
    public enum TutorialPhase { PatternReady, Demonstration, Countdown, PlayerPerformance, Result, Transition }

    // The tutorial owns only progression. ContestRun remains the sole source of judgements and scores.
    public sealed class ListenRepeatTutorialFlow
    {
        public const float MinimumBrainScale = 0.35f;
        public const float MaximumBrainScale = 0.90f;
        public const float InitialBrainScale = 0.40f;

        private readonly ListenAndRepeatPatterns.Entry[] patterns;
        private readonly int passAccuracyBasisPoints;
        private int patternIndex;
        private ContestRun run;
        private readonly HashSet<int> acceptedTapNotes = new HashSet<int>();
        private long lastAcceptedInputUs = long.MinValue;

        public ListenRepeatTutorialFlow(int passAccuracyBasisPoints = 6000)
        {
            if (passAccuracyBasisPoints < 0 || passAccuracyBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(passAccuracyBasisPoints));
            this.passAccuracyBasisPoints = passAccuracyBasisPoints;
            const int q = PatternBlock.TicksPerBeat;
            const int e = q / 2;
            patterns = new[]
            {
                Find("LR01"), Find("LR02"), Find("LR03"), Find("LR04"), Find("LR05"), Find("LR06"),
                TapPattern("TR07", "L R R L", new[] {0, q, 2*q, 3*q}, new[] {Hand.Left, Hand.Right, Hand.Right, Hand.Left}),
                TapPattern("TR08", "LR - LR -", new[] {0, e, 2*q, 2*q+e}, new[] {Hand.Left, Hand.Right, Hand.Left, Hand.Right}),
                TapPattern("TR09", "L R L - R L R -", new[] {0, e, q, 2*q, 2*q+e, 3*q},
                    new[] {Hand.Left, Hand.Right, Hand.Left, Hand.Right, Hand.Left, Hand.Right}),
                TapPattern("TR10", "L R L R L R L R", new[] {0, e, q, q+e, 2*q, 2*q+e, 3*q, 3*q+e},
                    new[] {Hand.Left, Hand.Right, Hand.Left, Hand.Right, Hand.Left, Hand.Right, Hand.Left, Hand.Right})
            };
            PreparePattern(0);
        }

        public TutorialPhase Phase { get; private set; }
        public int PatternIndex => patternIndex;
        public int PatternCount => patterns.Length;
        public bool IsLastPattern => patternIndex == patterns.Length - 1;
        public PatternBlock Pattern => patterns[patternIndex].Block;
        public ContestChart Chart { get; private set; }
        public float BrainScale { get; private set; } = InitialBrainScale;
        public RunSnapshot Result { get; private set; }
        public RunSnapshot CurrentSnapshot => run == null ? Result : run.Snapshot();
        public bool Passed { get; private set; }
        public int SetCombo { get; private set; }
        public long PatternDurationUs => Chart.Stages[0].DurationUs - ChartValidator.StageBoundaryExclusionUs;

        public void BeginDemonstration()
        {
            Require(TutorialPhase.PatternReady);
            Phase = TutorialPhase.Demonstration;
        }

        public void BeginCountdown()
        {
            Require(TutorialPhase.Demonstration);
            Phase = TutorialPhase.Countdown;
        }

        public void BeginPerformance()
        {
            Require(TutorialPhase.Countdown);
            acceptedTapNotes.Clear();
            lastAcceptedInputUs = long.MinValue;
            run = new ContestRun(Chart);
            if (!run.IsActive) throw new InvalidOperationException("Tutorial chart is invalid.");
            Phase = TutorialPhase.PlayerPerformance;
        }

        public void Press(Hand hand, int pointerId, long atUs, IList<JudgementEvent> output)
        {
            if (Phase != TutorialPhase.PlayerPerformance || !run.IsActive || atUs < lastAcceptedInputUs) return;
            // Free drumming never penalizes the tutorial. Only the first same-hand
            // input within an unclaimed note's existing Good window reaches the core.
            ChartNote? candidate = null;
            foreach (var note in Chart.Stages[0].Notes)
            {
                if (note.Kind != NoteKind.Tap || note.Hand != hand || acceptedTapNotes.Contains(note.Id)) continue;
                var distance = Math.Abs(atUs - note.TargetUs);
                if (distance > ContestRun.GoodWindowUs) continue;
                if (candidate == null || distance < Math.Abs(atUs - candidate.Value.TargetUs)) candidate = note;
            }
            if (candidate == null) return;
            run.ProcessPress(hand, pointerId, atUs, output);
            acceptedTapNotes.Add(candidate.Value.Id);
            lastAcceptedInputUs = atUs;
        }

        public void Release(int pointerId, long atUs, IList<JudgementEvent> output)
        {
            if (Phase == TutorialPhase.PlayerPerformance) run.ProcessRelease(pointerId, atUs, output);
        }

        public void Cancel(int pointerId, long atUs, IList<JudgementEvent> output)
        {
            if (Phase == TutorialPhase.PlayerPerformance) run.ProcessCancel(pointerId, atUs, output);
        }

        public void Advance(long atUs, IList<JudgementEvent> output)
        {
            if (Phase == TutorialPhase.PlayerPerformance) run.AdvanceTime(atUs, output);
        }

        public void Finish(IList<JudgementEvent> output)
        {
            Require(TutorialPhase.PlayerPerformance);
            run.FinalizeCurrentStage(output);
            Result = run.Snapshot();
            Passed = Result.EndReason == RunEndReason.Completed && Result.AccuracyBasisPoints >= passAccuracyBasisPoints;
            SetCombo = Passed ? SetCombo + 1 : 0;
            BrainScale = Clamp(BrainScale + (Passed ? 0.05f : -0.05f));
            Phase = TutorialPhase.Result;
        }

        public void BeginTransition()
        {
            Require(TutorialPhase.Result);
            Phase = TutorialPhase.Transition;
        }

        public bool NextPattern()
        {
            Require(TutorialPhase.Transition);
            if (IsLastPattern) return false;
            PreparePattern(patternIndex + 1);
            return true;
        }

        public void Replay()
        {
            Require(TutorialPhase.Transition);
            BrainScale = InitialBrainScale;
            SetCombo = 0;
            PreparePattern(0);
        }

        public void AbortAndRetryCurrentPattern()
        {
            PreparePattern(patternIndex);
        }

        private void PreparePattern(int index)
        {
            patternIndex = index;
            Chart = PatternCompiler.BuildSingleBarChart("tutorial-" + patterns[index].Id, patterns[index].Block, 80,
                passAccuracyBasisPoints);
            if (!ChartValidator.Validate(Chart).IsValid) throw new InvalidOperationException("Tutorial chart is invalid.");
            run = null;
            acceptedTapNotes.Clear();
            lastAcceptedInputUs = long.MinValue;
            Result = default;
            Passed = false;
            Phase = TutorialPhase.PatternReady;
        }

        private static ListenAndRepeatPatterns.Entry Find(string id)
        {
            foreach (var entry in ListenAndRepeatPatterns.All)
                if (entry.Id == id) return entry;
            throw new InvalidOperationException("Missing tutorial pattern " + id);
        }

        private static ListenAndRepeatPatterns.Entry TapPattern(string id, string description, int[] ticks, Hand[] hands)
        {
            var notes = new PatternNote[ticks.Length];
            for (var i = 0; i < ticks.Length; i++) notes[i] = new PatternNote(ticks[i], hands[i], NoteKind.Tap);
            return new ListenAndRepeatPatterns.Entry(id, 2, description, new PatternBlock(id, notes));
        }

        private static float Clamp(float value) => Math.Max(MinimumBrainScale, Math.Min(MaximumBrainScale, value));

        private void Require(TutorialPhase expected)
        {
            if (Phase != expected) throw new InvalidOperationException("Tutorial transition from " + Phase + " requires " + expected);
        }
    }
}
