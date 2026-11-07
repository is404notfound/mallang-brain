using System;

namespace Mallang.RR02
{
    public enum SubmissionState { Pending, Verifying, Confirmed, Held, Rejected }

    public interface IRankingServer
    {
        SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible);
    }

    public sealed class RankingSubmission
    {
        public RankingSubmission(IRankingServer server, string rulesVersion) => throw new NotImplementedException();

        // 같은 runId 재호출 = 같은 응답(중복 집계 없음). eligible=false → Rejected(연습 분리).
        public SubmissionState Submit(string runId, ReachKey key, bool eligible) => throw new NotImplementedException();

        // 타임아웃 → Held. 이전 Confirmed 기록은 유지하고 화면 이동은 막지 않는다(상태만 표시).
        public void OnTimeout(string runId) => throw new NotImplementedException();

        public SubmissionState StateOf(string runId) => throw new NotImplementedException();
    }

    // 계약대로 동작하는 스텁 서버(실서버 미구현 — assumptions.md ASM-002).
    public sealed class FakeRankingServer : IRankingServer
    {
        public SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible) => throw new NotImplementedException();
    }
}
