using System.Collections.Generic;

namespace Mallang.RR02
{
    public static class BrainScore
    {
        // 0~199→0(콩알) 200~399→1(자람) 400~699→2(점화) 700+→3(치솟음)
        public static int Stage(int score)
        {
            if (score < 200) return 0;
            if (score < 400) return 1;
            if (score < 700) return 2;
            return 3;
        }

        // eligible일 때만 max 병합, 아니면 before 그대로.
        public static IReadOnlyDictionary<Rr02Friend, int> MergeBest(
            IReadOnlyDictionary<Rr02Friend, int> before,
            IReadOnlyDictionary<Rr02Friend, int> run,
            bool eligible)
        {
            var keys = new List<Rr02Friend>();
            foreach (var pair in before) keys.Add(pair.Key);
            foreach (var pair in run)
            {
                if (!before.ContainsKey(pair.Key)) keys.Add(pair.Key);
            }

            var after = new Dictionary<Rr02Friend, int>();
            foreach (var key in keys)
            {
                int best = before.TryGetValue(key, out var b) ? b : 0;
                if (!eligible)
                {
                    after[key] = best;
                    continue;
                }
                int current = run.TryGetValue(key, out var r) ? r : 0;
                after[key] = best > current ? best : current;
            }
            return after;
        }

        // 네 동물 최고 합(한 도전 점수와 다름).
        public static int MyBrainTotal(IReadOnlyDictionary<Rr02Friend, int> bestAfter)
        {
            int total = 0;
            foreach (var value in bestAfter.Values) total += value;
            return total;
        }

        // "all-clear"(NormalCleared=9), "all-perfect"(AllPerfect), "loop-best"(Loops>prevBestLoops)
        public static IReadOnlyList<string> Badges(Rr02RunResult result, int prevBestLoops)
        {
            var badges = new List<string>();
            // FR-015/FR-016: 자격 없는 도전(연습 등)은 배지를 주지 않습니다.
            if (!result.OfficialEligible) return badges;

            if (result.NormalCleared == 9) badges.Add("all-clear");
            if (result.AllPerfect) badges.Add("all-perfect");
            if (result.Loops > prevBestLoops) badges.Add("loop-best");
            return badges;
        }
    }
}
