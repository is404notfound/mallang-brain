using System.Collections.Generic;

namespace Mallang.RhythmCore
{
    // Hand-authored, one-bar (4/4) rhythm-reproduction pattern vocabulary for the "듣고 따라하기"
    // (listen, then repeat) minigame. Quarter/eighth notes and rests only; no 16th notes or
    // syncopation in this first vocabulary, per design brief. Deliberately not procedurally
    // generated: repetition, small variation and a little surprise read better than pure randomness.
    public static class ListenAndRepeatPatterns
    {
        public sealed class Entry
        {
            public readonly string Id;
            public readonly int Level;
            public readonly string Description;
            public readonly PatternBlock Block;

            public Entry(string id, int level, string description, PatternBlock block)
            {
                Id = id;
                Level = level;
                Description = description;
                Block = block;
            }
        }

        public static IReadOnlyList<Entry> All { get; } = Build();

        private static List<Entry> Build()
        {
            const int q = PatternBlock.TicksPerBeat;      // one quarter note
            const int e = PatternBlock.TicksPerBeat / 2;   // one eighth note

            return new List<Entry>
            {
                // Level 1 - tap only, on the beat.
                new Entry("LR01", 1, "L - R -", new PatternBlock("LR01",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(2 * q, Hand.Right, NoteKind.Tap))),

                new Entry("LR02", 1, "L R L R", new PatternBlock("LR02",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(1 * q, Hand.Right, NoteKind.Tap),
                    new PatternNote(2 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(3 * q, Hand.Right, NoteKind.Tap))),

                new Entry("LR03", 1, "L L R R", new PatternBlock("LR03",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(1 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(2 * q, Hand.Right, NoteKind.Tap),
                    new PatternNote(3 * q, Hand.Right, NoteKind.Tap))),

                // Level 2 - eighth notes and more frequent hand crossing.
                new Entry("LR04", 2, "LL - RR -", new PatternBlock("LR04",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(0 * q + e, Hand.Left, NoteKind.Tap),
                    new PatternNote(2 * q, Hand.Right, NoteKind.Tap),
                    new PatternNote(2 * q + e, Hand.Right, NoteKind.Tap))),

                new Entry("LR05", 2, "- L - R - L - R (offbeat)", new PatternBlock("LR05",
                    new PatternNote(0 * q + e, Hand.Left, NoteKind.Tap),
                    new PatternNote(1 * q + e, Hand.Right, NoteKind.Tap),
                    new PatternNote(2 * q + e, Hand.Left, NoteKind.Tap),
                    new PatternNote(3 * q + e, Hand.Right, NoteKind.Tap))),

                new Entry("LR06", 2, "L R L R (fast, eighths)", new PatternBlock("LR06",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(0 * q + e, Hand.Right, NoteKind.Tap),
                    new PatternNote(1 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(1 * q + e, Hand.Right, NoteKind.Tap))),

                // Level 3 - hold introduced.
                new Entry("LR07", 3, "L====== R R", new PatternBlock("LR07",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Hold, 2 * q),
                    new PatternNote(2 * q + e, Hand.Right, NoteKind.Tap),
                    new PatternNote(3 * q + e, Hand.Right, NoteKind.Tap))),

                new Entry("LR08", 3, "L R====== L", new PatternBlock("LR08",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(1 * q, Hand.Right, NoteKind.Hold, 2 * q + e),
                    new PatternNote(3 * q, Hand.Left, NoteKind.Tap))),

                // Level 4 - hold plus opposite-hand taps during the hold (needs real multi-touch).
                new Entry("LR09", 4, "L================ (R . R while holding)", new PatternBlock("LR09",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Hold, 3 * q + e),
                    new PatternNote(2 * q, Hand.Right, NoteKind.Tap),
                    new PatternNote(3 * q, Hand.Right, NoteKind.Tap))),

                // Level 5 - tap and hold mixed on both hands within one understandable bar.
                new Entry("LR10", 5, "L R L====== R L", new PatternBlock("LR10",
                    new PatternNote(0 * q, Hand.Left, NoteKind.Tap),
                    new PatternNote(0 * q + e, Hand.Right, NoteKind.Tap),
                    new PatternNote(1 * q + e, Hand.Left, NoteKind.Hold, 2 * q + e),
                    new PatternNote(3 * q, Hand.Right, NoteKind.Tap),
                    new PatternNote(3 * q + e, Hand.Left, NoteKind.Tap))),
            };
        }
    }
}
