using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Mallang.RR02.Screens
{
    // 플레이 루프 글루(장면 레인). 로직은 Rr02Chart/Rr02Run/Rr02Judge/BeatCursor(로직 레인)가 하고,
    // 이 컴포넌트는 입력·타이밍·뷰·손맛을 잇습니다.
    //   듣기(Listen): 카드에 라운드 시퀀스를 전부 보여주고, 동물들이 '다같이 리듬 그루브'(hit 아님),
    //                 파란 커서가 박자마다 카드에 딱딱 걸리며 이동.
    //   따라치기(Repeat): 숨긴 칸은 '?'로. 키 1~4 / 모서리 탭으로 따라치면 그 동물만 hit 포즈로 튕김.
    //   라운드 클리어 -> 전구 보잉 점등. 실패 -> 1라운드부터.
    [RequireComponent(typeof(AudioSource))]
    public class Rr02PlayController : MonoBehaviour
    {
        public TextAsset chartJson;
        public TextAsset benchmarkJson; // Data/ranking-benchmark.json
        public StageLampRow lampRow;

        [Header("모서리 친구")]
        public RectTransform bubbleRabbit;
        public RectTransform bubbleCapybara;
        public RectTransform bubbleFox;
        public RectTransform bubbleSquirrel;

        [Header("타격 포즈(누를 때만)")]
        public Sprite hitRabbit, hitCapybara, hitFox, hitSquirrel;
        [Header("얼굴 칩(카드 표시)")]
        public Sprite faceRabbit, faceCapybara, faceFox, faceSquirrel;
        [Header("뇌 단계(머리 위 — 맞출 때 성장)")]
        public Sprite brainStage0, brainStage1, brainStage2, brainStage3;
        [Header("소리(친구 악기 + 8박 베이스라인)")]
        public AudioClip clipRabbit, clipCapybara, clipFox, clipSquirrel, clipBaseline;

        [Header("텍스트")]
        public Text roundText, instructionText, feedbackText;

        [Header("타이밍(초)")]
        public float listenTailSeconds = 0.7f;
        public float clearHoldSeconds = 0.9f;
        public float popDuration = 0.34f;

        enum State { Intro, Listen, CountIn, Repeat, Clear, Result }

        [Header("인트로")]
        public int introBeats = 4; // 시작 전 둠칫둠칫 카운트인 박 수
        public float inputOffsetMs = 0f;   // 입력 지연 보정(전역). 0=끔. 체계적 밀림이 확인되면만 소폭 사용.
        public float repeatPrerollMs = 180f; // 따라치기 전환 직후 프리롤: 노트는 드럼 그대로, 입력만 이만큼 일찍 허용(첫 노트 조기 입력)
        public float tempoScale = 1.15f;   // 재생 템포 배율(>1 = 느리게). 차트/판정 그대로, 재생만 느려짐. 1.15 ≈ 150->130BPM
        [Range(0f, 1f)] public float baselineVolume = 0.35f; // 하이햇(베이스라인) 볼륨. 악기 소리(1.0)보다 작게.
        [Range(0f, 1f)] public float rabbitVolume = 0.7f; // 토끼 악기 소리가 다른 악기보다 커서 따로 낮춤.
        public float introRestBeats = 0.18f; // 카운트인 끝나고 '잘 들어요' 전 짧은 쉼(박)

        Rr02Chart _chart;
        Rr02Run _run;
        State _state;
        double _clock;
        int _listenIndex;
        List<Rr02Note> _phrase = new List<Rr02Note>();
        long[] _targetsUs = new long[0];
        HashSet<int> _hidden = new HashSet<int>();
        int _lastNormalCleared;
        float _beatSec = 0.4f;

        Dictionary<Rr02Friend, RectTransform> _bubbles;
        Dictionary<Rr02Friend, Image> _charImg = new Dictionary<Rr02Friend, Image>();
        Dictionary<Rr02Friend, Sprite> _idle = new Dictionary<Rr02Friend, Sprite>();
        Dictionary<Rr02Friend, Sprite> _hit = new Dictionary<Rr02Friend, Sprite>();
        Dictionary<Rr02Friend, Sprite> _face = new Dictionary<Rr02Friend, Sprite>();
        Dictionary<Rr02Friend, Coroutine> _pulse = new Dictionary<Rr02Friend, Coroutine>();
        Coroutine _fbPop;

        RectTransform _cursor;
        Vector2[] _cardPos;
        Image[] _liveFace;
        Text[] _liveQ;
        int _cursorIdx = -1;
        Coroutine _cursorPop;

        // O/X 즉각 피드백(임시 색 글자 — 나중에 OX 에셋으로 교체 예정)과 콤보 불꽃/연기.
        Text[] _oxMark;          // 카드별 O/X 오버레이
        bool[] _cardResolved;    // 카드별 이번 구절 판정 확정 여부
        Image _comboBrain;       // 상단 뇌 에셋(brain-stage0, 불꽃 없는 깔끔한 뇌)
        Image _comboFlame;       // 뇌 뒤 분홍 underglow(은은한 바닥광)
        Image[] _flameTongues;   // 뇌 주변에서 일렁이는 분홍 불꽃 혓바닥(코드 생성)
        int _combo;              // 연속으로 맞힌 노트 수
        float _flameShow;        // 불꽃 강도(0~1, 부드럽게 추종). 바닥값 항상 + 콤보로 더 타오름
        Sprite _glowSprite;      // 코드로 만든 소프트 원형 글로우(underglow·연기 공용)
        Sprite _flameSprite;     // 코드로 만든 불꽃 혓바닥(세로 물방울) 스프라이트
        public int comboFlameThreshold = 4; // 이 콤보 이상이면 불꽃이 확 타오름(바닥 불은 항상 있음)

        // 소리(플레이스홀더 — 코드 생성 음. 실제 악기 소리는 에셋으로 교체 예정).
        AudioSource _audio;
        Dictionary<Rr02Friend, AudioClip> _tone = new Dictionary<Rr02Friend, AudioClip>();
        AudioClip _clearClip, _failClip, _baselineClip;
        int _lastBeat = -1; // 8박 베이스라인 메트로놈

        // 결과(게임오버/클리어) 화면 — 전체화면(screen-result-final 수렴)
        GameObject _resultRoot;
        Text _resultScoreBig, _resultGain;
        Image _centerBrain; // 결과 중앙 뇌(선이 도착하면 팝)
        int _resultTotalTarget; // 큰 점수 롤업 목표
        // 경로 B와 같은 안전 여백 프레임(레터박스) 패턴: 고정 16:9 프레임 안에 배경·오버레이를 묶고 FitFrame으로 스케일.
        RectTransform _resultFrame;

        // 랭킹(온라인 리더보드) 화면
        GameObject _rankingRoot;
        BenchmarkTable _benchTable;
        // 경로 B: 승인 캔버스(screen-ranking)를 배경 한 장으로 쓰고, 상태로 변하는 숫자만 폰트로 오버레이.
        // 배경·오버레이를 고정 16:9 프레임의 자식으로 묶고, 화면 크기에 맞춰 안전 여백 두고 스케일(레터박스).
        RectTransform _rankingFrame;
        Text _heroScore, _heroReach, _heroGradeLabel, _heroNextChip;
        Text _myRowRank, _myRowScore;
        Text[] _friendScore = new Text[4];
        Image[] _friendBrainHead = new Image[4];
        Rr02CurveLine[] _curves = new Rr02CurveLine[4]; // 뇌 -> 친구 곡선 연결선(글로우 리본)
        Coroutine _lineAnim;
        Coroutine _introRoutine; // 카운트인(정확히 introBeats박)
        bool _audioPrimed; // 오디오 콜드스타트 워밍업 1회
        RectTransform _playUiRt; // 고정 1280x720 디자인 컨테이너(화면에 맞춰 레터박스 스케일)
        GameObject _letterbox;   // PlayUI 밖 여백을 채우는 어두운 배경
        static readonly Rr02Friend[] _friendOrder = { Rr02Friend.Rabbit, Rr02Friend.Capybara, Rr02Friend.Fox, Rr02Friend.Squirrel };
        static readonly string[] _friendSpriteName = { "friend-rabbit", "friend-capybara", "friend-fox", "friend-squirrel" };

        // 머리 위 뇌(친구별). 맞출 때마다 pop + 임시 점수로 단계 성장(단계 계산은 BrainScore.Stage).
        Dictionary<Rr02Friend, Image> _brain = new Dictionary<Rr02Friend, Image>();
        Dictionary<Rr02Friend, int> _prov = new Dictionary<Rr02Friend, int>();
        Dictionary<Rr02Friend, Coroutine> _brainPop = new Dictionary<Rr02Friend, Coroutine>();
        Sprite[] _brainStages;

        void Awake() { InitRefs(); }

        // 멱등 참조 초기화. _bubbles가 없을 때만 짓는다(도메인 리로드로 Awake 생략돼도 OnEnable에서 보장).
        void InitRefs()
        {
            if (_bubbles != null) return;
            _bubbles = new Dictionary<Rr02Friend, RectTransform>
            {
                { Rr02Friend.Rabbit, bubbleRabbit }, { Rr02Friend.Capybara, bubbleCapybara },
                { Rr02Friend.Fox, bubbleFox }, { Rr02Friend.Squirrel, bubbleSquirrel },
            };
            _hit[Rr02Friend.Rabbit]=hitRabbit; _hit[Rr02Friend.Capybara]=hitCapybara; _hit[Rr02Friend.Fox]=hitFox; _hit[Rr02Friend.Squirrel]=hitSquirrel;
            _face[Rr02Friend.Rabbit]=faceRabbit; _face[Rr02Friend.Capybara]=faceCapybara; _face[Rr02Friend.Fox]=faceFox; _face[Rr02Friend.Squirrel]=faceSquirrel;

            foreach (var pair in _bubbles)
            {
                var bubble = pair.Value; var friend = pair.Key;
                if (bubble == null) continue;
                var imgs = bubble.GetComponentsInChildren<Image>();
                Image charImg = null;
                foreach (var im in imgs) if (im.gameObject.name.StartsWith("Char")) { charImg = im; break; }
                if (charImg != null) { _charImg[friend] = charImg; _idle[friend] = charImg.sprite; }
                Image target = charImg != null ? charImg : (imgs.Length > 0 ? imgs[0] : null);
                // 클릭이 동물 버튼에 닿도록: 버튼 대상만 raycast 켜고 나머지(방울 테두리 등)는 끈다.
                foreach (var im in imgs) im.raycastTarget = (im == target);
                if (target != null)
                {
                    var captured = friend;
                    // 리듬 입력은 누르는 순간 발동(pointer down). 기존 Button onClick(=손 뗄 때)은 지운다.
                    var existing = target.gameObject.GetComponent<Button>();
                    if (existing != null) existing.onClick.RemoveAllListeners();
                    var relay = target.gameObject.GetComponent<Rr02PressRelay>() ?? target.gameObject.AddComponent<Rr02PressRelay>();
                    relay.onDown = () => Press(captured);
                }
            }
            SetupAudio();

            // 레터박스: PlayUI(고정 1280x720)를 화면에 맞춰 축소하고, 그 밖 여백을 어둡게 채운다.
            _playUiRt = GameObject.Find("PlayUI") != null ? GameObject.Find("PlayUI").transform as RectTransform : null;
            if (_playUiRt != null && _letterbox == null)
            {
                var canvasT = _playUiRt.parent;
                var bd = new GameObject("LetterboxBackdrop", typeof(RectTransform), typeof(Image));
                bd.transform.SetParent(canvasT, false);
                bd.transform.SetAsFirstSibling(); // PlayUI 뒤로
                var im = bd.GetComponent<Image>(); im.color = new Color(0.08f, 0.07f, 0.12f, 1f); im.raycastTarget = false;
                var rt = im.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                _letterbox = bd;
            }
            FitPlayUIToScreen();
        }

        // 고정 1280x720 PlayUI를 캔버스에 '전부 들어오게'(contain/letterbox) 균일 축소. 어떤 해상도·화면비에서도 안 잘림.
        void FitPlayUIToScreen()
        {
            if (_playUiRt == null) return;
            var parent = _playUiRt.parent as RectTransform;
            if (parent == null) return;
            float cw = parent.rect.width, ch = parent.rect.height;
            if (cw < 1f || ch < 1f) return;
            float s = Mathf.Min(cw / 1280f, ch / 720f) * 0.98f; // 살짝 여백
            _playUiRt.localScale = new Vector3(s, s, 1f);
        }

        // 플레이스홀더 음 생성(친구마다 다른 음정, 펜타토닉). 실제 악기 소리는 에셋으로 교체.
        void SetupAudio()
        {
            // 모바일 오디오 출력 지연 축소(best latency). 첫 Hit 밀림 완화. 시작 시 1회.
            if (!_audioPrimed)
            {
                var cfg = AudioSettings.GetConfiguration();
                if (cfg.dspBufferSize > 256) { cfg.dspBufferSize = 256; AudioSettings.Reset(cfg); }
            }
            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
            }
            if (_tone.Count == 0)
            {
                // 고른 악기 클립(없으면 코드 생성 음으로 폴백).
                _tone[Rr02Friend.Rabbit] = clipRabbit != null ? clipRabbit : MakeTone(523.25f);
                _tone[Rr02Friend.Capybara] = clipCapybara != null ? clipCapybara : MakeTone(659.25f);
                _tone[Rr02Friend.Fox] = clipFox != null ? clipFox : MakeTone(783.99f);
                _tone[Rr02Friend.Squirrel] = clipSquirrel != null ? clipSquirrel : MakeTone(1046.5f);
                _baselineClip = clipBaseline;
                _clearClip = MakeTone(880f, 0.5f);
                _failClip = MakeTone(196f, 0.45f);
            }
            // 오디오 콜드스타트 워밍업: 첫 플레이에서 첫 PlayOneShot이 묻혀 카운트인 첫 박이
            // 안 들리는 문제(첫판 3박, 재시도 4박)를 막는다. 무음(volume 0)으로 경로를 미리 깨움.
            if (!_audioPrimed && _audio != null && _baselineClip != null)
            {
                _audio.PlayOneShot(_baselineClip, 0f);
                _audioPrimed = true;
            }
        }

        // 사인 + 빠른 어택·지수 감쇠로 마림바풍 소리 한 음.
        AudioClip MakeTone(float freq, float dur = 0.35f)
        {
            int sr = 44100; int n = (int)(sr * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 200f); // 감쇠 + 짧은 어택
                float w = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                s[i] = w * env * 0.4f;
            }
            var clip = AudioClip.Create("tone_" + (int)freq, n, 1, sr, false);
            clip.SetData(s, 0);
            return clip;
        }

        void PlayTone(Rr02Friend f)
        {
            if (_audio == null || !_tone.ContainsKey(f) || _tone[f] == null) return;
            float vol = f == Rr02Friend.Rabbit ? rabbitVolume : 1f; // 토끼만 볼륨 낮춤
            _audio.PlayOneShot(_tone[f], vol);
        }

        void OnEnable()
        {
            InitRefs();
            // 차트는 StartRun에서 판마다 절차적으로 생성한다(chartJson은 더는 쓰지 않음).
            if (benchmarkJson != null) _benchTable = RankingRules.ParseBenchmark(benchmarkJson.text);
            BuildGrid();
            BuildResult();
            BuildRanking();
            StartRun();
        }

        // 결과 화면(런타임 구성, 전체화면): 축제 광장 배경 위로 "내 리듬 점수" -> 큰 점수 방울 -> +증가 ->
        // 중앙 뇌 -> (뇌에서 각 친구로 뻗는 곡선 글로우 선) -> 네 친구(머리 위 뇌 + 점수판 + NEW 배지) -> 다음 버튼.
        // 랭킹 화면과 같은 안전 여백 프레임(레터박스) 패턴을 그대로 따른다(FitFrame으로 화면 크기에 맞춰 스케일).
        void BuildResult()
        {
            if (_resultRoot != null) return;
            var canvas = GameObject.Find("PlayUI");
            if (canvas == null) return;
            var font = LoadDisplayFont();

            _resultRoot = new GameObject("ResultRoot", typeof(RectTransform));
            _resultRoot.transform.SetParent(canvas.transform, false);
            var rr = (RectTransform)_resultRoot.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

            // 레터박스 바(프레임 밖 여백)를 어둡게 채움.
            var backdrop = NewImage("Backdrop", _resultRoot.transform, null, false);
            backdrop.color = new Color(0.12f, 0.1f, 0.18f, 1f);
            var bdrt = backdrop.rectTransform; bdrt.anchorMin = Vector2.zero; bdrt.anchorMax = Vector2.one; bdrt.offsetMin = bdrt.offsetMax = Vector2.zero;

            // 고정 16:9 프레임(1280x720). 배경·오버레이를 모두 이 프레임 자식으로 둬 한 덩어리로 스케일.
            // ShowResult에서 현재 화면 크기에 맞춰 안전 여백 두고 축소(어떤 화면비에서도 레터박스·모서리 안 붙음).
            var frameGo = new GameObject("Frame", typeof(RectTransform));
            frameGo.transform.SetParent(_resultRoot.transform, false);
            _resultFrame = (RectTransform)frameGo.transform;
            _resultFrame.anchorMin = _resultFrame.anchorMax = new Vector2(0.5f, 0.5f);
            _resultFrame.pivot = new Vector2(0.5f, 0.5f);
            _resultFrame.sizeDelta = new Vector2(1280, 720);
            _resultFrame.anchoredPosition = Vector2.zero;

            // 배경(축제 광장)
            var bg = NewImage("Bg", _resultFrame, LoadSprite("stage-background"), false);
            var bgrt = bg.rectTransform; bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

            // 뇌 -> 각 친구로 흐르는 곡선 연결선(진행도 0에서 시작, ShowResult에서 자람).
            // 배경 바로 뒤에 만들어 그 뒤 생성되는 뇌·친구가 형제 순서상 위(앞)에 그려지게 한다.
            float[] fx = { -440, -150, 150, 440 };
            Vector2 brainPt = new Vector2(0, 60);
            Color[] lineCol = {
                new Color(1f, 0.6f, 0.78f, 1f), new Color(0.6f, 0.9f, 0.62f, 1f),
                new Color(0.6f, 0.8f, 1f, 1f), new Color(1f, 0.85f, 0.5f, 1f) };
            float[] bow = { 30f, 15f, 15f, 30f };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Curve" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Rr02CurveLine));
                go.transform.SetParent(_resultFrame, false);
                var curve = go.GetComponent<Rr02CurveLine>();
                var crt = (RectTransform)curve.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(1280, 720);
                curve.color = lineCol[i]; curve.width = 12f; curve.glowWidth = 28f;
                // 친구 뇌(progress 0)에서 출발해 중앙 큰 뇌(progress 1)로 이어지는 흐름.
                curve.SetEndpoints(new Vector2(fx[i], -15), brainPt, bow[i]);
                curve.progress = 0f;
                curve.raycastTarget = false;
                _curves[i] = curve;
            }

            // 상단: 라벨 + 큰 점수(방울판) + +증가 + 중앙 뇌(연결선 위에 그려짐)
            var lbl = MakeText(_resultFrame, font, 38, new Vector2(0, 320), new Vector2(520, 70));
            lbl.text = "내 리듬 점수"; lbl.color = Color.white; AddOutline(lbl.gameObject, 0.2f, 0.25f, 0.4f);

            var scoreBubble = NewImage("ScoreBubble", _resultFrame, LoadSprite("score-bubble"), false);
            var sbrt = scoreBubble.rectTransform; sbrt.anchoredPosition = new Vector2(0, 208); sbrt.sizeDelta = new Vector2(420, 150);
            _resultScoreBig = MakeText(_resultFrame, font, 84, new Vector2(0, 208), new Vector2(380, 130));
            _resultScoreBig.color = new Color(0.95f, 0.35f, 0.45f, 1f); AddOutline(_resultScoreBig.gameObject, 1f, 1f, 1f, 1f, 4f);

            _resultGain = MakeText(_resultFrame, font, 32, new Vector2(0, 150), new Vector2(400, 60));
            _resultGain.color = new Color(0.95f, 0.45f, 0.6f, 1f); AddOutline(_resultGain.gameObject, 1f, 1f, 1f, 1f);

            _centerBrain = NewImage("CenterBrain", _resultFrame, LoadSprite("brain-stage0"), true);
            var cbrt = _centerBrain.rectTransform; cbrt.anchoredPosition = new Vector2(0, 60); cbrt.sizeDelta = new Vector2(190, 190);

            // 네 친구 + 머리 위 뇌 + 점수판(+NEW 배지)
            for (int i = 0; i < 4; i++)
            {
                var f = NewImage(_friendSpriteName[i], _resultFrame, LoadSprite(_friendSpriteName[i]), true);
                var frt = f.rectTransform; frt.anchoredPosition = new Vector2(fx[i], -120); frt.sizeDelta = new Vector2(220, 220);

                var hb = NewImage("Brain" + i, _resultFrame, LoadSprite("brain-stage0"), true);
                var hbrt = hb.rectTransform; hbrt.anchoredPosition = new Vector2(fx[i], -15); hbrt.sizeDelta = new Vector2(62, 62);
                _friendBrainHead[i] = hb;

                var plate = NewImage("ScorePlate" + i, _resultFrame, LoadSprite("score-plate"), false);
                var plrt = plate.rectTransform; plrt.anchoredPosition = new Vector2(fx[i], -250); plrt.sizeDelta = new Vector2(210, 74);
                var st = MakeText(_resultFrame, font, 40, new Vector2(fx[i], -250), new Vector2(190, 60));
                st.color = new Color(0.33f, 0.22f, 0.13f, 1f); AddOutline(st.gameObject, 1f, 1f, 1f, 1f);
                _friendScore[i] = st;

                var badge = NewImage("BadgeNew" + i, _resultFrame, LoadSprite("badge-new"), false);
                var badrt = badge.rectTransform; badrt.anchoredPosition = new Vector2(fx[i] + 95, -230); badrt.sizeDelta = new Vector2(56, 34);
            }

            // 다음 버튼 — 맨 아래. 프레임(720 높이) 안쪽 y=-320이라 안전 여백 안에서도 안 잘림.
            var btnGo = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_resultFrame, false);
            var bi = btnGo.GetComponent<Image>(); bi.sprite = LoadSprite("button-primary"); bi.preserveAspect = false;
            var brt2 = bi.rectTransform; brt2.anchorMin = brt2.anchorMax = new Vector2(0.5f, 0.5f); brt2.pivot = new Vector2(0.5f, 0.5f);
            brt2.anchoredPosition = new Vector2(0, -320); brt2.sizeDelta = new Vector2(300, 96);
            var bt = btnGo.GetComponent<Button>(); bt.transition = Selectable.Transition.None; bt.onClick.AddListener(OnNext);
            var bl = MakeText(btnGo.transform, font, 38, Vector2.zero, new Vector2(280, 90));
            bl.text = "다음"; bl.color = new Color(0.33f, 0.22f, 0.13f, 1f); bl.raycastTarget = false;

            _resultRoot.SetActive(false);
        }

        Image NewImage(string name, Transform parent, Sprite sprite, bool preserve)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = sprite; img.preserveAspect = preserve; img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        void AddOutline(GameObject go, float r, float g, float b, float a = 1f, float dist = 2.5f)
        {
            var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            o.effectColor = new Color(r, g, b, a); o.effectDistance = new Vector2(dist, -dist);
        }

        Text MakeText(Transform parent, Font font, int size, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = sz;
            return t;
        }

        // 빌드 포함을 위해 Resources에서 로드(에디터·기기 모두 동작). 아트는 Resources/Art 아래.
        Sprite LoadSprite(string n) { return Resources.Load<Sprite>("Art/" + n); }

        // 에디터에선 AssetDatabase, 빌드에선 이미 씬에 들어간 스프라이트를 재사용(여기선 간단히 에디터 로드).
        static Sprite UnityEditor_LoadOrNull(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }

        void ShowResult()
        {
            var res = _run.Result;
            _resultTotalTarget = res.TotalScore;
            // 큰 점수·증가분은 0에서 시작해 시퀀스에서 롤업. 친구 점수판은 바로 표시.
            if (_resultScoreBig != null) _resultScoreBig.text = "0";
            if (_resultGain != null) _resultGain.text = "+0";
            for (int i = 0; i < 4; i++)
            {
                int sc = res.AnimalScores != null && res.AnimalScores.ContainsKey(_friendOrder[i]) ? res.AnimalScores[_friendOrder[i]] : 0;
                if (_friendScore[i] != null) _friendScore[i].text = Comma(sc);
                if (_friendBrainHead[i] != null) _friendBrainHead[i].sprite = LoadSprite("brain-stage0");
            }
            if (_resultRoot != null) _resultRoot.SetActive(true);
            // 화면 맞춤은 PlayUI 전체 레터박스(FitPlayUIToScreen)가 담당.
            _state = State.Result;
            for (int i = 0; i < 4; i++)
            {
                if (_curves[i] == null) continue;
                _curves[i].progress = 0f; _curves[i].SetVerticesDirty();
            }
            if (_centerBrain != null) _centerBrain.rectTransform.localScale = Vector3.one;
            if (_lineAnim != null) StopCoroutine(_lineAnim);
            if (isActiveAndEnabled) _lineAnim = StartCoroutine(ResultSequence());
        }

        // 네 친구 뇌에서 중앙 큰 뇌로 선이 흐르고 -> 중앙 뇌 팝 + 큰 점수 0->목표 롤업.
        IEnumerator ResultSequence()
        {
            yield return new WaitForSeconds(0.3f);
            float dur = 0.5f;
            for (int i = 0; i < 4; i++)
            {
                if (_curves[i] == null) continue;
                StartCoroutine(OneCurveGrow(_curves[i], dur)); // progress 0(친구)->1(중앙)
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(dur * 0.6f); // 선이 중앙에 거의 도착할 즈음
            if (_centerBrain != null) StartCoroutine(BrainPop(_centerBrain.rectTransform));
            StartCoroutine(CountUp(_resultScoreBig, _resultTotalTarget, 0.8f, ""));
            StartCoroutine(CountUp(_resultGain, _resultTotalTarget, 0.8f, "+"));
        }

        IEnumerator OneCurveGrow(Rr02CurveLine curve, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - u) * (1f - u); // ease-out
                curve.progress = e; curve.SetVerticesDirty();
                yield return null;
            }
            curve.progress = 1f; curve.SetVerticesDirty();
        }

        // 숫자 0 -> target 롤업(ease-out), 천단위 콤마.
        IEnumerator CountUp(Text label, int target, float dur, string prefix)
        {
            if (label == null) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - u) * (1f - u);
                label.text = prefix + Comma(Mathf.RoundToInt(target * e));
                yield return null;
            }
            label.text = prefix + Comma(target);
        }

        // 중앙 뇌가 크게 떴다가 제자리로(선 도착 임팩트).
        IEnumerator BrainPop(RectTransform rt)
        {
            float t = 0f, dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - u) * (1f - u);
                rt.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, e);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        static string Comma(int n) { return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }

        // 결과 -> 랭킹 화면으로.
        void OnNext()
        {
            if (_resultRoot != null) _resultRoot.SetActive(false);
            ShowRanking();
        }

        // 랭킹(온라인 리더보드) 화면 구성: 히어로 바 + 리더보드 패널(헤더+10행+⋯+내 행) + 업적 배지 + 버튼 3개. 기본 숨김.
        void BuildRanking()
        {
            if (_rankingRoot != null) return;
            var canvas = GameObject.Find("PlayUI");
            if (canvas == null) return;
            var font = LoadDisplayFont();
            Color cream = new Color(1f, 0.95f, 0.84f, 1f);
            Color green = new Color(0.78f, 0.92f, 0.57f, 1f);
            Color brown = new Color(0.33f, 0.22f, 0.13f, 1f);

            _rankingRoot = new GameObject("RankingRoot", typeof(RectTransform));
            _rankingRoot.transform.SetParent(canvas.transform, false);
            var rr = (RectTransform)_rankingRoot.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

            // 레터박스 바(프레임 밖 여백)를 어둡게 채움.
            var backdrop = NewImage("Backdrop", _rankingRoot.transform, null, false);
            backdrop.color = new Color(0.12f, 0.1f, 0.18f, 1f);
            var bdrt = backdrop.rectTransform; bdrt.anchorMin = Vector2.zero; bdrt.anchorMax = Vector2.one; bdrt.offsetMin = bdrt.offsetMax = Vector2.zero;

            // 고정 16:9 프레임(1280x720). 배경·오버레이를 모두 이 프레임 자식으로 둬 한 덩어리로 스케일.
            // ShowRanking에서 현재 화면 크기에 맞춰 안전 여백 두고 축소(어떤 화면비에서도 레터박스·모서리 안 붙음).
            var frameGo = new GameObject("Frame", typeof(RectTransform));
            frameGo.transform.SetParent(_rankingRoot.transform, false);
            _rankingFrame = (RectTransform)frameGo.transform;
            _rankingFrame.anchorMin = _rankingFrame.anchorMax = new Vector2(0.5f, 0.5f);
            _rankingFrame.pivot = new Vector2(0.5f, 0.5f);
            _rankingFrame.sizeDelta = new Vector2(1280, 720);
            _rankingFrame.anchoredPosition = Vector2.zero;

            // 배경: 승인 캔버스 한 장(고정 구성). 프레임(16:9)에 꽉 채움 — 비율 일치라 왜곡 없음.
            // 상위 10행·메달·배지·버튼 아트가 전부 베이크됨. 코드가 그릴 건 '상태로 변하는 숫자'뿐(원칙 6).
            var bg = NewImage("Bg", _rankingFrame, LoadSprite("screen-ranking"), false);
            var bgrt = bg.rectTransform; bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

            // 변하는 숫자만 오버레이(프레임 자식 = 1280x720 좌표, 프레임과 함께 스케일).
            // 등급 리본·다음 칩은 삽화라 베이크 유지(프로토타입: 대부분 암보 합주). 숫자만 라이브.
            _heroScore = CoverText(_rankingFrame, font, 40, new Vector2(-44, 276), new Vector2(210, 58), cream, brown, TextAnchor.MiddleCenter);
            _heroReach = CoverText(_rankingFrame, font, 40, new Vector2(196, 276), new Vector2(224, 58), cream, brown, TextAnchor.MiddleCenter);
            _myRowRank = CoverText(_rankingFrame, font, 24, new Vector2(-312, -161), new Vector2(145, 36), green, brown, TextAnchor.MiddleCenter);
            _myRowScore = CoverText(_rankingFrame, font, 24, new Vector2(304, -161), new Vector2(165, 36), green, brown, TextAnchor.MiddleRight);

            // 베이크된 버튼 아트 위에 투명 클릭 영역.
            InvisibleButton(_rankingFrame, new Vector2(-360, -305), new Vector2(360, 108), OnRetry);
            InvisibleButton(_rankingFrame, new Vector2(0, -305), new Vector2(360, 108), OnShare);
            InvisibleButton(_rankingFrame, new Vector2(360, -305), new Vector2(360, 108), OnTitle);

            _rankingRoot.SetActive(false);
        }

        // 고정 디자인 프레임을 현재 부모(캔버스) 크기에 맞춰 안전 여백 두고 축소. 어떤 화면비에서도 레터박스.
        void FitFrame(RectTransform frame, float designW, float designH, float margin)
        {
            if (frame == null) return;
            var parent = frame.parent as RectTransform;
            if (parent == null) return;
            Canvas.ForceUpdateCanvases();
            float cw = parent.rect.width, ch = parent.rect.height;
            if (cw < 1f || ch < 1f) return;
            float s = Mathf.Min(cw * (1f - margin) / designW, ch * (1f - margin) / designH);
            frame.localScale = new Vector3(s, s, 1f);
        }

        // 디스플레이 폰트(BMJUA 주아체). 없으면 기본 폰트 폴백.
        Font LoadDisplayFont()
        {
            var f = Resources.Load<Font>("Fonts/BMJUA");
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // 가리개 플레이트(베이크된 샘플 숫자를 덮음) + 그 위 폰트 텍스트. 텍스트 반환.
        Text CoverText(Transform parent, Font font, int size, Vector2 pos, Vector2 plate, Color plateColor, Color textColor, TextAnchor align)
        {
            var cover = NewImage("Cover", parent, null, false); cover.color = plateColor;
            var crt = cover.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = pos; crt.sizeDelta = plate;
            var t = MakeText(cover.transform, font, size, Vector2.zero, new Vector2(plate.x - 8, plate.y - 4));
            t.color = textColor; t.alignment = align; t.fontStyle = FontStyle.Normal; t.raycastTarget = false;
            return t;
        }

        // 베이크된 버튼 아트 위의 투명 클릭 영역(아트는 캔버스에, 입력만 코드).
        void InvisibleButton(Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject("BtnZone", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bt = go.GetComponent<Button>(); bt.transition = Selectable.Transition.None; bt.onClick.AddListener(cb);
        }

        void MakeRibbonButton(Transform parent, Font font, string label, Vector2 pos, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var bi = go.GetComponent<Image>(); bi.sprite = LoadSprite("banner-ribbon"); bi.preserveAspect = false;
            var rt = bi.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(340, 100);
            var bt = go.GetComponent<Button>(); bt.transition = Selectable.Transition.None; bt.onClick.AddListener(cb);
            var tl = MakeText(go.transform, font, 36, Vector2.zero, new Vector2(280, 70)); tl.text = label; tl.color = new Color(0.33f, 0.22f, 0.13f, 1f); tl.raycastTarget = false;
        }

        static readonly string[] _gradeOrder = { "첫 합주 도전", "악보 합주", "기억 합주", "암보 합주", "All 마스터" };

        Sprite FaceSprite(int avatar)
        {
            switch (avatar)
            {
                case 0: return LoadSprite("face-rabbit");
                case 1: return LoadSprite("face-capybara");
                case 2: return LoadSprite("face-fox");
                default: return LoadSprite("face-squirrel");
            }
        }

        Color RowTint(int i, bool isMe)
        {
            if (isMe) return new Color(0.7f, 0.9f, 0.6f, 1f);
            if (i == 0) return new Color(1f, 0.85f, 0.4f, 1f);
            if (i == 1) return new Color(0.8f, 0.82f, 0.9f, 1f);
            if (i == 2) return new Color(0.9f, 0.7f, 0.55f, 1f);
            return new Color(1f, 1f, 1f, 0f);
        }

        void ShowRanking()
        {
            var res = _run.Result;
            var board = MockLeaderboard.Build(res.TotalScore, "나", 0);
            // 경로 B: 상태로 변하는 숫자만 오버레이. 10행·등급 리본·배지는 캔버스에 베이크.
            if (_heroScore != null) _heroScore.text = Comma(res.TotalScore);
            if (_heroReach != null) _heroReach.text = (res.NormalCleared >= 9 ? "All" : res.NormalCleared + "라운드");
            if (_myRowRank != null) _myRowRank.text = Comma(board.Me.Rank) + "등";
            if (_myRowScore != null) _myRowScore.text = Comma(board.Me.Score);

            if (_rankingRoot != null) _rankingRoot.SetActive(true);
            // 화면 맞춤은 PlayUI 전체 레터박스(FitPlayUIToScreen)가 담당 — 개별 프레임은 1280x720을 채움.
            _state = State.Result;
        }

        void OnShare()
        {
            Debug.Log("[RR02] 공유하기 (추후 구현)");
        }

        void OnRetry()
        {
            if (_resultRoot != null) _resultRoot.SetActive(false);
            if (_rankingRoot != null) _rankingRoot.SetActive(false);
            StartRun();
        }

        void OnTitle()
        {
            // 타이틀 화면 미구현 — 지금은 다시 시작(임시).
            if (_rankingRoot != null) _rankingRoot.SetActive(false);
            StartRun();
        }

        static Sprite UnityEditor_RoundedSprite()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
#else
            return null;
#endif
        }

        // 각 친구의 머리 위(중앙)에 뇌 Image를 만든다. 위치·크기는 조정 가능.
        void BuildBrains()
        {
            foreach (var pair in _bubbles)
            {
                var bubble = pair.Value; var friend = pair.Key;
                if (bubble == null) continue;
                float bw = bubble.sizeDelta.x;
                var go = new GameObject("Brain", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(bubble, false);
                var img = go.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false;
                var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, bw * 0.16f); // 동물 머리 안(학사모 아래 머리 영역)
                rt.sizeDelta = new Vector2(bw * 0.26f, bw * 0.26f);
                img.enabled = false; // 처음엔 없음(첫 성공 때 생김)
                _brain[friend] = img;
                _prov[friend] = 0;
            }
        }

        // 카드 슬롯에 런타임 얼굴 Image + '?' Text를 만들고, 커서를 카드 뒤로 보냅니다.
        void BuildGrid()
        {
            var panel = GameObject.Find("ScorePanel");
            if (panel == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var pos = new List<Vector2>(); var faces = new List<Image>(); var qs = new List<Text>();
            // 정적 미리보기 Face*/Q*는 숨김
            foreach (Transform c in panel.transform)
                if (c.name.StartsWith("Face") || c.name.StartsWith("Q")) c.gameObject.SetActive(false);

            // 멜로디 8노트(4분의 8박자): 카드 8개를 악보(panel-score 680x440)에 2×4 그리드로. 뇌 영역은 제거.
            const int cardCount = 8;
            const float cardSize = 150f;
            float[] colX = { -237f, -79f, 79f, 237f };
            float[] rowY = { 80f, -80f };

            var marks = new List<Text>();
            for (int i = 0; i < cardCount; i++)
            {
                var card = panel.transform.Find("Card" + i);
                if (card == null) continue;
                var crt = (RectTransform)card;
                crt.gameObject.SetActive(true);
                crt.anchoredPosition = new Vector2(colX[i % 4], rowY[i / 4]);
                crt.sizeDelta = new Vector2(cardSize, cardSize);
                pos.Add(crt.anchoredPosition);

                var fgo = new GameObject("LiveFace" + i, typeof(RectTransform), typeof(Image));
                fgo.transform.SetParent(card, false);
                var fim = fgo.GetComponent<Image>(); fim.preserveAspect = true; fim.raycastTarget = false;
                var frt = fim.rectTransform; frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f); frt.pivot = new Vector2(0.5f, 0.5f);
                frt.anchoredPosition = Vector2.zero; frt.sizeDelta = crt.sizeDelta * 0.82f;
                faces.Add(fim);

                var qgo = new GameObject("LiveQ" + i, typeof(RectTransform), typeof(Text));
                qgo.transform.SetParent(card, false);
                var q = qgo.GetComponent<Text>(); q.font = font; q.text = "?"; q.fontSize = 64; q.fontStyle = FontStyle.Bold;
                q.alignment = TextAnchor.MiddleCenter; q.color = new Color(0.45f, 0.37f, 0.28f, 1f); q.raycastTarget = false;
                var qrt = q.rectTransform; qrt.anchorMin = qrt.anchorMax = new Vector2(0.5f, 0.5f); qrt.pivot = new Vector2(0.5f, 0.5f);
                qrt.anchoredPosition = Vector2.zero; qrt.sizeDelta = crt.sizeDelta;
                qs.Add(q);

                // O/X 즉각 피드백 오버레이(칩 위에 덮어씀). 맞으면 파란 O, 틀리면(Miss) 분홍 X. 임시 색 글자.
                var mgo = new GameObject("OX" + i, typeof(RectTransform), typeof(Text));
                mgo.transform.SetParent(card, false);
                var m = mgo.GetComponent<Text>(); m.font = font; m.text = ""; m.fontSize = 96; m.fontStyle = FontStyle.Bold;
                m.alignment = TextAnchor.MiddleCenter; m.raycastTarget = false; m.enabled = false;
                var mrt = m.rectTransform; mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f); mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.anchoredPosition = Vector2.zero; mrt.sizeDelta = crt.sizeDelta;
                AddOutline(mgo, 1f, 1f, 1f, 1f, 3f);
                marks.Add(m);
            }
            _cardPos = pos.ToArray(); _liveFace = faces.ToArray(); _liveQ = qs.ToArray();
            _oxMark = marks.ToArray();
            _cardResolved = new bool[_oxMark.Length];

            var sel = panel.transform.Find("Select");
            if (sel != null)
            {
                _cursor = (RectTransform)sel;
                _cursor.sizeDelta = new Vector2(cardSize + 14f, cardSize + 14f);
                _cursor.SetAsFirstSibling();
            } // 카드 뒤 = 테두리 하이라이트
            // 뇌 영역(BuildComboArea)은 제거됨 — 플레이 화면에서 뇌·불꽃 표시 안 함.
        }

        // 악보 윗줄(상단)에 뇌(brain-stage0, 불꽃 없는 깔끔한 뇌)를 놓고, 그 '뒤'에 코드로 그린 분홍 불꽃을 깐다.
        // 불꽃 = 은은한 underglow(원형) + 뇌 윗동을 따라 솟는 불꽃 혓바닥 여러 개가 일렁인다. 바닥 불은 항상, 콤보면 확 타오름.
        void BuildComboArea(Transform panel)
        {
            if (_comboBrain != null) return;
            const float brainY = 95f, brainSize = 172f, r = brainSize * 0.5f;

            // 1) underglow(바닥광): 뇌 뒤 분홍 원형 글로우.
            _comboFlame = NewImage("ComboFlame", panel, GlowSprite(), false);
            _comboFlame.rectTransform.anchoredPosition = new Vector2(0f, brainY + 6f);
            _comboFlame.rectTransform.sizeDelta = new Vector2(brainSize * 1.5f, brainSize * 1.5f);
            _comboFlame.color = new Color(1f, 0.40f, 0.62f, 0f);

            // 2) 불꽃 혓바닥: 뇌 윗동(상단 반원)을 따라 7개. 밑동은 뇌 뒤에 숨고 끝이 뇌 위로 솟는다.
            const int tongues = 7;
            _flameTongues = new Image[tongues];
            for (int i = 0; i < tongues; i++)
            {
                float t = tongues == 1 ? 0.5f : (float)i / (tongues - 1); // 0..1
                float ang = Mathf.Lerp(150f, 30f, t) * Mathf.Deg2Rad;      // 상단 반원(왼→오)
                float px = Mathf.Cos(ang) * r * 0.62f;
                float py = brainY + Mathf.Sin(ang) * r * 0.35f + 18f;      // 뇌 윗동 근처
                var fl = NewImage("FlameTongue" + i, panel, FlameSprite(), false);
                bool mid = Mathf.Abs(t - 0.5f) < 0.2f;
                float w = mid ? 54f : 40f, h = mid ? 150f : 110f;          // 가운데가 더 큼
                fl.rectTransform.sizeDelta = new Vector2(w, h);
                fl.rectTransform.pivot = new Vector2(0.5f, 0f);            // 밑동 기준으로 위로 자람
                fl.rectTransform.anchoredPosition = new Vector2(px, py);
                fl.color = new Color(1f, 0.42f, 0.66f, 0f);
                _flameTongues[i] = fl;
            }

            // 3) 뇌: 불꽃보다 위에(마지막 형제).
            _comboBrain = NewImage("ComboBrain", panel, LoadSprite("brain-stage0"), true);
            _comboBrain.rectTransform.anchoredPosition = new Vector2(0f, brainY);
            _comboBrain.rectTransform.sizeDelta = new Vector2(brainSize, brainSize);
        }

        // 코드로 만든 불꽃 혓바닥(세로 물방울): 밑동은 둥글고 위로 뾰족, 가장자리는 부드럽게. 흰색이라 color로 분홍 입힘.
        Sprite FlameSprite()
        {
            if (_flameSprite != null) return _flameSprite;
            int w = 48, h = 96; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * h]; float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                float ty = (float)y / (h - 1); // 0 밑동 → 1 끝
                float taper = ty < 0.15f ? Mathf.Lerp(0.55f, 1f, ty / 0.15f) : Mathf.Pow(1f - ty, 0.75f);
                float halfW = taper * (w * 0.5f) * 0.92f;
                float vfade = Mathf.Clamp01(ty / 0.12f) * Mathf.Clamp01((1f - ty) / 0.12f + 0.25f); // 밑·끝 페이드
                for (int x = 0; x < w; x++)
                {
                    float edge = halfW > 0.001f ? Mathf.Clamp01(1f - Mathf.Abs(x - cx) / halfW) : 0f;
                    edge = edge * edge;
                    px[y * w + x] = new Color(1f, 1f, 1f, edge * vfade);
                }
            }
            tex.SetPixels(px); tex.Apply();
            _flameSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _flameSprite;
        }

        // 코드로 만든 소프트 원형 글로우 스프라이트(불꽃·연기 공용). 흰색이라 color로 색을 입힌다.
        Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[n * n]; float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d); a = a * a; // 중심이 진하고 가장자리로 부드럽게
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }

        int CardIndexForNote(int noteId)
        {
            for (int i = 0; i < _phrase.Count; i++) if (_phrase[i].NoteId == noteId) return i;
            return -1;
        }

        // 카드 i에 O(맞음, 파랑)/X(틀림, 분홍)를 덮어씌운다. 임시 색 글자 — 나중에 OX 에셋으로 교체.
        void MarkOX(int cardIdx, bool hit)
        {
            if (_oxMark == null || cardIdx < 0 || cardIdx >= _oxMark.Length) return;
            var m = _oxMark[cardIdx]; if (m == null) return;
            m.text = hit ? "O" : "X";
            m.color = hit ? new Color(0.20f, 0.60f, 1f, 1f) : new Color(1f, 0.30f, 0.55f, 1f);
            m.enabled = true;
            StartCoroutine(OXPop(m.rectTransform));
        }

        IEnumerator OXPop(RectTransform rt)
        {
            float t = 0f, dur = 0.22f;
            while (t < dur) { t += Time.deltaTime; float u = Mathf.Clamp01(t / dur); float s = Mathf.LerpUnclamped(1.5f, 1f, EaseOutBack(u)); rt.localScale = new Vector3(s, s, 1f); yield return null; }
            rt.localScale = Vector3.one;
        }

        void ClearOXMarks()
        {
            if (_oxMark == null) return;
            for (int i = 0; i < _oxMark.Length; i++)
            {
                if (_oxMark[i] != null) { _oxMark[i].text = ""; _oxMark[i].enabled = false; }
                if (_cardResolved != null && i < _cardResolved.Length) _cardResolved[i] = false;
            }
        }

        // 콤보 끊김: 불꽃이 타오르던 중이면 꺼지고 회색 연기 한 줄이 피어오른다.
        void BreakCombo()
        {
            bool wasBurning = _combo >= comboFlameThreshold;
            _combo = 0;
            if (wasBurning) SpawnSmoke();
        }

        void SpawnSmoke()
        {
            if (_comboBrain == null) return;
            var img = NewImage("ComboSmoke", _comboBrain.transform.parent, GlowSprite(), true);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(54f, 72f);
            rt.anchoredPosition = _comboBrain.rectTransform.anchoredPosition + new Vector2(0f, 20f);
            StartCoroutine(SmokeRise(rt, img));
        }

        IEnumerator SmokeRise(RectTransform rt, Image img)
        {
            float t = 0f, dur = 1.3f; Vector2 start = rt.anchoredPosition;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                rt.anchoredPosition = start + new Vector2(Mathf.Sin(u * 6.2831f) * 10f, u * 95f);
                float a = Mathf.Sin(u * Mathf.PI) * 0.5f; // 떠오르며 생겼다 사라짐
                img.color = new Color(0.62f, 0.62f, 0.66f, a);
                float s = 0.5f + u * 1.0f;
                rt.localScale = new Vector3(s * 0.8f, s * 1.2f, 1f);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        // 분홍 불꽃 일렁임(매 프레임). 바닥 불은 항상 은은하게, 콤보가 쌓이면 확 타오른다.
        void ComboUpdate()
        {
            if (_comboFlame == null) return;
            // 강도: 바닥값 0.38 + 콤보로 1.0까지. 콤보가 끊겨도 0으로 안 꺼지고 바닥 불은 남는다.
            float target = Mathf.Lerp(0.38f, 1f, Mathf.Clamp01(_combo / 10f));
            _flameShow = Mathf.MoveTowards(_flameShow, target, Time.deltaTime * 2.2f);

            // underglow: 뇌 뒤 분홍 바닥광이 숨 쉬듯.
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            _comboFlame.color = new Color(1f, 0.40f, 0.62f, _flameShow * 0.32f * breathe);
            float gs = 0.92f + 0.10f * breathe;
            _comboFlame.rectTransform.localScale = new Vector3(gs, gs, 1f);

            // 불꽃 혓바닥: 각자 다른 위상으로 높이·알파·좌우가 흔들린다(진짜 불처럼).
            if (_flameTongues == null) return;
            for (int i = 0; i < _flameTongues.Length; i++)
            {
                var fl = _flameTongues[i]; if (fl == null) continue;
                float n1 = Mathf.PerlinNoise(Time.time * 6.5f + i * 11.7f, 0.3f);      // 높이·알파
                float n2 = Mathf.PerlinNoise(0.7f, Time.time * 5.0f + i * 4.3f) - 0.5f; // 좌우 흔들림
                float flick = 0.55f + 0.45f * n1;
                float a = _flameShow * flick * 0.85f;
                // 분홍 핫→연분홍: 강할수록 밝고 흰기. 끝으로 갈수록 연해지는 건 스프라이트 vfade가 담당.
                fl.color = new Color(1f, 0.38f + 0.18f * flick, 0.60f + 0.22f * flick, a);
                float hy = (0.72f + 0.55f * n1) * (0.7f + 0.5f * _flameShow); // 세로로 춤추듯
                float hx = 0.9f + 0.12f * n1;
                fl.rectTransform.localScale = new Vector3(hx, hy, 1f);
                var p = fl.rectTransform.anchoredPosition;
                fl.rectTransform.anchoredPosition = new Vector2(p.x, p.y); // 위치 고정(흔들림은 회전으로)
                fl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, n2 * 14f); // 좌우로 날름
            }
        }

        int _runCount;
        void StartRun()
        {
            // 판마다 새 시드로 절차적 랜덤 악보 생성(다시 시도 때마다 새 악보, 한 판 안에서도 라운드마다 다름).
            int seed = System.Environment.TickCount ^ (_runCount++ * 7919);
            _chart = Rr02Chart.Generate(seed);
            _run = new Rr02Run(_chart);
            _lastNormalCleared = 0;
            _combo = 0; _flameShow = 0f; // 새 판: 콤보·불꽃 초기화
            if (lampRow != null) lampRow.SetClearedInstant(0);
            EnterIntro();
        }

        Sprite BrainSprite(int stage)
        {
            if (_brainStages == null || _brainStages.Length == 0) return null;
            return _brainStages[Mathf.Clamp(stage, 0, _brainStages.Length - 1)];
        }

        // 단계별 기본 크기: 점점 커지다 최대(불붙음)에서 가장 큼.
        static float BrainBaseScale(int stage)
        {
            switch (Mathf.Clamp(stage, 0, 3)) { case 0: return 0.7f; case 1: return 0.92f; case 2: return 1.12f; default: return 1.35f; }
        }

        // 친구를 맞췄을 때: 임시 점수 누적 -> BrainScore.Stage. 처음이면 쫀득하게 생기고, 이후엔 커지며 pop.
        void GrowBrain(Rr02Friend friend, Rr02Judgement j)
        {
            if (!_brain.ContainsKey(friend) || _brain[friend] == null) return;
            int add = j == Rr02Judgement.Perfect ? Rr02Rules.PerfectScore : j == Rr02Judgement.Good ? Rr02Rules.GoodScore : 0;
            _prov[friend] = _prov[friend] + add;
            int stage = BrainScore.Stage(_prov[friend]);
            var img = _brain[friend];
            bool firstAppear = !img.enabled;
            img.enabled = true;
            img.sprite = BrainSprite(stage); // stage3 = 불붙은 뇌
            float baseScale = BrainBaseScale(stage);
            if (_brainPop.ContainsKey(friend) && _brainPop[friend] != null) StopCoroutine(_brainPop[friend]);
            _brainPop[friend] = StartCoroutine(BrainGrowPop(img.rectTransform, baseScale, firstAppear));
        }

        // 생김/성장 연출: from -> baseScale로 ease-out-back(끝에서 오버슈트=쫀득). 처음 생길 땐 0에서 크게 튀어나옴.
        IEnumerator BrainGrowPop(RectTransform rt, float baseScale, bool firstAppear)
        {
            float from = firstAppear ? 0.1f : rt.localScale.x;
            float dur = firstAppear ? 0.42f : 0.26f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float s = Mathf.LerpUnclamped(from, baseScale, EaseOutBack(u));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one * baseScale;
        }

        // 라운드 음표·박자·숨김 세팅(인트로/듣기 공통).
        void SetupRound()
        {
            _phrase.Clear();
            // 일반 라운드를 다 지나면 무한 구절. BeginRepeat와 같은 k로 생성해 표시=판정 일치.
            var round = _run.CurrentRound > _chart.Rounds.Count
                ? _chart.InfinitePhrase(_run.Loops + 1)
                : _chart.Rounds[_run.CurrentRound - 1];
            foreach (var n in round.Notes) _phrase.Add(n);
            _phrase.Sort((a, b) => a.TargetUs.CompareTo(b.TargetUs));
            _targetsUs = new long[_phrase.Count];
            for (int i = 0; i < _phrase.Count; i++) _targetsUs[i] = _phrase[i].TargetUs;
            if (_targetsUs.Length >= 2) _beatSec = Mathf.Max(0.1f, (_targetsUs[1] - _targetsUs[0]) / 1000000f);
            _hidden = new HashSet<int>(round.HiddenIndices);
        }

        // 시작 전 8박 둠칫둠칫(기본 베이스만, 멜로디 없음). 동물은 리듬 타고, 카드는 비움.
        void EnterIntro()
        {
            SetupRound();
            _state = State.Intro; _clock = 0; _listenIndex = 0; _cursorIdx = -1; _lastBeat = -1;
            HideSequence();
            if (roundText != null) roundText.text = _run.CurrentRound + "번째 무대";
            if (instructionText != null) instructionText.text = "자, 준비 됐나요?";
            SetFeedback("");
            // 카운트인은 코루틴으로 정확히 introBeats박을 친다(프레임 폴링은 첫 프레임 큰 deltaTime에
            // beat 0을 건너뛰어 3박만 울리는 버그가 있었음). 끝나면 쉼 뒤 EnterListen.
            if (_introRoutine != null) StopCoroutine(_introRoutine);
            if (isActiveAndEnabled) _introRoutine = StartCoroutine(IntroRoutine());
        }

        IEnumerator IntroRoutine()
        {
            float bs = (_beatSec > 0.05f ? _beatSec : 0.4f) * Mathf.Max(0.1f, tempoScale); // 인트로도 같은 템포로
            yield return null; // 활성화 프레임에 첫 박을 치지 않음(오디오 준비 여유 → 첫판 첫 박 묻힘 방지)
            for (int i = 0; i < introBeats; i++)
            {
                if (_audio != null && _baselineClip != null) _audio.PlayOneShot(_baselineClip, baselineVolume);
                yield return new WaitForSeconds(bs);
            }
            if (introRestBeats > 0f) yield return new WaitForSeconds(introRestBeats * bs); // 쉼(무음)
            EnterListen();
        }

        void EnterListen()
        {
            SetupRound();
            _state = State.Listen; _clock = 0; _listenIndex = 0; _cursorIdx = -1; _lastBeat = -1;
            ClearOXMarks(); // 새 구절: O/X 지우기
            ShowSequence(true); // 듣기: 전부 공개
            if (instructionText != null) instructionText.text = "잘 들어요";
            SetFeedback("");
        }

        void HideSequence()
        {
            if (_liveFace == null) return;
            for (int i = 0; i < _liveFace.Length; i++)
            {
                if (_liveFace[i] != null) _liveFace[i].enabled = false;
                if (_liveQ[i] != null) _liveQ[i].enabled = false;
            }
        }

        // revealHidden=false면 숨김 인덱스는 '?'로.
        void ShowSequence(bool revealHidden)
        {
            if (_liveFace == null) return;
            for (int i = 0; i < _liveFace.Length && i < _phrase.Count; i++)
            {
                bool hide = !revealHidden && _hidden.Contains(i);
                if (hide)
                {
                    _liveFace[i].enabled = false;
                    if (_liveQ[i] != null) _liveQ[i].enabled = true;
                }
                else
                {
                    Sprite fs; _face.TryGetValue(_phrase[i].Friend, out fs);
                    _liveFace[i].sprite = fs; _liveFace[i].enabled = fs != null;
                    if (_liveQ[i] != null) _liveQ[i].enabled = false;
                }
            }
        }

        void Update()
        {
            FitPlayUIToScreen(); // 매 프레임 화면에 맞춤(해상도·폴드·회전 변화 대응). 비용 미미.
            if (_run == null) return;
            _clock += Time.deltaTime / Mathf.Max(0.1f, tempoScale); // 템포 배율만큼 느리게(차트·판정은 그대로)
            // 인트로 전환은 IntroRoutine 코루틴이 담당(프레임 폴링 스킵 방지). 여기선 _clock만 올려 그루브에 씀.
            if (_state == State.Listen) TickListen();
            else if (_state == State.Repeat) TickRepeat();
            else if (_state == State.Clear) { if (_clock >= clearHoldSeconds) EnterListen(); }
            // State.Result: 입력(다시하기 버튼) 대기, 자동 진행 없음

            UpdateCursor();
            GrooveUpdate();
            BeatMetronome();
            ComboUpdate();
            HideOXUnlessRepeat(); // O/X는 따라치기(피드백) 중에만 보임 — 듣기·카운트인·결과엔 빈 노트
        }

        // 따라치기(Repeat)가 아니면 O/X 오버레이를 항상 숨긴다(듣기 때 X가 미리 보이는 것 방지).
        void HideOXUnlessRepeat()
        {
            if (_state == State.Repeat || _oxMark == null) return;
            for (int i = 0; i < _oxMark.Length; i++)
                if (_oxMark[i] != null && _oxMark[i].enabled) { _oxMark[i].enabled = false; _oxMark[i].text = ""; }
        }

        // 기본 8박으로 깔리는 베이스라인(상시 반주 비트). 듣기/따라치기 내내 매 박.
        void BeatMetronome()
        {
            if (_baselineClip == null || _audio == null) { return; }
            // 인트로 카운트인은 IntroRoutine이 친다. 여기선 듣기/따라치기의 8박 베이스만.
            if (_state != State.Listen && _state != State.Repeat) { _lastBeat = -1; return; }
            int beat = (int)(_clock / _beatSec);
            // 프리롤(_clock<0) 동안은 드럼을 치지 않음(노트와 어긋나지 않게). 첫 드럼은 _clock=0에.
            if (beat != _lastBeat && beat < 8 && _clock >= 0.0)
            {
                _lastBeat = beat;
                // 4/4 느낌: 매 마디 첫 박(다운비트)을 강하게 — "하나-둘-셋-넷".
                float vol = (beat % 4 == 0) ? Mathf.Min(1f, baselineVolume * 1.7f) : baselineVolume;
                _audio.PlayOneShot(_baselineClip, vol);
            }
        }

        void TickListen()
        {
            if (_listenIndex < _phrase.Count && _clock >= _phrase[_listenIndex].TargetUs / 1000000.0)
            {
                PlayTone(_phrase[_listenIndex].Friend); // 듣기: 시퀀스를 소리로 들려줌(동물 hit 없음, 그루브)
                _listenIndex++;
            }
            if (_listenIndex >= _phrase.Count)
            {
                // 들어보세요는 정확히 '노트 수'만큼의 박(4노트=4박). 4박째 경계(_clock >= Count*_beatSec)에서
                // 바로 따라하기 전 '4박 하이햇 카운트인'으로 넘어간다(옛 5박 꼬리 제거 → 첫 따라치기 박이 안 밀림).
                if (_clock >= _phrase.Count * _beatSec)
                    StartRepeatCountIn();
            }
        }

        // 따라하기 직전 4박 하이햇 카운트인. 숨긴 칸을 '?'로 바꿔 보여주고, 끝나면 따라치기로 진입한다.
        void StartRepeatCountIn()
        {
            _state = State.CountIn; _lastBeat = -1;
            ShowSequence(false); // 숨긴 칸 '?'
            if (instructionText != null) instructionText.text = "준비!";
            if (_introRoutine != null) StopCoroutine(_introRoutine);
            if (isActiveAndEnabled) _introRoutine = StartCoroutine(RepeatCountInRoutine());
        }

        IEnumerator RepeatCountInRoutine()
        {
            float bs = (_beatSec > 0.05f ? _beatSec : 0.4f) * Mathf.Max(0.1f, tempoScale); // 인트로와 같은 템포로 정확히 4박
            for (int i = 0; i < introBeats; i++)
            {
                if (_audio != null && _baselineClip != null) _audio.PlayOneShot(_baselineClip, baselineVolume);
                yield return new WaitForSeconds(bs);
            }
            EnterRepeat();
        }

        void EnterRepeat()
        {
            _run.BeginRepeat();
            ResetBubbleScales();
            // 프리롤: _clock을 음수로 시작해 전환 직후부터 입력(판정) 허용. 노트/드럼은 _clock>=0에 그대로.
            _state = State.Repeat; _clock = -repeatPrerollMs / 1000.0; _lastBeat = -1;
            if (instructionText != null) instructionText.text = "따라 쳐요";
        }

        void TickRepeat()
        {
            ReadInput();
            long us = (long)(_clock * 1000000.0); // 프리롤(음수) 동안은 AdvanceTime이 무해(노트 안 닫힘)
            _run.AdvanceTime(us);
            // 지나간(Good 창이 닫힌) 미입력 노트는 즉시 분홍 X로 표시하고 콤보를 끊는다.
            if (_cardResolved != null)
                for (int i = 0; i < _cardResolved.Length && i < _phrase.Count; i++)
                    if (!_cardResolved[i] && us > _phrase[i].TargetUs + Rr02Rules.GoodWindowUs)
                    { _cardResolved[i] = true; MarkOX(i, false); BreakCombo(); }
            if (_run.Phase == Rr02Phase.Listen)
            {
                // 통과(전구+1·다음 라운드)와 실패-재도전(전구-1·같은 라운드)은 둘 다 Listen으로 끝납니다.
                // 전구 수가 늘었는지 줄었는지로 구분하고, 전구 UI는 늘든 줄든 항상 현재 값으로 갱신합니다.
                int nc = _run.NormalCleared;
                bool failed = nc < _lastNormalCleared;
                if (nc != _lastNormalCleared)
                {
                    if (lampRow != null) lampRow.SetCleared(nc); // 감소 시 꺼진 전구도 반영
                    _lastNormalCleared = nc;
                }
                if (failed)
                {
                    if (_audio != null && _failClip != null) _audio.PlayOneShot(_failClip);
                    SetFeedback("아깝다!");
                }
                else
                {
                    if (_audio != null && _clearClip != null) _audio.PlayOneShot(_clearClip);
                    SetFeedback("좋아요!");
                }
                _state = State.Clear; _clock = 0;
            }
            else if (_run.Phase == Rr02Phase.Ended)
            {
                if (_audio != null && _failClip != null) _audio.PlayOneShot(_failClip);
                SetFeedback("");
                ShowResult();
            }
        }

        void ReadInput()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Press(Rr02Friend.Rabbit);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Press(Rr02Friend.Capybara);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Press(Rr02Friend.Fox);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Press(Rr02Friend.Squirrel);
        }

        public void Press(Rr02Friend friend)
        {
            if (_state != State.Repeat || _run == null) return;
            // 프리롤(_clock 음수)이면 첫 노트의 조기 입력창이 열려 있음. inputOffsetMs(기본 0)로 전역 미세 보정 가능.
            long us = (long)(_clock * 1000000.0) - (long)(inputOffsetMs * 1000.0);
            var r = _run.ProcessPress(friend, us);
            PlayTone(friend); // 누를 때 소리
            float intensity = r.Kind == Rr02InputKind.Judged && r.Judgement == Rr02Judgement.Perfect ? 1.25f : 1.0f;
            HitPulse(friend, intensity);
            if (r.Kind == Rr02InputKind.Judged)
            {
                SetFeedback(r.Judgement == Rr02Judgement.Perfect ? "Perfect!" : r.Judgement == Rr02Judgement.Good ? "Good" : "Miss");
                int ci = CardIndexForNote(r.NoteId);
                if (ci >= 0 && _cardResolved != null && ci < _cardResolved.Length && !_cardResolved[ci])
                {
                    _cardResolved[ci] = true;
                    if (r.Judgement != Rr02Judgement.Miss) { MarkOX(ci, true); _combo++; }  // 맞음: 파란 O, 콤보 +1
                    else { MarkOX(ci, false); BreakCombo(); }                                 // 틀림: 분홍 X, 콤보 끊김
                }
            }
            else if (r.Kind == Rr02InputKind.Ghost) SetFeedback("!");
        }

        // 따라치기에서 누를 때만: 타격 포즈 + 쫀득 스프링.
        void HitPulse(Rr02Friend friend, float intensity)
        {
            RectTransform rt;
            if (!_bubbles.TryGetValue(friend, out rt) || rt == null) return;
            if (_pulse.ContainsKey(friend) && _pulse[friend] != null) StopCoroutine(_pulse[friend]);
            _pulse[friend] = StartCoroutine(HitRoutine(friend, rt, intensity));
        }

        IEnumerator HitRoutine(Rr02Friend friend, RectTransform rt, float intensity)
        {
            Image ci = _charImg.ContainsKey(friend) ? _charImg[friend] : null;
            Sprite hit = _hit.ContainsKey(friend) ? _hit[friend] : null;
            Sprite idle = _idle.ContainsKey(friend) ? _idle[friend] : null;
            if (ci != null && hit != null) ci.sprite = hit;
            float amp = 0.26f * intensity, t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / popDuration);
                float punch = Mathf.Sin(u * Mathf.PI) + 0.28f * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                rt.localScale = new Vector3(1f - amp * 0.55f * punch, 1f + amp * punch, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (ci != null && idle != null) ci.sprite = idle;
            _pulse[friend] = null;
        }

        // 듣기에서 동물들이 다같이 박자 그루브(hit 없음). 버블 그룹을 박자마다 통 튕김.
        void GrooveUpdate()
        {
            if (_bubbles == null) return;
            bool groove = _state == State.Intro || _state == State.Listen;
            if (!groove) return;
            float phase = (float)((_clock / _beatSec) % 1.0);
            float bob = Mathf.Sin(phase * Mathf.PI); // 매 박 0->1->0
            float s = 1f + 0.06f * bob;
            foreach (var pair in _bubbles)
            {
                var rt = pair.Value; if (rt == null) continue;
                if (_pulse.ContainsKey(pair.Key) && _pulse[pair.Key] != null) continue; // 눌림 연출 중이면 양보
                rt.localScale = new Vector3(s, s, 1f);
            }
        }

        void ResetBubbleScales()
        {
            if (_bubbles == null) return;
            foreach (var pair in _bubbles) if (pair.Value != null) pair.Value.localScale = Vector3.one;
        }

        // 파란 커서: 박자마다 카드에 '딱' 걸림(스냅) + 걸릴 때 팝. 위치 계산은 BeatCursor(로직).
        void UpdateCursor()
        {
            if (_cursor == null || _cardPos == null || _cardPos.Length == 0) return;
            bool active = (_state == State.Listen || _state == State.Repeat) && _targetsUs.Length > 0;
            if (_cursor.gameObject.activeSelf != active) _cursor.gameObject.SetActive(active);
            if (!active) { _cursorIdx = -1; return; }

            long nowUs = (long)(_clock * 1000000.0);
            int idx = Mathf.Clamp(Mathf.FloorToInt(BeatCursor.FloatIndex(nowUs, _targetsUs)), 0, _cardPos.Length - 1);
            if (idx != _cursorIdx)
            {
                _cursorIdx = idx;
                _cursor.anchoredPosition = _cardPos[idx]; // 스냅(보간 없음)
                if (_cursorPop != null) StopCoroutine(_cursorPop);
                _cursorPop = StartCoroutine(CursorPop());
            }
        }

        IEnumerator CursorPop()
        {
            float dur = 0.16f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float s = 1f + 0.18f * Mathf.Sin(u * Mathf.PI);
                _cursor.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _cursor.localScale = Vector3.one;
            _cursorPop = null;
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
            var rt = feedbackText.rectTransform; float dur = 0.3f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float s = 0.6f + 0.4f * EaseOutBack(u);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one; _fbPop = null;
        }

        static float EaseOutBack(float x)
        {
            float c1 = 1.70158f, c3 = c1 + 1f, p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
