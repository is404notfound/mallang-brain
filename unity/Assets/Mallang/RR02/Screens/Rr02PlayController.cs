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

        enum State { Intro, Listen, Repeat, Clear, Result }

        [Header("인트로")]
        public int introBeats = 4; // 시작 전 둠칫둠칫 카운트인 박 수

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

        // 소리(플레이스홀더 — 코드 생성 음. 실제 악기 소리는 에셋으로 교체 예정).
        AudioSource _audio;
        Dictionary<Rr02Friend, AudioClip> _tone = new Dictionary<Rr02Friend, AudioClip>();
        AudioClip _clearClip, _failClip, _baselineClip;
        int _lastBeat = -1; // 8박 베이스라인 메트로놈

        // 결과(게임오버/클리어) 화면 — 전체화면(screen-result-final 수렴)
        GameObject _resultRoot;
        Text _resultScoreBig, _resultGain;

        // 랭킹(온라인 리더보드) 화면
        GameObject _rankingRoot;
        BenchmarkTable _benchTable;
        // 경로 B: 승인 캔버스(screen-ranking)를 배경 한 장으로 쓰고, 상태로 변하는 숫자만 폰트로 오버레이.
        Text _heroScore, _heroReach, _heroGradeLabel, _heroNextChip;
        Text _myRowRank, _myRowScore;
        Text[] _friendScore = new Text[4];
        Image[] _friendBrainHead = new Image[4];
        Image[] _lines = new Image[4];       // 뇌 -> 친구 연결선
        float[] _lineLen = new float[4];      // 각 선의 완성 길이
        Coroutine _lineAnim;
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
                    var btn = target.gameObject.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.RemoveAllListeners();
                    var captured = friend;
                    btn.onClick.AddListener(() => Press(captured));
                }
            }
            SetupAudio();
        }

        // 플레이스홀더 음 생성(친구마다 다른 음정, 펜타토닉). 실제 악기 소리는 에셋으로 교체.
        void SetupAudio()
        {
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

        void PlayTone(Rr02Friend f) { if (_audio != null && _tone.ContainsKey(f) && _tone[f] != null) _audio.PlayOneShot(_tone[f]); }

        void OnEnable()
        {
            InitRefs();
            if (chartJson == null) { Debug.LogError("Rr02PlayController: chartJson 미설정"); enabled = false; return; }
            _chart = Rr02Chart.LoadFromJson(chartJson.text);
            if (benchmarkJson != null) _benchTable = RankingRules.ParseBenchmark(benchmarkJson.text);
            BuildGrid();
            BuildResult();
            BuildRanking();
            StartRun();
        }

        // 결과 화면(런타임 구성, 전체화면): 배경 + 상단 "내 리듬점수"+뇌 + 네 친구별 점수 + 다음. 기본 숨김.
        void BuildResult()
        {
            if (_resultRoot != null) return;
            var canvas = GameObject.Find("PlayUI");
            if (canvas == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _resultRoot = new GameObject("ResultRoot", typeof(RectTransform));
            _resultRoot.transform.SetParent(canvas.transform, false);
            var rr = (RectTransform)_resultRoot.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

            // 전체화면 배경
            var bg = NewImage("Bg", _resultRoot.transform, LoadSprite("stage-background"), false);
            var bgrt = bg.rectTransform; bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

            // 상단: 라벨 + 큰 점수 + +증가 + 중앙 뇌
            var lbl = MakeText(_resultRoot.transform, font, 40, new Vector2(0, 300), new Vector2(520, 70));
            lbl.text = "내 리듬점수"; lbl.color = new Color(1f, 1f, 1f, 1f); AddOutline(lbl.gameObject, 0.2f, 0.25f, 0.4f);
            _resultScoreBig = MakeText(_resultRoot.transform, font, 92, new Vector2(0, 225), new Vector2(760, 130));
            _resultScoreBig.color = new Color(0.95f, 0.35f, 0.45f, 1f); AddOutline(_resultScoreBig.gameObject, 1f, 1f, 1f, 1f, 4f);
            _resultGain = MakeText(_resultRoot.transform, font, 34, new Vector2(0, 150), new Vector2(400, 60));
            _resultGain.color = new Color(0.95f, 0.45f, 0.6f, 1f); AddOutline(_resultGain.gameObject, 1f, 1f, 1f, 1f);
            var centerBrain = NewImage("CenterBrain", _resultRoot.transform, LoadSprite("brain-stage1"), true);
            var cbrt = centerBrain.rectTransform; cbrt.anchoredPosition = new Vector2(0, 60); cbrt.sizeDelta = new Vector2(190, 190);

            // 네 친구 + 머리 뇌 + 점수
            float[] fx = { -440, -150, 150, 440 };
            for (int i = 0; i < 4; i++)
            {
                var f = NewImage("Friend" + i, _resultRoot.transform, LoadSprite(_friendSpriteName[i]), true);
                var frt = f.rectTransform; frt.anchoredPosition = new Vector2(fx[i], -120); frt.sizeDelta = new Vector2(220, 220);
                var hb = NewImage("Brain" + i, _resultRoot.transform, LoadSprite("brain-stage0"), true);
                var hbrt = hb.rectTransform; hbrt.anchoredPosition = new Vector2(fx[i], -15); hbrt.sizeDelta = new Vector2(62, 62);
                _friendBrainHead[i] = hb;
                var st = MakeText(_resultRoot.transform, font, 44, new Vector2(fx[i], -250), new Vector2(230, 70));
                st.color = new Color(0.33f, 0.22f, 0.13f, 1f); AddOutline(st.gameObject, 1f, 1f, 1f, 1f);
                _friendScore[i] = st;
            }

            // 뇌 -> 각 친구 연결선(처음 길이 0, ShowResult에서 뻗음). 친구·뇌 뒤(배경 위)로.
            Vector2 brainPt = new Vector2(0, 60);
            Color[] lineCol = {
                new Color(1f, 0.6f, 0.78f, 0.9f), new Color(0.6f, 0.9f, 0.62f, 0.9f),
                new Color(0.6f, 0.8f, 1f, 0.9f), new Color(1f, 0.85f, 0.5f, 0.9f) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 fp = new Vector2(fx[i], -15);
                Vector2 d = fp - brainPt; float len = d.magnitude; float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                var ln = NewImage("Line" + i, _resultRoot.transform, null, false); ln.color = lineCol[i];
                var lrt = ln.rectTransform; lrt.pivot = new Vector2(0f, 0.5f); lrt.anchoredPosition = brainPt;
                lrt.sizeDelta = new Vector2(0f, 7f); lrt.localEulerAngles = new Vector3(0, 0, ang);
                ln.transform.SetSiblingIndex(1); // 배경(0) 바로 위 = 뇌·친구 뒤
                _lines[i] = ln; _lineLen[i] = len;
            }

            // 다음 버튼(배너 리본) — 맨 아래
            var btnGo = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_resultRoot.transform, false);
            var bi = btnGo.GetComponent<Image>(); bi.sprite = LoadSprite("banner-ribbon"); bi.preserveAspect = false;
            var brt2 = bi.rectTransform; brt2.anchorMin = brt2.anchorMax = new Vector2(0.5f, 0.5f); brt2.pivot = new Vector2(0.5f, 0.5f);
            brt2.anchoredPosition = new Vector2(0, -335); brt2.sizeDelta = new Vector2(320, 90);
            var bt = btnGo.GetComponent<Button>(); bt.transition = Selectable.Transition.None; bt.onClick.AddListener(OnNext);
            var bl = MakeText(btnGo.transform, font, 40, Vector2.zero, new Vector2(300, 80));
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

        Sprite LoadSprite(string n) { return UnityEditor_LoadOrNull("Assets/Mallang/RR02/Art/" + n + ".png"); }

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
            if (_resultScoreBig != null) _resultScoreBig.text = Comma(res.TotalScore);
            if (_resultGain != null) _resultGain.text = "+" + Comma(res.TotalScore);
            for (int i = 0; i < 4; i++)
            {
                int sc = res.AnimalScores != null && res.AnimalScores.ContainsKey(_friendOrder[i]) ? res.AnimalScores[_friendOrder[i]] : 0;
                if (_friendScore[i] != null) _friendScore[i].text = Comma(sc);
                if (_friendBrainHead[i] != null) _friendBrainHead[i].sprite = LoadSprite("brain-stage" + Mathf.Clamp(BrainScore.Stage(sc), 0, 3));
            }
            if (_resultRoot != null) _resultRoot.SetActive(true);
            _state = State.Result;
            // 연결선: 길이 0에서 시작해 뇌 -> 친구로 뻗는 애니메이션
            for (int i = 0; i < 4; i++) if (_lines[i] != null) _lines[i].rectTransform.sizeDelta = new Vector2(0f, 7f);
            if (_lineAnim != null) StopCoroutine(_lineAnim);
            if (isActiveAndEnabled) _lineAnim = StartCoroutine(LinesGrow());
        }

        IEnumerator LinesGrow()
        {
            yield return new WaitForSeconds(0.25f); // 점수 팝 뒤에 선이 뻗음
            float dur = 0.45f;
            for (int i = 0; i < 4; i++)
            {
                if (_lines[i] == null) continue;
                StartCoroutine(OneLineGrow(_lines[i].rectTransform, _lineLen[i], dur));
                yield return new WaitForSeconds(0.08f); // 친구마다 살짝 스태거
            }
        }

        IEnumerator OneLineGrow(RectTransform rt, float target, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float u = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - u) * (1f - u); // ease-out
                rt.sizeDelta = new Vector2(target * e, rt.sizeDelta.y);
                yield return null;
            }
            rt.sizeDelta = new Vector2(target, rt.sizeDelta.y);
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

            // 배경: 승인 캔버스 한 장(고정 구성). 상위 10행·메달·배지·버튼 아트가 전부 베이크됨.
            // 목 리더보드가 캔버스와 동일하므로 코드가 그릴 건 '상태로 변하는 숫자'뿐이다(원칙 6: 변하는가로 분해).
            var bg = NewImage("Bg", _rankingRoot.transform, LoadSprite("screen-ranking"), false);
            bg.preserveAspect = true;
            var bgrt = bg.rectTransform; bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

            // 상태로 변하는 값만 오버레이: 가리개 플레이트(베이크 샘플 숫자를 덮음) + 폰트 텍스트.
            // 등급 리본·다음 칩은 삽화라 베이크 유지(프로토타입: 대부분 암보 합주). 숫자만 라이브.
            _heroScore = CoverText(_rankingRoot.transform, font, 44, new Vector2(-44, 276), new Vector2(230, 64), cream, brown, TextAnchor.MiddleCenter);
            _heroReach = CoverText(_rankingRoot.transform, font, 44, new Vector2(204, 276), new Vector2(210, 64), cream, brown, TextAnchor.MiddleCenter);
            _myRowRank = CoverText(_rankingRoot.transform, font, 26, new Vector2(-312, -161), new Vector2(150, 38), green, brown, TextAnchor.MiddleCenter);
            _myRowScore = CoverText(_rankingRoot.transform, font, 26, new Vector2(304, -161), new Vector2(170, 38), green, brown, TextAnchor.MiddleRight);

            // 베이크된 버튼 아트 위에 투명 클릭 영역.
            InvisibleButton(_rankingRoot.transform, new Vector2(-360, -305), new Vector2(360, 110), OnRetry);
            InvisibleButton(_rankingRoot.transform, new Vector2(0, -305), new Vector2(360, 110), OnShare);
            InvisibleButton(_rankingRoot.transform, new Vector2(360, -305), new Vector2(360, 110), OnTitle);

            _rankingRoot.SetActive(false);
        }

        // 디스플레이 폰트(BMJUA 주아체). 없으면 기본 폰트 폴백.
        Font LoadDisplayFont()
        {
#if UNITY_EDITOR
            var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Mallang/RR02/Fonts/BMJUA.ttf");
            if (f != null) return f;
#endif
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

            for (int i = 0; i < 8; i++)
            {
                var card = panel.transform.Find("Card" + i);
                if (card == null) continue;
                var crt = (RectTransform)card;
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
            }
            _cardPos = pos.ToArray(); _liveFace = faces.ToArray(); _liveQ = qs.ToArray();

            var sel = panel.transform.Find("Select");
            if (sel != null) { _cursor = (RectTransform)sel; _cursor.SetAsFirstSibling(); } // 카드 뒤 = 테두리 하이라이트
        }

        void StartRun()
        {
            _run = new Rr02Run(_chart);
            _lastNormalCleared = 0;
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
            foreach (var n in _chart.Rounds[_run.CurrentRound - 1].Notes) _phrase.Add(n);
            _phrase.Sort((a, b) => a.TargetUs.CompareTo(b.TargetUs));
            _targetsUs = new long[_phrase.Count];
            for (int i = 0; i < _phrase.Count; i++) _targetsUs[i] = _phrase[i].TargetUs;
            if (_targetsUs.Length >= 2) _beatSec = Mathf.Max(0.1f, (_targetsUs[1] - _targetsUs[0]) / 1000000f);
            _hidden = new HashSet<int>(_chart.Rounds[_run.CurrentRound - 1].HiddenIndices);
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
        }

        void EnterListen()
        {
            SetupRound();
            _state = State.Listen; _clock = 0; _listenIndex = 0; _cursorIdx = -1; _lastBeat = -1;
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
            if (_run == null) return;
            _clock += Time.deltaTime;
            if (_state == State.Intro) { if (_clock >= introBeats * _beatSec) EnterListen(); }
            else if (_state == State.Listen) TickListen();
            else if (_state == State.Repeat) TickRepeat();
            else if (_state == State.Clear) { if (_clock >= clearHoldSeconds) EnterListen(); }
            // State.Result: 입력(다시하기 버튼) 대기, 자동 진행 없음

            UpdateCursor();
            GrooveUpdate();
            BeatMetronome();
        }

        // 기본 8박으로 깔리는 베이스라인(상시 반주 비트). 듣기/따라치기 내내 매 박.
        void BeatMetronome()
        {
            if (_baselineClip == null || _audio == null) { return; }
            if (_state != State.Intro && _state != State.Listen && _state != State.Repeat) { _lastBeat = -1; return; }
            int beat = (int)(_clock / _beatSec);
            if (beat != _lastBeat && beat < 8)
            {
                _lastBeat = beat;
                _audio.PlayOneShot(_baselineClip, 0.55f);
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
                double lastSec = _phrase.Count > 0 ? _phrase[_phrase.Count - 1].TargetUs / 1000000.0 : 0;
                if (_clock >= lastSec + listenTailSeconds)
                {
                    _run.BeginRepeat();
                    ResetBubbleScales();
                    ShowSequence(false); // 따라치기: 숨긴 칸 '?'
                    _state = State.Repeat; _clock = 0; _lastBeat = -1;
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
                if (_audio != null && _clearClip != null) _audio.PlayOneShot(_clearClip);
                SetFeedback("좋아요!"); _state = State.Clear; _clock = 0;
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
            long us = (long)(_clock * 1000000.0);
            var r = _run.ProcessPress(friend, us);
            PlayTone(friend); // 누를 때 소리
            float intensity = r.Kind == Rr02InputKind.Judged && r.Judgement == Rr02Judgement.Perfect ? 1.25f : 1.0f;
            HitPulse(friend, intensity);
            if (r.Kind == Rr02InputKind.Judged)
            {
                SetFeedback(r.Judgement == Rr02Judgement.Perfect ? "Perfect!" : r.Judgement == Rr02Judgement.Good ? "Good" : "Miss");
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
