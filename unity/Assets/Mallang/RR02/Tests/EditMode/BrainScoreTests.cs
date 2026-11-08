using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L4 뇌 점수·배지. AC-015의 예시 수치를 그대로 씁니다.
    [TestFixture]
    public class BrainScoreTests
    {
        private static IReadOnlyDictionary<Rr02Friend, int> Scores(int rabbit, int capybara, int fox, int squirrel)
        {
            return new Dictionary<Rr02Friend, int>
            {
                { Rr02Friend.Rabbit, rabbit },
                { Rr02Friend.Capybara, capybara },
                { Rr02Friend.Fox, fox },
                { Rr02Friend.Squirrel, squirrel }
            };
        }

        private static Rr02RunResult MakeResult(int normalCleared, int loops, bool allPerfect, bool eligible)
        {
            return new Rr02RunResult
            {
                NormalCleared = normalCleared,
                Loops = loops,
                AllPerfect = allPerfect,
                OfficialEligible = eligible,
                AnimalScores = Scores(0, 0, 0, 0),
                TotalScore = 0
            };
        }

        // FR-025 첫 기준안: 0=콩알(0), 200 이상=자람(1), 400 이상=점화(2), 700 이상=치솟음(3)
        [TestCase(0, 0)]
        [TestCase(199, 0)]
        [TestCase(200, 1)]
        [TestCase(399, 1)]
        [TestCase(400, 2)]
        [TestCase(699, 2)]
        [TestCase(700, 3)]
        [TestCase(7200, 3)]
        public void StageBoundariesFollowFirstBaseline(int score, int expectedStage)
        {
            Assert.AreEqual(expectedStage, BrainScore.Stage(score));
        }

        [Test]
        public void MergeBestTakesPerAnimalMaxWhenEligible()
        {
            var before = Scores(1200, 1100, 900, 800);
            var run = Scores(1400, 980, 1100, 850);

            var after = BrainScore.MergeBest(before, run, eligible: true);

            // AC-015: 동물 최고 1400/1100/1100/850
            Assert.AreEqual(1400, after[Rr02Friend.Rabbit]);
            Assert.AreEqual(1100, after[Rr02Friend.Capybara]); // 카피바라 최고 1100 유지(980으로 내려가지 않음)
            Assert.AreEqual(1100, after[Rr02Friend.Fox]);
            Assert.AreEqual(850, after[Rr02Friend.Squirrel]);
        }

        [Test]
        public void MyBrainTotalSumsFourBestsDistinctFromRunTotal()
        {
            var before = Scores(1200, 1100, 900, 800);
            var run = Scores(1400, 980, 1100, 850);

            var after = BrainScore.MergeBest(before, run, eligible: true);

            // AC-015: 내 뇌 4000 → 4450, 이번 도전 합 4330과 구분.
            Assert.AreEqual(4000, BrainScore.MyBrainTotal(before));
            Assert.AreEqual(4450, BrainScore.MyBrainTotal(after));
            Assert.AreEqual(4330, run.Values.Sum()); // 이번 도전 점수(순위 키)와 다른 수치임을 고정
            Assert.AreNotEqual(4330, BrainScore.MyBrainTotal(after));
        }

        [Test]
        public void IneligibleRunDoesNotChangeBests()
        {
            var before = Scores(1200, 1100, 900, 800);
            var run = Scores(9999, 9999, 9999, 9999);

            var after = BrainScore.MergeBest(before, run, eligible: false);

            // FR-015: 정상 소리·무도움·무중단·유효 패턴의 종료 도전만 성장 자격이 있습니다.
            Assert.AreEqual(1200, after[Rr02Friend.Rabbit]);
            Assert.AreEqual(1100, after[Rr02Friend.Capybara]);
            Assert.AreEqual(900, after[Rr02Friend.Fox]);
            Assert.AreEqual(800, after[Rr02Friend.Squirrel]);
        }

        [Test]
        public void AllClearBadgeRequiresNineNormalRounds()
        {
            // FR-016: All Clear는 일반 9라운드를 모두 성공했을 때만.
            var cleared = BrainScore.Badges(MakeResult(9, 0, false, true), prevBestLoops: 0);
            CollectionAssert.Contains(cleared, "all-clear");

            var notCleared = BrainScore.Badges(MakeResult(8, 0, false, true), prevBestLoops: 0);
            CollectionAssert.DoesNotContain(notCleared, "all-clear");
        }

        [Test]
        public void AllPerfectBadgeRequiresAllPerfectRun()
        {
            // FR-016: All Perfect는 일반 72판정 전부 Perfect(무한 구간 제외).
            var perfect = BrainScore.Badges(MakeResult(9, 0, true, true), prevBestLoops: 0);
            CollectionAssert.Contains(perfect, "all-perfect");

            var notPerfect = BrainScore.Badges(MakeResult(9, 0, false, true), prevBestLoops: 0);
            CollectionAssert.DoesNotContain(notPerfect, "all-perfect");
        }

        [Test]
        public void LoopBestBadgeRequiresStrictlyHigherLoops()
        {
            // FR-016: 무한 최고는 All+N 최고 기록을 갱신했을 때만.
            var improved = BrainScore.Badges(MakeResult(9, 5, false, true), prevBestLoops: 4);
            CollectionAssert.Contains(improved, "loop-best");

            var tied = BrainScore.Badges(MakeResult(9, 4, false, true), prevBestLoops: 4);
            CollectionAssert.DoesNotContain(tied, "loop-best");
        }

        [Test]
        public void IneligibleRunEarnsNoBadges()
        {
            // FR-016: 배지 자격은 FR-015의 성장 자격과 같습니다 — 연습(자격 상실) 도전은 배지 0.
            var badges = BrainScore.Badges(MakeResult(9, 10, true, eligible: false), prevBestLoops: 0);

            Assert.AreEqual(0, badges.Count);
        }
    }
}
