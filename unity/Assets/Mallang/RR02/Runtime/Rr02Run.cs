using System;
using System.Collections.Generic;

namespace Mallang.RR02
{
    public sealed class Rr02RunResult
    {
        public int NormalCleared;
        public int Loops;
        public bool OfficialEligible;
        public IReadOnlyDictionary<Rr02Friend, int> AnimalScores;
        public int TotalScore;
        public bool AllPerfect;
    }

    public sealed class Rr02Run
    {
        private readonly Rr02Chart _chart;
        private Rr02Phase _phase = Rr02Phase.Listen;
        private int _currentRound = 1;
        private int _normalCleared;
        private int _loops;
        private bool _officialEligible = true;
        private readonly Dictionary<Rr02Friend, int> _scores = new Dictionary<Rr02Friend, int>();

        // 진행 중 구절. _phraseNotes는 judge의 Outcomes 순서(note_id 오름차순, 동률 시 TargetUs)와 같은 순서로 정렬합니다.
        private Rr02Judge _judge;
        private IReadOnlyList<Rr02Note> _phraseNotes;

        // FR-016: 완료된 구절의 판정이 전부 Perfect일 때만 유지됩니다.
        private bool _allPerfect = true;

        public Rr02Run(Rr02Chart chart)
        {
            _chart = chart;
            _scores[Rr02Friend.Rabbit] = 0;
            _scores[Rr02Friend.Capybara] = 0;
            _scores[Rr02Friend.Fox] = 0;
            _scores[Rr02Friend.Squirrel] = 0;
        }

        public Rr02Phase Phase => _phase;
        public int CurrentRound => _currentRound;
        public int NormalCleared => _normalCleared;
        public int Loops => _loops;
        public bool OfficialEligible => _officialEligible;

        // 듣기→따라치기 전환. 이후 시간 인자는 이 구절의 따라치기 시작 기준 µs.
        public void BeginRepeat()
        {
            if (_phase != Rr02Phase.Listen) return;

            // hiddenIndices는 표현만 바꾸고 노트를 바꾸지 않으므로 Notes를 그대로 씁니다.
            // 일반 라운드(_chart.Rounds.Count개)를 모두 지나면 무한 구절(k = Loops + 1)로 진입합니다.
            Rr02Round round = _currentRound > _chart.Rounds.Count
                ? _chart.InfinitePhrase(_loops + 1)
                : _chart.Rounds[_currentRound - 1];

            _phraseNotes = SortForOutcomes(round.Notes);
            _judge = new Rr02Judge(_phraseNotes);
            _phase = Rr02Phase.Repeat;
        }

        public Rr02InputResult ProcessPress(Rr02Friend friend, long usFromRepeatStart)
        {
            if (_judge == null)
                return new Rr02InputResult(Rr02InputKind.OutsideOwnership, -1, Rr02Judgement.Miss);
            return _judge.ProcessPress(friend, usFromRepeatStart);
        }

        // 구절 종료 확정 포함(마지막 목표 +180ms에 1회).
        public void AdvanceTime(long usFromRepeatStart)
        {
            if (_phase != Rr02Phase.Repeat || _judge == null) return; // Ended 이후에는 멱등

            _judge.AdvanceTime(usFromRepeatStart); // Good 밴드를 지난 열린 목표는 Miss로 닫힙니다.
            if (!_judge.Completed(usFromRepeatStart)) return;
            FinalizePhrase();
        }

        // 공식 자격 상실(연습 전환). 완료 단계 유지, 같은 라운드 듣기부터 재개(MCH-007).
        public void InvalidateOfficial(Rr02PracticeReason reason)
        {
            _officialEligible = false;

            // 진행 중 구절은 확정 전이므로 점수가 아직 반영되지 않았습니다. judge만 폐기합니다.
            if (_phase == Rr02Phase.Repeat)
            {
                _judge = null;
                _phraseNotes = null;
                _phase = Rr02Phase.Listen;
            }
        }

        public IReadOnlyDictionary<Rr02Friend, int> RunAnimalScores => _scores;

        public Rr02RunResult Result
        {
            get
            {
                int total = 0;
                foreach (var pair in _scores) total += pair.Value;
                return new Rr02RunResult
                {
                    NormalCleared = _normalCleared,
                    Loops = _loops,
                    OfficialEligible = _officialEligible,
                    AnimalScores = new Dictionary<Rr02Friend, int>(_scores),
                    TotalScore = total,
                    AllPerfect = _allPerfect,
                };
            }
        }

        // 마지막 목표 +180ms 도달 시 1회 실행됩니다. 실패여도 조기 중단 없이 전 판정을 반영합니다.
        private void FinalizePhrase()
        {
            var outcomes = _judge.Outcomes;
            bool phraseAllPerfect = true;
            bool anyMiss = false;

            for (int i = 0; i < outcomes.Count; i++)
            {
                var judgement = outcomes[i];
                if (judgement == Rr02Judgement.Miss) anyMiss = true;
                if (judgement != Rr02Judgement.Perfect) phraseAllPerfect = false;

                // 콤보 배율 없음: Perfect 100 / Good 70 / Miss 0. 노트의 친구에게 가산합니다.
                int points = judgement == Rr02Judgement.Perfect ? Rr02Rules.PerfectScore
                    : judgement == Rr02Judgement.Good ? Rr02Rules.GoodScore
                    : 0;
                _scores[_phraseNotes[i].Friend] += points;
            }

            bool inLoop = _currentRound > _chart.Rounds.Count;
            if (inLoop)
            {
                // 무한 구간: 기존 엄격 규칙(미스/ghost 없어야 성공). 실패하면 도전 종료(리더보드 기록용).
                bool loopSuccess = _judge.GhostCount == 0 && !anyMiss;
                if (loopSuccess) { _loops++; _phase = Rr02Phase.Listen; }
                else { _phase = Rr02Phase.Ended; }
            }
            else
            {
                // 일반 라운드(라이프 모델): 통과 기준은 100%. 모든 노트를 맞혀야(Miss 0) 통과합니다.
                // 하나라도 틀리면(Miss) 전구-1·같은 라운드 재도전, 전구 0에서 실패=게임오버.
                // (ghost 같은 여분 입력은 0점일 뿐 Miss가 아니므로 통과를 막지 않습니다.)
                bool pass = !anyMiss;
                if (pass)
                {
                    _allPerfect = _allPerfect && phraseAllPerfect;
                    _currentRound++;                         // 다음 라운드(통과할 때만 전진)
                    if (_normalCleared < _chart.Rounds.Count) _normalCleared++; // 전구 +1
                    _phase = Rr02Phase.Listen;
                }
                else
                {
                    if (_normalCleared <= 0) { _phase = Rr02Phase.Ended; } // 전구 0에서 실패 = 게임오버
                    else { _normalCleared--; _phase = Rr02Phase.Listen; }  // 전구 -1, _currentRound 유지(재도전)
                }
            }

            _judge = null;
            _phraseNotes = null;
        }

        // Outcomes가 note_id 오름차순(동률 시 TargetUs)이므로 같은 순서로 정렬해 판정과 노트를 짝짓습니다.
        private static List<Rr02Note> SortForOutcomes(IReadOnlyList<Rr02Note> notes)
        {
            var sorted = new List<Rr02Note>(notes);
            sorted.Sort((a, b) => a.NoteId != b.NoteId
                ? a.NoteId.CompareTo(b.NoteId)
                : a.TargetUs.CompareTo(b.TargetUs));
            return sorted;
        }
    }
}
