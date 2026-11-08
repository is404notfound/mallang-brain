using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Mallang.RR02.Screens
{
    // 플레이 루프 글루(장면 레인). 로직은 전부 로직 레인의 Rr02Chart/Rr02Run/Rr02Judge가 하고,
    // 이 컴포넌트는 그걸 입력·타이밍·뷰에 잇습니다.
    //   듣기(Listen): 라운드 시퀀스를 모서리 친구를 순서대로 퐁 시켜 시연.
    //   따라치기(Repeat): 키 1~4 또는 모서리 탭 -> Rr02Run.ProcessPress.
    //   라운드 클리어 -> StageLampRow.SetCleared(NormalCleared)로 전구 점등(퐁). 실패 -> 1라운드부터 다시.
    // 타이밍·손맛은 사람 플레이테스트로 다듬습니다.
    public class Rr02PlayController : MonoBehaviour
    {
        public TextAsset chartJson;
        public StageLampRow lampRow;

        [Header("모서리 친구(입력/시연 대상)")]
        public RectTransform bubbleRabbit;
        public RectTransform bubbleCapybara;
        public RectTransform bubbleFox;
        public RectTransform bubbleSquirrel;

        [Header("텍스트")]
        public Text roundText;      // 상단 배너
        public Text instructionText; // 하단 배너
        public Text feedbackText;   // 중앙 피드백(없으면 자동 생성)

        [Header("타이밍(초)")]
        public float listenNoteGap = 0.0f; // 노트 간격은 차트 TargetUs를 따름
        public float listenTailSeconds = 0.7f;   // 시퀀스 끝난 뒤 따라치기 전환 여유
        public float clearHoldSeconds = 0.9f;

        enum State { Listen, Repeat, Clear, Failed }

        Rr02Chart _chart;
        Rr02Run _run;
        State _state;
        double _clock;        // 현재 상태 경과(초)
        int _listenIndex;     // 듣기에서 다음 재생할 노트
        List<Rr02Note> _phrase = new List<Rr02Note>();
        int _lastNormalCleared;
        Dictionary<Rr02Friend, RectTransform> _bubbles;
        Dictionary<Rr02Friend, Coroutine> _pulse = new Dictionary<Rr02Friend, Coroutine>();

        void Awake()
        {
            _bubbles = new Dictionary<Rr02Friend, RectTransform>
            {
                { Rr02Friend.Rabbit, bubbleRabbit },
                { Rr02Friend.Capybara, bubbleCapybara },
                { Rr02Friend.Fox, bubbleFox },
                { Rr02Friend.Squirrel, bubbleSquirrel },
            };
            // 모서리 탭 입력: 각 버블의 Image에 Button을 달아 Press로 연결(런타임 배선).
            foreach (var pair in _bubbles)
            {
                var bubble = pair.Value; var friend = pair.Key;
                if (bubble == null) continue;
                var img = bubble.GetComponentInChildren<Image>();
                if (img == null) continue;
                var btn = img.gameObject.GetComponent<Button>();
                if (btn == null) btn = img.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => Press(friend));
            }
        }

        void OnEnable()
        {
            if (chartJson == null) { Debug.LogError("Rr02PlayController: chartJson 미설정"); enabled = false; return; }
            _chart = Rr02Chart.LoadFromJson(chartJson.text);
            StartRun();
        }

        void StartRun()
        {
            _run = new Rr02Run(_chart);
            _lastNormalCleared = 0;
            if (lampRow != null) lampRow.SetClearedInstant(0);
            EnterListen();
        }

        void EnterListen()
        {
            _state = State.Listen;
            _clock = 0; _listenIndex = 0;
            _phrase.Clear();
            var notes = _chart.Rounds[_run.CurrentRound - 1].Notes;
            foreach (var n in notes) _phrase.Add(n);
            _phrase.Sort((a, b) => a.TargetUs.CompareTo(b.TargetUs));
            if (roundText != null) roundText.text = _run.CurrentRound + "번째 무대";
            if (instructionText != null) instructionText.text = "잘 들어요";
            SetFeedback("");
        }

        void Update()
        {
            if (_run == null) return;
            float dt = Time.deltaTime;
            _clock += dt;

            if (_state == State.Listen) TickListen();
            else if (_state == State.Repeat) TickRepeat();
            else if (_state == State.Clear) { if (_clock >= clearHoldSeconds) EnterListen(); }
            else if (_state == State.Failed) { if (_clock >= clearHoldSeconds) StartRun(); }
        }

        void TickListen()
        {
            // 각 노트를 TargetUs(초) 시점에 해당 친구 퐁
            while (_listenIndex < _phrase.Count &&
                   _clock >= _phrase[_listenIndex].TargetUs / 1000000.0)
            {
                Pulse(_phrase[_listenIndex].Friend);
                _listenIndex++;
            }
            if (_listenIndex >= _phrase.Count)
            {
                double lastSec = _phrase.Count > 0 ? _phrase[_phrase.Count - 1].TargetUs / 1000000.0 : 0;
                if (_clock >= lastSec + listenTailSeconds)
                {
                    _run.BeginRepeat();
                    _state = State.Repeat;
                    _clock = 0;
                    if (instructionText != null) instructionText.text = "따라 쳐요";
                }
            }
        }

        void TickRepeat()
        {
            ReadInput();
            long us = (long)(_clock * 1000000.0);
            _run.AdvanceTime(us);

            if (_run.Phase == Rr02Phase.Listen)
            {
                // 구절 성공 -> 다음 라운드(또는 무한). NormalCleared 증가분을 전구로.
                if (_run.NormalCleared > _lastNormalCleared)
                {
                    if (lampRow != null) lampRow.SetCleared(_run.NormalCleared);
                    _lastNormalCleared = _run.NormalCleared;
                }
                SetFeedback("좋아요!");
                _state = State.Clear; _clock = 0;
            }
            else if (_run.Phase == Rr02Phase.Ended)
            {
                SetFeedback("다시 해봐요");
                _state = State.Failed; _clock = 0;
            }
        }

        void ReadInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Press(Rr02Friend.Rabbit);
                if (kb.digit2Key.wasPressedThisFrame) Press(Rr02Friend.Capybara);
                if (kb.digit3Key.wasPressedThisFrame) Press(Rr02Friend.Fox);
                if (kb.digit4Key.wasPressedThisFrame) Press(Rr02Friend.Squirrel);
            }
        }

        // 모서리 탭/버튼에서도 호출(공개).
        public void Press(Rr02Friend friend)
        {
            if (_state != State.Repeat || _run == null) return;
            long us = (long)(_clock * 1000000.0);
            var r = _run.ProcessPress(friend, us);
            Pulse(friend);
            if (r.Kind == Rr02InputKind.Judged)
                SetFeedback(r.Judgement == Rr02Judgement.Perfect ? "Perfect!"
                    : r.Judgement == Rr02Judgement.Good ? "Good" : "Miss");
            else if (r.Kind == Rr02InputKind.Ghost)
                SetFeedback("!");
        }

        void Pulse(Rr02Friend friend)
        {
            RectTransform rt;
            if (_bubbles == null || !_bubbles.TryGetValue(friend, out rt) || rt == null) return;
            if (_pulse.ContainsKey(friend) && _pulse[friend] != null) StopCoroutine(_pulse[friend]);
            _pulse[friend] = StartCoroutine(PulseRoutine(rt));
        }

        IEnumerator PulseRoutine(RectTransform rt)
        {
            const float dur = 0.22f, peak = 1.12f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float s = 1f + (peak - 1f) * Mathf.Sin(u * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        void SetFeedback(string s) { if (feedbackText != null) feedbackText.text = s; }
    }
}
