using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Mallang.RR02.Screens
{
    // 플레이 루프 글루(장면 레인). 로직은 전부 로직 레인의 Rr02Chart/Rr02Run/Rr02Judge가 하고,
    // 이 컴포넌트는 그걸 입력·타이밍·뷰에 잇고 '손맛(juice)'을 입힙니다.
    //   듣기(Listen): 라운드 시퀀스를 모서리 친구가 순서대로 '연주'(타격 포즈+스쿼시 스트레치)로 시연.
    //   따라치기(Repeat): 키 1~4 또는 모서리 탭 -> Rr02Run.ProcessPress, 친구가 쫀득하게 튕김.
    //   라운드 클리어 -> StageLampRow.SetCleared로 전구 보잉 점등. 실패 -> 1라운드부터 다시.
    public class Rr02PlayController : MonoBehaviour
    {
        public TextAsset chartJson;
        public StageLampRow lampRow;

        [Header("모서리 친구(입력/시연 대상)")]
        public RectTransform bubbleRabbit;
        public RectTransform bubbleCapybara;
        public RectTransform bubbleFox;
        public RectTransform bubbleSquirrel;

        [Header("타격 포즈 스프라이트(눌림/연주 순간)")]
        public Sprite hitRabbit;
        public Sprite hitCapybara;
        public Sprite hitFox;
        public Sprite hitSquirrel;

        [Header("텍스트")]
        public Text roundText;
        public Text instructionText;
        public Text feedbackText;

        [Header("타이밍(초)")]
        public float listenTailSeconds = 0.7f;
        public float clearHoldSeconds = 0.9f;
        public float popDuration = 0.34f; // 쫀득한 튕김 길이

        enum State { Listen, Repeat, Clear, Failed }

        Rr02Chart _chart;
        Rr02Run _run;
        State _state;
        double _clock;
        int _listenIndex;
        List<Rr02Note> _phrase = new List<Rr02Note>();
        int _lastNormalCleared;

        Dictionary<Rr02Friend, RectTransform> _bubbles;
        Dictionary<Rr02Friend, Image> _charImg = new Dictionary<Rr02Friend, Image>();
        Dictionary<Rr02Friend, Sprite> _idle = new Dictionary<Rr02Friend, Sprite>();
        Dictionary<Rr02Friend, Sprite> _hit = new Dictionary<Rr02Friend, Sprite>();
        Dictionary<Rr02Friend, Coroutine> _pulse = new Dictionary<Rr02Friend, Coroutine>();
        Coroutine _fbPop;

        void Awake()
        {
            _bubbles = new Dictionary<Rr02Friend, RectTransform>
            {
                { Rr02Friend.Rabbit, bubbleRabbit },
                { Rr02Friend.Capybara, bubbleCapybara },
                { Rr02Friend.Fox, bubbleFox },
                { Rr02Friend.Squirrel, bubbleSquirrel },
            };
            _hit[Rr02Friend.Rabbit] = hitRabbit;
            _hit[Rr02Friend.Capybara] = hitCapybara;
            _hit[Rr02Friend.Fox] = hitFox;
            _hit[Rr02Friend.Squirrel] = hitSquirrel;

            foreach (var pair in _bubbles)
            {
                var bubble = pair.Value; var friend = pair.Key;
                if (bubble == null) continue;
                // 캐릭터 Image(= Ring이 아닌 쪽) 찾기
                var imgs = bubble.GetComponentsInChildren<Image>();
                foreach (var im in imgs)
                {
                    if (im.gameObject.name.StartsWith("Char"))
                    {
                        _charImg[friend] = im;
                        _idle[friend] = im.sprite;
                        break;
                    }
                }
                // 탭 입력: 캐릭터 Image에 Button
                Image target = _charImg.ContainsKey(friend) ? _charImg[friend] : (imgs.Length > 0 ? imgs[0] : null);
                if (target != null)
                {
                    var btn = target.gameObject.GetComponent<Button>();
                    if (btn == null) btn = target.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    var captured = friend;
                    btn.onClick.AddListener(() => Press(captured));
                }
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
            foreach (var n in _chart.Rounds[_run.CurrentRound - 1].Notes) _phrase.Add(n);
            _phrase.Sort((a, b) => a.TargetUs.CompareTo(b.TargetUs));
            if (roundText != null) roundText.text = _run.CurrentRound + "번째 무대";
            if (instructionText != null) instructionText.text = "잘 들어요";
            SetFeedback("");
        }

        void Update()
        {
            if (_run == null) return;
            _clock += Time.deltaTime;
            if (_state == State.Listen) TickListen();
            else if (_state == State.Repeat) TickRepeat();
            else if (_state == State.Clear) { if (_clock >= clearHoldSeconds) EnterListen(); }
            else if (_state == State.Failed) { if (_clock >= clearHoldSeconds) StartRun(); }
        }

        void TickListen()
        {
            while (_listenIndex < _phrase.Count && _clock >= _phrase[_listenIndex].TargetUs / 1000000.0)
            {
                Pulse(_phrase[_listenIndex].Friend, 0.9f);
                _listenIndex++;
            }
            if (_listenIndex >= _phrase.Count)
            {
                double lastSec = _phrase.Count > 0 ? _phrase[_phrase.Count - 1].TargetUs / 1000000.0 : 0;
                if (_clock >= lastSec + listenTailSeconds)
                {
                    _run.BeginRepeat();
                    _state = State.Repeat; _clock = 0;
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
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Press(Rr02Friend.Rabbit);
            if (kb.digit2Key.wasPressedThisFrame) Press(Rr02Friend.Capybara);
            if (kb.digit3Key.wasPressedThisFrame) Press(Rr02Friend.Fox);
            if (kb.digit4Key.wasPressedThisFrame) Press(Rr02Friend.Squirrel);
        }

        public void Press(Rr02Friend friend)
        {
            if (_state != State.Repeat || _run == null) return;
            long us = (long)(_clock * 1000000.0);
            var r = _run.ProcessPress(friend, us);
            float intensity = r.Kind == Rr02InputKind.Judged && r.Judgement == Rr02Judgement.Perfect ? 1.25f : 1.0f;
            Pulse(friend, intensity);
            if (r.Kind == Rr02InputKind.Judged)
                SetFeedback(r.Judgement == Rr02Judgement.Perfect ? "Perfect!"
                    : r.Judgement == Rr02Judgement.Good ? "Good" : "Miss");
            else if (r.Kind == Rr02InputKind.Ghost)
                SetFeedback("!");
        }

        // 스쿼시-스트레치 + 타격 포즈 전환(쫀득한 손맛)
        void Pulse(Rr02Friend friend, float intensity)
        {
            RectTransform rt;
            if (_bubbles == null || !_bubbles.TryGetValue(friend, out rt) || rt == null) return;
            if (_pulse.ContainsKey(friend) && _pulse[friend] != null) StopCoroutine(_pulse[friend]);
            _pulse[friend] = StartCoroutine(PulseRoutine(friend, rt, intensity));
        }

        IEnumerator PulseRoutine(Rr02Friend friend, RectTransform rt, float intensity)
        {
            // 타격 포즈로 스왑
            Image ci = _charImg.ContainsKey(friend) ? _charImg[friend] : null;
            Sprite hit = _hit.ContainsKey(friend) ? _hit[friend] : null;
            Sprite idle = _idle.ContainsKey(friend) ? _idle[friend] : null;
            if (ci != null && hit != null) ci.sprite = hit;

            float amp = 0.26f * intensity;
            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / popDuration);
                // 스프링 펀치: 1 -> 크게 -> 작은 반동 -> 1 (쫀득)
                float punch = Mathf.Sin(u * Mathf.PI) + 0.28f * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                float sy = 1f + amp * punch;        // 세로 스트레치
                float sx = 1f - amp * 0.55f * punch; // 가로 스쿼시(부피 보존 느낌)
                rt.localScale = new Vector3(sx, sy, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (ci != null && idle != null) ci.sprite = idle; // 대기 포즈 복귀
            _pulse[friend] = null;
        }

        void SetFeedback(string s)
        {
            if (feedbackText == null) return;
            feedbackText.text = s;
            if (!string.IsNullOrEmpty(s))
            {
                if (_fbPop != null) StopCoroutine(_fbPop);
                _fbPop = StartCoroutine(FeedbackPop());
            }
        }

        IEnumerator FeedbackPop()
        {
            var rt = feedbackText.rectTransform;
            float dur = 0.3f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eob = EaseOutBack(u);
                float s = 0.6f + 0.4f * eob; // 0.6 -> 1(오버슈트)
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _fbPop = null;
        }

        static float EaseOutBack(float x)
        {
            float c1 = 1.70158f, c3 = c1 + 1f, p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
