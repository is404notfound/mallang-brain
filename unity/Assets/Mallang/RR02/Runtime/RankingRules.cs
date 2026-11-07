using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public readonly struct ReachKey
    {
        public readonly int NormalCleared;
        public readonly int Loops;
        public readonly int RunScore;

        public ReachKey(int normalCleared, int loops, int runScore)
        {
            NormalCleared = normalCleared;
            Loops = loops;
            RunScore = runScore;
        }
    }

    public enum RankingMode { BenchmarkOnly, RankWithBenchmark, Official }

    public sealed class BenchmarkTable
    {
        public string Id;
        public bool Empirical;
        public bool PercentileAvailable;
        public List<BenchmarkGradeRow> Rows;
    }

    // 기준표 구간 한 줄(JSON의 label/minRound/maxRound와 이름·철자를 맞춥니다).
    [Serializable]
    public sealed class BenchmarkGradeRow
    {
        public string label;
        public int minRound;
        public int maxRound;
    }

    public static class RankingRules
    {
        // 내림차순 키 (일반완료, 무한완료, 그 도전 점수).
        // 양수 = 앞 인자(a)가 더 높은 기록, 0 = 완전 동점, 음수 = 뒤 인자(b)가 더 높습니다.
        public static int Compare(ReachKey a, ReachKey b)
        {
            int c = a.NormalCleared.CompareTo(b.NormalCleared);
            if (c != 0) return c;
            c = a.Loops.CompareTo(b.Loops);
            if (c != 0) return c;
            return a.RunScore.CompareTo(b.RunScore);
        }

        // 완전 동점은 공동 순위(1,1,3 형태).
        public static IReadOnlyList<(string player, int rank)> Rank(IReadOnlyList<(string player, ReachKey key)> entries)
        {
            var sorted = new List<(string player, ReachKey key)>(entries);
            sorted.Sort((x, y) => Compare(y.key, x.key)); // 높은 기록이 앞으로 오는 내림차순 정렬.

            var ranked = new List<(string player, int rank)>(sorted.Count);
            int rank = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                // 앞 기록과 완전 동점이면 같은 순위를 쓰고, 다르면 자리(index+1)로 씁니다.
                if (i == 0 || Compare(sorted[i].key, sorted[i - 1].key) != 0) rank = i + 1;
                ranked.Add((sorted[i].player, rank));
            }
            return ranked;
        }

        // ceil(1000×rank/n)/10.0 — 실수 대신 정수(long)로 ceiling해小数 오차를 막습니다.
        public static double Percentile(int rank, int n)
        {
            long permilleCeil = (1000L * rank + n - 1) / n; // ceil(a/b) = (a + b - 1) / b.
            return permilleCeil / 10.0;
        }

        // 0→BenchmarkOnly, 1~19→RankWithBenchmark, 20+→Official
        public static RankingMode Mode(int population)
        {
            if (population <= 0) return RankingMode.BenchmarkOnly;
            if (population < 20) return RankingMode.RankWithBenchmark;
            return RankingMode.Official;
        }

        public static string BenchmarkGrade(BenchmarkTable table, ReachKey mine)
        {
            var rows = table.Rows;
            BenchmarkGradeRow band = null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (mine.NormalCleared >= rows[i].minRound && mine.NormalCleared <= rows[i].maxRound)
                {
                    band = rows[i];
                    break;
                }
            }
            if (band == null) band = rows[rows.Count - 1]; // 구간을 넘친 기록은 마지막 구간으로 봅니다.

            // FR-013 표기: 마지막(무한) 구간에서 완료 구절이 있으면 "All+완료 구절 수"로 씁니다.
            if (band == rows[rows.Count - 1] && mine.Loops > 0) return "All+" + mine.Loops;
            return band.label;
        }

        // ranking-benchmark.json(패키지 원본 그대로) 파싱.
        public static BenchmarkTable ParseBenchmark(string json)
        {
            var dto = UnityEngine.JsonUtility.FromJson<BenchmarkJson>(json);
            return new BenchmarkTable
            {
                Id = dto.id,
                Empirical = dto.empirical,
                PercentileAvailable = dto.percentileAvailable,
                Rows = dto.rows,
            };
        }

        // JsonUtility 매핑 DTO(JSON 키와 이름·철자를 그대로 맞춥니다).
        [Serializable]
        private sealed class BenchmarkJson
        {
            public string id;
            public bool empirical;
            public bool percentileAvailable;
            public List<BenchmarkGradeRow> rows;
        }
    }
}
