using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L1 차트 로드·타이밍 산수. 실제 패키지 사본 four-friend-chart.json을 그대로 읽습니다.
    [TestFixture]
    public class Rr02ChartTests
    {
        private static string ChartJson()
        {
            return File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath, "Mallang/RR02/Data/four-friend-chart.json"));
        }

        private static Rr02Chart LoadChart() => Rr02Chart.LoadFromJson(ChartJson());

        private static Rr02Friend[] FriendSeq(Rr02Round round) =>
            round.Notes.Select(n => n.Friend).ToArray();

        private static readonly Rr02Friend[] RoundOnePattern =
        {
            Rr02Friend.Rabbit, Rr02Friend.Rabbit, Rr02Friend.Capybara, Rr02Friend.Capybara,
            Rr02Friend.Rabbit, Rr02Friend.Capybara, Rr02Friend.Rabbit, Rr02Friend.Capybara
        };

        private static readonly Rr02Friend[] RoundTwoPattern =
        {
            Rr02Friend.Capybara, Rr02Friend.Fox, Rr02Friend.Squirrel, Rr02Friend.Rabbit,
            Rr02Friend.Fox, Rr02Friend.Capybara, Rr02Friend.Squirrel, Rr02Friend.Rabbit
        };

        private static readonly Rr02Friend[] RoundNinePattern =
        {
            Rr02Friend.Rabbit, Rr02Friend.Capybara, Rr02Friend.Fox, Rr02Friend.Squirrel,
            Rr02Friend.Rabbit, Rr02Friend.Fox, Rr02Friend.Capybara, Rr02Friend.Squirrel
        };

        [Test]
        public void LoadsNineNormalRounds()
        {
            var chart = LoadChart();

            Assert.AreEqual(9, chart.Rounds.Count); // FR-011: 일반 라운드 R1~R9
            for (int i = 0; i < 9; i++)
                Assert.AreEqual(i + 1, chart.Rounds[i].Number);
        }

        [Test]
        public void RoundOneUsesOnlyRabbitAndCapybaraEightBeats()
        {
            var chart = LoadChart();
            var seq = FriendSeq(chart.Rounds[0]);

            // AC-003: R1은 토끼·카피바라 두 소리의 8박
            CollectionAssert.AreEqual(RoundOnePattern, seq);
            Assert.AreEqual(2, seq.Distinct().Count());
        }

        [Test]
        public void HiddenIndicesFollowTailRoundMinusOneRule()
        {
            var chart = LoadChart();

            // AC-011: R1=0개, R2=[7], R9=8개(0~7). 숨김은 뒤쪽 r−1개.
            Assert.AreEqual(0, chart.Rounds[0].HiddenIndices.Count);
            CollectionAssert.AreEqual(new[] { 7 }, chart.Rounds[1].HiddenIndices);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, chart.Rounds[8].HiddenIndices);

            for (int r = 1; r <= 9; r++)
            {
                var hidden = chart.Rounds[r - 1].HiddenIndices;
                Assert.AreEqual(r - 1, hidden.Count, $"R{r} hidden count");
                var expectedTail = Enumerable.Range(8 - (r - 1), r - 1).ToArray();
                CollectionAssert.AreEquivalent(expectedTail, hidden.ToArray(), $"R{r}는 뒤쪽 칸만 숨깁니다");
            }
        }

        [Test]
        public void TargetTicksConvertToMicrosecondsAtOneBeatPer400000Us()
        {
            var chart = LoadChart();

            // FR-007: 150BPM, 1박 = 480 tick = 400,000µs. targetTicks 0..3360 → 0..2,800,000µs.
            foreach (var round in chart.Rounds)
            {
                Assert.AreEqual(8, round.Notes.Count, $"R{round.Number}는 8박");
                for (int i = 0; i < 8; i++)
                {
                    Assert.AreEqual(i * 400000L, round.Notes[i].TargetUs,
                        $"R{round.Number} note {i} TargetUs");
                    Assert.AreEqual(i, round.Notes[i].NoteId, $"R{round.Number} note_id 순서");
                }
            }
        }

        [Test]
        public void NotesAreSingleInstantTapsWithoutHoldsOrChords()
        {
            var chart = LoadChart();

            // AC-002: RR02 홀드 0·동시목표 0 — 노트는 전부 단일 시각이고 같은 시각이 겹치지 않습니다.
            foreach (var round in chart.Rounds)
            {
                var targets = round.Notes.Select(n => n.TargetUs).ToArray();
                Assert.AreEqual(targets.Length, targets.Distinct().Count(), $"R{round.Number} 동시목표 없음");
                for (int i = 1; i < targets.Length; i++)
                    Assert.Less(targets[i - 1], targets[i], $"R{round.Number} 목표 시각 단조 증가");
            }
        }

        [Test]
        public void InfinitePhraseCyclesRoundTwoThroughNineFullyHidden()
        {
            var chart = LoadChart();

            // AC-013: k=1→R2 … k=8→R9, k=9→다시 R2 순환, 입력은 전부 숨김(0~7).
            CollectionAssert.AreEqual(RoundTwoPattern, FriendSeq(chart.InfinitePhrase(1)));
            CollectionAssert.AreEqual(RoundNinePattern, FriendSeq(chart.InfinitePhrase(8)));
            CollectionAssert.AreEqual(RoundTwoPattern, FriendSeq(chart.InfinitePhrase(9)));

            for (int k = 1; k <= 9; k++)
            {
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
                    chart.InfinitePhrase(k).HiddenIndices.ToArray(), $"무한 k={k} 전부 숨김");
            }
        }

        [Test]
        public void InfinitePhraseNeverReusesTwoSoundIntroPattern()
        {
            var chart = LoadChart();

            // AC-013: 무한은 네 친구 차트 순환이며 R1 두 소리 입문 패턴을 재사용하지 않습니다.
            for (int k = 1; k <= 16; k++)
            {
                var seq = FriendSeq(chart.InfinitePhrase(k));
                CollectionAssert.AreNotEqual(RoundOnePattern, seq, $"무한 k={k}");
                Assert.AreEqual(4, seq.Distinct().Count(), $"무한 k={k}는 네 친구 전부 등장");
            }
        }

        [Test]
        public void ScheduleMatchesChallengeTimingArithmetic()
        {
            // AC-007: 카운트인 12박=4.8초, 시연 8박 3.2초, 라운드 20박=8초, All 전환까지 76.8초.
            Assert.AreEqual(4800000L, Rr02Schedule.CountInUs);
            Assert.AreEqual(4800000L, Rr02Schedule.ListenStartUs(1)); // 첫 듣기 = 카운트인 직후

            for (int r = 1; r <= 9; r++)
            {
                Assert.AreEqual(Rr02Schedule.ListenStartUs(r) + 3200000L, Rr02Schedule.RepeatStartUs(r),
                    $"R{r} 따라치기 시작 = 듣기 시작 + 8박(3,200,000µs)");
                Assert.AreEqual(8000000L, Rr02Schedule.RoundEndUs(r) - Rr02Schedule.ListenStartUs(r),
                    $"R{r} 한 라운드 20박 = 8,000,000µs");
            }

            for (int r = 1; r <= 8; r++)
            {
                Assert.AreEqual(Rr02Schedule.RoundEndUs(r), Rr02Schedule.ListenStartUs(r + 1),
                    $"R{r} 마무리 4박 뒤 바로 다음 시연");
            }

            Assert.AreEqual(76800000L, Rr02Schedule.RoundEndUs(9)); // AC-007: All 전환까지 76.8초
        }

        [Test]
        public void LoadFromJsonRejectsMalformedChart()
        {
            // 형식이 어긋나면 조용한 기본값 대신 ArgumentException(L1 계약). 예외 종류를 고정해
            // 미구현(NotImplementedException) 상태에서는 이 테스트도 실패하게 한다.
            Assert.Throws<ArgumentException>(() => Rr02Chart.LoadFromJson("{}"));
            Assert.Throws<ArgumentException>(() => Rr02Chart.LoadFromJson(null));
            Assert.Throws<ArgumentException>(() => Rr02Chart.LoadFromJson(""));
        }
    }
}
