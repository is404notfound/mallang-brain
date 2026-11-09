using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // 절차 생성(Procedural generation) 차트: 난이도 스케줄과 시드 결정성(determinism)을 검증합니다.
    [TestFixture]
    public class Rr02ChartGenTests
    {
        [Test]
        public void ScheduleHasFortyEntriesWithExpectedRamp()
        {
            var schedule = Rr02Chart.BuildSchedule();

            Assert.AreEqual(40, schedule.Count);

            // h=0 블록: n=1..4가 n번씩 반복 → variety [1,2,2,3,3,3,4,4,4,4], hidden 전부 0.
            var expectedFirstTen = new[] { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 };
            for (int i = 0; i < expectedFirstTen.Length; i++)
            {
                Assert.AreEqual(expectedFirstTen[i], schedule[i].variety, $"index {i} variety");
                Assert.AreEqual(0, schedule[i].hidden, $"index {i} hidden");
            }

            // 전체 램프: h 0..3 각 블록(10개)마다 variety 1..4가 n번씩 반복된다.
            int idx = 0;
            for (int h = 0; h <= 3; h++)
            {
                for (int n = 1; n <= 4; n++)
                {
                    for (int rep = 0; rep < n; rep++)
                    {
                        Assert.AreEqual(n, schedule[idx].variety, $"index {idx} variety (h={h}, n={n}, rep={rep})");
                        Assert.AreEqual(h, schedule[idx].hidden, $"index {idx} hidden (h={h}, n={n}, rep={rep})");
                        idx++;
                    }
                }
            }
            Assert.AreEqual(40, idx);
        }

        [Test]
        public void GenerateIsDeterministicForSameSeed()
        {
            var a = Rr02Chart.Generate(123);
            var b = Rr02Chart.Generate(123);

            Assert.AreEqual(a.Rounds.Count, b.Rounds.Count);
            for (int r = 0; r < a.Rounds.Count; r++)
            {
                var ra = a.Rounds[r];
                var rb = b.Rounds[r];
                Assert.AreEqual(ra.Number, rb.Number, $"round {r} number");
                Assert.AreEqual(ra.Notes.Count, rb.Notes.Count, $"round {r} note count");
                for (int i = 0; i < ra.Notes.Count; i++)
                {
                    Assert.AreEqual(ra.Notes[i].Friend, rb.Notes[i].Friend, $"round {r} note {i} friend");
                    Assert.AreEqual(ra.Notes[i].TargetUs, rb.Notes[i].TargetUs, $"round {r} note {i} targetUs");
                }
                CollectionAssert.AreEqual(ra.HiddenIndices, rb.HiddenIndices, $"round {r} hiddenIndices");
            }
        }

        [Test]
        public void GenerateDiffersForDifferentSeeds()
        {
            var a = Rr02Chart.Generate(123);
            var b = Rr02Chart.Generate(456);

            bool anyDifference = false;
            for (int r = 0; r < a.Rounds.Count && !anyDifference; r++)
            {
                var ra = a.Rounds[r];
                var rb = b.Rounds[r];
                for (int i = 0; i < ra.Notes.Count; i++)
                {
                    if (ra.Notes[i].Friend != rb.Notes[i].Friend) { anyDifference = true; break; }
                }
                if (!anyDifference && !ra.HiddenIndices.SequenceEqual(rb.HiddenIndices)) anyDifference = true;
            }

            Assert.IsTrue(anyDifference, "서로 다른 시드는 적어도 한 군데는 다른 결과를 내야 합니다.");
        }

        [Test]
        public void EachRoundHasFourNotesWithFixedTargetTimes()
        {
            var chart = Rr02Chart.Generate(7);
            var expectedTargets = new long[] { 0L, 400000L, 800000L, 1200000L };

            foreach (var round in chart.Rounds)
            {
                Assert.AreEqual(4, round.Notes.Count);
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(expectedTargets[i], round.Notes[i].TargetUs, $"round {round.Number} note {i}");
            }
        }

        [Test]
        public void HiddenIndicesMatchScheduleAndAreDistinctSorted()
        {
            var chart = Rr02Chart.Generate(42);
            var schedule = Rr02Chart.BuildSchedule();

            for (int r = 0; r < chart.Rounds.Count; r++)
            {
                var round = chart.Rounds[r];
                var hidden = round.HiddenIndices;
                Assert.AreEqual(schedule[r].hidden, hidden.Count, $"round {r} hidden count");

                var distinct = new HashSet<int>(hidden);
                Assert.AreEqual(hidden.Count, distinct.Count, $"round {r} hidden indices must be distinct");

                for (int i = 0; i < hidden.Count; i++)
                    Assert.That(hidden[i], Is.InRange(0, 3), $"round {r} hidden index {i} out of range");

                for (int i = 1; i < hidden.Count; i++)
                    Assert.Less(hidden[i - 1], hidden[i], $"round {r} hidden indices must be sorted ascending");
            }
        }

        [Test]
        public void VarietyMatchesDistinctFriendCountAndCoversChosenAnimals()
        {
            var chart = Rr02Chart.Generate(2024);
            var schedule = Rr02Chart.BuildSchedule();

            for (int r = 0; r < chart.Rounds.Count; r++)
            {
                var round = chart.Rounds[r];
                int expectedVariety = schedule[r].variety;

                var distinctFriends = round.Notes.Select(n => n.Friend).Distinct().ToList();
                Assert.AreEqual(expectedVariety, distinctFriends.Count,
                    $"round {r} (scheduled variety {expectedVariety}) should use exactly that many distinct friends");

                foreach (var friend in distinctFriends)
                {
                    int count = round.Notes.Count(n => n.Friend == friend);
                    Assert.GreaterOrEqual(count, 1, $"round {r} friend {friend} should appear at least once");
                }
            }
        }

        [Test]
        public void RoundsCountIsForty()
        {
            var chart = Rr02Chart.Generate(999);
            Assert.AreEqual(40, chart.Rounds.Count);
        }
    }
}
