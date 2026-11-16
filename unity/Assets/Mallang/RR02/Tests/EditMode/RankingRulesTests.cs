using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L5 랭킹 규칙. 기준표는 실제 패키지 사본 ranking-benchmark.json을 읽습니다.
    [TestFixture]
    public class RankingRulesTests
    {
        private static string BenchmarkJson()
        {
            return File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath, "Mallang/RR02/Data/ranking-benchmark.json"));
        }

        [Test]
        public void CompareOrdersByClearedThenLoopsThenRunScoreDescending()
        {
            var a = new ReachKey(9, 3, 5000);
            var b = new ReachKey(9, 2, 9999);
            var c = new ReachKey(8, 0, 9999);

            // AC-021: key=(일반 완료, 무한 완료, 이번 도전 점수) 큰 순서. 양수 = 앞 인자가 더 높은 기록.
            Assert.Greater(RankingRules.Compare(a, b), 0); // (9,3,5000) > (9,2,9999)
            Assert.Greater(RankingRules.Compare(b, c), 0); // (9,2,9999) > (8,0,9999)
            Assert.Greater(RankingRules.Compare(a, c), 0);
            Assert.Less(RankingRules.Compare(c, a), 0);
            Assert.AreEqual(0, RankingRules.Compare(a, new ReachKey(9, 3, 5000))); // 완전 동점
        }

        [Test]
        public void ExactTieSharesRankAndNextRankSkips()
        {
            var entries = new List<(string, ReachKey)>
            {
                ("A", new ReachKey(9, 3, 5000)),
                ("B", new ReachKey(9, 3, 5000)),
                ("C", new ReachKey(8, 0, 9999))
            };

            var ranked = RankingRules.Rank(entries);

            // AC-021: 완전 동점은 공동 순위 1,1 다음은 3.
            Assert.AreEqual(1, ranked.First(r => r.player == "A").rank);
            Assert.AreEqual(1, ranked.First(r => r.player == "B").rank);
            Assert.AreEqual(3, ranked.First(r => r.player == "C").rank);
        }

        [Test]
        public void RankAggregatesOnlyProvidedRealEntries()
        {
            var entries = new List<(string, ReachKey)>
            {
                ("A", new ReachKey(9, 0, 7200)),
                ("B", new ReachKey(3, 0, 1500))
            };

            var ranked = RankingRules.Rank(entries);

            // AC-022: 기준표는 공식 집계에 합산하지 않습니다 — 입력한 실제 참여자 수만 나옵니다.
            Assert.AreEqual(2, ranked.Count);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, ranked.Select(r => r.player).ToArray());
        }

        // AC-022·Review Focus 5: 백분위 = ceil(1000×rank/N)/10
        [TestCase(1, 20, 5.0)]
        [TestCase(20, 20, 100.0)] // 꼴찌 백분위 = ceil(1000×20/20)/10 = 100.0
        [TestCase(7, 23, 30.5)]   // ceil(7000/23)=305 → 30.5
        public void PercentileUsesCeilToOneDecimal(int rank, int n, double expected)
        {
            Assert.AreEqual(expected, RankingRules.Percentile(rank, n), 1e-9);
        }

        // AC-022·Review Focus 5: N=0 기준표만, 1~19 실순위+기준표, 20부터 공식 순위·백분위.
        [TestCase(0, RankingMode.BenchmarkOnly)]
        [TestCase(1, RankingMode.RankWithBenchmark)]
        [TestCase(19, RankingMode.RankWithBenchmark)]
        [TestCase(20, RankingMode.Official)]
        [TestCase(21, RankingMode.Official)]
        public void ModeSwitchesAtPopulationBoundaries(int population, RankingMode expected)
        {
            Assert.AreEqual(expected, RankingRules.Mode(population));
        }

        [Test]
        public void ParseBenchmarkReadsRubricMetadata()
        {
            var table = RankingRules.ParseBenchmark(BenchmarkJson());

            // AC-022: 기준표는 기획 등급이며 통계 분포가 아닙니다.
            Assert.AreEqual("RR02-rubric-v1", table.Id);
            Assert.IsFalse(table.Empirical);
            Assert.IsFalse(table.PercentileAvailable);
        }

        // ranking-benchmark.json 구간: 첫 도전(0)/악보 합주(1~2)/기억 합주(3~5)/암보 합주(6~8)/All 마스터(9)
        [TestCase(0, "첫 합주 도전")]
        [TestCase(1, "악보 합주")]
        [TestCase(2, "악보 합주")]
        [TestCase(3, "기억 합주")]
        [TestCase(5, "기억 합주")]
        [TestCase(6, "암보 합주")]
        [TestCase(8, "암보 합주")]
        [TestCase(9, "All 마스터")]
        public void BenchmarkGradeMapsCompletedRoundsToRubricLabel(int normalCleared, string expectedLabel)
        {
            var table = RankingRules.ParseBenchmark(BenchmarkJson());

            var grade = RankingRules.BenchmarkGrade(table, new ReachKey(normalCleared, 0, 0));

            Assert.AreEqual(expectedLabel, grade); // AC-022
        }

        [Test]
        public void BenchmarkGradeShowsAllPlusCompletedLoops()
        {
            var table = RankingRules.ParseBenchmark(BenchmarkJson());

            var grade = RankingRules.BenchmarkGrade(table, new ReachKey(9, 3, 4000));

            // AC-022·FR-013 표기: 무한 구간은 "All+완료 구절 수"(예: All+9)로 씁니다.
            Assert.AreEqual("All+3", grade);
        }
    }
}
