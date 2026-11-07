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
    }

    public static class RankingRules
    {
        // 내림차순 키 (일반완료, 무한완료, 그 도전 점수).
        public static int Compare(ReachKey a, ReachKey b) => throw new NotImplementedException();

        // 완전 동점은 공동 순위(1,1,3 형태).
        public static IReadOnlyList<(string player, int rank)> Rank(IReadOnlyList<(string player, ReachKey key)> entries) => throw new NotImplementedException();

        // ceil(1000×rank/n)/10.0
        public static double Percentile(int rank, int n) => throw new NotImplementedException();

        // 0→BenchmarkOnly, 1~19→RankWithBenchmark, 20+→Official
        public static RankingMode Mode(int population) => throw new NotImplementedException();

        public static string BenchmarkGrade(BenchmarkTable table, ReachKey mine) => throw new NotImplementedException();

        // ranking-benchmark.json(패키지 원본 그대로) 파싱.
        public static BenchmarkTable ParseBenchmark(string json) => throw new NotImplementedException();
    }
}
