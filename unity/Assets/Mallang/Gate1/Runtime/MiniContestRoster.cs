using System;
using System.Collections.Generic;
using Mallang.RhythmCore;

namespace Mallang.Gate1.Runtime
{
    public sealed class MiniContestRecord
    {
        public string ParticipantId { get; }
        public string DisplayName { get; }
        public string Source { get; }
        public string ChartId { get; }
        public int RulesetVersion { get; }
        public string RankRuleset { get; }
        public int RosterVersion { get; }
        public RunEndReason EndReason { get; }
        public int LastCompletedStage { get; }
        public int FailStage { get; }
        public int AccuracyBasisPoints { get; }
        public int RankScore { get; }

        public MiniContestRecord(
            string participantId,
            string displayName,
            string source,
            string chartId,
            int rulesetVersion,
            string rankRuleset,
            int rosterVersion,
            RunEndReason endReason,
            int lastCompletedStage,
            int failStage,
            int accuracyBasisPoints,
            int rankScore)
        {
            ParticipantId = participantId;
            DisplayName = displayName;
            Source = source;
            ChartId = chartId;
            RulesetVersion = rulesetVersion;
            RankRuleset = rankRuleset;
            RosterVersion = rosterVersion;
            EndReason = endReason;
            LastCompletedStage = lastCompletedStage;
            FailStage = failStage;
            AccuracyBasisPoints = accuracyBasisPoints;
            RankScore = rankScore;
        }
    }

    public readonly struct MiniContestStanding
    {
        public readonly string ParticipantId;
        public readonly string DisplayName;
        public readonly string Source;
        public readonly int DisplayRank;
        public readonly RunEndReason EndReason;
        public readonly int LastCompletedStage;
        public readonly int AccuracyBasisPoints;
        public readonly int RankScore;

        public MiniContestStanding(
            string participantId,
            string displayName,
            string source,
            int displayRank,
            RunEndReason endReason,
            int lastCompletedStage,
            int accuracyBasisPoints,
            int rankScore)
        {
            ParticipantId = participantId;
            DisplayName = displayName;
            Source = source;
            DisplayRank = displayRank;
            EndReason = endReason;
            LastCompletedStage = lastCompletedStage;
            AccuracyBasisPoints = accuracyBasisPoints;
            RankScore = rankScore;
        }
    }

    public sealed class MiniContestRoster
    {
        public const int ParticipantCount = 20;
        public const int TraineeCount = 19;
        public const int StageCount = 3;
        public const int RosterVersion = 1;
        public const string RankRuleset = "mini-contest-rank-v1";
        public const string TraineeSource = "trainee";
        public const string PlayerSource = "player";

        private readonly IReadOnlyList<MiniContestRecord> trainees;

        public MiniContestRoster(IReadOnlyList<MiniContestRecord> trainees)
        {
            this.trainees = trainees;
        }

        public IReadOnlyList<MiniContestRecord> Trainees => trainees;

        public static MiniContestRoster CreateDefault()
        {
            var records = new List<MiniContestRecord>(TraineeCount);
            AddFailed(records, 1, 8, new[] { 1200, 1800, 2400, 2900, 3400, 3800, 4300, 4800 }, 180);
            AddFailed(records, 2, 6, new[] { 4200, 4600, 5000, 5300, 5600, 5900 }, 900);
            AddFailed(records, 3, 3, new[] { 6100, 6500, 6900 }, 1900);
            records.Add(CreateRecord(18, RunEndReason.Completed, 3, 4, 8200, 3300));
            records.Add(CreateRecord(19, RunEndReason.Completed, 3, 4, 9400, 4100));
            return new MiniContestRoster(records.AsReadOnly());
        }

        public bool Validate(out string error)
        {
            if (trainees == null || trainees.Count != TraineeCount)
            {
                error = "trainee-count";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var stageOneFailures = 0;
            var stageTwoFailures = 0;
            var stageThreeFailures = 0;
            var completions = 0;

            for (var index = 0; index < trainees.Count; index++)
            {
                var record = trainees[index];
                if (record == null)
                {
                    error = "null-record";
                    return false;
                }
                if (string.IsNullOrEmpty(record.ParticipantId) || !ids.Add(record.ParticipantId))
                {
                    error = "participant-id";
                    return false;
                }
                if (record.DisplayName != "연습생 " + (index + 1).ToString("00") || record.Source != TraineeSource)
                {
                    error = "trainee-label";
                    return false;
                }
                if (record.ChartId != Gate1ChartFactory.MiniContestChartId
                    || record.RulesetVersion != Gate1ChartFactory.MiniContestRulesetVersion
                    || record.RankRuleset != RankRuleset
                    || record.RosterVersion != RosterVersion)
                {
                    error = "version";
                    return false;
                }
                if (record.AccuracyBasisPoints < 0 || record.AccuracyBasisPoints > 10000 || record.RankScore < 0)
                {
                    error = "score-range";
                    return false;
                }
                if (!IsConsistentPerformance(record.EndReason, record.LastCompletedStage, record.FailStage))
                {
                    error = "result-consistency";
                    return false;
                }

                if (record.EndReason == RunEndReason.Completed) completions++;
                else if (record.FailStage == 1) stageOneFailures++;
                else if (record.FailStage == 2) stageTwoFailures++;
                else if (record.FailStage == 3) stageThreeFailures++;
            }

            if (stageOneFailures != 8 || stageTwoFailures != 6 || stageThreeFailures != 3 || completions != 2)
            {
                error = "elimination-curve";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryGetPlayerRank(RunSnapshot player, out int rank, out string error)
        {
            rank = 0;
            if (!Validate(out error)) return false;
            if (!IsConsistentPerformance(player.EndReason, player.LastCompletedStage, player.FailStage)
                || player.AccuracyBasisPoints < 0 || player.AccuracyBasisPoints > 10000 || player.RankScore < 0)
            {
                error = "player-result";
                return false;
            }

            rank = 1;
            foreach (var trainee in trainees)
            {
                if (ComparePerformance(
                    trainee.EndReason,
                    trainee.LastCompletedStage,
                    trainee.AccuracyBasisPoints,
                    trainee.RankScore,
                    player.EndReason,
                    player.LastCompletedStage,
                    player.AccuracyBasisPoints,
                    player.RankScore) < 0)
                    rank++;
            }

            return true;
        }

        public bool TryBuildStandings(RunSnapshot player, out IReadOnlyList<MiniContestStanding> standings, out string error)
        {
            standings = Array.Empty<MiniContestStanding>();
            if (!TryGetPlayerRank(player, out _, out error)) return false;

            var rows = new List<MutableStanding>(ParticipantCount)
            {
                new MutableStanding("player", "나", PlayerSource, player.EndReason, player.LastCompletedStage, player.AccuracyBasisPoints, player.RankScore)
            };
            foreach (var trainee in trainees)
                rows.Add(new MutableStanding(trainee.ParticipantId, trainee.DisplayName, trainee.Source, trainee.EndReason, trainee.LastCompletedStage, trainee.AccuracyBasisPoints, trainee.RankScore));

            rows.Sort((left, right) =>
            {
                var performance = ComparePerformance(left.EndReason, left.LastCompletedStage, left.AccuracyBasisPoints, left.RankScore, right.EndReason, right.LastCompletedStage, right.AccuracyBasisPoints, right.RankScore);
                return performance != 0 ? performance : string.CompareOrdinal(left.ParticipantId, right.ParticipantId);
            });

            var result = new List<MiniContestStanding>(ParticipantCount);
            var displayRank = 1;
            for (var index = 0; index < rows.Count; index++)
            {
                if (index > 0 && ComparePerformance(rows[index - 1].EndReason, rows[index - 1].LastCompletedStage, rows[index - 1].AccuracyBasisPoints, rows[index - 1].RankScore, rows[index].EndReason, rows[index].LastCompletedStage, rows[index].AccuracyBasisPoints, rows[index].RankScore) != 0)
                    displayRank = index + 1;
                var row = rows[index];
                result.Add(new MiniContestStanding(row.ParticipantId, row.DisplayName, row.Source, displayRank, row.EndReason, row.LastCompletedStage, row.AccuracyBasisPoints, row.RankScore));
            }

            standings = result.AsReadOnly();
            return true;
        }

        public static int RemainingParticipantsAfterStage(int completedStage)
        {
            if (completedStage <= 0) return 20;
            if (completedStage == 1) return 12;
            if (completedStage == 2) return 6;
            return 3;
        }

        private static void AddFailed(List<MiniContestRecord> records, int failStage, int count, IReadOnlyList<int> accuracies, int baseScore)
        {
            for (var index = 0; index < count; index++)
            {
                var participantNumber = records.Count + 1;
                records.Add(CreateRecord(participantNumber, RunEndReason.Failed, failStage - 1, failStage, accuracies[index], baseScore + index * 90));
            }
        }

        private static MiniContestRecord CreateRecord(int participantNumber, RunEndReason endReason, int lastCompletedStage, int failStage, int accuracyBasisPoints, int rankScore)
        {
            return new MiniContestRecord(
                "trainee-" + participantNumber.ToString("00"),
                "연습생 " + participantNumber.ToString("00"),
                TraineeSource,
                Gate1ChartFactory.MiniContestChartId,
                Gate1ChartFactory.MiniContestRulesetVersion,
                RankRuleset,
                RosterVersion,
                endReason,
                lastCompletedStage,
                failStage,
                accuracyBasisPoints,
                rankScore);
        }

        private static bool IsConsistentPerformance(RunEndReason endReason, int lastCompletedStage, int failStage)
        {
            if (endReason == RunEndReason.Completed)
                return lastCompletedStage == StageCount && failStage == StageCount + 1;
            if (endReason == RunEndReason.Failed)
                return failStage >= 1 && failStage <= StageCount && lastCompletedStage == failStage - 1;
            return false;
        }

        // Negative means left ranks ahead of right. Participant IDs never affect this comparison.
        private static int ComparePerformance(
            RunEndReason leftEndReason, int leftStage, int leftAccuracy, int leftScore,
            RunEndReason rightEndReason, int rightStage, int rightAccuracy, int rightScore)
        {
            var leftCompleted = leftEndReason == RunEndReason.Completed ? 1 : 0;
            var rightCompleted = rightEndReason == RunEndReason.Completed ? 1 : 0;
            if (leftCompleted != rightCompleted) return rightCompleted.CompareTo(leftCompleted);
            if (leftStage != rightStage) return rightStage.CompareTo(leftStage);
            if (leftAccuracy != rightAccuracy) return rightAccuracy.CompareTo(leftAccuracy);
            return rightScore.CompareTo(leftScore);
        }

        private sealed class MutableStanding
        {
            public readonly string ParticipantId;
            public readonly string DisplayName;
            public readonly string Source;
            public readonly RunEndReason EndReason;
            public readonly int LastCompletedStage;
            public readonly int AccuracyBasisPoints;
            public readonly int RankScore;

            public MutableStanding(string participantId, string displayName, string source, RunEndReason endReason, int lastCompletedStage, int accuracyBasisPoints, int rankScore)
            {
                ParticipantId = participantId;
                DisplayName = displayName;
                Source = source;
                EndReason = endReason;
                LastCompletedStage = lastCompletedStage;
                AccuracyBasisPoints = accuracyBasisPoints;
                RankScore = rankScore;
            }
        }
    }
}
