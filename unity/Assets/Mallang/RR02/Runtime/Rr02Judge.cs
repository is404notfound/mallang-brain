using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public sealed class Rr02Judge
    {
        // 노트별 내부 상태. 닫힌 목표는 되살리지 않으므로 Closed 플래트로 관리합니다.
        private sealed class NoteState
        {
            public int NoteId;
            public Rr02Friend Friend;
            public long TargetUs;
            public bool Closed;
            public Rr02Judgement Judgement;
        }

        private readonly List<NoteState> _states = new List<NoteState>(); // note_id 오름차순 (Outcomes 순서)
        private readonly long _firstTargetUs;
        private readonly long _lastTargetUs;
        private readonly bool _hasNotes;

        public int GhostCount { get; private set; }

        public Rr02Judge(IReadOnlyList<Rr02Note> notes)
        {
            foreach (var note in notes)
            {
                _states.Add(new NoteState
                {
                    NoteId = note.NoteId,
                    Friend = note.Friend,
                    TargetUs = note.TargetUs,
                    Closed = false,
                    Judgement = Rr02Judgement.Miss, // 아직 열려 있는 노트의 초기값(닫히기 전에는 Outcomes로 노출되지 않음)
                });
            }
            _states.Sort((a, b) => a.NoteId != b.NoteId
                ? a.NoteId.CompareTo(b.NoteId)
                : a.TargetUs.CompareTo(b.TargetUs));

            _hasNotes = _states.Count > 0;
            long first = long.MaxValue, last = long.MinValue;
            foreach (var state in _states)
            {
                if (state.TargetUs < first) first = state.TargetUs;
                if (state.TargetUs > last) last = state.TargetUs;
            }
            _firstTargetUs = first;
            _lastTargetUs = last;
        }

        // 호출 순서 = sequence_id 순. 같은 친구의 열린 목표 중 |offset| 최소 → 이른 목표 → 낮은 note_id.
        public Rr02InputResult ProcessPress(Rr02Friend friend, long inputUs)
        {
            // 1) 후보 선택: ±GoodWindowUs 안의 열린 목표 중 |offset| 최소 → 이른 TargetUs → 낮은 NoteId. 친구는 선택 후 비교합니다.
            NoteState best = null;
            long bestAbs = 0;
            foreach (var state in _states)
            {
                if (state.Closed) continue;
                long abs = Math.Abs(inputUs - state.TargetUs); // 전부 long 마이크로초 (float/double 사용 금지)
                if (abs > Rr02Rules.GoodWindowUs) continue;
                if (best == null
                    || abs < bestAbs
                    || (abs == bestAbs && state.TargetUs < best.TargetUs)
                    || (abs == bestAbs && state.TargetUs == best.TargetUs && state.NoteId < best.NoteId))
                {
                    best = state;
                    bestAbs = abs;
                }
            }

            if (best != null)
            {
                Rr02Judgement judgement = best.Friend == friend
                    ? (bestAbs <= Rr02Rules.PerfectWindowUs ? Rr02Judgement.Perfect : Rr02Judgement.Good)
                    : Rr02Judgement.Miss;
                best.Closed = true;
                best.Judgement = judgement;
                return new Rr02InputResult(Rr02InputKind.Judged, best.NoteId, judgement);
            }

            // 2) 후보 없음: 소유 구간(첫 목표 −180ms ~ 마지막 목표 +180ms, 양끝 포함) 안이면 Ghost, 밖이면 OutsideOwnership.
            if (_hasNotes
                && inputUs >= _firstTargetUs - Rr02Rules.OwnershipPadUs
                && inputUs <= _lastTargetUs + Rr02Rules.OwnershipPadUs)
            {
                GhostCount++;
                return new Rr02InputResult(Rr02InputKind.Ghost, -1, Rr02Judgement.Miss);
            }
            return new Rr02InputResult(Rr02InputKind.OutsideOwnership, -1, Rr02Judgement.Miss);
        }

        // 목표 시각 +120ms(GoodWindowUs)를 지난 노트를 Miss로 닫는다.
        public void AdvanceTime(long nowUs)
        {
            foreach (var state in _states)
            {
                if (state.Closed) continue;
                if (state.TargetUs + Rr02Rules.GoodWindowUs < nowUs) // 정확히 +120,000µs까지는 열려 있음(연속 경계)
                {
                    state.Closed = true;
                    state.Judgement = Rr02Judgement.Miss;
                }
            }
        }

        public IReadOnlyList<Rr02Judgement> Outcomes
        {
            get
            {
                var list = new List<Rr02Judgement>(_states.Count);
                foreach (var state in _states) list.Add(state.Judgement); // note_id 오름차순 저장 순서
                return list;
            }
        }

        // 마지막 목표 +180ms(OwnershipPadUs) 경과 여부.
        public bool Completed(long nowUs)
        {
            return _hasNotes && nowUs >= _lastTargetUs + Rr02Rules.OwnershipPadUs;
        }
    }
}
