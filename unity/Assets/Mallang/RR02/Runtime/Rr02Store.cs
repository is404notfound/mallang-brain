using System;

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
        public Rr02Store(IKeyValueStore store) => throw new NotImplementedException();

        // 키 brain:RR02:v1:<friend>. 읽기 실패는 Unverified=true (RR01 키 자동 변환 없음).
        public BrainRecord ReadBrain(Rr02Friend friend) => throw new NotImplementedException();

        // max 병합 + revision+1 직렬. 쓰기 확인 후에만 true. 역순·중복 revision은 확정값을 덮지 않는다.
        public bool WriteBrainMax(Rr02Friend friend, int candidate) => throw new NotImplementedException();

        // 키 reach:RR02:v1
        public (int normalCleared, int loops, string runId) ReadReach() => throw new NotImplementedException();

        // RankingRules.Compare로 더 높을 때만 갱신.
        public bool WriteReachMax(ReachKey key, string runId) => throw new NotImplementedException();

        // 키 badge:RR02:v1:<badgeId>, 최초 1회만.
        public bool WriteBadge(string badgeId, string runId, long earnedAtUs) => throw new NotImplementedException();
    }
}
