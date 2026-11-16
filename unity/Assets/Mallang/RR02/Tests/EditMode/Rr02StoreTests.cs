using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L6 저장·병합. IKeyValueStore 계약: TryRead의 반환값은 "읽기 동작 성공" 여부이고,
    // 키가 없으면 true + null 값(valid empty)로 봅니다. false는 읽기 실패(미지원/오류)입니다 (FR-017).
    [TestFixture]
    public class Rr02StoreTests
    {
        private sealed class FakeKv : IKeyValueStore
        {
            public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
            public bool FailReads;
            public bool FailWrites;

            public bool TryRead(string key, out string value)
            {
                value = null;
                if (FailReads) return false;
                Data.TryGetValue(key, out value);
                return true;
            }

            public bool TryWrite(string key, string value)
            {
                if (FailWrites) return false;
                Data[key] = value;
                return true;
            }
        }

        private const string RabbitKey = "brain:RR02:v1:rabbit";

        [Test]
        public void EmptyStoreReadsAsVerifiedZero()
        {
            var store = new Rr02Store(new FakeKv());

            var record = store.ReadBrain(Rr02Friend.Rabbit);

            // FR-017: 키 없음은 valid empty — 미확인이 아닙니다.
            Assert.IsFalse(record.Unverified);
            Assert.AreEqual(0, record.Confirmed);
        }

        [Test]
        public void BrainKeysUseExactPerFriendFormat()
        {
            var kv = new FakeKv();
            var store = new Rr02Store(kv);

            store.WriteBrainMax(Rr02Friend.Rabbit, 100);
            store.WriteBrainMax(Rr02Friend.Capybara, 100);
            store.WriteBrainMax(Rr02Friend.Fox, 100);
            store.WriteBrainMax(Rr02Friend.Squirrel, 100);

            // FR-017: 저장 키는 정확히 brain:RR02:v1:<character_id> 형태입니다.
            Assert.IsTrue(kv.Data.ContainsKey("brain:RR02:v1:rabbit"));
            Assert.IsTrue(kv.Data.ContainsKey("brain:RR02:v1:capybara"));
            Assert.IsTrue(kv.Data.ContainsKey("brain:RR02:v1:fox"));
            Assert.IsTrue(kv.Data.ContainsKey("brain:RR02:v1:squirrel"));
        }

        [Test]
        public void WriteBrainMaxReturnsFalseWhenWriteIsNotConfirmed()
        {
            var kv = new FakeKv { FailWrites = true };
            var store = new Rr02Store(kv);

            // FR-017: "저장됨"은 쓰기 확인 뒤에만 — TryWrite 실패면 false.
            Assert.IsFalse(store.WriteBrainMax(Rr02Friend.Rabbit, 500));
            Assert.IsFalse(kv.Data.ContainsKey(RabbitKey));
        }

        [Test]
        public void ReadFailureMarksUnverifiedAndBlocksOverwrite()
        {
            var kv = new FakeKv { FailReads = true };
            kv.Data[RabbitKey] = "ORIGINAL";
            var store = new Rr02Store(kv);

            var record = store.ReadBrain(Rr02Friend.Rabbit);
            bool wrote = store.WriteBrainMax(Rr02Friend.Rabbit, 700);

            // AC-017: 읽기 실패는 미확인 — 원본 덮어쓰기 0.
            Assert.IsTrue(record.Unverified);
            Assert.IsFalse(wrote);
            Assert.AreEqual("ORIGINAL", kv.Data[RabbitKey]);
        }

        [Test]
        public void CorruptedPayloadIsUnverifiedAndNotOverwritten()
        {
            var kv = new FakeKv();
            kv.Data[RabbitKey] = "###not-a-brain-record###";
            var store = new Rr02Store(kv);

            var record = store.ReadBrain(Rr02Friend.Rabbit);
            bool wrote = store.WriteBrainMax(Rr02Friend.Rabbit, 700);

            // AC-017·FR-017: 손상 데이터도 원본을 덮지 않습니다.
            Assert.IsTrue(record.Unverified);
            Assert.IsFalse(wrote);
            Assert.AreEqual("###not-a-brain-record###", kv.Data[RabbitKey]);
        }

        [Test]
        public void MaxMergeKeepsHigherConfirmedValue()
        {
            var store = new Rr02Store(new FakeKv());
            store.WriteBrainMax(Rr02Friend.Rabbit, 900);

            store.WriteBrainMax(Rr02Friend.Rabbit, 800);

            // AC-017: max 병합 — 저장소 900 vs 후보 800이면 900 유지.
            Assert.AreEqual(900, store.ReadBrain(Rr02Friend.Rabbit).Confirmed);
        }

        [Test]
        public void RevisionIncrementsSeriallyPerConfirmedWrite()
        {
            var store = new Rr02Store(new FakeKv());

            Assert.IsTrue(store.WriteBrainMax(Rr02Friend.Rabbit, 1000));
            int firstRevision = store.ReadBrain(Rr02Friend.Rabbit).Revision;
            Assert.IsTrue(store.WriteBrainMax(Rr02Friend.Rabbit, 1200));
            int secondRevision = store.ReadBrain(Rr02Friend.Rabbit).Revision;

            // FR-017: 단일 작성자·revision+1 직렬 저장.
            Assert.AreEqual(firstRevision + 1, secondRevision);
            Assert.AreEqual(1200, store.ReadBrain(Rr02Friend.Rabbit).Confirmed);
        }

        [Test]
        public void StaleLowerRevisionRedeliveryDoesNotOverrideConfirmed()
        {
            var kv = new FakeKv();
            var store = new Rr02Store(kv);
            store.WriteBrainMax(Rr02Friend.Rabbit, 1000);
            string stalePayload = kv.Data[RabbitKey]; // 낮은 revision 스냅샷
            store.WriteBrainMax(Rr02Friend.Rabbit, 1200);

            kv.Data[RabbitKey] = stalePayload; // Review Focus 4: 뒤늦게 도착한 낮은 revision

            // AC-017: 역순 revision이 확정값을 덮지 않습니다.
            Assert.AreEqual(1200, store.ReadBrain(Rr02Friend.Rabbit).Confirmed);
        }

        [Test]
        public void DuplicateRevisionRedeliveryDoesNotChangeConfirmedState()
        {
            var kv = new FakeKv();
            var store = new Rr02Store(kv);
            store.WriteBrainMax(Rr02Friend.Rabbit, 1000);
            store.WriteBrainMax(Rr02Friend.Rabbit, 1200);
            string currentPayload = kv.Data[RabbitKey];
            int revisionBefore = store.ReadBrain(Rr02Friend.Rabbit).Revision;

            kv.Data[RabbitKey] = currentPayload; // Review Focus 4: 같은 revision 중복 도착

            // AC-017: 중복은 확정값·revision을 바꾸지 않습니다.
            var record = store.ReadBrain(Rr02Friend.Rabbit);
            Assert.AreEqual(1200, record.Confirmed);
            Assert.AreEqual(revisionBefore, record.Revision);
        }

        [Test]
        public void LegacyRr01KeysAreNeverConverted()
        {
            var kv = new FakeKv();
            kv.Data["brain:RR01:v1:rabbit"] = "1234";
            var store = new Rr02Store(kv);

            var record = store.ReadBrain(Rr02Friend.Rabbit);
            bool wrote = store.WriteBrainMax(Rr02Friend.Rabbit, 500);

            // AC-017·FR-017: 예전 기억 키의 숫자 변환 0, 기존 원본 보존.
            Assert.AreEqual(0, record.Confirmed);
            Assert.IsTrue(wrote);
            Assert.AreEqual("1234", kv.Data["brain:RR01:v1:rabbit"]);
            Assert.IsTrue(kv.Data.ContainsKey(RabbitKey));
        }

        [Test]
        public void WriteReachMaxUpdatesOnlyOnStrictlyHigherKey()
        {
            var store = new Rr02Store(new FakeKv());

            Assert.IsTrue(store.WriteReachMax(new ReachKey(9, 2, 5000), "runA"));
            Assert.IsFalse(store.WriteReachMax(new ReachKey(9, 1, 9999), "runB")); // 더 낮은 키

            var reach = store.ReadReach();
            // 데이터 계약: reach:RR02:v1은 자격 있는 도전이 더 높을 때만 갱신(RankingRules.Compare 기준).
            Assert.AreEqual(9, reach.normalCleared);
            Assert.AreEqual(2, reach.loops);
            Assert.AreEqual("runA", reach.runId);

            Assert.IsTrue(store.WriteReachMax(new ReachKey(9, 3, 100), "runC")); // 더 높은 키
            Assert.AreEqual("runC", store.ReadReach().runId);
            Assert.AreEqual(3, store.ReadReach().loops);
        }

        [Test]
        public void WriteReachMaxIgnoresExactTie()
        {
            var store = new Rr02Store(new FakeKv());
            store.WriteReachMax(new ReachKey(9, 2, 5000), "runA");

            bool updated = store.WriteReachMax(new ReachKey(9, 2, 5000), "runB");

            // 더 높을 때만 갱신 — 완전 동점은 기존 기록 유지.
            Assert.IsFalse(updated);
            Assert.AreEqual("runA", store.ReadReach().runId);
        }

        [Test]
        public void WriteBadgeIsFirstWriteOnlyWithExactKey()
        {
            var kv = new FakeKv();
            var store = new Rr02Store(kv);

            bool first = store.WriteBadge("all-clear", "runA", 123456789L);
            bool second = store.WriteBadge("all-clear", "runB", 987654321L);

            // 데이터 계약: badge:RR02:v1:<badge_id> — 최초 1회만 기록, 계정 영속.
            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.IsTrue(kv.Data.ContainsKey("badge:RR02:v1:all-clear"));
        }
    }
}
