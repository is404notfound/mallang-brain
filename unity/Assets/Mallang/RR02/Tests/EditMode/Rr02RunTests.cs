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

        [Test]
        public void ClearingRoundNineEntersInfiniteLoopCounting()
        {
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);

            for (int r = 0; r < 9; r++)
                ClearPhrase(run, chart.Rounds[r].Notes);

            // AC-013: R9 성공은 All — 도전은 끝나지 않고 무한 구절로 이어집니다.
            Assert.AreEqual(9, run.NormalCleared);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            Assert.AreEqual(0, run.Loops);

            ClearPhrase(run, chart.InfinitePhrase(1).Notes); // 무한 k=1 = R2 패턴
            Assert.AreEqual(1, run.Loops); // AC-013: 구절 성공마다 무한 완료 수 +1

            ClearPhrase(run, chart.InfinitePhrase(2).Notes); // 무한 k=2 = R3 패턴
            Assert.AreEqual(2, run.Loops);

            // 다음 무한 구절을 무입력으로 실패시켜 도전을 종료합니다. 진행 중 구절은 Loops에 넣지 않습니다.
            run.BeginRepeat();
            var failing = chart.InfinitePhrase(3).Notes;
            run.AdvanceTime(failing[failing.Count - 1].TargetUs + PadUs);

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(9, run.Result.NormalCleared);
            Assert.AreEqual(2, run.Result.Loops); // AC-013: All+2 = 완료한 무한 구절만
            // FR-016 기준안: All Perfect는 일반 9라운드 72판정 기준이며 무한 구간은 포함하지 않습니다.
            Assert.IsTrue(run.Result.AllPerfect);
        }

        [Test]
        public void AllPerfectIsFalseWhenAnyNormalJudgementIsGood()
        {
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);

            ClearPhrase(run, chart.Rounds[0].Notes, goodIndex: 3); // R1에 Good 1개
            for (int r = 1; r < 9; r++)
                ClearPhrase(run, chart.Rounds[r].Notes);

            run.BeginRepeat(); // 무한 첫 구절 무입력 실패로 종료
            var inf = chart.InfinitePhrase(1).Notes;
            run.AdvanceTime(inf[inf.Count - 1].TargetUs + PadUs);

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(9, run.Result.NormalCleared);
            Assert.IsFalse(run.Result.AllPerfect); // FR-016: 72판정 전부 Perfect일 때만
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
            run.AdvanceTime(1200000L + PadUs);                       // 마지막 노트 미입력 → Miss 0, 구절 실패

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(270, ScoreOf(run.Result.AnimalScores, Rr02Friend.Rabbit)); // AC-006
            Assert.AreEqual(270, run.Result.TotalScore);
            Assert.AreEqual(0, run.Result.NormalCleared); // 실패한 단계는 클리어 수에 넣지 않음
        }

        [Test]
        public void GhostInputScoresZeroAndFailsThePhrase()
        {
            // AC-006: ghost는 0점이며 그 단계를 실패시킵니다. 올바른 입력 점수는 포함합니다(AC-008).
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
            Assert.AreEqual(Rr02Phase.Ended, run.Phase); // 전 목표 성공이어도 ghost가 있으면 단계 실패
            Assert.AreEqual(0, run.Result.NormalCleared);
            Assert.AreEqual(400, ScoreOf(run.Result.AnimalScores, Rr02Friend.Rabbit));
            Assert.AreEqual(0, ScoreOf(run.Result.AnimalScores, Rr02Friend.Capybara)); // ghost 0점
            Assert.AreEqual(400, run.Result.TotalScore);
        }

        [Test]
        public void MidPhraseMissKeepsMusicRunningAndEndsOnceAtPhraseEnd()
        {
            // AC-008: 길이 3 입력 중 2번째 Miss — 음악은 계속, 마지막 목표 +180ms에 종료 1회.
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

            run.AdvanceTime(800000L + PadUs); // 종료는 마지막 목표 +180ms에 1회
            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(0, run.Result.NormalCleared); // 완료 단계는 직전까지
            Assert.AreEqual(200, run.Result.TotalScore);  // 올바른 입력 점수 포함

            run.AdvanceTime(5000000L); // 추가 진행 호출이 상태·결과를 바꾸지 않습니다.
            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            Assert.AreEqual(200, run.Result.TotalScore);
        }

        [Test]
        public void InvalidateOfficialKeepsProgressAndRestartsSameListen()
        {
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);
            ClearPhrase(run, chart.Rounds[0].Notes); // R1 성공(토끼 400·카피바라 400)

            run.BeginRepeat(); // R2 따라치기 진입
            run.ProcessPress(Rr02Friend.Capybara, 0L); // R2 첫 노트(카피바라) Perfect
            run.InvalidateOfficial(Rr02PracticeReason.Pause);

            // AC-009·MCH-007: 중단은 공식 자격만 상실, 완료 단계 유지, 같은 라운드 듣기부터 재개.
            Assert.IsFalse(run.OfficialEligible);
            Assert.AreEqual(1, run.NormalCleared);
            Assert.AreEqual(2, run.CurrentRound);
            Assert.AreEqual(Rr02Phase.Listen, run.Phase);
            // 미완료 구절 입력은 폐기 — R2에서 친 카피바라 점수가 남지 않습니다.
            Assert.AreEqual(400, ScoreOf(run.RunAnimalScores, Rr02Friend.Capybara));

            ClearPhrase(run, chart.Rounds[1].Notes); // 연습으로 R2 재도전·성공
            Assert.AreEqual(2, run.NormalCleared);
            Assert.IsFalse(run.OfficialEligible); // 자격은 돌아오지 않습니다
        }

        [Test]
        public void OnlyCorrectlyPressedFriendGainsAnimalScore()
        {
            // FR-025 전제(AC-025): R2 입력 구간에서 토끼만 올바르게 치면 토끼 점수만 증가합니다.
            var chart = LoadRealChart();
            var run = new Rr02Run(chart);
            ClearPhrase(run, chart.Rounds[0].Notes); // R1: 토끼 400·카피바라 400

            run.BeginRepeat();
            // R2 시퀀스에서 토끼는 index 3(1,200,000µs)·index 7(2,800,000µs)입니다.
            run.ProcessPress(Rr02Friend.Rabbit, 1200000L);
            run.ProcessPress(Rr02Friend.Rabbit, 2800000L);
            run.AdvanceTime(2800000L + PadUs); // 나머지 6목표 Miss → 구절 실패 → 종료

            Assert.AreEqual(Rr02Phase.Ended, run.Phase);
            var scores = run.Result.AnimalScores;
            Assert.AreEqual(600, ScoreOf(scores, Rr02Friend.Rabbit));    // 400 + Perfect 2회
            Assert.AreEqual(400, ScoreOf(scores, Rr02Friend.Capybara));  // R1 값에서 변화 없음
            Assert.AreEqual(0, ScoreOf(scores, Rr02Friend.Fox));
            Assert.AreEqual(0, ScoreOf(scores, Rr02Friend.Squirrel));
            Assert.AreEqual(1, run.Result.NormalCleared);
        }
    }
}
