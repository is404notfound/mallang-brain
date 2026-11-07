using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public sealed class Rr02RunResult
    {
        public int NormalCleared;
        public int Loops;
        public bool OfficialEligible;
        public IReadOnlyDictionary<Rr02Friend, int> AnimalScores;
        public int TotalScore;
        public bool AllPerfect;
    }

    public sealed class Rr02Run
    {
        public Rr02Run(Rr02Chart chart) => throw new NotImplementedException();

        public Rr02Phase Phase => throw new NotImplementedException();
        public int CurrentRound => throw new NotImplementedException();
        public int NormalCleared => throw new NotImplementedException();
        public int Loops => throw new NotImplementedException();
        public bool OfficialEligible => throw new NotImplementedException();

        // 듣기→따라치기 전환. 이후 시간 인자는 이 구절의 따라치기 시작 기준 µs.
        public void BeginRepeat() => throw new NotImplementedException();

        public Rr02InputResult ProcessPress(Rr02Friend friend, long usFromRepeatStart) => throw new NotImplementedException();

        // 구절 종료 확정 포함(마지막 목표 +180ms에 1회).
        public void AdvanceTime(long usFromRepeatStart) => throw new NotImplementedException();

        // 공식 자격 상실(연습 전환). 완료 단계 유지, 같은 라운드 듣기부터 재개(MCH-007).
        public void InvalidateOfficial(Rr02PracticeReason reason) => throw new NotImplementedException();

        public IReadOnlyDictionary<Rr02Friend, int> RunAnimalScores => throw new NotImplementedException();
        public Rr02RunResult Result => throw new NotImplementedException();
    }
}
