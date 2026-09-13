using System;
using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    public sealed class ContestRun
    {
        public const long PerfectWindowUs = 60000;
        public const long GoodWindowUs = 120000;
        public const long HoldLateGoodWindowUs = 180000;
        public const int GhostMissPenalty = 30;

        private enum NoteState { Unseen, Holding, Closed }

        private sealed class NoteProgress
        {
            public readonly ChartNote Note;
            public NoteState State;
            public int PointerId;
            public int StartScore;

            public NoteProgress(ChartNote note)
            {
                Note = note;
                PointerId = int.MinValue;
            }
        }

        private readonly ContestChart chart;
        private readonly ChartValidationResult validation;
        private NoteProgress[] notes;
        private int stageIndex;
        private long lastTimestampUs = long.MinValue;
        private int baseScore;
        private int comboAdjustedScore;
        private int combo;
        private int ghostMissCount;
        private int stageBaseScore;
        private int stageGhostMissCount;
        private int attemptedNoteMaxScore;
        private bool currentStageCountedForAccuracy;
        private int lastCompletedStage;
        private int failStage;
        private RunEndReason endReason;

        public ContestRun(ContestChart chart)
        {
            this.chart = chart ?? throw new ArgumentNullException(nameof(chart));
            validation = ChartValidator.Validate(chart);
            if (!validation.IsValid)
            {
                endReason = RunEndReason.Invalid;
                failStage = validation.StageNumber;
                return;
            }
            LoadStage(0);
        }

        public bool IsActive => endReason == RunEndReason.None;
        public int CurrentStageNumber => IsActive ? chart.Stages[stageIndex].Number : 0;
        public ChartValidationResult Validation => validation;

        public void ProcessPress(Hand hand, int pointerId, long inputUs, IList<JudgementEvent> output = null)
        {
            if (!EnsureTimestamp(inputUs, output)) return;
            CountCurrentStageForAccuracy();
            AdvanceTo(inputUs, output);
            if (!IsActive) return;

            var matching = FindNearestUnseen(hand, inputUs);
            if (matching != null)
            {
                if (matching.Note.Kind == NoteKind.Tap)
                {
                    var judgement = JudgeTap(inputUs - matching.Note.TargetUs);
                    matching.State = NoteState.Closed;
                    ApplySimpleNote(matching.Note, judgement, output);
                }
                else
                {
                    var judgement = JudgeTap(inputUs - matching.Note.TargetUs);
                    if (judgement == Judgement.Miss)
                    {
                        matching.State = NoteState.Closed;
                        BreakCombo();
                        Add(output, JudgementEventKind.Note, matching.Note, judgement, 0);
                    }
                    else
                    {
                        matching.State = NoteState.Holding;
                        matching.PointerId = pointerId;
                        matching.StartScore = ScoreForTap(judgement) / 2;
                        baseScore += matching.StartScore;
                        stageBaseScore += matching.StartScore;
                        Add(output, JudgementEventKind.Note, matching.Note, judgement, 0);
                    }
                }
                return;
            }

            var wrongHand = FindNearestUnseen(Opposite(hand), inputUs);
            if (wrongHand != null)
            {
                if (CurrentStageNumber == 1)
                {
                    Add(output, JudgementEventKind.Guidance, wrongHand.Note, Judgement.Miss, 0);
                }
                else
                {
                    wrongHand.State = NoteState.Closed;
                    BreakCombo();
                    Add(output, JudgementEventKind.Note, wrongHand.Note, Judgement.Miss, 0);
                }
                return;
            }

            RegisterGhostMiss(hand, output);
        }

        public void ProcessRelease(int pointerId, long inputUs, IList<JudgementEvent> output = null)
        {
            if (!EnsureTimestamp(inputUs, output)) return;
            CountCurrentStageForAccuracy();
            AdvanceTo(inputUs, output);
            if (!IsActive) return;

            var hold = FindHolding(pointerId);
            if (hold == null)
            {
                Add(output, JudgementEventKind.UnmatchedRelease, -1, Hand.Left, Judgement.Miss, 0);
                return;
            }

            var judgement = JudgeHoldRelease(inputUs - hold.Note.EndUs);
            var endScore = ScoreForHoldEndpoint(judgement);
            baseScore += endScore;
            stageBaseScore += endScore;
            hold.State = NoteState.Closed;
            if (judgement == Judgement.Miss)
            {
                comboAdjustedScore += hold.StartScore;
                BreakCombo();
                Add(output, JudgementEventKind.Note, hold.Note, judgement, hold.StartScore);
                return;
            }

            CommitSuccessfulNote(hold.Note, hold.StartScore + endScore, judgement, output);
        }

        public void ProcessCancel(int pointerId, long inputUs, IList<JudgementEvent> output = null)
        {
            if (!EnsureTimestamp(inputUs, output)) return;
            CountCurrentStageForAccuracy();
            AdvanceTo(inputUs, output);
            if (!IsActive) return;

            var hold = FindHolding(pointerId);
            if (hold == null)
            {
                Add(output, JudgementEventKind.UnmatchedRelease, -1, Hand.Left, Judgement.Miss, 0);
                return;
            }

            CloseFailedHold(hold, output);
        }

        public void FinalizeCurrentStage(IList<JudgementEvent> output = null)
        {
            if (!IsActive) return;
            CountCurrentStageForAccuracy();
            var stage = chart.Stages[stageIndex];
            AdvanceTo(stage.DurationUs + HoldLateGoodWindowUs + 1, output);
            if (!IsActive) return;

            var noteMaxScore = stage.Notes.Count * 100;
            var stageAccuracyScore = Math.Max(0, stageBaseScore - stageGhostMissCount * GhostMissPenalty);
            var stageAccuracy = (int)RhythmMath.RoundHalfAwayFromZero((long)stageAccuracyScore * 10000, noteMaxScore);
            if (stageAccuracy < stage.PassAccuracyBasisPoints)
            {
                endReason = RunEndReason.Failed;
                failStage = stage.Number;
                return;
            }

            lastCompletedStage = stage.Number;
            if (stageIndex == chart.Stages.Count - 1)
            {
                endReason = RunEndReason.Completed;
                failStage = 21;
                return;
            }

            LoadStage(stageIndex + 1);
        }

        public RunSnapshot Snapshot()
        {
            var accuracyScore = Math.Max(0, baseScore - ghostMissCount * GhostMissPenalty);
            var accuracy = attemptedNoteMaxScore == 0 ? 0 : (int)RhythmMath.RoundHalfAwayFromZero((long)accuracyScore * 10000, attemptedNoteMaxScore);
            return new RunSnapshot(endReason, lastCompletedStage, failStage, baseScore, accuracyScore, Math.Max(0, comboAdjustedScore - ghostMissCount * GhostMissPenalty), accuracy, combo, ghostMissCount);
        }

        private void LoadStage(int nextStageIndex)
        {
            stageIndex = nextStageIndex;
            var stage = chart.Stages[stageIndex];
            notes = new NoteProgress[stage.Notes.Count];
            for (var index = 0; index < notes.Length; index++) notes[index] = new NoteProgress(stage.Notes[index]);
            lastTimestampUs = long.MinValue;
            stageBaseScore = 0;
            stageGhostMissCount = 0;
            currentStageCountedForAccuracy = false;
        }

        private void CountCurrentStageForAccuracy()
        {
            if (currentStageCountedForAccuracy) return;
            attemptedNoteMaxScore += chart.Stages[stageIndex].Notes.Count * 100;
            currentStageCountedForAccuracy = true;
        }

        private bool EnsureTimestamp(long timestampUs, IList<JudgementEvent> output)
        {
            if (!IsActive) return false;
            if (timestampUs < lastTimestampUs)
            {
                endReason = RunEndReason.Invalid;
                failStage = CurrentStageNumber;
                Add(output, JudgementEventKind.RunInvalid, -1, Hand.Left, Judgement.Miss, 0);
                return false;
            }
            lastTimestampUs = timestampUs;
            return true;
        }

        private void AdvanceTo(long timeUs, IList<JudgementEvent> output)
        {
            foreach (var progress in notes)
            {
                if (progress.State == NoteState.Unseen && timeUs > progress.Note.TargetUs + GoodWindowUs)
                {
                    progress.State = NoteState.Closed;
                    BreakCombo();
                    Add(output, JudgementEventKind.Note, progress.Note, Judgement.Miss, 0);
                }
                else if (progress.State == NoteState.Holding && timeUs > progress.Note.EndUs + HoldLateGoodWindowUs)
                {
                    CloseFailedHold(progress, output);
                }
            }
        }

        private NoteProgress FindNearestUnseen(Hand hand, long inputUs)
        {
            NoteProgress selected = null;
            foreach (var progress in notes)
            {
                if (progress.State != NoteState.Unseen || progress.Note.Hand != hand) continue;
                var offset = inputUs - progress.Note.TargetUs;
                if (offset < -GoodWindowUs || offset > GoodWindowUs) continue;
                if (selected == null || IsBetterCandidate(progress.Note, selected.Note, inputUs)) selected = progress;
            }
            return selected;
        }

        private static bool IsBetterCandidate(ChartNote candidate, ChartNote current, long inputUs)
        {
            var candidateOffset = Math.Abs(inputUs - candidate.TargetUs);
            var currentOffset = Math.Abs(inputUs - current.TargetUs);
            if (candidateOffset != currentOffset) return candidateOffset < currentOffset;
            if (candidate.TargetUs != current.TargetUs) return candidate.TargetUs < current.TargetUs;
            return candidate.Id < current.Id;
        }

        private NoteProgress FindHolding(int pointerId)
        {
            foreach (var progress in notes)
                if (progress.State == NoteState.Holding && progress.PointerId == pointerId) return progress;
            return null;
        }

        private void ApplySimpleNote(ChartNote note, Judgement judgement, IList<JudgementEvent> output)
        {
            if (judgement == Judgement.Miss)
            {
                BreakCombo();
                Add(output, JudgementEventKind.Note, note, judgement, 0);
                return;
            }
            CommitSuccessfulNote(note, ScoreForTap(judgement), judgement, output);
        }

        private void CommitSuccessfulNote(ChartNote note, int baseNoteScore, Judgement judgement, IList<JudgementEvent> output)
        {
            baseScore += note.Kind == NoteKind.Tap ? baseNoteScore : 0;
            stageBaseScore += note.Kind == NoteKind.Tap ? baseNoteScore : 0;
            combo++;
            var scoreDelta = RhythmMath.ScaleScore(baseNoteScore, ComboMultiplierNumerator(combo));
            comboAdjustedScore += scoreDelta;
            Add(output, JudgementEventKind.Note, note, judgement, scoreDelta);
        }

        private void CloseFailedHold(NoteProgress hold, IList<JudgementEvent> output)
        {
            hold.State = NoteState.Closed;
            comboAdjustedScore += hold.StartScore;
            BreakCombo();
            Add(output, JudgementEventKind.Note, hold.Note, Judgement.Miss, hold.StartScore);
        }

        private void RegisterGhostMiss(Hand hand, IList<JudgementEvent> output)
        {
            ghostMissCount++;
            stageGhostMissCount++;
            BreakCombo();
            Add(output, JudgementEventKind.GhostMiss, -1, hand, Judgement.Miss, 0);
        }

        private void BreakCombo() => combo = 0;

        private static int ComboMultiplierNumerator(int currentCombo)
        {
            if (currentCombo >= 40) return 150;
            if (currentCombo >= 20) return 125;
            if (currentCombo >= 10) return 110;
            return 100;
        }

        private static Judgement JudgeTap(long offsetUs)
        {
            var absolute = Math.Abs(offsetUs);
            return absolute <= PerfectWindowUs ? Judgement.Perfect : absolute <= GoodWindowUs ? Judgement.Good : Judgement.Miss;
        }

        private static Judgement JudgeHoldRelease(long offsetUs)
        {
            if (offsetUs >= -PerfectWindowUs && offsetUs <= PerfectWindowUs) return Judgement.Perfect;
            if ((offsetUs > -GoodWindowUs && offsetUs < -PerfectWindowUs) || (offsetUs > PerfectWindowUs && offsetUs <= HoldLateGoodWindowUs)) return Judgement.Good;
            return Judgement.Miss;
        }

        private static int ScoreForTap(Judgement judgement) => judgement == Judgement.Perfect ? 100 : judgement == Judgement.Good ? 70 : 0;
        private static int ScoreForHoldEndpoint(Judgement judgement) => judgement == Judgement.Perfect ? 50 : judgement == Judgement.Good ? 35 : 0;
        private static Hand Opposite(Hand hand) => hand == Hand.Left ? Hand.Right : Hand.Left;

        private void Add(IList<JudgementEvent> output, JudgementEventKind kind, ChartNote note, Judgement judgement, int scoreDelta)
        {
            Add(output, kind, note.Id, note.Hand, judgement, scoreDelta);
        }

        private void Add(IList<JudgementEvent> output, JudgementEventKind kind, int noteId, Hand hand, Judgement judgement, int scoreDelta)
        {
            if (output == null) return;
            output.Add(new JudgementEvent(kind, noteId, hand, judgement, combo, scoreDelta));
        }
    }
}
