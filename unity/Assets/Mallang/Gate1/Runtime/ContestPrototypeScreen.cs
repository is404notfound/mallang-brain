using System;
using System.Collections.Generic;
using Mallang.Gate0.Runtime.Clock;
using Mallang.RhythmCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mallang.Gate1.Runtime
{
    // Gate 1's first playable screen. It deliberately owns presentation only; scoring stays in RhythmCore.
    [RequireComponent(typeof(AudioSource))]
    public sealed class ContestPrototypeScreen : MonoBehaviour
    {
        private enum ScreenPhase { Calibrating, Ready, CountIn, Playing, Result, Interrupted }

        private static readonly Color Night = new Color(0.04f, 0.07f, 0.13f);
        private static readonly Color Ink = new Color(0.88f, 0.92f, 0.95f);
        private static readonly Color Mint = new Color(0.23f, 0.82f, 0.70f);
        private static readonly Color Coral = new Color(0.95f, 0.43f, 0.38f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.30f);
        private static readonly Color Panel = new Color(0.10f, 0.15f, 0.25f, 0.95f);

        private readonly List<JudgementEvent> events = new List<JudgementEvent>(16);
        private readonly SystemRhythmClock clock = new SystemRhythmClock();
        private ClockCalibrator calibrator;
        private ClockCalibration calibration;
        private ContestChart chart;
        private ContestRun run;
        private ScreenPhase phase;
        private double countInStartDsp;
        private double stageStartDsp;
        private Hand mouseHand;
        private string feedback = "준비 중";
        private AudioSource audioSource;
        private AudioClip leftTone;
        private AudioClip rightTone;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle metricStyle;
        private GUIStyle buttonStyle;
        [SerializeField] private double userCalibrationSeconds;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            audioSource.volume = 0.25f;
            try
            {
                leftTone = CreateMarimbaTone("Gate 1 left marimba", 329.63f);
                rightTone = CreateMarimbaTone("Gate 1 right marimba", 493.88f);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Gate 1 audio is unavailable; visual play continues. " + error.Message);
            }
            StartCalibration();
        }

        private void Update()
        {
            ReadInput();
            if (phase == ScreenPhase.Calibrating)
            {
                calibrator.AddSample(clock);
                calibration = calibrator.Build();
                if (calibration.Valid)
                {
                    phase = ScreenPhase.Ready;
                    feedback = "준비 완료 · 시작을 누르세요";
                }
                return;
            }

            if (phase == ScreenPhase.CountIn && clock.DspSeconds >= countInStartDsp + CountInDurationSeconds())
            {
                stageStartDsp = countInStartDsp + CountInDurationSeconds();
                phase = ScreenPhase.Playing;
                feedback = "연주 시작";
                return;
            }

            if (phase != ScreenPhase.Playing || run == null) return;
            var stage = chart.Stages[run.CurrentStageNumber - 1];
            if (clock.DspSeconds >= stageStartDsp + stage.DurationUs / 1000000.0)
            {
                events.Clear();
                run.FinalizeCurrentStage(events);
                CaptureFeedback();
                if (!run.IsActive)
                {
                    phase = ScreenPhase.Result;
                    return;
                }
                BeginCountIn();
            }
        }

        private void ReadInput()
        {
            var hasTouch = false;
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        hasTouch = true;
                        var hand = touch.position.ReadValue().x < Screen.width * .5f ? Hand.Left : Hand.Right;
                        HandlePress(hand, touch.touchId.ReadValue(), EventTime(touch.press));
                    }
                    if (touch.press.wasReleasedThisFrame) HandleRelease(touch.touchId.ReadValue(), EventTime(touch.press));
                }
            }

            if (!hasTouch && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    mouseHand = Mouse.current.position.ReadValue().x < Screen.width * .5f ? Hand.Left : Hand.Right;
                    HandlePress(mouseHand, 900, EventTime(Mouse.current.leftButton));
                }
                if (Mouse.current.leftButton.wasReleasedThisFrame) HandleRelease(900, EventTime(Mouse.current.leftButton));
            }

            if (Keyboard.current == null) return;
            if (Keyboard.current.aKey.wasPressedThisFrame) HandlePress(Hand.Left, 901, EventTime(Keyboard.current.aKey));
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) HandlePress(Hand.Left, 901, EventTime(Keyboard.current.leftArrowKey));
            if (Keyboard.current.lKey.wasPressedThisFrame) HandlePress(Hand.Right, 902, EventTime(Keyboard.current.lKey));
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) HandlePress(Hand.Right, 902, EventTime(Keyboard.current.rightArrowKey));
            if (Keyboard.current.aKey.wasReleasedThisFrame) HandleRelease(901, EventTime(Keyboard.current.aKey));
            if (Keyboard.current.leftArrowKey.wasReleasedThisFrame) HandleRelease(901, EventTime(Keyboard.current.leftArrowKey));
            if (Keyboard.current.lKey.wasReleasedThisFrame) HandleRelease(902, EventTime(Keyboard.current.lKey));
            if (Keyboard.current.rightArrowKey.wasReleasedThisFrame) HandleRelease(902, EventTime(Keyboard.current.rightArrowKey));
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Night, 0, 0);
            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            DrawScreen();
            GUI.matrix = oldMatrix;
        }

        private void DrawScreen()
        {
            DrawText(new Rect(52, 28, 730, 42), "MALLANG BRAIN · MARIMBA CONTEST", titleStyle, Ink);
            DrawText(new Rect(52, 75, 730, 26), PhaseLabel(), labelStyle, Mint);
            DrawPanel(new Rect(860, 28, 365, 76));
            DrawText(new Rect(884, 43, 155, 22), "현재 점수", labelStyle, Ink);
            DrawText(new Rect(1050, 40, 150, 30), Snapshot().RankScore.ToString("N0"), metricStyle, Gold, TextAnchor.MiddleRight);
            DrawText(new Rect(884, 72, 300, 20), "정확도 " + (Snapshot().AccuracyBasisPoints / 100f).ToString("0.0") + "% · 콤보 " + Snapshot().Combo, labelStyle, Ink);

            DrawPanel(new Rect(52, 122, 1176, 188));
            DrawText(new Rect(76, 142, 260, 25), "백 명의 참가자", labelStyle, Ink);
            var remaining = CrowdRemaining();
            DrawText(new Rect(956, 142, 245, 25), "남은 인원 " + remaining + "명", labelStyle, Gold, TextAnchor.MiddleRight);
            DrawCrowd(remaining);

            DrawPanel(new Rect(260, 335, 760, 150));
            DrawText(new Rect(285, 356, 710, 26), CueLabel(), labelStyle, Ink, TextAnchor.MiddleCenter);
            DrawText(new Rect(285, 389, 710, 46), feedback, metricStyle, FeedbackColor(), TextAnchor.MiddleCenter);
            DrawText(new Rect(285, 438, 710, 22), "A / ← 는 왼손 · L / → 는 오른손 · 화면의 건반을 눌러도 됩니다", labelStyle, Ink, TextAnchor.MiddleCenter);

            DrawKey(new Rect(52, 522, 540, 136), Hand.Left, "왼손", "A / ←", Mint);
            DrawKey(new Rect(636, 522, 540, 136), Hand.Right, "오른손", "L / →", Coral);
            DrawPanel(new Rect(1050, 335, 178, 126));
            DrawText(new Rect(1064, 349, 150, 24), "내 말랑뇌", labelStyle, Ink, TextAnchor.MiddleCenter);
            GUI.color = Mint;
            GUI.Box(new Rect(1116, 385, 46, 42), GUIContent.none);
            GUI.color = Color.white;
            DrawText(new Rect(1064, 432, 150, 20), "무대 중앙", labelStyle, Mint, TextAnchor.MiddleCenter);
            DrawText(new Rect(52, 680, 1175, 22), "Gate 1 프로토타입 · 판정과 점수는 화면과 분리된 RhythmCore가 계산합니다.", labelStyle, Ink, TextAnchor.MiddleCenter);

            if (phase == ScreenPhase.Ready)
            {
                DrawPanel(new Rect(425, 250, 430, 210));
                DrawText(new Rect(450, 277, 380, 34), "마림바 콘테스트", titleStyle, Ink, TextAnchor.MiddleCenter);
                DrawText(new Rect(450, 329, 380, 42), "시작하면 4박 카운트 뒤\nStage 1부터 연주합니다.", labelStyle, Ink, TextAnchor.MiddleCenter);
                if (GUI.Button(new Rect(510, 390, 260, 48), "시작", buttonStyle)) BeginCountIn();
            }
            else if (phase == ScreenPhase.Calibrating || phase == ScreenPhase.Interrupted)
            {
                DrawPanel(new Rect(425, 270, 430, 150));
                DrawText(new Rect(450, 300, 380, 34), "시계 준비 중", titleStyle, Ink, TextAnchor.MiddleCenter);
                DrawText(new Rect(450, 348, 380, 32), "" + calibrator.SampleCount + " / " + calibrator.RequiredSamples + " 표본", labelStyle, Mint, TextAnchor.MiddleCenter);
            }
            else if (phase == ScreenPhase.Result)
            {
                DrawResult();
            }
        }

        private void DrawResult()
        {
            var snapshot = Snapshot();
            DrawPanel(new Rect(395, 214, 490, 298));
            DrawText(new Rect(420, 240, 440, 38), snapshot.EndReason == RunEndReason.Completed ? "첫 실기 완주" : "이번 연주는 여기까지", titleStyle, Ink, TextAnchor.MiddleCenter);
            DrawText(new Rect(420, 300, 440, 28), "도달 Stage " + (snapshot.EndReason == RunEndReason.Failed ? snapshot.FailStage : 20), metricStyle, Gold, TextAnchor.MiddleCenter);
            DrawText(new Rect(420, 344, 440, 26), "정확도 " + (snapshot.AccuracyBasisPoints / 100f).ToString("0.0") + "% · 총점 " + snapshot.RankScore.ToString("N0"), labelStyle, Ink, TextAnchor.MiddleCenter);
            DrawText(new Rect(420, 382, 440, 24), "다시 참가해서 리듬을 익혀 보세요.", labelStyle, Mint, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(510, 432, 260, 48), "다시 참가", buttonStyle)) StartCalibration();
        }

        private void DrawCrowd(int remaining)
        {
            const int columns = 25;
            for (var index = 0; index < 100; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var alive = index < remaining;
                var tint = alive ? (index < DetailedCrowdCount() ? Mint : new Color(0.27f, 0.44f, 0.55f)) : new Color(0.17f, 0.21f, 0.28f);
                GUI.color = tint;
                GUI.Box(new Rect(82 + column * 44, 184 + row * 25, 24, 15), GUIContent.none);
            }
            GUI.color = Color.white;
        }

        private void DrawKey(Rect rect, Hand hand, string handText, string keyText, Color color)
        {
            GUI.color = color;
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;
            DrawText(new Rect(rect.x, rect.y + 20, rect.width, 34), handText, titleStyle, Night, TextAnchor.MiddleCenter);
            DrawText(new Rect(rect.x, rect.y + 72, rect.width, 28), keyText, metricStyle, Night, TextAnchor.MiddleCenter);
            GUI.Box(rect, GUIContent.none, GUIStyle.none);
        }

        private void HandlePress(Hand hand, int pointerId, double realtime)
        {
            if (phase == ScreenPhase.Ready) { BeginCountIn(); return; }
            if (phase == ScreenPhase.Result) { StartCalibration(); return; }
            if (phase != ScreenPhase.Playing || run == null) return;
            events.Clear();
            run.ProcessPress(hand, pointerId, ToChartUs(realtime), events);
            CaptureFeedback();
            PlayTone(hand);
        }

        private void HandleRelease(int pointerId, double realtime)
        {
            if (phase != ScreenPhase.Playing || run == null) return;
            events.Clear();
            run.ProcessRelease(pointerId, ToChartUs(realtime), events);
            CaptureFeedback();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StartCalibration();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StartCalibration();
        }

        private void OnDestroy()
        {
            if (leftTone != null) Destroy(leftTone);
            if (rightTone != null) Destroy(rightTone);
        }

        private void BeginCountIn()
        {
            if (!calibration.Valid) return;
            if (chart == null) chart = Gate1ChartFactory.Create();
            if (run == null) run = new ContestRun(chart);
            countInStartDsp = clock.DspSeconds + 0.15;
            phase = ScreenPhase.CountIn;
            feedback = "카운트 4";
        }

        private void StartCalibration()
        {
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);
            chart = null;
            run = null;
            phase = ScreenPhase.Calibrating;
            feedback = "시계 준비 중";
        }

        private long ToChartUs(double realtime)
        {
            var microseconds = (calibration.ToDspSeconds(realtime) - stageStartDsp - userCalibrationSeconds) * 1000000.0;
            return microseconds >= 0 ? (long)Math.Floor(microseconds + .5) : (long)Math.Ceiling(microseconds - .5);
        }

        // InputState.currentTime is the Input System event timeline, matching Time.realtimeSinceStartup.
        // Individual controls do not expose their own event timestamp in Input System 1.20.
        private static double EventTime(InputControl control) => InputState.currentTime;

        private void CaptureFeedback()
        {
            if (events.Count == 0) return;
            var result = events[events.Count - 1];
            feedback = result.Kind == JudgementEventKind.Guidance ? "반대 손이에요 · 다시 왼손" : result.Kind == JudgementEventKind.GhostMiss ? "MISS · 박자를 다시 들어요" : result.Kind == JudgementEventKind.UnmatchedRelease ? "놓기 입력 확인" : result.Judgement.ToString().ToUpperInvariant();
        }

        private RunSnapshot Snapshot() => run == null ? new RunSnapshot(RunEndReason.None, 0, 0, 0, 0, 0, 0, 0, 0) : run.Snapshot();
        private int CrowdRemaining() => run == null ? 100 : Mathf.Max(1, 100 - Mathf.Max(0, run.CurrentStageNumber - 1) * 3);
        private int DetailedCrowdCount() => SystemInfo.systemMemorySize < 3000 ? 11 : 23;
        private double CountInDurationSeconds() => 4.0 * 60.0 / chart.Stages[run.CurrentStageNumber - 1].Bpm;
        private string PhaseLabel() => phase == ScreenPhase.Playing ? "STAGE " + run.CurrentStageNumber + " · " + chart.Stages[run.CurrentStageNumber - 1].Bpm + " BPM" : phase == ScreenPhase.CountIn ? "카운트 인 · " + Mathf.Clamp(4 - Mathf.FloorToInt((float)((clock.DspSeconds - countInStartDsp) / (CountInDurationSeconds() / 4.0))), 1, 4) : "GATE 1 PLAYABLE PROTOTYPE";
        private string CueLabel()
        {
            if (phase == ScreenPhase.CountIn) return "4박 카운트 인";
            if (phase != ScreenPhase.Playing) return "100인 합주 · 첫 실기";
            var stage = chart.Stages[run.CurrentStageNumber - 1];
            var currentUs = ToChartUs(clock.RealtimeSeconds);
            foreach (var note in stage.Notes)
                if (note.TargetUs >= currentUs - ContestRun.GoodWindowUs)
                    return "다음 큐 · " + (note.Hand == Hand.Left ? "왼손" : "오른손") + (note.Kind == NoteKind.Hold ? " 길게" : " 짧게");
            return "스테이지 마무리";
        }
        private Color FeedbackColor() => feedback == "PERFECT" ? Mint : feedback == "GOOD" ? Gold : feedback.StartsWith("MISS", StringComparison.Ordinal) ? Coral : Ink;

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            metricStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        private static void DrawPanel(Rect rect)
        {
            GUI.color = Panel;
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;
        }

        private static void DrawText(Rect rect, string text, GUIStyle style, Color color, TextAnchor? alignment = null)
        {
            var original = style.alignment;
            if (alignment.HasValue) style.alignment = alignment.Value;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
            style.alignment = original;
        }

        private void PlayTone(Hand hand)
        {
            var tone = hand == Hand.Left ? leftTone : rightTone;
            if (audioSource != null && tone != null) audioSource.PlayOneShot(tone);
        }

        private static AudioClip CreateMarimbaTone(string clipName, float frequency)
        {
            const int sampleRate = 44100;
            var samples = new float[sampleRate / 4];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)sampleRate;
                var attack = Mathf.Min(1f, time / .004f);
                var release = Mathf.Exp(-time * 14f);
                samples[index] = .38f * attack * release * (Mathf.Sin(2f * Mathf.PI * frequency * time) + .22f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * time));
            }
            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("마림바 음을 만들 수 없습니다.");
            return clip;
        }
    }
}
