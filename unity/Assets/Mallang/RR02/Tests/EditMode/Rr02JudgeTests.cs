using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L2 탭 판정기. 정밀 제어를 위해 합성 노트 목록을 씁니다. 시간 값은 전부 long 마이크로초입니다.
    [TestFixture]
    public class Rr02JudgeTests
    {
        private const long T = 1000000L; // 기준 목표 시각(소유 구간 음수 방지용 여유)

        private static Rr02Judge SingleNoteJudge(Rr02Friend friend, long targetUs)
        {
            return new Rr02Judge(new List<Rr02Note> { new Rr02Note(0, friend, targetUs) });
        }

        private static Rr02Judge MakeJudge(params Rr02Note[] notes)
        {
            return new Rr02Judge(new List<Rr02Note>(notes));
        }

        // --- 경계 전수 (AC-005, Review Focus 1·2) ---

        [TestCase(-60000L)] // Review Focus 2: 음수 offset 대칭 — 정확히 −60,000µs는 Perfect
        [TestCase(0L)]
        [TestCase(60000L)]
        public void PerfectWindowIsInclusiveAtSixtyMs(long offsetUs)
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + offsetUs);

            Assert.AreEqual(Rr02InputKind.Judged, result.Kind); // AC-005
            Assert.AreEqual(Rr02Judgement.Perfect, result.Judgement);
            Assert.AreEqual(0, result.NoteId);
        }

        [TestCase(-120000L)] // Review Focus 1: 정확히 ±120,000µs는 양끝 포함으로 Good 소비
        [TestCase(-61000L)]
        [TestCase(-60001L)]
        [TestCase(60001L)]
        [TestCase(61000L)]
        [TestCase(120000L)]
        public void GoodWindowIsInclusiveAtOneHundredTwentyMs(long offsetUs)
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + offsetUs);

            Assert.AreEqual(Rr02InputKind.Judged, result.Kind); // AC-005
            Assert.AreEqual(Rr02Judgement.Good, result.Judgement);
        }

        [TestCase(-121000L)] // AC-005: −121ms는 소비하지 않음
        [TestCase(-120001L)] // Review Focus 1: ±120,001µs는 소비하지 않음
        [TestCase(120001L)]
        [TestCase(121000L)]
        public void OffsetsBeyondGoodWindowDoNotConsumeTarget(long offsetUs)
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + offsetUs);

            Assert.AreNotEqual(Rr02InputKind.Judged, result.Kind); // AC-005: 창 밖은 목표 미소비

            if (offsetUs < 0)
            {
                // 이른 미소비 입력 뒤에도 목표는 열려 있어야 합니다(중복 소비 0의 대칭 확인).
                var retry = judge.ProcessPress(Rr02Friend.Rabbit, T);
                Assert.AreEqual(Rr02InputKind.Judged, retry.Kind);
                Assert.AreEqual(Rr02Judgement.Perfect, retry.Judgement);
            }
        }

        [Test]
        public void UnansweredNoteClosesAsMissAfterGoodWindow()
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            judge.AdvanceTime(T + 120001L); // AC-005: +120ms를 "넘긴" 미입력은 Miss

            Assert.AreEqual(Rr02Judgement.Miss, judge.Outcomes[0]);
        }

        [Test]
        public void AdvanceTimeAtExactGoodBoundaryKeepsNoteOpen()
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            judge.AdvanceTime(T + 120000L); // 정확히 +120,000µs까지는 아직 소비 가능해야 합니다.
            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + 120000L);

            Assert.AreEqual(Rr02InputKind.Judged, result.Kind); // AC-005: 소비 창 양끝 포함
            Assert.AreEqual(Rr02Judgement.Good, result.Judgement);
        }

        // --- 중복 소비·거의 동시 두 입력 (AC-004·AC-005, Review Focus 3) ---

        [Test]
        public void NearSimultaneousSecondInputDoesNotReviveConsumedTarget()
        {
            var judge = SingleNoteJudge(Rr02Friend.Rabbit, T);

            var first = judge.ProcessPress(Rr02Friend.Rabbit, T);           // sequence_id 1
            var second = judge.ProcessPress(Rr02Friend.Rabbit, T + 20000L); // sequence_id 2(같은 프레임 가정)

            // Review Focus 3: 호출 순서대로 하나씩 처리하고, 먼저 소비된 목표를 되살리지 않습니다.
            Assert.AreEqual(Rr02InputKind.Judged, first.Kind);
            Assert.AreEqual(Rr02Judgement.Perfect, first.Judgement);
            Assert.AreNotEqual(Rr02InputKind.Judged, second.Kind); // AC-005: 중복 소비 0
            Assert.AreEqual(1, judge.GhostCount);
            Assert.AreEqual(Rr02Judgement.Perfect, judge.Outcomes[0]);
        }

        [Test]
        public void WrongFriendPressClosesTargetAsMissWithoutGhost()
        {
            var judge = SingleNoteJudge(Rr02Friend.Fox, T); // 여우 목표가 열린 상태

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T); // 토끼 패드 0ms

            // AC-004: 첫 입력은 그 목표 Miss 1, ghost 0 (한 입력에 ghost를 더하지 않음)
            Assert.AreEqual(Rr02InputKind.Judged, result.Kind);
            Assert.AreEqual(Rr02Judgement.Miss, result.Judgement);
            Assert.AreEqual(0, result.NoteId);
            Assert.AreEqual(0, judge.GhostCount);
            Assert.AreEqual(Rr02Judgement.Miss, judge.Outcomes[0]);
        }

        [Test]
        public void SecondInputDoesNotReviveTargetClosedByWrongFriend()
        {
            var judge = SingleNoteJudge(Rr02Friend.Fox, T);
            judge.ProcessPress(Rr02Friend.Rabbit, T); // 다른 친구가 목표를 Miss로 닫음

            var second = judge.ProcessPress(Rr02Friend.Fox, T + 20000L); // +20ms 두 번째 입력

            // AC-004: 닫힌 목표를 되살리지 않고 ghost 1
            Assert.AreNotEqual(Rr02InputKind.Judged, second.Kind);
            Assert.AreEqual(1, judge.GhostCount);
            Assert.AreEqual(Rr02Judgement.Miss, judge.Outcomes[0]);
        }

        [Test]
        public void GhostIsCountedWhenNoOpenTargetIsInWindow()
        {
            // 소유 구간 안이지만 ±120ms 안에 열린 목표가 없는 입력은 ghost 1입니다 (FR-004).
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T + 800000L));

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + 400000L); // 두 목표 모두 창 밖

            Assert.AreEqual(Rr02InputKind.Ghost, result.Kind);
            Assert.AreEqual(1, judge.GhostCount);
        }

        // --- 목표 선택 규칙 (FR-004·FR-005 보존 계약) ---

        [Test]
        public void CrossFriendSelectionClosesNearestOpenTargetAsMiss()
        {
            // FR-004: 먼저 ±120ms 안의 열린 목표를 고른 뒤 친구를 비교합니다.
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Fox, T + 400000L));

            var wrong = judge.ProcessPress(Rr02Friend.Fox, T + 50000L); // 창 안 후보는 토끼 목표뿐

            Assert.AreEqual(Rr02InputKind.Judged, wrong.Kind);
            Assert.AreEqual(0, wrong.NoteId); // 토끼 목표가 Miss로 닫힘
            Assert.AreEqual(Rr02Judgement.Miss, wrong.Judgement);
            Assert.AreEqual(0, judge.GhostCount);

            var own = judge.ProcessPress(Rr02Friend.Fox, T + 400000L); // 여우 목표는 그대로 열려 있음
            Assert.AreEqual(Rr02InputKind.Judged, own.Kind);
            Assert.AreEqual(1, own.NoteId);
            Assert.AreEqual(Rr02Judgement.Perfect, own.Judgement);
        }

        [Test]
        public void SelectionPicksSmallestAbsoluteOffsetFirst()
        {
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T + 200000L));

            // offset +90,000 vs −110,000 → 절댓값 최소인 note 0 (FR-004·FR-005)
            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + 90000L);

            Assert.AreEqual(0, result.NoteId);
            Assert.AreEqual(Rr02Judgement.Good, result.Judgement);

            var next = judge.ProcessPress(Rr02Friend.Rabbit, T + 200000L);
            Assert.AreEqual(1, next.NoteId); // 남은 목표는 그대로 열려 있음
        }

        [Test]
        public void SelectionTieOnAbsoluteOffsetPrefersEarlierTarget()
        {
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T + 180000L));

            // offset +90,000 vs −90,000 동률 → 이른 목표 note 0 (FR-005 보존 계약)
            var result = judge.ProcessPress(Rr02Friend.Rabbit, T + 90000L);

            Assert.AreEqual(0, result.NoteId);
        }

        [Test]
        public void SelectionTieOnTargetTimePrefersLowerNoteId()
        {
            // 차트에는 동시목표가 없지만 판정기 계약은 note_id 동률 규칙을 보존합니다 (FR-005).
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T));

            var result = judge.ProcessPress(Rr02Friend.Rabbit, T);

            Assert.AreEqual(0, result.NoteId);
        }

        // --- 소유 구간 (FR-005: 첫 목표 −180ms ~ 마지막 목표 +180ms, 양끝 포함) ---

        [TestCase(820000L, false)]   // 첫 목표 −180,000µs: 구간 내
        [TestCase(819999L, true)]    // 첫 목표 −180,001µs: 구간 밖
        [TestCase(2180000L, false)]  // 마지막 목표 +180,000µs: 구간 내
        [TestCase(2180001L, true)]   // 마지막 목표 +180,001µs: 구간 밖
        public void OwnershipSpanIsInclusiveAt180MsPads(long inputUs, bool outside)
        {
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T + 1000000L));

            var result = judge.ProcessPress(Rr02Friend.Rabbit, inputUs);

            if (outside)
            {
                Assert.AreEqual(Rr02InputKind.OutsideOwnership, result.Kind); // FR-005
                Assert.AreEqual(0, judge.GhostCount); // 소유 경계 밖 입력은 무효(ghost 아님)
            }
            else
            {
                Assert.AreNotEqual(Rr02InputKind.OutsideOwnership, result.Kind);
            }
        }

        [Test]
        public void CompletedBecomesTrueAtLastTargetPlus180Ms()
        {
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Rabbit, T + 1000000L));

            // AC-007: 입력 마지막 목표 +180ms에 확정합니다.
            Assert.IsFalse(judge.Completed(T + 1000000L + 179999L));
            Assert.IsTrue(judge.Completed(T + 1000000L + 180000L));
        }

        [Test]
        public void OutcomesReportFinalJudgementsInNoteIdOrder()
        {
            var judge = MakeJudge(
                new Rr02Note(0, Rr02Friend.Rabbit, T),
                new Rr02Note(1, Rr02Friend.Capybara, T + 400000L),
                new Rr02Note(2, Rr02Friend.Rabbit, T + 800000L));

            judge.ProcessPress(Rr02Friend.Rabbit, T);                      // Perfect
            judge.AdvanceTime(T + 800000L);                                // note 1 미입력 → Miss로 닫힘
            judge.ProcessPress(Rr02Friend.Rabbit, T + 800000L + 100000L); // Good
            judge.AdvanceTime(T + 800000L + 180000L);

            CollectionAssert.AreEqual(
                new[] { Rr02Judgement.Perfect, Rr02Judgement.Miss, Rr02Judgement.Good },
                new List<Rr02Judgement>(judge.Outcomes)); // note_id 순 최종 판정
            Assert.AreEqual(0, judge.GhostCount);
        }
    }
}
