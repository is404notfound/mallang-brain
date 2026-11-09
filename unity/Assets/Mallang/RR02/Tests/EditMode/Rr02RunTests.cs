using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L3 도전 상태기계. 점수·실패 테스트는 합성 차트, 라운드 진행 테스트는 실제 JSON 차트를 씁니다.
    [TestFixture]
    public class Rr02RunTests
    {
        private const long PadUs = 180000L; // 구절 확정 여유(마지막 목표 +180ms)

        private static Rr02Chart LoadRealChart()
        {
            string json = File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath, "Mallang/RR02/Data/four-friend-chart.json"));
            return Rr02Chart.LoadFromJson(json);
        }

        private static Rr02Round MakeRound(int number, int[] hidden, params (Rr02Friend friend, long targetUs)[] notes)
        {
            var list = new List<Rr02Note>();
            for (int i = 0; i < notes.Length; i++)
                list.Add(new Rr02Note(i, notes[i].friend, notes[i].targetUs));
            return new Rr02Round { Number = number, Notes = list, HiddenIndices = hidden };
        }

        private static Rr02Chart MakeChart(params Rr02Round[] rounds)
        {
            return new Rr02Chart { Rounds = rounds };
        }

        // 구절 전체를 성공 입력합니다. goodIndex가 있으면 그 노트만 +100,000µs(Good)로 칩니다.
        private static void ClearPhrase(Rr02Run run, IReadOnlyList<Rr02Note> notes, int goodIndex = -1)
        {
            run.BeginRepeat();
            for (int i = 0; i < notes.Count; i++)
            {
                long pressUs = notes[i].TargetUs + (i == goodIndex ? 100000L : 0L);
                run.AdvanceTime(pressUs);
                run.ProcessPress(notes[i].Friend, pressUs);
            }
            run.AdvanceTime(notes[notes.Count - 1].TargetUs + PadUs);
        }

        private static int ScoreOf(IReadOnlyDictionary<Rr02Friend, int> scores, Rr02Friend friend)
        {
            return scores.TryGetValue(friend, out int v) ? v : 0;
        }

        [Test]
        public void StartsListeningAtRoundOneWithOfficialEligibility()
        {
            var run = new Rr02Run(LoadRealChart());

            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(1, run.CurrentRound);
            Assert.AreEqual(0, run.NormalCleared);
            Assert.AreEqual(0, run.Loops);
            Assert.IsTrue(run.OfficialEligible); // FR-009: 자격은 중단·도움 전까지 유지
        }

        [Test]
        public void ClearingPhraseAdvancesToNextRoundListen()
        {
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);

            ClearPhrase(run, chart.Rounds[0].Notes);

            // AC-003: 한 구절(8박 듣기→8박 따라치기) 성공이면 다음 라운드로 갑니다.
            Assert.AreEqual(1, run.NormalCleared);
            Assert.AreEqual(2, run.CurrentRound);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
        }

        [Test]
        public void PhraseConfirmsExactlyAtLastTargetPlus180Ms()
        {
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);
            var notes = chart.Rounds[0].Notes;

            run.BeginRepeat();
            foreach (var n in notes)
            {
                run.AdvanceTime(n.TargetUs);
                run.ProcessPress(n.Friend, n.TargetUs);
            }
            long lastUs = notes[notes.Count - 1].TargetUs;

            run.AdvanceTime(lastUs + PadUs - 1L);
            Assert.AreEqual(Rr02Phase.Repeat, run.Phase); // 아직 확정 전

            run.AdvanceTime(lastUs + PadUs);
            // AC-007: 입력 마지막 목표 +180ms에 확정하고 다음 듣기로 갑니다.
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(1, run.NormalCleared);
        }

        // 일반 라운드 수는 더 이상 9로 고정되지 않으므로(Rr02Chart.Generate가 라운드 수를 결정)
        // 작은 커스텀 차트로 "일반 라운드를 전부 지나면 무한 구절로 진입"하는 동작만 확인합니다.
        private static Rr02Chart MakeThreeRoundChart()
        {
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Rabbit, 400000L),
                (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Rabbit, 1200000L));
            var round2 = MakeRound(2, new int[0],
                (Rr02Friend.Capybara, 0L),
                (Rr02Friend.Capybara, 400000L),
                (Rr02Friend.Capybara, 800000L),
                (Rr02Friend.Capybara, 1200000L));
            var round3 = MakeRound(3, new int[0],
                (Rr02Friend.Fox, 0L),
                (Rr02Friend.Fox, 400000L),
                (Rr02Friend.Fox, 800000L),
                (Rr02Friend.Fox, 1200000L));
            return MakeChart(round1, round2, round3);
        }

        [Test]
        public void ClearingAllNormalRoundsEntersInfiniteLoopCounting()
        {
            var chart = MakeThreeRoundChart();
            var run = new Rr02Run(chart);

            for (int r = 0; r < chart.Rounds.Count; r++)
                ClearPhrase(run, chart.Rounds[r].Notes);

            // AC-013: 마지막 일반 라운드 성공은 끝이 아니라 — 도전은 끝나지 않고 무한 구절로 이어집니다.
            Assert.AreEqual(chart.Rounds.Count, run.NormalCleared);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(0, run.Loops);

            ClearPhrase(run, chart.InfinitePhrase(1).Notes); // 무한 k=1
            Assert.AreEqual(1, run.Loops); // AC-013: 구절 성공마다 무한 완료 수 +1

            ClearPhrase(run, chart.InfinitePhrase(2).Notes); // 무한 k=2
            Assert.AreEqual(2, run.Loops);

            // 다음 무한 구절을 무입력으로 실패시켜 도전을 종료합니다. 진행 중 구절은 Loops에 넣지 않습니다.
            run.BeginRepeat();
            var failing = chart.InfinitePhrase(3).Notes;
            run.AdvanceTime(failing[failing.Count - 1].TargetUs + PadUs);

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(chart.Rounds.Count, run.Result.NormalCleared);
            Assert.AreEqual(2, run.Result.Loops); // AC-013: All+2 = 완료한 무한 구절만
            // FR-016 기준안: All Perfect는 일반 라운드 판정 기준이며 무한 구간은 포함하지 않습니다.
            Assert.IsTrue(run.Result.AllPerfect);
        }

        [Test]
        public void AllPerfectIsFalseWhenAnyNormalJudgementIsGood()
        {
            var chart = MakeThreeRoundChart();
            var run = new Rr02Run(chart);

            ClearPhrase(run, chart.Rounds[0].Notes, goodIndex: 3); // R1에 Good 1개
            for (int r = 1; r < chart.Rounds.Count; r++)
                ClearPhrase(run, chart.Rounds[r].Notes);

            run.BeginRepeat(); // 무한 첫 구절 무입력 실패로 종료
            var inf = chart.InfinitePhrase(1).Notes;
            run.AdvanceTime(inf[inf.Count - 1].TargetUs + PadUs);

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(chart.Rounds.Count, run.Result.NormalCleared);
            Assert.IsFalse(run.Result.AllPerfect); // FR-016: 일반 라운드 판정 전부 Perfect일 때만
        }

        [Test]
        public void ScoreSumsPerfectGoodMissWithoutComboMultiplier()
        {
            // AC-006: Perfect 2·Good 1·Miss 1 = 100+100+70+0 = 270점, 콤보 배율 없음.
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Rabbit, 400000L),
                (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Rabbit, 1200000L));
            var round2 = MakeRound(2, new[] { 0 }, (Rr02Friend.Capybara, 0L));
            var run = new Rr02Run(MakeChart(round1, round2));

            run.BeginRepeat();
            run.ProcessPress(Rr02Friend.Rabbit, 0L);                 // Perfect 100
            run.ProcessPress(Rr02Friend.Rabbit, 400000L + 100000L);  // Good 70
            run.ProcessPress(Rr02Friend.Rabbit, 800000L);            // Perfect 100
            run.AdvanceTime(1200000L + PadUs);                       // 마지막 노트 Miss, 3/4=75% >= 60% → 통과

            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(270, ScoreOf(run.Result.AnimalScores, Rr02Friend.Rabbit)); // 점수 합산(콤보 배율 없음)
            Assert.AreEqual(270, run.Result.TotalScore);
            Assert.AreEqual(1, run.Result.NormalCleared); // 60% 이상 통과 → 전구 +1
        }

        [Test]
        public void GhostInputScoresZeroButDoesNotFailNormalRound()
        {
            // 라이프 모델: ghost는 0점이지만 일반 라운드를 실패시키지 않습니다(4/4 통과). 올바른 입력 점수는 포함.
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Rabbit, 400000L),
                (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Rabbit, 1200000L));
            var round2 = MakeRound(2, new[] { 0 }, (Rr02Friend.Capybara, 0L));
            var run = new Rr02Run(MakeChart(round1, round2));

            run.BeginRepeat();
            run.ProcessPress(Rr02Friend.Rabbit, 0L);
            run.ProcessPress(Rr02Friend.Rabbit, 400000L);
            var ghost = run.ProcessPress(Rr02Friend.Capybara, 600000L); // 열린 목표 없음 → ghost
            run.ProcessPress(Rr02Friend.Rabbit, 800000L);
            run.ProcessPress(Rr02Friend.Rabbit, 1200000L);
            run.AdvanceTime(1200000L + PadUs);

            Assert.AreEqual(Rr02InputKind.Ghost, ghost.Kind);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase); // ghost는 0점일 뿐, 4개 노트 다 맞아 통과
            Assert.AreEqual(1, run.Result.NormalCleared);
            Assert.AreEqual(400, ScoreOf(run.Result.AnimalScores, Rr02Friend.Rabbit));
            Assert.AreEqual(0, ScoreOf(run.Result.AnimalScores, Rr02Friend.Capybara)); // ghost 0점
            Assert.AreEqual(400, run.Result.TotalScore);
        }

        [Test]
        public void MidPhraseMissKeepsMusicRunningAndFinalizesOnceAtPhraseEnd()
        {
            // 길이 3 중 2번째 Miss(2/3=67% >= 60% 통과) — 음악은 계속, 마지막 목표 +180ms에 확정 1회.
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Rabbit, 400000L),
                (Rr02Friend.Rabbit, 800000L));
            var round2 = MakeRound(2, new[] { 0 }, (Rr02Friend.Capybara, 0L));
            var run = new Rr02Run(MakeChart(round1, round2));

            run.BeginRepeat();
            run.ProcessPress(Rr02Friend.Rabbit, 0L);   // Perfect
            run.AdvanceTime(400000L + 120001L);        // 2번째 목표 Miss 확정 시각
            Assert.AreEqual(Rr02Phase.Repeat, run.Phase); // 음악(구간)은 계속

            var late = run.ProcessPress(Rr02Friend.Rabbit, 800000L); // 실패 확정 뒤 올바른 입력도 판정·점수 포함
            Assert.AreEqual(Rr02InputKind.Judged, late.Kind);
            Assert.AreEqual(Rr02Judgement.Perfect, late.Judgement);

            run.AdvanceTime(800000L + PadUs - 1L);
            Assert.AreEqual(Rr02Phase.Repeat, run.Phase);

            run.AdvanceTime(800000L + PadUs); // 확정은 마지막 목표 +180ms에 1회
            Assert.AreEqual(Rr02Phase.Listen, run.Phase); // 2/3=67% >= 60% → 통과(다음 라운드 듣기)
            Assert.AreEqual(1, run.Result.NormalCleared); // 통과 → 전구 +1
            Assert.AreEqual(200, run.Result.TotalScore);  // 올바른 입력 점수 포함

            run.AdvanceTime(5000000L); // 추가 진행 호출이 상태·결과를 바꾸지 않습니다.
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(200, run.Result.TotalScore);
        }

        [Test]
        public void InvalidateOfficialKeepsProgressAndRestartsSameListen()
        {
            // R1: 토끼·카피바라가 번갈아 2회씩 → 각 Perfect 2회 = 200점.
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Capybara, 400000L),
                (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Capybara, 1200000L));
            var round2 = MakeRound(2, new int[0],
                (Rr02Friend.Capybara, 0L),
                (Rr02Friend.Fox, 400000L),
                (Rr02Friend.Squirrel, 800000L),
                (Rr02Friend.Rabbit, 1200000L));
            var chart = MakeChart(round1, round2);
            var run = new Rr02Run(chart);
            ClearPhrase(run, chart.Rounds[0].Notes); // R1 성공(토끼 200·카피바라 200)

            run.BeginRepeat(); // R2 따라치기 진입
            run.ProcessPress(Rr02Friend.Capybara, 0L); // R2 첫 노트(카피바라) Perfect
            run.InvalidateOfficial(Rr02PracticeReason.Pause);

            // AC-009·MCH-007: 중단은 공식 자격만 상실, 완료 단계 유지, 같은 라운드 듣기부터 재개.
            Assert.IsFalse(run.OfficialEligible);
            Assert.AreEqual(1, run.NormalCleared);
            Assert.AreEqual(2, run.CurrentRound);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            // 미완료 구절 입력은 폐기 — R2에서 친 카피바라 점수가 남지 않습니다.
            Assert.AreEqual(200, ScoreOf(run.RunAnimalScores, Rr02Friend.Capybara));

            ClearPhrase(run, chart.Rounds[1].Notes); // 연습으로 R2 재도전·성공
            Assert.AreEqual(2, run.NormalCleared);
            Assert.IsFalse(run.OfficialEligible); // 자격은 돌아오지 않습니다
        }

        [Test]
        public void OnlyCorrectlyPressedFriendGainsAnimalScore()
        {
            // FR-025 전제(AC-025): R2 입력 구간에서 토끼만 올바르게 치면 토끼 점수만 증가합니다.
            // R1: 토끼·카피바라가 번갈아 2회씩 → 각 Perfect 2회 = 200점.
            var round1 = MakeRound(1, new int[0],
                (Rr02Friend.Rabbit, 0L),
                (Rr02Friend.Capybara, 400000L),
                (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Capybara, 1200000L));
            // R2: 8노트, 토끼는 index 3(1,200,000µs)·index 7(2,800,000µs)뿐입니다.
            var round2 = MakeRound(2, new int[0],
                (Rr02Friend.Fox, 0L),
                (Rr02Friend.Fox, 400000L),
                (Rr02Friend.Squirrel, 800000L),
                (Rr02Friend.Rabbit, 1200000L),
                (Rr02Friend.Squirrel, 1600000L),
                (Rr02Friend.Fox, 2000000L),
                (Rr02Friend.Squirrel, 2400000L),
                (Rr02Friend.Rabbit, 2800000L));
            var chart = MakeChart(round1, round2);
            var run = new Rr02Run(chart);
            ClearPhrase(run, chart.Rounds[0].Notes); // R1: 토끼 200·카피바라 200

            run.BeginRepeat();
            run.ProcessPress(Rr02Friend.Rabbit, 1200000L);
            run.ProcessPress(Rr02Friend.Rabbit, 2800000L);
            run.AdvanceTime(2800000L + PadUs); // 2/8=25% < 60% 실패: 전구 1→0 깎고 같은 라운드 재도전(게임오버 아님)

            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            var scores = run.Result.AnimalScores;
            Assert.AreEqual(400, ScoreOf(scores, Rr02Friend.Rabbit));    // 200 + Perfect 2회
            Assert.AreEqual(200, ScoreOf(scores, Rr02Friend.Capybara)); // R1 값에서 변화 없음
            Assert.AreEqual(0, ScoreOf(scores, Rr02Friend.Fox));
            Assert.AreEqual(0, ScoreOf(scores, Rr02Friend.Squirrel));
            Assert.AreEqual(0, run.Result.NormalCleared); // R1 통과(1) 뒤 R2 실패로 전구 1→0
        }

        [Test]
        public void FailsWhenHiddenNotesWrongEvenIfShownAreRight()
        {
            // 기억 판정: 8노트 중 index 6,7을 숨김. 보이는 6개(토끼)는 다 맞히고 숨김 2개를 틀리면,
            // 전체 6/8=75%여도 숨김 0/2=0% < 60% → 실패(보이는 노트만 쳐서 넘어가지 못함).
            var round1 = MakeRound(1, new[] { 6, 7 },
                (Rr02Friend.Rabbit, 0L), (Rr02Friend.Rabbit, 400000L), (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Rabbit, 1200000L), (Rr02Friend.Rabbit, 1600000L), (Rr02Friend.Rabbit, 2000000L),
                (Rr02Friend.Capybara, 2400000L), (Rr02Friend.Fox, 2800000L)); // 숨김 6,7 = 카피바라/여우
            var round2 = MakeRound(2, new int[0], (Rr02Friend.Rabbit, 0L));
            var run = new Rr02Run(MakeChart(round1, round2));

            run.BeginRepeat();
            for (int i = 0; i < 6; i++) { run.AdvanceTime((long)i * 400000L); run.ProcessPress(Rr02Friend.Rabbit, (long)i * 400000L); }
            run.AdvanceTime(2400000L); run.ProcessPress(Rr02Friend.Rabbit, 2400000L); // 숨김6 실제 카피바라 → Miss
            run.AdvanceTime(2800000L); run.ProcessPress(Rr02Friend.Rabbit, 2800000L); // 숨김7 실제 여우 → Miss
            run.AdvanceTime(2800000L + PadUs);

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);     // 전구 0에서 실패 → 게임오버
            Assert.AreEqual(0, run.Result.NormalCleared);
        }

        [Test]
        public void PassesWhenHiddenNotesRecalledCorrectly()
        {
            // 같은 구성에서 숨김 2개를 올바르게 치면 통과(전구+1, 다음 라운드).
            var round1 = MakeRound(1, new[] { 6, 7 },
                (Rr02Friend.Rabbit, 0L), (Rr02Friend.Rabbit, 400000L), (Rr02Friend.Rabbit, 800000L),
                (Rr02Friend.Rabbit, 1200000L), (Rr02Friend.Rabbit, 1600000L), (Rr02Friend.Rabbit, 2000000L),
                (Rr02Friend.Capybara, 2400000L), (Rr02Friend.Fox, 2800000L));
            var round2 = MakeRound(2, new int[0], (Rr02Friend.Rabbit, 0L));
            var run = new Rr02Run(MakeChart(round1, round2));

            run.BeginRepeat();
            for (int i = 0; i < 6; i++) { run.AdvanceTime((long)i * 400000L); run.ProcessPress(Rr02Friend.Rabbit, (long)i * 400000L); }
            run.AdvanceTime(2400000L); run.ProcessPress(Rr02Friend.Capybara, 2400000L); // 숨김6 정답
            run.AdvanceTime(2800000L); run.ProcessPress(Rr02Friend.Fox, 2800000L);      // 숨김7 정답
            run.AdvanceTime(2800000L + PadUs);

            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(1, run.Result.NormalCleared);
        }
    }
}
