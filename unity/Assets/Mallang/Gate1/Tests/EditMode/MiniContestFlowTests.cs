using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;

namespace Mallang.Gate1.Tests.EditMode
{
    public sealed class MiniContestFlowTests
    {
        [Test]
        public void PassingStagesReturnToCountInAndCompletionReachesResult()
        {
            var flow = ReadyFlow();
            flow.StartRun();

            for (var stage = 1; stage <= 2; stage++)
            {
                flow.StartPlaying();
                flow.ResolveStage(Snapshot(RunEndReason.None, stage, 0));
                Assert.That(flow.Phase, Is.EqualTo(MiniContestPhase.StageResult));
                flow.CompleteStageResult();
                Assert.That(flow.Phase, Is.EqualTo(MiniContestPhase.CountIn));
            }

            flow.StartPlaying();
            flow.ResolveStage(Snapshot(RunEndReason.Completed, 3, 4));
            flow.CompleteStageResult();
            Assert.That(flow.Phase, Is.EqualTo(MiniContestPhase.Result));
        }

        [Test]
        public void FailureShowsResultWithoutStartingAnotherStage()
        {
            var flow = ReadyFlow();
            flow.StartRun();
            flow.StartPlaying();

            flow.ResolveStage(Snapshot(RunEndReason.Failed, 0, 1));

            Assert.That(flow.Phase, Is.EqualTo(MiniContestPhase.Result));
        }

        [Test]
        public void FocusLossAlwaysReturnsToCalibration()
        {
            var flow = ReadyFlow();
            flow.StartRun();
            flow.StartPlaying();

            flow.BeginCalibration();

            Assert.That(flow.Phase, Is.EqualTo(MiniContestPhase.Calibrating));
        }

        [Test]
        public void InvalidTransitionFailsClosed()
        {
            var flow = new MiniContestFlow();
            Assert.Throws<System.InvalidOperationException>(() => flow.StartPlaying());
        }

        private static MiniContestFlow ReadyFlow()
        {
            var flow = new MiniContestFlow();
            flow.CompleteCalibration();
            return flow;
        }

        private static RunSnapshot Snapshot(RunEndReason reason, int lastStage, int failStage)
            => new RunSnapshot(reason, lastStage, failStage, 0, 0, 0, 0, 0, 0);
    }
}
