using System;
using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    public static class RhythmMath
    {
        public static long RoundHalfAwayFromZero(long numerator, long denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            var sign = numerator < 0 ? -1L : 1L;
            var magnitude = numerator < 0 ? -numerator : numerator;
            return sign * ((magnitude + denominator / 2) / denominator);
        }

        public static int ScaleScore(int score, int numerator)
        {
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            return (int)RoundHalfAwayFromZero((long)score * numerator, 100);
        }
    }

    public static class Gate1ChartFactory
    {
        private static readonly IReadOnlyDictionary<string, PatternBlock> Patterns = CreatePatterns();

        private readonly struct StageRecipe
        {
            public readonly int Bpm;
            public readonly int PassBasisPoints;
            public readonly string[] Blocks;

            public StageRecipe(int bpm, int passBasisPoints, params string[] blocks)
            {
                Bpm = bpm;
                PassBasisPoints = passBasisPoints;
                Blocks = blocks;
            }
        }

        private static readonly StageRecipe[] Recipes =
        {
            new StageRecipe(80, 5000, "P01", "P02", "P01", "P02"),
            new StageRecipe(80, 5000, "P03", "P01", "P03", "P02"),
            new StageRecipe(90, 6000, "P03", "P11", "P03", "P11"),
            new StageRecipe(90, 6000, "P05", "P01", "P05", "P02"),
            new StageRecipe(100, 7000, "P06", "P03", "P06", "P11"),
            new StageRecipe(100, 7000, "P04", "P02", "P04", "P03"),
            new StageRecipe(110, 7000, "P07", "P01", "P07", "P03"),
            new StageRecipe(110, 8000, "P08", "P11", "P08", "P03"),
            new StageRecipe(120, 8000, "P09", "P02", "P09", "P06"),
            new StageRecipe(120, 8000, "P10", "P11", "P10", "P03"),
            new StageRecipe(130, 8500, "P06", "P10", "P06", "P11"),
            new StageRecipe(130, 8500, "P08", "P05", "P08", "P11"),
            new StageRecipe(140, 8500, "P12", "P03", "P09", "P11"),
            new StageRecipe(140, 8500, "P10", "P06", "P12", "P01"),
            new StageRecipe(140, 8500, "P08", "P04", "P12", "P11"),
            new StageRecipe(140, 8500, "P09", "P10", "P12", "P03"),
            new StageRecipe(140, 8500, "P12", "P05", "P08", "P11"),
            new StageRecipe(140, 8500, "P10", "P12", "P06", "P01"),
            new StageRecipe(140, 8500, "P08", "P12", "P09", "P11"),
            new StageRecipe(140, 8500, "P12", "P10", "P08", "P06")
        };

        public static ContestChart Create()
        {
            var stages = new List<StageDefinition>(Recipes.Length);
            var nextNoteId = 1;
            for (var stageIndex = 0; stageIndex < Recipes.Length; stageIndex++)
            {
                var recipe = Recipes[stageIndex];
                var notes = new List<ChartNote>();
                for (var blockIndex = 0; blockIndex < recipe.Blocks.Length; blockIndex++)
                {
                    var pattern = Patterns[recipe.Blocks[blockIndex]];
                    foreach (var note in pattern.Notes)
                    {
                        var startTick = blockIndex * PatternBlock.TicksPerBlock + note.StartTick;
                        var endTick = note.Kind == NoteKind.Hold ? blockIndex * PatternBlock.TicksPerBlock + note.EndTick : 0;
                        notes.Add(new ChartNote(
                            nextNoteId++,
                            stageIndex + 1,
                            TickToUs(startTick, recipe.Bpm),
                            note.Hand,
                            note.Kind,
                            note.Kind == NoteKind.Hold ? TickToUs(endTick, recipe.Bpm) : 0));
                    }
                }

                notes.Sort((left, right) => left.TargetUs != right.TargetUs ? left.TargetUs.CompareTo(right.TargetUs) : left.Id.CompareTo(right.Id));
                // The playable 16 beats are followed by the validator's 180 ms release guard.
                // This lets a final-block hold end safely before the stage is finalized.
                var durationUs = TickToUs(PatternBlock.TicksPerBlock * 4, recipe.Bpm) + ChartValidator.StageBoundaryExclusionUs;
                stages.Add(new StageDefinition(stageIndex + 1, recipe.Bpm, recipe.PassBasisPoints, durationUs, notes));
            }

            return new ContestChart("gate1-marimba-v1", 1, stages);
        }

        public static long TickToUs(long tick, int bpm)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            return RhythmMath.RoundHalfAwayFromZero(tick * 60000000L, (long)bpm * PatternBlock.TicksPerBeat);
        }

        private static IReadOnlyDictionary<string, PatternBlock> CreatePatterns()
        {
            return new Dictionary<string, PatternBlock>
            {
                ["P01"] = new PatternBlock("P01", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(480, Hand.Left, NoteKind.Tap), new PatternNote(960, Hand.Left, NoteKind.Tap), new PatternNote(1440, Hand.Left, NoteKind.Tap)),
                ["P02"] = new PatternBlock("P02", new PatternNote(0, Hand.Right, NoteKind.Tap), new PatternNote(480, Hand.Right, NoteKind.Tap), new PatternNote(960, Hand.Right, NoteKind.Tap), new PatternNote(1440, Hand.Right, NoteKind.Tap)),
                ["P03"] = new PatternBlock("P03", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(480, Hand.Right, NoteKind.Tap), new PatternNote(960, Hand.Left, NoteKind.Tap), new PatternNote(1440, Hand.Right, NoteKind.Tap)),
                ["P04"] = new PatternBlock("P04", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(240, Hand.Left, NoteKind.Tap), new PatternNote(960, Hand.Right, NoteKind.Tap), new PatternNote(1200, Hand.Right, NoteKind.Tap)),
                ["P05"] = new PatternBlock("P05", new PatternNote(0, Hand.Left, NoteKind.Hold, 960), new PatternNote(1200, Hand.Right, NoteKind.Tap), new PatternNote(1680, Hand.Right, NoteKind.Tap)),
                ["P06"] = new PatternBlock("P06", new PatternNote(0, Hand.Left, NoteKind.Hold, 720), new PatternNote(960, Hand.Right, NoteKind.Tap), new PatternNote(1440, Hand.Right, NoteKind.Hold, 1800)),
                ["P07"] = new PatternBlock("P07", new PatternNote(240, Hand.Left, NoteKind.Tap), new PatternNote(720, Hand.Right, NoteKind.Tap), new PatternNote(1200, Hand.Left, NoteKind.Tap), new PatternNote(1680, Hand.Right, NoteKind.Tap)),
                ["P08"] = new PatternBlock("P08", new PatternNote(240, Hand.Left, NoteKind.Tap), new PatternNote(480, Hand.Right, NoteKind.Tap), new PatternNote(960, Hand.Left, NoteKind.Tap), new PatternNote(1440, Hand.Right, NoteKind.Tap), new PatternNote(1680, Hand.Left, NoteKind.Tap)),
                ["P09"] = new PatternBlock("P09", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(480, Hand.Right, NoteKind.Hold, 1200), new PatternNote(1440, Hand.Left, NoteKind.Tap)),
                ["P10"] = new PatternBlock("P10", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(240, Hand.Right, NoteKind.Tap), new PatternNote(480, Hand.Left, NoteKind.Tap), new PatternNote(720, Hand.Right, NoteKind.Tap), new PatternNote(1200, Hand.Left, NoteKind.Tap), new PatternNote(1440, Hand.Right, NoteKind.Tap)),
                ["P11"] = new PatternBlock("P11", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(960, Hand.Right, NoteKind.Tap), new PatternNote(1440, Hand.Left, NoteKind.Tap)),
                ["P12"] = new PatternBlock("P12", new PatternNote(0, Hand.Left, NoteKind.Tap), new PatternNote(240, Hand.Right, NoteKind.Tap), new PatternNote(720, Hand.Left, NoteKind.Hold, 1200), new PatternNote(1440, Hand.Right, NoteKind.Tap), new PatternNote(1680, Hand.Left, NoteKind.Tap))
            };
        }
    }
}
