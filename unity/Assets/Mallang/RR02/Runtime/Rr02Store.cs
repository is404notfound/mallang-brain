using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public interface IKeyValueStore
    {
        bool TryRead(string key, out string value);
        bool TryWrite(string key, string value);
    }

    public readonly struct BrainRecord
    {
        public readonly int Confirmed;
        public readonly int Revision;
        public readonly bool Unverified;

        public BrainRecord(int confirmed, int revision, bool unverified)
        {
            Confirmed = confirmed;
            Revision = revision;
            Unverified = unverified;
        }
    }

    public sealed class Rr02Store
    {
        private readonly IKeyValueStore _store;

        // 친구별 확정 상태 캐시: 역순·중복 revision 재전달이 확정값을 덮지 못하게 막습니다.
        private readonly Dictionary<string, (int Confirmed, int Revision)> _brainCache =
            new Dictionary<string, (int Confirmed, int Revision)>();

        public Rr02Store(IKeyValueStore store) => _store = store;

        // 키 brain:RR02:v1:<friend>. 읽기 실패는 Unverified=true (RR01 키 자동 변환 없음).
        public BrainRecord ReadBrain(Rr02Friend friend)
        {
            string key = BrainKey(friend);
            _brainCache.TryGetValue(key, out var cached);

            if (!_store.TryRead(key, out string value))
                return new BrainRecord(cached.Confirmed, cached.Revision, true); // 읽기 실패 (FR-017)

            if (value == null) // 키 없음 = valid empty — 미확인이 아닙니다.
                return new BrainRecord(cached.Confirmed, cached.Revision, false);

            if (!TryParseBrain(value, out int confirmed, out int revision))
                return new BrainRecord(cached.Confirmed, cached.Revision, true); // 손상 데이터

            if (revision < cached.Revision) // 뒤늦게 도착한 낮은 revision은 확정값을 덮지 않습니다.
                return new BrainRecord(cached.Confirmed, cached.Revision, false);

            _brainCache[key] = (confirmed, revision);
            return new BrainRecord(confirmed, revision, false);
        }

        // max 병합 + revision+1 직렬. 쓰기 확인 후에만 true. 역순·중복 revision은 확정값을 덮지 않는다.
        public bool WriteBrainMax(Rr02Friend friend, int candidate)
        {
            var current = ReadBrain(friend);
            if (current.Unverified) return false; // 미확인이면 원본을 덮지 않습니다.

            string key = BrainKey(friend);
            int confirmed = current.Confirmed > candidate ? current.Confirmed : candidate;
            int revision = current.Revision + 1; // 단일 작성자: current+1가 아닌 쓰기는 하지 않습니다.

            if (!_store.TryWrite(key, BuildBrain(value: confirmed, revision: revision)))
                return false;

            _brainCache[key] = (confirmed, revision);
            return true;
        }

        // 키 reach:RR02:v1
        public (int normalCleared, int loops, string runId) ReadReach()
        {
            if (!_store.TryRead(ReachKeyString, out string value))
                return (0, 0, null);
            if (!TryParseReach(value, out int n, out int l, out int t, out string runId))
                return (0, 0, null);
            return (n, l, runId);
        }

        // RankingRules.Compare로 더 높을 때만 갱신.
        public bool WriteReachMax(ReachKey key, string runId)
        {
            if (!_store.TryRead(ReachKeyString, out string value))
                return false;

            if (value != null)
            {
                if (!TryParseReach(value, out int n, out int l, out int t, out _))
                    return false; // 손상 기록은 덮지 않습니다.
                var stored = new ReachKey(n, l, t);
                if (RankingRules.Compare(key, stored) <= 0)
                    return false; // 더 높을 때만 갱신 (완전 동점 제외).
            }

            return _store.TryWrite(ReachKeyString, BuildReach(key, runId));
        }

        // 키 badge:RR02:v1:<badgeId>, 최초 1회만.
        public bool WriteBadge(string badgeId, string runId, long earnedAtUs)
        {
            string key = "badge:RR02:v1:" + badgeId;
            if (!_store.TryRead(key, out string value))
                return false;
            if (value != null)
                return false; // 이미 획득 — 최초 1회만 기록.

            return _store.TryWrite(key, "{\"r\":\"" + runId + "\",\"t\":" + earnedAtUs + "}");
        }

        private static string BrainKey(Rr02Friend friend) =>
            "brain:RR02:v1:" + friend.ToString().ToLowerInvariant();

        private const string ReachKeyString = "reach:RR02:v1";

        // 저장 형식은 최소 필드의 단순 JSON입니다 ({"c":확정값,"v":revision}).
        private static string BuildBrain(int value, int revision) =>
            "{\"c\":" + value + ",\"v\":" + revision + "}";

        private static bool TryParseBrain(string json, out int confirmed, out int revision)
        {
            confirmed = 0;
            revision = 0;
            return TryIntField(json, "\"c\":", out confirmed) &&
                   TryIntField(json, "\"v\":", out revision);
        }

        // 저장 형식 ({"n":노말,"l":반복,"t":점수,"r":"런"}).
        private static string BuildReach(ReachKey key, string runId) =>
            "{\"n\":" + key.NormalCleared + ",\"l\":" + key.Loops + ",\"t\":" + key.RunScore +
            ",\"r\":\"" + runId + "\"}";

        private static bool TryParseReach(string json, out int n, out int l, out int t, out string runId)
        {
            n = 0;
            l = 0;
            t = 0;
            runId = null;
            if (!TryIntField(json, "\"n\":", out n)) return false;
            if (!TryIntField(json, "\"l\":", out l)) return false;
            if (!TryIntField(json, "\"t\":", out t)) return false;

            int i = json.IndexOf("\"r\":\"", StringComparison.Ordinal);
            if (i < 0) return false;
            i += 5;
            int end = json.IndexOf('"', i);
            if (end < 0) return false;
            runId = json.Substring(i, end - i);
            return true;
        }

        private static bool TryIntField(string json, string marker, out int result)
        {
            result = 0;
            int i = json.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return false;
            int start = i + marker.Length;
            int end = start;
            if (end < json.Length && json[end] == '-') end++;
            while (end < json.Length && json[end] >= '0' && json[end] <= '9') end++;
            if (end == start) return false;
            return int.TryParse(json.Substring(start, end - start), out result);
        }
    }
}
