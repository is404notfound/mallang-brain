using System;
using System.Globalization;
using System.IO;
using System.Text;
using Mallang.RhythmCore;
using UnityEngine;

namespace Mallang.Gate1.Runtime
{
    // Best-effort local Gate 1 telemetry. A storage failure is reported to the screen but never blocks play.
    public sealed class Gate1LocalRunLog
    {
        private sealed class LogEntry
        {
            public string event_name;
            public string occurred_at_utc;
            public string run_id;
            public string retry_of_run_id;
            public string chart_id;
            public int ruleset_version;
            public int roster_version;
            public string rank_ruleset;
            public int eligible_human_run;
            public int synthetic_trainee_count;
            public int stage;
            public int remaining_participants;
            public string end_reason;
            public long duration_ms;
            public int accuracy_basis_points;
            public int rank_score;
            public int perfect_count;
            public int good_count;
            public int miss_count;
            public int ghost_miss_count;
            public string action;
        }

        private readonly Action<string> writeLine;
        private readonly Func<string> createRunId;
        private readonly Action<string> reportWarning;
        private string runId;
        private string retryOfRunId;
        private string chartId;
        private int rulesetVersion;
        private int perfectCount;
        private int goodCount;
        private int missCount;
        private int ghostMissCount;

        public Gate1LocalRunLog(Action<string> writeLineOverride = null, Func<string> runIdFactory = null, Action<string> warningOverride = null)
        {
            writeLine = writeLineOverride ?? WriteToDefaultFile;
            createRunId = runIdFactory ?? (() => Guid.NewGuid().ToString("N"));
            reportWarning = warningOverride ?? (message => Debug.LogWarning(message));
        }

        public string CurrentRunId => runId;
        public string LastError { get; private set; }

        public bool StartRun(ContestChart chart, string retryOf = null)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            runId = createRunId();
            retryOfRunId = retryOf;
            chartId = chart.ChartId;
            rulesetVersion = chart.RulesetVersion;
            perfectCount = 0;
            goodCount = 0;
            missCount = 0;
            ghostMissCount = 0;
            return Write(NewEntry("run_start"));
        }

        public void CountJudgements(System.Collections.Generic.IReadOnlyList<JudgementEvent> events)
        {
            if (events == null) return;
            foreach (var entry in events)
            {
                if (entry.Kind == JudgementEventKind.GhostMiss)
                {
                    ghostMissCount++;
                    continue;
                }
                if (entry.Kind != JudgementEventKind.Note) continue;
                if (entry.Judgement == Judgement.Perfect) perfectCount++;
                else if (entry.Judgement == Judgement.Good) goodCount++;
                else missCount++;
            }
        }

        public bool RecordCrowdDrop(int completedStage, int remainingParticipants)
        {
            var entry = NewEntry("crowd_drop");
            entry.stage = completedStage;
            entry.remaining_participants = remainingParticipants;
            return Write(entry);
        }

        public bool EndRun(RunSnapshot snapshot, double durationSeconds)
        {
            var entry = NewEntry("run_end");
            entry.eligible_human_run = snapshot.EndReason == RunEndReason.Invalid ? 0 : 1;
            entry.stage = snapshot.EndReason == RunEndReason.Completed ? snapshot.LastCompletedStage : snapshot.FailStage;
            entry.end_reason = snapshot.EndReason.ToString().ToLowerInvariant();
            entry.duration_ms = Math.Max(0, (long)Math.Round(durationSeconds * 1000.0, MidpointRounding.AwayFromZero));
            entry.accuracy_basis_points = snapshot.AccuracyBasisPoints;
            entry.rank_score = snapshot.RankScore;
            entry.perfect_count = perfectCount;
            entry.good_count = goodCount;
            entry.miss_count = missCount;
            entry.ghost_miss_count = ghostMissCount;
            return Write(entry);
        }

        public bool RecordResultAction(string action)
        {
            var entry = NewEntry("result_action");
            entry.action = action;
            return Write(entry);
        }

        private LogEntry NewEntry(string eventName)
        {
            return new LogEntry
            {
                event_name = eventName,
                occurred_at_utc = DateTime.UtcNow.ToString("O"),
                run_id = runId,
                retry_of_run_id = retryOfRunId,
                chart_id = chartId,
                ruleset_version = rulesetVersion,
                roster_version = MiniContestRoster.RosterVersion,
                rank_ruleset = MiniContestRoster.RankRuleset,
                eligible_human_run = 1,
                synthetic_trainee_count = MiniContestRoster.TraineeCount
            };
        }

        private bool Write(LogEntry entry)
        {
            try
            {
                writeLine(Serialize(entry));
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                reportWarning("Gate 1 기록 저장 실패. 플레이는 계속합니다. " + error.Message);
                return false;
            }
        }

        private static string Serialize(LogEntry entry)
        {
            var json = new StringBuilder(512);
            json.Append('{');
            Append(json, "event_name", entry.event_name);
            Append(json, "occurred_at_utc", entry.occurred_at_utc);
            Append(json, "run_id", entry.run_id);
            Append(json, "retry_of_run_id", entry.retry_of_run_id);
            Append(json, "chart_id", entry.chart_id);
            Append(json, "ruleset_version", entry.ruleset_version);
            Append(json, "roster_version", entry.roster_version);
            Append(json, "rank_ruleset", entry.rank_ruleset);
            Append(json, "eligible_human_run", entry.eligible_human_run);
            Append(json, "synthetic_trainee_count", entry.synthetic_trainee_count);
            Append(json, "stage", entry.stage);
            Append(json, "remaining_participants", entry.remaining_participants);
            Append(json, "end_reason", entry.end_reason);
            Append(json, "duration_ms", entry.duration_ms);
            Append(json, "accuracy_basis_points", entry.accuracy_basis_points);
            Append(json, "rank_score", entry.rank_score);
            Append(json, "perfect_count", entry.perfect_count);
            Append(json, "good_count", entry.good_count);
            Append(json, "miss_count", entry.miss_count);
            Append(json, "ghost_miss_count", entry.ghost_miss_count);
            Append(json, "action", entry.action, false);
            json.Append('}');
            return json.ToString();
        }

        private static void Append(StringBuilder json, string name, string value, bool comma = true)
        {
            json.Append('"').Append(name).Append("\":");
            if (value == null) json.Append("null");
            else json.Append('"').Append(Escape(value)).Append('"');
            if (comma) json.Append(',');
        }

        private static void Append(StringBuilder json, string name, int value, bool comma = true)
        {
            json.Append('"').Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) json.Append(',');
        }

        private static void Append(StringBuilder json, string name, long value, bool comma = true)
        {
            json.Append('"').Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) json.Append(',');
        }

        private static string Escape(string value)
        {
            var escaped = new StringBuilder(value.Length + 8);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '"': escaped.Append("\\\""); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < 32) escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        else escaped.Append(character);
                        break;
                }
            }
            return escaped.ToString();
        }

        private static void WriteToDefaultFile(string line)
        {
            var path = Path.Combine(Application.persistentDataPath, "gate1-mini-contest-runs.jsonl");
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
