using System.Collections.Generic;
using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Mallang.Gate1.Tests.EditMode
{
    public sealed class ListenRepeatTutorialFlowTests
    {
        [Test]
        public void RabbitAtlasIsAvailableToTheTutorialScene()
        {
            var atlas = Resources.Load<Texture2D>("lop_rabbit_atlas");
            Assert.That(atlas, Is.Not.Null);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas));
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            Assert.That(width, Is.EqualTo(1774));
            Assert.That(height, Is.EqualTo(887));
            Assert.That(atlas.width / (float)atlas.height, Is.EqualTo(2f), "Android import must preserve the atlas aspect ratio.");
        }

        [Test]
        public void ReusesAuthoredPatternsAtTheSameTargetsForDemoAndPlayer()
        {
            var flow = new ListenRepeatTutorialFlow();
            Assert.That(flow.Pattern.Id, Is.EqualTo("LR01"));
            Assert.That(flow.Chart.Stages[0].Bpm, Is.EqualTo(80));
            Assert.That(flow.Chart.Stages[0].PassAccuracyBasisPoints, Is.EqualTo(6000));
            Assert.That(flow.PatternCount, Is.EqualTo(10));
            Assert.That(flow.Chart.Stages[0].Notes.Count, Is.EqualTo(2));
            foreach (var note in flow.Chart.Stages[0].Notes)
                Assert.That(note.TargetUs, Is.EqualTo(PatternCompiler.TickToUs(flow.Pattern.Notes[note.Id - 1].StartTick, 80)));

            flow.BeginDemonstration();
            flow.BeginCountdown();
            flow.BeginPerformance();
            var output = new List<JudgementEvent>();
            foreach (var note in flow.Chart.Stages[0].Notes)
                flow.Press(note.Hand, note.Id, note.TargetUs, output);
            flow.Finish(output);
            Assert.That(flow.Passed, Is.True);
            Assert.That(flow.Result.AccuracyBasisPoints, Is.EqualTo(10000));

            flow.BeginTransition();
            Assert.That(flow.NextPattern(), Is.True);
            Assert.That(flow.Pattern.Id, Is.EqualTo("LR02"));
            Assert.That(flow.Chart.Stages[0].Notes.Count, Is.EqualTo(4));
            Assert.That(flow.Result.AccuracyBasisPoints, Is.Zero);
        }

        [Test]
        public void DemoAndCountdownInputsNeverEnterJudgement()
        {
            var flow = new ListenRepeatTutorialFlow();
            var output = new List<JudgementEvent>();
            flow.BeginDemonstration();
            flow.Press(Hand.Left, 1, 0, output);
            flow.BeginCountdown();
            flow.Press(Hand.Right, 2, 0, output);
            Assert.That(output, Is.Empty);

            flow.BeginPerformance();
            flow.Finish(output);
            Assert.That(flow.Result.AccuracyBasisPoints, Is.Zero);
            Assert.That(flow.Passed, Is.False);
            Assert.That(output.FindAll(item => item.Kind == JudgementEventKind.Note && item.Judgement == Judgement.Miss).Count,
                Is.EqualTo(2));
            Assert.That(flow.BrainScale, Is.LessThan(ListenRepeatTutorialFlow.InitialBrainScale));
        }

        [Test]
        public void SuccessGrowsBrainAndReplayResetsProgressAndScore()
        {
            var flow = new ListenRepeatTutorialFlow();
            var output = new List<JudgementEvent>();
            flow.BeginDemonstration();
            flow.BeginCountdown();
            flow.BeginPerformance();
            foreach (var note in flow.Chart.Stages[0].Notes)
                flow.Press(note.Hand, note.Id, note.TargetUs, output);
            flow.Finish(output);
            Assert.That(flow.BrainScale, Is.GreaterThan(ListenRepeatTutorialFlow.InitialBrainScale));
            flow.BeginTransition();
            flow.NextPattern();
            flow.BeginDemonstration();
            flow.BeginCountdown();
            flow.BeginPerformance();
            flow.Finish(output);
            flow.BeginTransition();
            Assert.That(flow.Phase, Is.EqualTo(TutorialPhase.Transition));
            flow.Replay();
            Assert.That(flow.Phase, Is.EqualTo(TutorialPhase.PatternReady));
            Assert.That(flow.PatternIndex, Is.Zero);
            Assert.That(flow.BrainScale, Is.EqualTo(ListenRepeatTutorialFlow.InitialBrainScale));
            Assert.That(flow.Result.AccuracyBasisPoints, Is.Zero);
            Assert.That(flow.SetCombo, Is.Zero);
        }

        [Test]
        public void TenTapPatternsGrowBrainWithSetComboAndRespectHeadLimit()
        {
            var flow = new ListenRepeatTutorialFlow();
            var output = new List<JudgementEvent>();
            for (var index = 0; index < 10; index++)
            {
                foreach (var note in flow.Chart.Stages[0].Notes) Assert.That(note.Kind, Is.EqualTo(NoteKind.Tap));
                flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
                foreach (var note in flow.Chart.Stages[0].Notes) flow.Press(note.Hand, note.Id, note.TargetUs, output);
                flow.Finish(output);
                Assert.That(flow.SetCombo, Is.EqualTo(index + 1));
                Assert.That(flow.BrainScale, Is.LessThanOrEqualTo(ListenRepeatTutorialFlow.MaximumBrainScale));
                flow.BeginTransition();
                Assert.That(flow.NextPattern(), Is.EqualTo(index < 9));
            }
            Assert.That(flow.BrainScale, Is.EqualTo(ListenRepeatTutorialFlow.MaximumBrainScale).Within(0.00001f));
            flow.Replay();
            Assert.That(flow.SetCombo, Is.Zero);
        }

        [Test]
        public void ExactlySeventyPercentContinuesComboAndBelowSeventyBreaksIt()
        {
            var flow = new ListenRepeatTutorialFlow();
            var output = new List<JudgementEvent>();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            foreach (var note in flow.Chart.Stages[0].Notes) flow.Press(note.Hand, note.Id, note.TargetUs + 80000, output);
            flow.Finish(output);
            Assert.That(flow.Result.AccuracyBasisPoints, Is.EqualTo(7000));
            Assert.That(flow.SetCombo, Is.EqualTo(1));
            var size = flow.BrainScale;
            flow.BeginTransition(); flow.NextPattern();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            flow.Finish(output);
            Assert.That(flow.SetCombo, Is.Zero);
            Assert.That(flow.BrainScale, Is.LessThan(size));
        }

        [Test]
        public void SixtyPercentGrowsBrainAndLowerAccuracyBreaksSetCombo()
        {
            var flow = new ListenRepeatTutorialFlow();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            foreach (var note in flow.Chart.Stages[0].Notes) flow.Press(note.Hand, note.Id, note.TargetUs, null);
            flow.Finish(null); flow.BeginTransition(); flow.NextPattern();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            var notes = flow.Chart.Stages[0].Notes;
            var size = flow.BrainScale;
            flow.Press(notes[0].Hand, 1, notes[0].TargetUs, null);
            flow.Press(notes[1].Hand, 2, notes[1].TargetUs + 80000, null);
            flow.Press(notes[2].Hand, 3, notes[2].TargetUs + 80000, null);
            flow.Finish(null);
            Assert.That(flow.Result.AccuracyBasisPoints, Is.EqualTo(6000));
            Assert.That(flow.Passed, Is.True);
            Assert.That(flow.SetCombo, Is.EqualTo(2));
            Assert.That(flow.BrainScale, Is.GreaterThan(size));
            flow.BeginTransition(); flow.NextPattern();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            size = flow.BrainScale;
            flow.Finish(null);
            Assert.That(flow.Passed, Is.False);
            Assert.That(flow.SetCombo, Is.Zero);
            Assert.That(flow.BrainScale, Is.LessThan(size));
        }

        [Test]
        public void FirstTapClaimsNoteAndFreeTapsNeverReduceAccuracy()
        {
            var flow = new ListenRepeatTutorialFlow();
            flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
            var notes = flow.Chart.Stages[0].Notes;
            flow.Press(notes[0].Hand, 1, notes[0].TargetUs + 80000, null);
            flow.Press(notes[0].Hand, 2, notes[0].TargetUs + 81000, null);
            flow.Press(notes[0].Hand, 3, notes[0].TargetUs + 500000, null);
            flow.Press(notes[1].Hand, 4, notes[1].TargetUs, null);
            flow.Press(notes[1].Hand, 5, notes[1].TargetUs + 1000, null);
            flow.Finish(null);
            Assert.That(flow.Result.AccuracyBasisPoints, Is.EqualTo(8500), "First Good and one Perfect must remain 85% despite extra taps.");
            Assert.That(flow.Result.GhostMissCount, Is.Zero);
        }

        [Test]
        public void ExistingJudgeHandlesTwoHandsAtTheSameTimestamp()
        {
            var notes = new[]
            {
                new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap),
                new ChartNote(2, 1, 0, Hand.Right, NoteKind.Tap)
            };
            var chart = new ContestChart("two-hands", 1, new[] { new StageDefinition(1, 80, 6000, 1000000, notes) });
            var run = new ContestRun(chart);
            var output = new List<JudgementEvent>();
            run.ProcessPress(Hand.Left, 1, 0, output);
            run.ProcessPress(Hand.Right, 2, 0, output);
            run.FinalizeCurrentStage(output);
            Assert.That(run.Snapshot().AccuracyBasisPoints, Is.EqualTo(10000));
            Assert.That(run.Snapshot().EndReason, Is.EqualTo(RunEndReason.Completed));
        }

        [Test]
        public void PerfectAndGoodBoundariesRemainTheExistingCoreRules()
        {
            Assert.That(JudgeSingleTap(60000), Is.EqualTo(Judgement.Perfect));
            Assert.That(JudgeSingleTap(60001), Is.EqualTo(Judgement.Good));
            Assert.That(JudgeSingleTap(120000), Is.EqualTo(Judgement.Good));
            Assert.That(JudgeSingleTap(120001), Is.EqualTo(Judgement.Miss));
        }

        private static Judgement JudgeSingleTap(long atUs)
        {
            var chart = new ContestChart("boundary", 1, new[]
            {
                new StageDefinition(1, 80, 0, 1000000, new[] { new ChartNote(1, 1, 0, Hand.Left, NoteKind.Tap) })
            });
            var run = new ContestRun(chart);
            var output = new List<JudgementEvent>();
            run.ProcessPress(Hand.Left, 1, atUs, output);
            foreach (var entry in output) if (entry.Kind == JudgementEventKind.Note) return entry.Judgement;
            return Judgement.Miss;
        }
    }
}
