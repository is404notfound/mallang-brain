using System.Collections.Generic;
using Mallang.RhythmCore;
using NUnit.Framework;

namespace Mallang.Gate1.Tests.EditMode
{
    public sealed class MiniContestRosterTests
    {
        [Test]
        public void DefaultRosterHasTheFixedNineteenTraineeCurve()
        {
            var roster = Runtime.MiniContestRoster.CreateDefault();

            Assert.That(roster.Validate(out var error), Is.True, error);
            Assert.That(roster.Trainees.Count, Is.EqualTo(19));
            Assert.That(CountFailures(roster, 1), Is.EqualTo(8));
            Assert.That(CountFailures(roster, 2), Is.EqualTo(6));
            Assert.That(CountFailures(roster, 3), Is.EqualTo(3));
            Assert.That(CountCompletions(roster), Is.EqualTo(2));
        }

        [Test]
        public void DefaultRosterIsDeterministicAcrossRetries()
        {
            var first = Runtime.MiniContestRoster.CreateDefault();
            var second = Runtime.MiniContestRoster.CreateDefault();

            for (var index = 0; index < first.Trainees.Count; index++)
            {
                Assert.That(second.Trainees[index].ParticipantId, Is.EqualTo(first.Trainees[index].ParticipantId));
                Assert.That(second.Trainees[index].AccuracyBasisPoints, Is.EqualTo(first.Trainees[index].AccuracyBasisPoints));
                Assert.That(second.Trainees[index].RankScore, Is.EqualTo(first.Trainees[index].RankScore));
            }
        }

        [Test]
        public void ExactPerformanceTieSharesDisplayRank()
        {
            var roster = Runtime.MiniContestRoster.CreateDefault();
            var tied = roster.Trainees[17];
            var player = Snapshot(tied.EndReason, tied.LastCompletedStage, tied.FailStage, tied.AccuracyBasisPoints, tied.RankScore);

            Assert.That(roster.TryBuildStandings(player, out var standings, out var error), Is.True, error);
            var playerRank = FindRank(standings, "player");
            var traineeRank = FindRank(standings, tied.ParticipantId);

            Assert.That(playerRank, Is.EqualTo(traineeRank));
        }

        [Test]
        public void ParticipantIdOnlyStabilizesDrawOrderAndDoesNotBreakTies()
        {
            var roster = Runtime.MiniContestRoster.CreateDefault();
            var tied = roster.Trainees[18];
            var player = Snapshot(tied.EndReason, tied.LastCompletedStage, tied.FailStage, tied.AccuracyBasisPoints, tied.RankScore);

            Assert.That(roster.TryBuildStandings(player, out var standings, out var error), Is.True, error);
            Assert.That(FindRank(standings, "player"), Is.EqualTo(FindRank(standings, tied.ParticipantId)));
            Assert.That(FindIndex(standings, "player"), Is.Not.EqualTo(FindIndex(standings, tied.ParticipantId)));
        }

        [Test]
        public void InvalidRosterDisablesRankingWithoutThrowing()
        {
            var records = new List<Runtime.MiniContestRecord>(Runtime.MiniContestRoster.CreateDefault().Trainees);
            records.RemoveAt(records.Count - 1);
            var roster = new Runtime.MiniContestRoster(records);

            var ranked = roster.TryGetPlayerRank(Snapshot(RunEndReason.Failed, 0, 1, 0, 0), out var rank, out var error);

            Assert.That(ranked, Is.False);
            Assert.That(rank, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo("trainee-count"));
        }

        [TestCase(0, 20)]
        [TestCase(1, 12)]
        [TestCase(2, 6)]
        [TestCase(3, 3)]
        public void RemainingCountMatchesTheEliminationPresentation(int completedStage, int expected)
        {
            Assert.That(Runtime.MiniContestRoster.RemainingParticipantsAfterStage(completedStage), Is.EqualTo(expected));
        }

        private static RunSnapshot Snapshot(RunEndReason endReason, int lastStage, int failStage, int accuracy, int score)
            => new RunSnapshot(endReason, lastStage, failStage, score, score, score, accuracy, 0, 0);

        private static int CountFailures(Runtime.MiniContestRoster roster, int stage)
        {
            var count = 0;
            foreach (var record in roster.Trainees) if (record.EndReason == RunEndReason.Failed && record.FailStage == stage) count++;
            return count;
        }

        private static int CountCompletions(Runtime.MiniContestRoster roster)
        {
            var count = 0;
            foreach (var record in roster.Trainees) if (record.EndReason == RunEndReason.Completed) count++;
            return count;
        }

        private static int FindRank(IReadOnlyList<Runtime.MiniContestStanding> standings, string participantId)
        {
            foreach (var row in standings) if (row.ParticipantId == participantId) return row.DisplayRank;
            return 0;
        }

        private static int FindIndex(IReadOnlyList<Runtime.MiniContestStanding> standings, string participantId)
        {
            for (var index = 0; index < standings.Count; index++) if (standings[index].ParticipantId == participantId) return index;
            return -1;
        }
    }
}
