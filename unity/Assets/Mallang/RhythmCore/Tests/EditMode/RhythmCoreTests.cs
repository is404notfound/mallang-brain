using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mallang.RhythmCore.Tests.EditMode
{
    public sealed class RhythmCoreTests
    {
        [Test]
        public void Gate1FactoryCreatesAValidTwentyStageChart()
        {
            var chart = Gate1ChartFactory.Create();

            Assert.That(chart.Stages.Count, Is.EqualTo(20));
            Assert.That(ChartValidator.Validate(chart).IsValid, Is.True);
            Assert.That(chart.Stages[0].Bpm, Is.EqualTo(80));
            Assert.That(chart.Stages[19].Bpm, Is.EqualTo(140));
        }

        [Test]
        public void MiniContestFactoryCreatesTheThreeSpecifiedStages()
        {
            var chart = Gate1ChartFactory.CreateMiniContest();

            Assert.That(chart.ChartId, Is.EqualTo(Gate1ChartFactory.MiniContestChartId));
            Assert.That(chart.RulesetVersion, Is.EqualTo(Gate1ChartFactory.MiniContestRulesetVersion));
            Assert.That(ChartValidator.Validate(chart).IsValid, Is.True);
            Assert.That(chart.Stages.Count, Is.EqualTo(3));
            Assert.That(chart.Stages[0].Bpm, Is.EqualTo(80));
            Assert.That(chart.Stages[0].PassAccuracyBasisPoints, Is.EqualTo(5000));
            Assert.That(chart.Stages[1].Bpm, Is.EqualTo(90));
            Assert.That(chart.Stages[1].PassAccuracyBasisPoints, Is.EqualTo(6000));
            Assert.That(chart.Stages[2].Bpm, Is.EqualTo(100));
            Assert.That(chart.Stages[2].PassAccuracyBasisPoints, Is.EqualTo(7000));
            Assert.That(chart.Stages[2].Notes, Has.Some.Matches<ChartNote>(note => note.Kind == NoteKind.Hold));
        }

        [Test]
        public void MiniContestWithoutInputFailsStageOne()
        {
            var run = new ContestRun(Gate1ChartFactory.CreateMiniContest());

            run.FinalizeCurrentStage();

            var snapshot = run.Snapshot();
            Assert.That(snapshot.EndReason, Is.EqualTo(RunEndReason.Failed));
            Assert.That(snapshot.FailStage, Is.EqualTo(1));
            Assert.That(snapshot.LastCompletedStage, Is.EqualTo(0));
            Assert.That(snapshot.AccuracyBasisPoints, Is.EqualTo(0));
        }

        [Test]
        public void PerfectReplayCompletesMiniContestAndUsesChartLengthSentinel()
        {
            var chart = Gate1ChartFactory.CreateMiniContest();
            var run = new ContestRun(chart);

            foreach (var stage in chart.Stages)
            {
                PlayStagePerfect(run, stage);
                run.FinalizeCurrentStage();
            }

            var snapshot = run.Snapshot();
            Assert.That(snapshot.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(snapshot.LastCompletedStage, Is.EqualTo(3));
            Assert.That(snapshot.FailStage, Is.EqualTo(4));
            Assert.That(snapshot.AccuracyBasisPoints, Is.EqualTo(10000));
        }

        [TestCase(0, 80, 0)]
        [TestCase(480, 80, 750000)]
        [TestCase(480, 90, 666667)]
        [TestCase(240, 120, 250000)]
        public void TickCompilerUsesDeterministicHalfAwayRounding(int tick, int bpm, long expectedUs)
        {
            Assert.That(Gate1ChartFactory.TickToUs(tick, bpm), Is.EqualTo(expectedUs));
        }

        [TestCase(-5, -1)]
        [TestCase(5, 1)]
        [TestCase(-15, -2)]
        [TestCase(15, 2)]
        public void RoundingMovesHalfValuesAwayFromZero(long numerator, long expected)
        {
            Assert.That(RhythmMath.RoundHalfAwayFromZero(numerator, 10), Is.EqualTo(expected));
        }

        [Test]
        public void ValidatorRejectsDuplicateIdsAndUnsortedNotes()
        {
            var duplicate = ChartWith(new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap), new ChartNote(1, 1, 480000, Hand.Right, NoteKind.Tap));
            var unsorted = ChartWith(new ChartNote(1, 1, 480000, Hand.Left, NoteKind.Tap), new ChartNote(2, 1, 0, Hand.Right, NoteKind.Tap));

            Assert.That(ChartValidator.Validate(duplicate).Error, Is.EqualTo(ChartValidationError.DuplicateNoteId));
            Assert.That(ChartValidator.Validate(unsorted).Error, Is.EqualTo(ChartValidationError.NoteOrder));
        }

        [Test]
        public void ValidatorRejectsInvalidHoldAndStageBoundary()
        {
            var invalidHold = ChartWith(new ChartNote(1, 1, 0, Hand.Left, NoteKind.Hold, 0));
            var boundary = ChartWith(new[] { new ChartNote(1, 1, 900000, Hand.Left, NoteKind.Tap) }, durationUs: 1000000);

            Assert.That(ChartValidator.Validate(invalidHold).Error, Is.EqualTo(ChartValidationError.HoldEnd));
            Assert.That(ChartValidator.Validate(boundary).Error, Is.EqualTo(ChartValidationError.StageBoundary));
        }

        [Test]
        public void ValidatorRejectsInvalidHandKindAndStageNumber()
        {
            var invalidHand = ChartWith(new ChartNote(1, 1, 0, (Hand)9, NoteKind.Tap));
            var invalidKind = ChartWith(new ChartNote(1, 1, 0, Hand.Left, (NoteKind)9));
            var wrongStage = ChartWith(new[] { new ChartNote(1, 2, 0, Hand.Left, NoteKind.Tap) });

            Assert.That(ChartValidator.Validate(invalidHand).Error, Is.EqualTo(ChartValidationError.NoteRules));
            Assert.That(ChartValidator.Validate(invalidKind).Error, Is.EqualTo(ChartValidationError.NoteRules));
            Assert.That(ChartValidator.Validate(wrongStage).Error, Is.EqualTo(ChartValidationError.NoteRules));
        }

        [Test]
        public void ValidatorRejectsEmptyChartAndNegativeTime()
        {
            var empty = new ContestChart("empty", 1, Array.Empty<StageDefinition>());
            var negative = ChartWith(new ChartNote(1, 1, -1, Hand.Left, NoteKind.Tap));

            Assert.That(ChartValidator.Validate(empty).Error, Is.EqualTo(ChartValidationError.Empty));
            Assert.That(ChartValidator.Validate(negative).Error, Is.EqualTo(ChartValidationError.StageBoundary));
        }

        [Test]
        public void ValidatorRejectsOverlappingSameHandHolds()
        {
            var chart = ChartWith(new[]
            {
                new ChartNote(1, 1, 0, Hand.Left, NoteKind.Hold, 600000),
                new ChartNote(2, 1, 480000, Hand.Left, NoteKind.Hold, 900000)
            }, durationUs: 1200000);

            Assert.That(ChartValidator.Validate(chart).Error, Is.EqualTo(ChartValidationError.OverlappingHold));
        }

        [TestCase(-121000, JudgementEventKind.GhostMiss, Judgement.Miss)]
        [TestCase(-120000, JudgementEventKind.Note, Judgement.Good)]
        [TestCase(-60000, JudgementEventKind.Note, Judgement.Perfect)]
        [TestCase(0, JudgementEventKind.Note, Judgement.Perfect)]
        [TestCase(60000, JudgementEventKind.Note, Judgement.Perfect)]
        [TestCase(120000, JudgementEventKind.Note, Judgement.Good)]
        [TestCase(121000, JudgementEventKind.Note, Judgement.Miss)]
        public void TapWindowsConsumeOnlyInputsInsideTheGoodWindow(long offsetUs, JudgementEventKind expectedKind, Judgement expectedJudgement)
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 1, 500000 + offsetUs, events);

            Assert.That(events[0].Kind, Is.EqualTo(expectedKind));
            Assert.That(events[0].Judgement, Is.EqualTo(expectedJudgement));
        }

        [Test]
        public void PressChoosesTheNearestMatchingCue()
        {
            var run = new ContestRun(ChartWith(
                new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap),
                new ChartNote(2, 1, 550000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 1, 530000, events);

            Assert.That(events[0].NoteId, Is.EqualTo(2));
        }

        [Test]
        public void EqualDistanceChoosesTheEarlierCueBeforeNoteId()
        {
            var run = new ContestRun(ChartWith(
                new ChartNote(20, 1, 480000, Hand.Left, NoteKind.Tap),
                new ChartNote(10, 1, 520000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 1, 500000, events);

            Assert.That(events[0].NoteId, Is.EqualTo(20));
        }

        [Test]
        public void StageOneWrongHandShowsGuidanceWithoutConsumingTheCue()
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Right, 1, 500000, events);
            run.ProcessPress(Hand.Left, 2, 500000, events);

            Assert.That(events[0].Kind, Is.EqualTo(JudgementEventKind.Guidance));
            Assert.That(events[1].Judgement, Is.EqualTo(Judgement.Perfect));
            Assert.That(run.Snapshot().GhostMissCount, Is.EqualTo(0));
        }

        [Test]
        public void StageTwoWrongHandClosesTheCueAsMiss()
        {
            var firstStage = new StageDefinition(1, 80, 5000, 1000000, new[] { new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap) });
            var secondStage = new StageDefinition(2, 80, 5000, 1000000, new[] { new ChartNote(2, 2, 0, Hand.Left, NoteKind.Tap) });
            var run = new ContestRun(new ContestChart("two-stage", 1, new[] { firstStage, secondStage }));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 1, 0, events);
            run.FinalizeCurrentStage(events);
            events.Clear();
            run.ProcessPress(Hand.Right, 1, 0, events);
            run.ProcessPress(Hand.Left, 2, 0, events);

            Assert.That(events[0].Judgement, Is.EqualTo(Judgement.Miss));
            Assert.That(events[1].Kind, Is.EqualTo(JudgementEventKind.GhostMiss));
        }

        [Test]
        public void AdvanceTimeClosesAnOverdueCueImmediatelyAndOnlyOnce()
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.AdvanceTime(500000 + ContestRun.GoodWindowUs, events);
            Assert.That(events, Is.Empty, "The good-window boundary is still playable.");

            run.AdvanceTime(500000 + ContestRun.GoodWindowUs + 1, events);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Judgement, Is.EqualTo(Judgement.Miss));
            Assert.That(run.Snapshot().AccuracyBasisPoints, Is.EqualTo(0));

            events.Clear();
            run.AdvanceTime(800000, events);
            Assert.That(events, Is.Empty, "A closed cue must not emit duplicate misses.");
        }

        [Test]
        public void AdvanceTimeClosesAnOverdueHoldImmediately()
        {
            var run = new ContestRun(ChartWith(new[] { new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Hold, 1000000) }, durationUs: 1400000));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 7, 500000, events);
            events.Clear();
            run.AdvanceTime(1000000 + ContestRun.HoldLateGoodWindowUs + 1, events);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Judgement, Is.EqualTo(Judgement.Miss));
            Assert.That(run.Snapshot().Combo, Is.EqualTo(0));
        }

        [TestCase(-120000, Judgement.Miss, 50)]
        [TestCase(-60000, Judgement.Perfect, 100)]
        [TestCase(0, Judgement.Perfect, 100)]
        [TestCase(60000, Judgement.Perfect, 100)]
        [TestCase(180000, Judgement.Good, 85)]
        [TestCase(181000, Judgement.Miss, 50)]
        public void HoldReleaseUsesTheSpecifiedAsymmetricWindow(long releaseOffsetUs, Judgement expected, int expectedBaseScore)
        {
            var run = new ContestRun(ChartWith(new[] { new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Hold, 1000000) }, durationUs: 1400000));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 12, 500000, events);
            run.ProcessRelease(12, 1000000 + releaseOffsetUs, events);

            Assert.That(events[events.Count - 1].Judgement, Is.EqualTo(expected));
            Assert.That(run.Snapshot().BaseScore, Is.EqualTo(expectedBaseScore));
        }

        [Test]
        public void UnmatchedReleaseDoesNotChangeScoreOrCombo()
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            var events = new List<JudgementEvent>();

            run.ProcessRelease(99, 0, events);

            Assert.That(events[0].Kind, Is.EqualTo(JudgementEventKind.UnmatchedRelease));
            Assert.That(run.Snapshot().BaseScore, Is.EqualTo(0));
            Assert.That(run.Snapshot().Combo, Is.EqualTo(0));
        }

        [Test]
        public void CancelClosesHoldOnlyOnce()
        {
            var run = new ContestRun(ChartWith(new[] { new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Hold, 1000000) }, durationUs: 1400000));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 7, 500000, events);
            run.ProcessCancel(7, 700000, events);
            run.ProcessCancel(7, 800000, events);

            Assert.That(run.Snapshot().BaseScore, Is.EqualTo(50));
            Assert.That(events[events.Count - 1].Kind, Is.EqualTo(JudgementEventKind.UnmatchedRelease));
        }

        [Test]
        public void MissedHoldStartCannotEarnAReleaseScore()
        {
            var run = new ContestRun(ChartWith(new[] { new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Hold, 1000000) }, durationUs: 1400000));
            var events = new List<JudgementEvent>();

            run.ProcessPress(Hand.Left, 3, 621000, events);
            run.ProcessRelease(3, 1000000, events);

            Assert.That(run.Snapshot().BaseScore, Is.EqualTo(0));
            Assert.That(events[events.Count - 1].Kind, Is.EqualTo(JudgementEventKind.UnmatchedRelease));
        }

        [Test]
        public void ComboThresholdsAffectRankScoreButNotAccuracy()
        {
            var notes = new List<ChartNote>();
            for (var index = 0; index < 40; index++) notes.Add(new ChartNote(index + 1, 1, index * 300000L, Hand.Left, NoteKind.Tap));
            var run = new ContestRun(ChartWith(notes.ToArray(), durationUs: 12500000));

            for (var index = 0; index < 40; index++) run.ProcessPress(Hand.Left, index, index * 300000L);
            var snapshot = run.Snapshot();

            Assert.That(snapshot.BaseScore, Is.EqualTo(4000));
            Assert.That(snapshot.AccuracyBasisPoints, Is.EqualTo(10000));
            Assert.That(snapshot.RankScore, Is.EqualTo(4650));
            Assert.That(snapshot.Combo, Is.EqualTo(40));
        }

        [TestCase(9, 900)]
        [TestCase(10, 1010)]
        [TestCase(19, 2000)]
        [TestCase(20, 2125)]
        [TestCase(39, 4500)]
        [TestCase(40, 4650)]
        public void ComboMultiplierChangesAtEverySpecifiedBoundary(int noteCount, int expectedRankScore)
        {
            var notes = new ChartNote[noteCount];
            for (var index = 0; index < noteCount; index++) notes[index] = new ChartNote(index + 1, 1, index * 250000L, Hand.Left, NoteKind.Tap);
            var run = new ContestRun(ChartWith(notes, durationUs: noteCount * 250000L + 200000));

            for (var index = 0; index < noteCount; index++) run.ProcessPress(Hand.Left, index, index * 250000L);

            Assert.That(run.Snapshot().RankScore, Is.EqualTo(expectedRankScore));
        }

        [Test]
        public void GhostMissPenaltyCannotMakeAccuracyNegative()
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            for (var index = 0; index < 10; index++) run.ProcessPress(Hand.Right, index, 100000L + index);

            Assert.That(run.Snapshot().AccuracyScore, Is.EqualTo(0));
            Assert.That(run.Snapshot().RankScore, Is.EqualTo(0));
        }

        [Test]
        public void ExactPassThresholdPassesAndOneBasisPointBelowFails()
        {
            var passingRun = new ContestRun(ChartWith(new[]
            {
                new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap),
                new ChartNote(2, 1, 500000, Hand.Right, NoteKind.Tap)
            }, passBasisPoints: 5000));
            passingRun.ProcessPress(Hand.Left, 1, 0);
            passingRun.FinalizeCurrentStage();

            var failingRun = new ContestRun(ChartWith(new[]
            {
                new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap),
                new ChartNote(2, 1, 500000, Hand.Right, NoteKind.Tap)
            }, passBasisPoints: 5001));
            failingRun.ProcessPress(Hand.Left, 1, 0);
            failingRun.FinalizeCurrentStage();

            Assert.That(passingRun.Snapshot().EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(failingRun.Snapshot().EndReason, Is.EqualTo(RunEndReason.Failed));
        }

        [Test]
        public void PassingStageAdvancesToTheNextStage()
        {
            var first = new StageDefinition(1, 80, 5000, 1000000, new[] { new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap) });
            var second = new StageDefinition(2, 80, 5000, 1000000, new[] { new ChartNote(2, 2, 0, Hand.Right, NoteKind.Tap) });
            var run = new ContestRun(new ContestChart("advance", 1, new[] { first, second }));

            run.ProcessPress(Hand.Left, 1, 0);
            run.FinalizeCurrentStage();

            Assert.That(run.CurrentStageNumber, Is.EqualTo(2));
            Assert.That(run.Snapshot().LastCompletedStage, Is.EqualTo(1));
        }

        [Test]
        public void NextStageDoesNotReduceAccuracyBeforeItHasStarted()
        {
            var first = new StageDefinition(1, 80, 5000, 1000000, new[] { new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap) });
            var second = new StageDefinition(2, 80, 5000, 1000000, new[] { new ChartNote(2, 2, 0, Hand.Right, NoteKind.Tap) });
            var run = new ContestRun(new ContestChart("accuracy", 1, new[] { first, second }));

            run.ProcessPress(Hand.Left, 1, 0);
            run.FinalizeCurrentStage();

            Assert.That(run.Snapshot().AccuracyBasisPoints, Is.EqualTo(10000));
        }

        [Test]
        public void RewoundTimestampInvalidatesTheRun()
        {
            var run = new ContestRun(ChartWith(new ChartNote(1, 1, 500000, Hand.Left, NoteKind.Tap)));
            run.ProcessPress(Hand.Right, 1, 100000);
            run.ProcessPress(Hand.Left, 2, 99999);

            Assert.That(run.Snapshot().EndReason, Is.EqualTo(RunEndReason.Invalid));
        }

        [Test]
        public void SameInputSequenceProducesTheSameSnapshotAndEvents()
        {
            var first = Replay();
            var second = Replay();

            Assert.That(first.snapshot.BaseScore, Is.EqualTo(second.snapshot.BaseScore));
            Assert.That(first.snapshot.RankScore, Is.EqualTo(second.snapshot.RankScore));
            Assert.That(first.snapshot.AccuracyBasisPoints, Is.EqualTo(second.snapshot.AccuracyBasisPoints));
            Assert.That(first.events, Is.EqualTo(second.events));
        }

        [Test]
        public void PerfectReplayCompletesAllTwentyStagesAndEmitsTheStageTwentySentinel()
        {
            var chart = Gate1ChartFactory.Create();
            var run = new ContestRun(chart);

            foreach (var stage in chart.Stages)
            {
                PlayStagePerfect(run, stage);
                run.FinalizeCurrentStage();
            }

            var snapshot = run.Snapshot();
            Assert.That(snapshot.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(snapshot.LastCompletedStage, Is.EqualTo(20));
            Assert.That(snapshot.FailStage, Is.EqualTo(21));
            Assert.That(snapshot.AccuracyBasisPoints, Is.EqualTo(10000));
        }

        // Listen & Repeat minigame: PatternCompiler is additive (Gate1ChartFactory/ContestRun untouched),
        // so every pattern it builds must still satisfy the same validator and judgement core.
        [Test]
        public void AllListenAndRepeatPatternsCompileToValidCharts()
        {
            foreach (var entry in ListenAndRepeatPatterns.All)
            {
                var chart = PatternCompiler.BuildSingleBarChart("test-" + entry.Id, entry.Block, 90);
                var validation = ChartValidator.Validate(chart);
                Assert.That(validation.IsValid, Is.True, entry.Id + " should compile to a valid chart but failed with " + validation.Error);
                Assert.That(chart.Stages.Count, Is.EqualTo(1));
            }
        }

        [TestCase(0, 90, 0)]
        [TestCase(480, 90, 666667)]
        [TestCase(1920, 90, 2666667)]
        public void PatternCompilerUsesTheSameRoundingRuleAsGate1Factory(int tick, int bpm, long expectedUs)
        {
            Assert.That(PatternCompiler.TickToUs(tick, bpm), Is.EqualTo(expectedUs));
            Assert.That(PatternCompiler.TickToUs(tick, bpm), Is.EqualTo(Gate1ChartFactory.TickToUs(tick, bpm)));
        }

        [Test]
        public void PerfectReplayOfEachListenAndRepeatPatternCompletesTheBar()
        {
            foreach (var entry in ListenAndRepeatPatterns.All)
            {
                var chart = PatternCompiler.BuildSingleBarChart("test-" + entry.Id, entry.Block, 90);
                var run = new ContestRun(chart);
                PlayStagePerfect(run, chart.Stages[0]);
                run.FinalizeCurrentStage();

                var snapshot = run.Snapshot();
                Assert.That(snapshot.EndReason, Is.EqualTo(RunEndReason.Completed), entry.Id + " should complete on a perfect replay.");
                Assert.That(snapshot.AccuracyBasisPoints, Is.EqualTo(10000), entry.Id + " should be 100% accurate on a perfect replay.");
            }
        }

        private static (RunSnapshot snapshot, List<JudgementEvent> events) Replay()
        {
            var chart = ChartWith(new[]
            {
                new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap),
                new ChartNote(2, 1, 500000, Hand.Right, NoteKind.Hold, 1000000),
                new ChartNote(3, 1, 1500000, Hand.Left, NoteKind.Tap)
            }, durationUs: 2000000);
            var run = new ContestRun(chart);
            var events = new List<JudgementEvent>();
            run.ProcessPress(Hand.Left, 1, 0, events);
            run.ProcessPress(Hand.Right, 2, 500000, events);
            run.ProcessRelease(2, 1000000, events);
            run.ProcessPress(Hand.Left, 3, 1500000, events);
            run.FinalizeCurrentStage(events);
            return (run.Snapshot(), events);
        }

        private static ContestChart ChartWith(params ChartNote[] notes) => ChartWith(notes, 1, 5000, 2000000);

        private static void PlayStagePerfect(ContestRun run, StageDefinition stage)
        {
            var inputs = new List<ScheduledInput>();
            foreach (var note in stage.Notes)
            {
                inputs.Add(new ScheduledInput(note.TargetUs, false, note.Hand, note.Id));
                if (note.Kind == NoteKind.Hold) inputs.Add(new ScheduledInput(note.EndUs, true, note.Hand, note.Id));
            }

            inputs.Sort((left, right) =>
            {
                var timeOrder = left.TimeUs.CompareTo(right.TimeUs);
                if (timeOrder != 0) return timeOrder;
                return left.IsRelease == right.IsRelease ? 0 : (left.IsRelease ? 1 : -1);
            });

            foreach (var input in inputs)
            {
                if (input.IsRelease) run.ProcessRelease(input.PointerId, input.TimeUs);
                else run.ProcessPress(input.Hand, input.PointerId, input.TimeUs);
            }
        }

        private readonly struct ScheduledInput
        {
            public readonly long TimeUs;
            public readonly bool IsRelease;
            public readonly Hand Hand;
            public readonly int PointerId;

            public ScheduledInput(long timeUs, bool isRelease, Hand hand, int pointerId)
            {
                TimeUs = timeUs;
                IsRelease = isRelease;
                Hand = hand;
                PointerId = pointerId;
            }
        }

        private static ContestChart ChartWith(ChartNote[] notes, int stageNumber = 1, int passBasisPoints = 5000, long durationUs = 2000000)
        {
            var stage = new StageDefinition(stageNumber, 80, passBasisPoints, durationUs, notes);
            return new ContestChart("test", 1, new[] { stage });
        }
    }
}
