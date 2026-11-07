using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public static class BrainScore
    {
        // 0~199→0(콩알) 200~399→1(자람) 400~699→2(점화) 700+→3(치솟음)
        public static int Stage(int score) => throw new NotImplementedException();

        // eligible일 때만 max 병합, 아니면 before 그대로.
        public static IReadOnlyDictionary<Rr02Friend, int> MergeBest(
            IReadOnlyDictionary<Rr02Friend, int> before,
            IReadOnlyDictionary<Rr02Friend, int> run,
            bool eligible) => throw new NotImplementedException();

        // 네 동물 최고 합(한 도전 점수와 다름).
        public static int MyBrainTotal(IReadOnlyDictionary<Rr02Friend, int> bestAfter) => throw new NotImplementedException();

        // "all-clear"(NormalCleared=9), "all-perfect"(AllPerfect), "loop-best"(Loops>prevBestLoops)
        public static IReadOnlyList<string> Badges(Rr02RunResult result, int prevBestLoops) => throw new NotImplementedException();
    }
}
