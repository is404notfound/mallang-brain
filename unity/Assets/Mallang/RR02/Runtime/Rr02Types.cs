using System;

namespace Mallang.RR02
{
    public enum Rr02Friend { Rabbit, Capybara, Fox, Squirrel }
    public enum Rr02Judgement { Perfect, Good, Miss }
    public enum Rr02InputKind { Judged, Ghost, OutsideOwnership }
    public enum Rr02Phase { Listen, Repeat, Ended }
    public enum Rr02PracticeReason { None, Pause, ClockError, SoundFallback, UserAssist }

    public static class Rr02Rules
    {
        public const long PerfectWindowUs = 60000;
        public const long GoodWindowUs = 120000;
        public const long OwnershipPadUs = 180000;
        public const long BeatUs = 400000;
        public const int TicksPerBeat = 480;
        public const int PerfectScore = 100;
        public const int GoodScore = 70;
    }

    public readonly struct Rr02Note
    {
        public readonly int NoteId;
        public readonly Rr02Friend Friend;
        public readonly long TargetUs;

        public Rr02Note(int noteId, Rr02Friend friend, long targetUs)
        {
            NoteId = noteId;
            Friend = friend;
            TargetUs = targetUs;
        }
    }

    public readonly struct Rr02InputResult
    {
        public readonly Rr02InputKind Kind;
        public readonly int NoteId;
        public readonly Rr02Judgement Judgement;

        public Rr02InputResult(Rr02InputKind kind, int noteId, Rr02Judgement judgement)
        {
            Kind = kind;
            NoteId = noteId;
            Judgement = judgement;
        }
    }
}
