using System;
using System.Collections.Generic;
using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;

namespace Mallang.Gate1.Tests.EditMode
{
    public sealed class Gate1LocalRunLogTests
    {
        [Test]
        public void RunEndIncludesJudgementTallyAndKeepsTraineesSynthetic()
        {
            var lines = new List<string>();
            var log = new Gate1LocalRunLog(lines.Add, () => "run-1");
            var chart = Gate1ChartFactory.CreateMiniContest();

            Assert.That(log.StartRun(chart), Is.True);
            log.CountJudgements(new[]
            {
                new JudgementEvent(JudgementEventKind.Note, 1, Hand.Left, Judgement.Perfect, 1, 100),
                new JudgementEvent(JudgementEventKind.Note, 2, Hand.Right, Judgement.Good, 2, 70),
                new JudgementEvent(JudgementEventKind.Note, 3, Hand.Left, Judgement.Miss, 0, 0),
                new JudgementEvent(JudgementEventKind.GhostMiss, -1, Hand.Right, Judgement.Miss, 0, 0)
            });
            Assert.That(log.EndRun(new RunSnapshot(RunEndReason.Failed, 0, 1, 170, 140, 170, 3500, 0, 1), 12.345), Is.True);

            Assert.That(lines.Count, Is.EqualTo(2));
            StringAssert.Contains("\"event_name\":\"run_end\"", lines[1]);
            StringAssert.Contains("\"perfect_count\":1", lines[1]);
            StringAssert.Contains("\"good_count\":1", lines[1]);
            StringAssert.Contains("\"miss_count\":1", lines[1]);
            StringAssert.Contains("\"ghost_miss_count\":1", lines[1]);
            StringAssert.Contains("\"eligible_human_run\":1", lines[1]);
            StringAssert.Contains("\"synthetic_trainee_count\":19", lines[1]);
        }

        [Test]
        public void StorageFailureReturnsFalseAndDoesNotThrow()
        {
            var log = new Gate1LocalRunLog(_ => throw new InvalidOperationException("disk unavailable"), () => "run-1", _ => { });

            var saved = log.StartRun(Gate1ChartFactory.CreateMiniContest());

            Assert.That(saved, Is.False);
            Assert.That(log.LastError, Is.EqualTo("disk unavailable"));
        }

        [Test]
        public void RetryRunLinksToThePreviousRun()
        {
            var lines = new List<string>();
            var ids = new Queue<string>(new[] { "run-1", "run-2" });
            var log = new Gate1LocalRunLog(lines.Add, ids.Dequeue);
            var chart = Gate1ChartFactory.CreateMiniContest();
            log.StartRun(chart);
            var firstRunId = log.CurrentRunId;
            log.StartRun(chart, firstRunId);

            StringAssert.Contains("\"run_id\":\"run-2\"", lines[1]);
            StringAssert.Contains("\"retry_of_run_id\":\"run-1\"", lines[1]);
        }

        [Test]
        public void TechnicalInvalidRunIsNotCountedAsAnEligibleHumanRun()
        {
            var lines = new List<string>();
            var log = new Gate1LocalRunLog(lines.Add, () => "run-invalid");
            log.StartRun(Gate1ChartFactory.CreateMiniContest());

            log.EndRun(new RunSnapshot(RunEndReason.Invalid, 1, 2, 100, 100, 100, 5000, 0, 0), 4);

            StringAssert.Contains("\"end_reason\":\"invalid\"", lines[1]);
            StringAssert.Contains("\"eligible_human_run\":0", lines[1]);
        }
    }
}
