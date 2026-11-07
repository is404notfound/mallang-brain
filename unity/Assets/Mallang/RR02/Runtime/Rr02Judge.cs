using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public sealed class Rr02Judge
    {
        public Rr02Judge(IReadOnlyList<Rr02Note> notes) => throw new NotImplementedException();

        // 호출 순서 = sequence_id 순. 같은 친구의 열린 목표 중 |offset| 최소 → 이른 목표 → 낮은 note_id.
        public Rr02InputResult ProcessPress(Rr02Friend friend, long inputUs) => throw new NotImplementedException();

        // 목표 시각 +120ms(GoodWindowUs)를 지난 노트를 Miss로 닫는다.
        public void AdvanceTime(long nowUs) => throw new NotImplementedException();

        public IReadOnlyList<Rr02Judgement> Outcomes => throw new NotImplementedException();
        public int GhostCount => throw new NotImplementedException();

        // 마지막 목표 +180ms(OwnershipPadUs) 경과 여부.
        public bool Completed(long nowUs) => throw new NotImplementedException();
    }
}
