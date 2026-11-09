using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // 목 리더보드(온라인 랭킹 화면 뷰 모델). 캔버스 screen-ranking-final.png의
    // 상위 10명·내 등수 표시를 그대로 뷰 모델로 둔다.
    [TestFixture]
    public class MockLeaderboardTests
    {
        [Test]
        public void PopulationHas10DescendingWithCanvasData()
        {
            var p = MockLeaderboard.Population;
            Assert.AreEqual(10, p.Count);
            Assert.AreEqual("토끼대장", p[0].Name);
            Assert.AreEqual(9870, p[0].Score);
            Assert.AreEqual(1, p[0].Rank);
            Assert.AreEqual(6580, p[9].Score);
            Assert.AreEqual(10, p[9].Rank);
            for (int i = 1; i < p.Count; i++)
                Assert.Less(p[i].Score, p[i - 1].Score, "점수는 1등부터 내림차순");
        }

        [Test]
        public void RankForScoreTopScoresMapToLeaderboardPositions()
        {
            Assert.AreEqual(1, MockLeaderboard.RankForScore(9999));
            Assert.AreEqual(1, MockLeaderboard.RankForScore(9870));
            Assert.AreEqual(10, MockLeaderboard.RankForScore(6580));
        }

        [Test]
        public void RankForScoreCanvasSampleIs1234()
        {
            // 캔버스 샘플: 내 점수 4,450 -> 1,234등.
            Assert.AreEqual(1234, MockLeaderboard.RankForScore(4450));
        }

        [Test]
        public void RankForScoreIsMonotonicNonIncreasing()
        {
            int prev = MockLeaderboard.RankForScore(0);
            for (int s = 100; s <= 10000; s += 100)
            {
                int r = MockLeaderboard.RankForScore(s);
                Assert.LessOrEqual(r, prev, "점수가 높을수록 등수 숫자는 작거나 같아야 한다");
                prev = r;
            }
        }

        [Test]
        public void BuildLowScoreShowsSeparateMyRowWithEllipsis()
        {
            var b = MockLeaderboard.Build(4450);
            Assert.AreEqual(10, b.Top.Count);
            Assert.IsTrue(b.ShowEllipsis);
            Assert.IsFalse(b.MeInTop);
            Assert.IsFalse(b.Top.Any(e => e.IsMe), "상위권 밖이면 내가 Top에 없다");
            Assert.AreEqual(1234, b.Me.Rank);
            Assert.AreEqual("나", b.Me.Name);
            Assert.AreEqual(4450, b.Me.Score);
            Assert.IsTrue(b.Me.IsMe);
            Assert.AreEqual(0, b.Me.Avatar);
        }

        [Test]
        public void BuildTopScorePlacesMeInTopNoEllipsis()
        {
            var b = MockLeaderboard.Build(9999);
            Assert.AreEqual(10, b.Top.Count);
            Assert.IsFalse(b.ShowEllipsis);
            Assert.IsTrue(b.MeInTop);
            Assert.AreEqual(1, b.Top[0].Rank);
            Assert.IsTrue(b.Top[0].IsMe);
            Assert.AreEqual(9999, b.Top[0].Score);
            Assert.IsTrue(b.Me.IsMe);
            Assert.AreEqual(1, b.Me.Rank);
        }
    }
}
