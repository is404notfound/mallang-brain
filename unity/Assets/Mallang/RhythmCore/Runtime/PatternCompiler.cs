using System;
using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    // Compiles a single hand-authored PatternBlock (one 4/4 bar, tick-based) into a one-stage ContestChart.
    // Additive alongside Gate1ChartFactory: reuses the same RhythmMath rounding rule and ChartValidator
    // boundary policy, but never edits either, so the 20-stage marimba contest is untouched.
    public static class PatternCompiler
    {
        public static ContestChart BuildSingleBarChart(string chartId, PatternBlock pattern, int bpm, int passAccuracyBasisPoints = 0)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            var notes = new List<ChartNote>(pattern.Notes.Count);
            var nextNoteId = 1;
            foreach (var note in pattern.Notes)
            {
                notes.Add(new ChartNote(
                    nextNoteId++,
                    1,
                    TickToUs(note.StartTick, bpm),
                    note.Hand,
                    note.Kind,
                    note.Kind == NoteKind.Hold ? TickToUs(note.EndTick, bpm) : 0));
            }

            // ChartValidator requires non-decreasing TargetUs; sort defensively even though every
            // ListenAndRepeatPatterns entry is already authored in order (same rule Gate1ChartFactory follows).
            notes.Sort((left, right) => left.TargetUs != right.TargetUs ? left.TargetUs.CompareTo(right.TargetUs) : left.Id.CompareTo(right.Id));

            // Same release-guard tail as Gate1ChartFactory: lets a bar-final hold end safely before finalize.
            var durationUs = TickToUs(PatternBlock.TicksPerBlock, bpm) + ChartValidator.StageBoundaryExclusionUs;
            var stage = new StageDefinition(1, bpm, passAccuracyBasisPoints, durationUs, notes);
            return new ContestChart(chartId, 1, new[] { stage });
        }

        public static long TickToUs(long tick, int bpm)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            return RhythmMath.RoundHalfAwayFromZero(tick * 60000000L, (long)bpm * PatternBlock.TicksPerBeat);
        }
    }
}
