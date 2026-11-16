using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public enum SubmissionState { Pending, Verifying, Confirmed, Held, Rejected }

    public interface IRankingServer
    {
        SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible);
    }

    public sealed class RankingSubmission
    {
        private readonly IRankingServer server;
        private readonly string rulesVersion;
        private readonly Dictionary<string, SubmissionState> records = new Dictionary<string, SubmissionState>();

        public RankingSubmission(IRankingServer server, string rulesVersion)
        {
            this.server = server;
            this.rulesVersion = rulesVersion;
        }

        // 같은 runId 재호출 = 같은 응답(중복 집계 없음). eligible=false → Rejected(연습 분리).
        public SubmissionState Submit(string runId, ReachKey key, bool eligible)
        {
            // 이미 기록된 runId는 서버를 다시 부르지 않고 기록된 상태를 그대로 돌려준다.
            if (records.TryGetValue(runId, out var recorded)) return recorded;

            // 부적격(연습)은 서버로 보내지 않고 Rejected로만 기록해 공식 집계에서 분리한다.
            if (!eligible)
            {
                records[runId] = SubmissionState.Rejected;
                return SubmissionState.Rejected;
            }

            var state = server.Submit(runId, rulesVersion, key, eligible);
            records[runId] = state;
            return state;
        }

        // 타임아웃 → Held. 이전 Confirmed 기록은 유지하고 화면 이동은 막지 않는다(상태만 표시).
        public void OnTimeout(string runId)
        {
            // Confirmed 기록은 어떤 경우에도 강등하지 않는다.
            if (records.TryGetValue(runId, out var recorded) && recorded == SubmissionState.Confirmed) return;
            records[runId] = SubmissionState.Held;
        }

        public SubmissionState StateOf(string runId)
        {
            return records.TryGetValue(runId, out var recorded) ? recorded : SubmissionState.Pending;
        }
    }

    // 계약대로 동작하는 스텁 서버(실서버 미구현 — assumptions.md ASM-002).
    public sealed class FakeRankingServer : IRankingServer
    {
        // 차트 JSON(four-friend-chart.json)의 공식 규칙 버전. 테스트가 고정한 값.
        private const string AcceptedRulesVersion = "RR02-v1-draft";

        private readonly Dictionary<string, SubmissionState> byRunId = new Dictionary<string, SubmissionState>();

        public SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible)
        {
            // runId 단위 멱등: 같은 runId 재호출은 처음 기록한 응답을 그대로 돌려준다.
            if (byRunId.TryGetValue(runId, out var recorded)) return recorded;

            SubmissionState state;
            if (!eligible || rulesVersion != AcceptedRulesVersion)
            {
                // 부적격이거나 다른 규칙 버전은 등록하지 않는다.
                state = SubmissionState.Rejected;
            }
            else
            {
                state = SubmissionState.Confirmed;
            }

            byRunId[runId] = state;
            return state;
        }
    }
}
