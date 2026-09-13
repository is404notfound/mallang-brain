using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    public static class ChartValidator
    {
        public const long StageBoundaryExclusionUs = 180000;

        public static ChartValidationResult Validate(ContestChart chart)
        {
            if (chart == null || chart.Stages == null || chart.Stages.Count == 0)
                return new ChartValidationResult(ChartValidationError.Empty);

            var noteIds = new HashSet<int>();
            for (var stageIndex = 0; stageIndex < chart.Stages.Count; stageIndex++)
            {
                var stage = chart.Stages[stageIndex];
                if (stage == null || stage.Number != stageIndex + 1)
                    return new ChartValidationResult(ChartValidationError.StageNumber, stageIndex + 1);
                if (stage.Bpm <= 0 || stage.DurationUs <= StageBoundaryExclusionUs || stage.PassAccuracyBasisPoints < 0 || stage.PassAccuracyBasisPoints > 10000)
                    return new ChartValidationResult(ChartValidationError.StageRules, stage.Number);
                if (stage.Notes == null || stage.Notes.Count == 0)
                    return new ChartValidationResult(ChartValidationError.Empty, stage.Number);

                long previousTargetUs = long.MinValue;
                var leftHoldEndUs = long.MinValue;
                var rightHoldEndUs = long.MinValue;
                foreach (var note in stage.Notes)
                {
                    if (note.Id <= 0 || !noteIds.Add(note.Id))
                        return new ChartValidationResult(ChartValidationError.DuplicateNoteId, stage.Number);
                    if (note.StageNumber != stage.Number || (note.Hand != Hand.Left && note.Hand != Hand.Right) || (note.Kind != NoteKind.Tap && note.Kind != NoteKind.Hold))
                        return new ChartValidationResult(ChartValidationError.NoteRules, stage.Number);
                    if (note.TargetUs < previousTargetUs)
                        return new ChartValidationResult(ChartValidationError.NoteOrder, stage.Number);
                    previousTargetUs = note.TargetUs;
                    if (note.TargetUs < 0 || note.TargetUs > stage.DurationUs - StageBoundaryExclusionUs)
                        return new ChartValidationResult(ChartValidationError.StageBoundary, stage.Number);
                    if (note.Kind != NoteKind.Hold) continue;
                    if (note.EndUs <= note.TargetUs || note.EndUs > stage.DurationUs - StageBoundaryExclusionUs)
                        return new ChartValidationResult(ChartValidationError.HoldEnd, stage.Number);

                    if (note.Hand == Hand.Left)
                    {
                        if (note.TargetUs < leftHoldEndUs)
                            return new ChartValidationResult(ChartValidationError.OverlappingHold, stage.Number);
                        leftHoldEndUs = note.EndUs;
                    }
                    else
                    {
                        if (note.TargetUs < rightHoldEndUs)
                            return new ChartValidationResult(ChartValidationError.OverlappingHold, stage.Number);
                        rightHoldEndUs = note.EndUs;
                    }
                }
            }

            return ChartValidationResult.Valid;
        }
    }
}
