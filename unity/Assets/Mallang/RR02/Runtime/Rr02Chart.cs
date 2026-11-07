using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public sealed class Rr02Round
    {
        public int Number;
        public IReadOnlyList<Rr02Note> Notes;
        public IReadOnlyList<int> HiddenIndices;
    }

    public sealed class Rr02Chart
    {
        public IReadOnlyList<Rr02Round> Rounds;

        // four-friend-chart.json(패키지 원본 그대로)을 파싱한다. 형식이 어긋나면 조용한 기본값 대신 예외.
        public static Rr02Chart LoadFromJson(string json) => throw new NotImplementedException();

        // k=1→R2 … k=8→R9, k=9→다시 R2 순환. HiddenIndices는 0~7 전부. R1 두 소리 패턴은 쓰지 않는다.
        public Rr02Round InfinitePhrase(int k) => throw new NotImplementedException();
    }

    // 구절 시작 절대시각의 단일 진실(stage-review 결정 3). 장면 코드는 이 계산기만 사용한다 (필수).
    public static class Rr02Schedule
    {
        public const long CountInUs = 4800000; // 12박

        public static long ListenStartUs(int round) => throw new NotImplementedException();
        public static long RepeatStartUs(int round) => throw new NotImplementedException();
        public static long RoundEndUs(int round) => throw new NotImplementedException();
    }
}
