using System;
using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    public enum Hand { Left, Right }
    public enum NoteKind { Tap, Hold }
    public enum Judgement { Perfect, Good, Miss }
    public enum RunEndReason { None, Failed, Completed, Invalid }
    public enum JudgementEventKind { Note, GhostMiss, Guidance, UnmatchedRelease, RunInvalid }
    public enum ChartValidationError
    {
        None,
        Empty,
        StageNumber,
        StageRules,
        DuplicateNoteId,
        NoteRules,
        NoteOrder,
        NoteTime,
        HoldEnd,
        OverlappingHold,
        StageBoundary
    }

    public readonly struct PatternNote
    {
        public readonly int StartTick;
        public readonly Hand Hand;
        public readonly NoteKind Kind;
        public readonly int EndTick;

        public PatternNote(int startTick, Hand hand, NoteKind kind, int endTick = 0)
        {
            StartTick = startTick;
            Hand = hand;
            Kind = kind;
            EndTick = endTick;
        }
    }

    public sealed class PatternBlock
    {
        public const int TicksPerBeat = 480;
        public const int TicksPerBlock = TicksPerBeat * 4;

        public string Id { get; }
        public IReadOnlyList<PatternNote> Notes { get; }

        public PatternBlock(string id, params PatternNote[] notes)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Notes = notes ?? throw new ArgumentNullException(nameof(notes));
        }
    }

    public readonly struct ChartNote
    {
        public readonly int Id;
        public readonly int StageNumber;
        public readonly long TargetUs;
        public readonly Hand Hand;
        public readonly NoteKind Kind;
        public readonly long EndUs;

        public ChartNote(int id, int stageNumber, long targetUs, Hand hand, NoteKind kind, long endUs = 0)
        {
            Id = id;
            StageNumber = stageNumber;
            TargetUs = targetUs;
            Hand = hand;
            Kind = kind;
            EndUs = endUs;
        }
    }

    public sealed class StageDefinition
    {
        public int Number { get; }
        public int Bpm { get; }
        public int PassAccuracyBasisPoints { get; }
        public long DurationUs { get; }
        public IReadOnlyList<ChartNote> Notes { get; }

        public StageDefinition(int number, int bpm, int passAccuracyBasisPoints, long durationUs, IReadOnlyList<ChartNote> notes)
        {
            Number = number;
            Bpm = bpm;
            PassAccuracyBasisPoints = passAccuracyBasisPoints;
            DurationUs = durationUs;
            Notes = notes ?? throw new ArgumentNullException(nameof(notes));
        }
    }

    public sealed class ContestChart
    {
        public string ChartId { get; }
        public int RulesetVersion { get; }
        public IReadOnlyList<StageDefinition> Stages { get; }

        public ContestChart(string chartId, int rulesetVersion, IReadOnlyList<StageDefinition> stages)
        {
            ChartId = chartId ?? throw new ArgumentNullException(nameof(chartId));
            RulesetVersion = rulesetVersion;
            Stages = stages ?? throw new ArgumentNullException(nameof(stages));
        }
    }

    public readonly struct ChartValidationResult
    {
        public readonly ChartValidationError Error;
        public readonly int StageNumber;

        public ChartValidationResult(ChartValidationError error, int stageNumber = 0)
        {
            Error = error;
            StageNumber = stageNumber;
        }

        public bool IsValid => Error == ChartValidationError.None;
        public static ChartValidationResult Valid => new ChartValidationResult(ChartValidationError.None);
    }

    public readonly struct JudgementEvent
    {
        public readonly JudgementEventKind Kind;
        public readonly int NoteId;
        public readonly Hand Hand;
        public readonly Judgement Judgement;
        public readonly int Combo;
        public readonly int ScoreDelta;

        public JudgementEvent(JudgementEventKind kind, int noteId, Hand hand, Judgement judgement, int combo, int scoreDelta)
        {
            Kind = kind;
            NoteId = noteId;
            Hand = hand;
            Judgement = judgement;
            Combo = combo;
            ScoreDelta = scoreDelta;
        }
    }

    public readonly struct RunSnapshot
    {
        public readonly RunEndReason EndReason;
        public readonly int LastCompletedStage;
        public readonly int FailStage;
        public readonly int BaseScore;
        public readonly int AccuracyScore;
        public readonly int RankScore;
        public readonly int AccuracyBasisPoints;
        public readonly int Combo;
        public readonly int GhostMissCount;

        public RunSnapshot(RunEndReason endReason, int lastCompletedStage, int failStage, int baseScore, int accuracyScore, int rankScore, int accuracyBasisPoints, int combo, int ghostMissCount)
        {
            EndReason = endReason;
            LastCompletedStage = lastCompletedStage;
            FailStage = failStage;
            BaseScore = baseScore;
            AccuracyScore = accuracyScore;
            RankScore = rankScore;
            AccuracyBasisPoints = accuracyBasisPoints;
            Combo = combo;
            GhostMissCount = ghostMissCount;
        }
    }
}
