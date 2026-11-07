using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L7 공식 기록 제출. 상태 제어가 필요한 테스트는 스크립트 가능한 서버 대역을 쓰고,
    // 스켈레톤이 약속한 FakeRankingServer 계약(멱등·부적격 Rejected·다른 rulesVersion Rejected)도 고정합니다.
    [TestFixture]
    public class RankingSubmissionTests
    {
        // 차트 JSON의 공식 규칙 버전(four-friend-chart.json rulesVersion).
        private const string RulesVersion = "RR02-v1-draft";

        private static readonly ReachKey SampleKey = new ReachKey(9, 2, 5000);

        private sealed class ScriptedServer : IRankingServer
        {
            public SubmissionState Reply = SubmissionState.Confirmed;
            public readonly HashSet<string> AggregatedRunIds = new HashSet<string>();

            public SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible)
            {
                if (!eligible) return SubmissionState.Rejected;
                AggregatedRunIds.Add(runId); // 멱등 집계: 같은 runId는 한 번만 셉니다.
                return Reply;
            }
        }

        [Test]
        public void DuplicateRunIdGetsSameResponseAndSingleAggregation()
        {
            var server = new ScriptedServer { Reply = SubmissionState.Confirmed };
            var submission = new RankingSubmission(server, RulesVersion);

            var first = submission.Submit("run-1", SampleKey, eligible: true);
            var second = submission.Submit("run-1", SampleKey, eligible: true);

            // AC-024: run_id 중복 전송은 같은 응답, 집계는 1회.
            Assert.AreEqual(first, second);
            Assert.AreEqual(SubmissionState.Confirmed, submission.StateOf("run-1"));
            Assert.AreEqual(1, server.AggregatedRunIds.Count);
        }

        [Test]
        public void IneligibleRunIsRejectedAndNotAggregated()
        {
            var server = new ScriptedServer();
            var submission = new RankingSubmission(server, RulesVersion);

            var state = submission.Submit("practice-1", SampleKey, eligible: false);

            // AC-024: 부적격(연습)은 거부하고 공식 집계에서 분리합니다.
            Assert.AreEqual(SubmissionState.Rejected, state);
            Assert.AreEqual(SubmissionState.Rejected, submission.StateOf("practice-1"));
            Assert.AreEqual(0, server.AggregatedRunIds.Count);
        }

        [Test]
        public void VerifyingStateIsNotTreatedAsConfirmed()
        {
            var server = new ScriptedServer { Reply = SubmissionState.Verifying };
            var submission = new RankingSubmission(server, RulesVersion);

            var state = submission.Submit("run-2", SampleKey, eligible: true);

            // AC-024: 검증 전에는 공식 순위가 없습니다 — 상태는 Verifying이지 Confirmed가 아닙니다.
            Assert.AreEqual(SubmissionState.Verifying, state);
            Assert.AreEqual(SubmissionState.Verifying, submission.StateOf("run-2"));
            Assert.AreNotEqual(SubmissionState.Confirmed, submission.StateOf("run-2"));
        }

        [Test]
        public void TimeoutMovesToHeldAndKeepsPriorConfirmedRecord()
        {
            var server = new ScriptedServer { Reply = SubmissionState.Confirmed };
            var submission = new RankingSubmission(server, RulesVersion);
            submission.Submit("run-old", SampleKey, eligible: true); // 이전 확정 기록

            server.Reply = SubmissionState.Verifying;
            submission.Submit("run-new", new ReachKey(9, 3, 100), eligible: true);
            submission.OnTimeout("run-new");

            // AC-024: 타임아웃은 기록 보류(Held)이며 집계 0이 아니고, 이전 확정 기록은 유지합니다.
            Assert.AreEqual(SubmissionState.Held, submission.StateOf("run-new"));
            Assert.AreEqual(SubmissionState.Confirmed, submission.StateOf("run-old"));
        }

        [Test]
        public void FakeServerRejectsIneligibleRun()
        {
            var submission = new RankingSubmission(new FakeRankingServer(), RulesVersion);

            var state = submission.Submit("fake-practice", SampleKey, eligible: false);

            Assert.AreEqual(SubmissionState.Rejected, state); // AC-024: 연습 분리
        }

        [Test]
        public void FakeServerRejectsMismatchedRulesVersion()
        {
            // AC-024·FR-024: 다른 규칙 버전은 등록하지 않습니다. 유효 버전은 차트의 RR02-v1-draft입니다.
            var mismatched = new RankingSubmission(new FakeRankingServer(), "RR02-v0-other");
            Assert.AreEqual(SubmissionState.Rejected, mismatched.Submit("fake-ver", SampleKey, eligible: true));

            var matched = new RankingSubmission(new FakeRankingServer(), RulesVersion);
            Assert.AreNotEqual(SubmissionState.Rejected, matched.Submit("fake-ok", SampleKey, eligible: true));
        }

        [Test]
        public void FakeServerIsIdempotentForSameRunId()
        {
            var submission = new RankingSubmission(new FakeRankingServer(), RulesVersion);

            var first = submission.Submit("fake-dup", SampleKey, eligible: true);
            var second = submission.Submit("fake-dup", SampleKey, eligible: true);

            // AC-024: 같은 runId 재호출은 같은 응답이며 중복 집계가 없습니다.
            Assert.AreEqual(first, second);
            Assert.AreNotEqual(SubmissionState.Rejected, first);
            Assert.AreNotEqual(SubmissionState.Held, first);
        }
    }
}
