using System;
using System.Collections.Generic;
using Mallang.Gate0.Runtime.Clock;
using Mallang.RhythmCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mallang.Gate1.Runtime
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ContestPrototypeScreen : MonoBehaviour
    {
        private enum ScreenPhase { Calibrating, Ready, CountIn, Playing, Result }

        private static readonly Color Cream = Hex("FFF4D6");
        private static readonly Color Ink = Hex("20343A");
        private static readonly Color Teal = Hex("1F8F83");
        private static readonly Color Mint = Hex("6FE0C2");
        private static readonly Color Coral = Hex("F06B5B");
        private static readonly Color Gold = Hex("F5C451");
        private static readonly Color Walnut = Hex("4A271C");

        [Header("Gate 1 visual assets")]
        [SerializeField] private Texture2D stageBackground;
        [SerializeField] private Texture2D mascotTexture;
        [SerializeField] private Font koreanFont;
        [SerializeField] private double userCalibrationSeconds;

        private readonly List<JudgementEvent> events = new List<JudgementEvent>(16);
        private readonly SystemRhythmClock clock = new SystemRhythmClock();
        private ClockCalibrator calibrator;
        private ClockCalibration calibration;
        private ContestChart chart;
        private ContestRun run;
        private ScreenPhase phase;
        private double countInStartDsp;
        private double stageStartDsp;
        private double feedbackUntil;
        private double mascotBounceUntil;
        private Hand mouseHand;
        private Hand lastHitHand;
        private string feedback = "";
        private AudioSource audioSource;
        private AudioSource backingSource;
        private AudioSource[] countInSources;
        private AudioClip leftTone;
        private AudioClip rightTone;
        private AudioClip countInTone;
        private AudioClip backingLoop;
        private Texture2D roundedTexture;
        private Texture2D circleTexture;
        private Texture2D ringTexture;
        private GUIStyle displayStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            audioSource.volume = 0.25f;
            backingSource = CreateAudioSource("Gate 1 accompaniment", .22f);
            countInSources = new AudioSource[4];
            for (var index = 0; index < countInSources.Length; index++)
                countInSources[index] = CreateAudioSource("Gate 1 count " + (index + 1), .34f);
            roundedTexture = CreateShapeTexture(false, false);
            circleTexture = CreateShapeTexture(true, false);
            ringTexture = CreateShapeTexture(true, true);
            try
            {
                leftTone = CreateMarimbaTone("Left marimba", 329.63f);
                rightTone = CreateMarimbaTone("Right marimba", 493.88f);
                countInTone = CreateMarimbaTone("Count-in", 880f);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Gate 1 audio unavailable; visual play continues. " + error.Message);
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
                if (calibration.Valid) phase = ScreenPhase.Ready;
                return;
            }

            if (phase == ScreenPhase.CountIn && clock.DspSeconds >= countInStartDsp + CountInDurationSeconds())
            {
                phase = ScreenPhase.Playing;
                feedback = "시작!";
                feedbackUntil = clock.RealtimeSeconds + .65;
                return;
            }

            if (phase != ScreenPhase.Playing || run == null) return;
            events.Clear();
            var finalTimestampUs = CurrentStage().DurationUs + ContestRun.HoldLateGoodWindowUs + 1;
            run.AdvanceTime(Math.Min(CurrentStageUs(), finalTimestampUs), events);
            CaptureFeedback();
            if (clock.DspSeconds < stageStartDsp + CurrentStage().DurationUs / 1000000.0) return;
            events.Clear();
            run.FinalizeCurrentStage(events);
            CaptureFeedback();
            if (!run.IsActive)
            {
                StopScheduledAudio();
                phase = ScreenPhase.Result;
            }
            else BeginCountIn();
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
                        HandlePress(hand, touch.touchId.ReadValue(), EventTime());
                    }
                    if (touch.press.wasReleasedThisFrame) HandleRelease(touch.touchId.ReadValue(), EventTime());
                }
            }

            if (!hasTouch && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    mouseHand = Mouse.current.position.ReadValue().x < Screen.width * .5f ? Hand.Left : Hand.Right;
                    HandlePress(mouseHand, 900, EventTime());
                }
                if (Mouse.current.leftButton.wasReleasedThisFrame) HandleRelease(900, EventTime());
            }

            if (Keyboard.current == null) return;
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) HandlePress(Hand.Left, 901, EventTime());
            if (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) HandlePress(Hand.Right, 902, EventTime());
            if (Keyboard.current.aKey.wasReleasedThisFrame || Keyboard.current.leftArrowKey.wasReleasedThisFrame) HandleRelease(901, EventTime());
            if (Keyboard.current.lKey.wasReleasedThisFrame || Keyboard.current.rightArrowKey.wasReleasedThisFrame) HandleRelease(902, EventTime());
        }

        private void OnGUI()
        {
            EnsureStyles();
            var safe = GuiSafeArea();
            DrawBackdrop();
            DrawTopShade(safe);
            if (phase == ScreenPhase.Calibrating) DrawCalibrating(safe);
            else if (phase == ScreenPhase.Ready) DrawReady(safe);
            else if (phase == ScreenPhase.Result) DrawResult(safe);
            else DrawPerformance(safe);
        }

        private void DrawBackdrop()
        {
            GUI.color = Color.white;
            if (stageBackground != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), stageBackground, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, Screen.width, Screen.height), Walnut);
        }

        private void DrawTopShade(Rect safe)
        {
            Fill(new Rect(0, 0, Screen.width, Mathf.Max(112, safe.y + safe.height * .15f)), new Color(.08f, .04f, .02f, .68f));
            Fill(new Rect(0, Screen.height * .79f, Screen.width, Screen.height * .21f), new Color(.08f, .04f, .02f, .44f));
        }

        private void DrawCalibrating(Rect safe)
        {
            var unit = Unit(safe);
            DrawTitle(safe, unit);
            var panel = CenterRect(safe, Mathf.Min(520 * unit, safe.width * .82f), 168 * unit, -.02f);
            DrawPanel(panel, new Color(.12f, .12f, .10f, .86f));
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 24 * unit, panel.width - 40 * unit, 44 * unit), "악기 조율 중…", headingStyle, Cream, TextAnchor.MiddleCenter, 26 * unit);
            DrawText(new Rect(panel.x + 24 * unit, panel.y + 80 * unit, panel.width - 48 * unit, 28 * unit), "잠시만 기다려 주세요", bodyStyle, Cream, TextAnchor.MiddleCenter, 18 * unit);
            DrawProgress(new Rect(panel.x + 50 * unit, panel.y + 124 * unit, panel.width - 100 * unit, 10 * unit), calibrator.SampleCount / (float)calibrator.RequiredSamples, Mint);
        }

        private void DrawReady(Rect safe)
        {
            var unit = Unit(safe);
            DrawTitle(safe, unit);
            DrawMascot(safe, unit);
            var panelWidth = Mathf.Min(620 * unit, safe.width * .88f);
            var panel = new Rect(safe.center.x - panelWidth / 2, safe.y + safe.height * .12f, panelWidth, 202 * unit);
            DrawPanel(panel, new Color(.13f, .09f, .05f, .88f));
            DrawText(new Rect(panel.x + 24 * unit, panel.y + 18 * unit, panel.width - 48 * unit, 52 * unit), "마림바 첫 실기", displayStyle, Cream, TextAnchor.MiddleCenter, 38 * unit);
            DrawText(new Rect(panel.x + 26 * unit, panel.y + 72 * unit, panel.width - 52 * unit, 48 * unit), "음표가 원에 닿는 순간, 같은 쪽 화면을 누르세요", bodyStyle, Cream, TextAnchor.MiddleCenter, 19 * unit);
            var button = new Rect(panel.center.x - 142 * unit, panel.yMax - 64 * unit, 284 * unit, 48 * unit);
            GUI.color = Teal;
            if (GUI.Button(button, "연주 시작", buttonStyle)) BeginCountIn();
            GUI.color = Color.white;
        }

        private void DrawTitle(Rect safe, float unit)
        {
            DrawText(new Rect(safe.x + 24 * unit, safe.y + 18 * unit, safe.width - 48 * unit, 42 * unit), "말랑두뇌 음악대학", headingStyle, Cream, TextAnchor.MiddleLeft, 26 * unit);
        }

        private void DrawPerformance(Rect safe)
        {
            var unit = Unit(safe);
            DrawHud(safe, unit);
            DrawRhythmLane(safe, unit);
            DrawMascot(safe, unit);
            DrawHandZones(safe, unit);
            if (phase == ScreenPhase.CountIn) DrawCountIn(safe, unit);
            else if (clock.RealtimeSeconds < feedbackUntil) DrawFeedback(safe, unit);
        }

        private void DrawHud(Rect safe, float unit)
        {
            var snapshot = Snapshot();
            var stageNumber = run == null ? 1 : run.CurrentStageNumber;
            DrawText(new Rect(safe.x + 24 * unit, safe.y + 14 * unit, 260 * unit, 34 * unit), "STAGE " + stageNumber + " / 20", headingStyle, Cream, TextAnchor.MiddleLeft, 22 * unit);
            DrawText(new Rect(safe.x + 24 * unit, safe.y + 48 * unit, 260 * unit, 28 * unit), CurrentBpm() + " BPM", bodyStyle, Mint, TextAnchor.MiddleLeft, 16 * unit);
            DrawText(new Rect(safe.center.x - 180 * unit, safe.y + 12 * unit, 360 * unit, 34 * unit), "현재 " + CrowdRank() + "위", headingStyle, Gold, TextAnchor.MiddleCenter, 23 * unit);
            DrawProgress(new Rect(safe.center.x - 150 * unit, safe.y + 53 * unit, 300 * unit, 9 * unit), stageNumber / 20f, Gold);
            var rightEdge = safe.xMax - 116 * unit;
            DrawText(new Rect(rightEdge - 276 * unit, safe.y + 12 * unit, 276 * unit, 30 * unit), snapshot.RankScore.ToString("N0") + "점", headingStyle, Cream, TextAnchor.MiddleRight, 21 * unit);
            DrawText(new Rect(rightEdge - 276 * unit, safe.y + 45 * unit, 276 * unit, 27 * unit), "콤보 " + snapshot.Combo + "  ·  정확도 " + (snapshot.AccuracyBasisPoints / 100f).ToString("0.0") + "%", smallStyle, Cream, TextAnchor.MiddleRight, 14 * unit);
        }

        private void DrawRhythmLane(Rect safe, float unit)
        {
            var lane = new Rect(safe.x + safe.width * .15f, safe.y + safe.height * .16f, safe.width * .70f, safe.height * .34f);
            DrawPanel(lane, new Color(.10f, .07f, .04f, .60f));
            var leftX = lane.x + lane.width * .30f;
            var rightX = lane.x + lane.width * .70f;
            var targetY = lane.yMax - 48 * unit;
            var targetSize = 72 * unit;
            DrawRing(new Rect(leftX - targetSize / 2, targetY - targetSize / 2, targetSize, targetSize), Mint);
            DrawRing(new Rect(rightX - targetSize / 2, targetY - targetSize / 2, targetSize, targetSize), Coral);
            DrawText(new Rect(leftX - 90 * unit, targetY + 39 * unit, 180 * unit, 26 * unit), "왼손", bodyStyle, Cream, TextAnchor.MiddleCenter, 16 * unit);
            DrawText(new Rect(rightX - 90 * unit, targetY + 39 * unit, 180 * unit, 26 * unit), "오른손", bodyStyle, Cream, TextAnchor.MiddleCenter, 16 * unit);

            if (phase != ScreenPhase.Playing || run == null) return;
            var nowUs = CurrentStageUs();
            foreach (var note in CurrentStage().Notes)
            {
                var deltaUs = note.TargetUs - nowUs;
                if (deltaUs < -ContestRun.GoodWindowUs || deltaUs > 1400000) continue;
                var progress = Mathf.Clamp01(1f - deltaUs / 1200000f);
                var x = note.Hand == Hand.Left ? leftX : rightX;
                var y = Mathf.Lerp(lane.y + 32 * unit, targetY, progress);
                var size = (note.Kind == NoteKind.Hold ? 48 : 40) * unit;
                DrawDisc(new Rect(x - size / 2, y - size / 2, size, size), note.Hand == Hand.Left ? Mint : Coral);
                if (note.Kind == NoteKind.Hold) DrawText(new Rect(x - 60 * unit, y - 13 * unit, 120 * unit, 26 * unit), "길게", smallStyle, Ink, TextAnchor.MiddleCenter, 13 * unit);
            }
        }

        private void DrawMascot(Rect safe, float unit)
        {
            if (mascotTexture == null) return;
            var height = Mathf.Min(safe.height * .35f, 280 * unit);
            var width = height * mascotTexture.width / mascotTexture.height;
            var bounce = clock.RealtimeSeconds < mascotBounceUntil ? Mathf.Sin((float)(clock.RealtimeSeconds * 32)) * 7 * unit : 0;
            var lean = clock.RealtimeSeconds < mascotBounceUntil ? (lastHitHand == Hand.Left ? -7 : 7) * unit : 0;
            var rect = new Rect(safe.center.x - width / 2 + lean, safe.y + safe.height * .48f - height * .18f + bounce, width, height);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, mascotTexture, ScaleMode.ScaleToFit, true);
        }

        private void DrawHandZones(Rect safe, float unit)
        {
            var gap = 14 * unit;
            var side = (safe.width - gap - 32 * unit) / 2;
            var height = Mathf.Min(104 * unit, safe.height * .16f);
            var y = safe.yMax - height - 14 * unit;
            DrawHandZone(new Rect(safe.x + 16 * unit, y, side, height), "왼손", "화면 왼쪽", Mint, TextAnchor.MiddleLeft, unit);
            DrawHandZone(new Rect(safe.x + 16 * unit + side + gap, y, side, height), "오른손", "화면 오른쪽", Coral, TextAnchor.MiddleRight, unit);
        }

        private void DrawHandZone(Rect rect, string title, string hint, Color color, TextAnchor alignment, float unit)
        {
            DrawPanel(rect, new Color(color.r, color.g, color.b, .88f));
            var padding = 24 * unit;
            DrawText(new Rect(rect.x + padding, rect.y + 13 * unit, rect.width - padding * 2, 40 * unit), title, headingStyle, Ink, alignment, 27 * unit);
            DrawText(new Rect(rect.x + padding, rect.y + 52 * unit, rect.width - padding * 2, 28 * unit), hint, smallStyle, Ink, alignment, 14 * unit);
        }

        private void DrawCountIn(Rect safe, float unit)
        {
            var beatDuration = CountInDurationSeconds() / 4.0;
            var elapsed = Math.Max(0, clock.DspSeconds - countInStartDsp);
            var count = Mathf.Clamp(4 - Mathf.FloorToInt((float)(elapsed / beatDuration)), 1, 4);
            var panel = new Rect(safe.center.x - 90 * unit, safe.center.y - 90 * unit, 180 * unit, 180 * unit);
            DrawDisc(panel, Gold);
            DrawText(panel, count.ToString(), displayStyle, Ink, TextAnchor.MiddleCenter, 76 * unit);
            DrawText(new Rect(panel.x - 100 * unit, panel.y - 42 * unit, panel.width + 200 * unit, 32 * unit), "리듬을 들어 보세요", headingStyle, Cream, TextAnchor.MiddleCenter, 20 * unit);
        }

        private void DrawFeedback(Rect safe, float unit)
        {
            var color = feedback == "PERFECT" ? Mint : feedback == "GOOD" ? Gold : feedback.StartsWith("MISS", StringComparison.Ordinal) ? Coral : Cream;
            DrawText(new Rect(safe.center.x - 220 * unit, safe.y + safe.height * .51f, 440 * unit, 58 * unit), feedback, displayStyle, color, TextAnchor.MiddleCenter, 42 * unit);
        }

        private void DrawResult(Rect safe)
        {
            var unit = Unit(safe);
            DrawMascot(safe, unit);
            var snapshot = Snapshot();
            var panelWidth = Mathf.Min(580 * unit, safe.width * .88f);
            var panel = CenterRect(safe, panelWidth, 286 * unit, -.04f);
            DrawPanel(panel, new Color(.12f, .08f, .04f, .90f));
            var completed = snapshot.EndReason == RunEndReason.Completed;
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 18 * unit, panel.width - 40 * unit, 48 * unit), completed ? "첫 실기 완주!" : "다시 도전해 볼까요?", displayStyle, completed ? Gold : Cream, TextAnchor.MiddleCenter, 34 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 78 * unit, panel.width - 40 * unit, 36 * unit), "최종 " + CrowdRank() + "위  ·  " + snapshot.RankScore.ToString("N0") + "점", headingStyle, Cream, TextAnchor.MiddleCenter, 22 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 119 * unit, panel.width - 40 * unit, 30 * unit), "정확도 " + (snapshot.AccuracyBasisPoints / 100f).ToString("0.0") + "%  ·  마지막 콤보 " + snapshot.Combo, bodyStyle, Mint, TextAnchor.MiddleCenter, 17 * unit);
            GUI.color = Coral;
            if (GUI.Button(new Rect(panel.center.x - 140 * unit, panel.yMax - 72 * unit, 280 * unit, 50 * unit), "다시 연주", buttonStyle)) StartCalibration();
            GUI.color = Color.white;
        }

        private void HandlePress(Hand hand, int pointerId, double realtime)
        {
            if (phase == ScreenPhase.Ready) { BeginCountIn(); return; }
            if (phase == ScreenPhase.Result) { StartCalibration(); return; }
            if (phase != ScreenPhase.Playing || run == null) return;
            events.Clear();
            run.ProcessPress(hand, pointerId, ToChartUs(realtime), events);
            CaptureFeedback();
            lastHitHand = hand;
            mascotBounceUntil = clock.RealtimeSeconds + .22;
            PlayTone(hand);
        }

        private void HandleRelease(int pointerId, double realtime)
        {
            if (phase != ScreenPhase.Playing || run == null) return;
            events.Clear();
            run.ProcessRelease(pointerId, ToChartUs(realtime), events);
            CaptureFeedback();
        }

        private void CaptureFeedback()
        {
            if (events.Count == 0) return;
            var result = events[events.Count - 1];
            feedback = result.Kind == JudgementEventKind.Guidance ? "왼손부터 시작해요" : result.Kind == JudgementEventKind.GhostMiss ? "MISS" : result.Kind == JudgementEventKind.UnmatchedRelease ? "" : result.Judgement.ToString().ToUpperInvariant();
            feedbackUntil = clock.RealtimeSeconds + .72;
        }

        private void BeginCountIn()
        {
            if (!calibration.Valid) return;
            if (chart == null) chart = Gate1ChartFactory.Create();
            if (run == null) run = new ContestRun(chart);
            StopScheduledAudio();
            countInStartDsp = clock.DspSeconds + .15;
            stageStartDsp = countInStartDsp + CountInDurationSeconds();
            ScheduleStageAudio();
            phase = ScreenPhase.CountIn;
            feedback = "";
        }

        private void StartCalibration()
        {
            StopScheduledAudio();
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);
            chart = null;
            run = null;
            phase = ScreenPhase.Calibrating;
            feedback = "";
        }

        private void OnApplicationPause(bool paused) { if (paused) StartCalibration(); }
        private void OnApplicationFocus(bool focused) { if (!focused) StartCalibration(); }

        private void OnDestroy()
        {
            StopScheduledAudio();
            if (leftTone != null) Destroy(leftTone);
            if (rightTone != null) Destroy(rightTone);
            if (countInTone != null) Destroy(countInTone);
            if (backingLoop != null) Destroy(backingLoop);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (circleTexture != null) Destroy(circleTexture);
            if (ringTexture != null) Destroy(ringTexture);
        }

        private long ToChartUs(double realtime)
        {
            var microseconds = (calibration.ToDspSeconds(realtime) - stageStartDsp - userCalibrationSeconds) * 1000000.0;
            return microseconds >= 0 ? (long)Math.Floor(microseconds + .5) : (long)Math.Ceiling(microseconds - .5);
        }

        private long CurrentStageUs() => ToChartUs(clock.RealtimeSeconds);
        private static double EventTime() => InputState.currentTime;
        private StageDefinition CurrentStage() => chart.Stages[run.CurrentStageNumber - 1];
        private int CurrentBpm() => run == null || chart == null ? 80 : CurrentStage().Bpm;
        private double CountInDurationSeconds() => 4.0 * 60.0 / CurrentBpm();
        private RunSnapshot Snapshot() => run == null ? new RunSnapshot(RunEndReason.None, 0, 0, 0, 0, 0, 0, 0, 0) : run.Snapshot();
        private int CrowdRank() => run == null ? 100 : Mathf.Clamp(100 - Mathf.RoundToInt((run.CurrentStageNumber - 1) / 19f * 99f), 1, 100);

        private static Rect GuiSafeArea()
        {
            var area = Screen.safeArea;
            return new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
        }

        private static float Unit(Rect safe) => Mathf.Clamp(Mathf.Min(safe.width / 1280f, safe.height / 720f), .72f, 2.5f);
        private static Rect CenterRect(Rect safe, float width, float height, float yOffsetRatio) => new Rect(safe.center.x - width / 2, safe.center.y - height / 2 + safe.height * yOffsetRatio, width, height);

        private void EnsureStyles()
        {
            if (displayStyle != null) return;
            displayStyle = Style(FontStyle.Bold);
            headingStyle = Style(FontStyle.Bold);
            bodyStyle = Style(FontStyle.Normal);
            smallStyle = Style(FontStyle.Normal);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = koreanFont, fontStyle = FontStyle.Bold, fontSize = 22, alignment = TextAnchor.MiddleCenter, border = new RectOffset(20, 20, 20, 20) };
            buttonStyle.normal.background = roundedTexture;
            buttonStyle.hover.background = roundedTexture;
            buttonStyle.active.background = roundedTexture;
            buttonStyle.normal.textColor = Cream;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Cream;
        }

        private GUIStyle Style(FontStyle fontStyle) => new GUIStyle(GUI.skin.label) { font = koreanFont, fontStyle = fontStyle, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };

        private static void DrawText(Rect rect, string text, GUIStyle style, Color color, TextAnchor alignment, float fontSize)
        {
            style.alignment = alignment;
            style.fontSize = Mathf.Max(10, Mathf.RoundToInt(fontSize));
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, roundedTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        private void DrawDisc(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        private void DrawRing(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, ringTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        private void DrawProgress(Rect rect, float value, Color color)
        {
            DrawPanel(rect, new Color(1, 1, 1, .24f));
            if (value > 0) DrawPanel(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static Texture2D CreateShapeTexture(bool circle, bool ring)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = ring ? "Gate1 ring" : circle ? "Gate1 circle" : "Gate1 rounded panel" };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float alpha;
                if (circle)
                {
                    var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f));
                    alpha = ring ? Mathf.Clamp01(2f - Mathf.Abs(distance - 25f)) : Mathf.Clamp01(31f - distance);
                }
                else
                {
                    var dx = Mathf.Max(0, 12 - Mathf.Min(x, size - 1 - x));
                    var dy = Mathf.Max(0, 12 - Mathf.Min(y, size - 1 - y));
                    alpha = Mathf.Clamp01(12.5f - Mathf.Sqrt(dx * dx + dy * dy));
                }
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void PlayTone(Hand hand)
        {
            var tone = hand == Hand.Left ? leftTone : rightTone;
            if (audioSource != null && tone != null) audioSource.PlayOneShot(tone);
        }

        private AudioSource CreateAudioSource(string objectName, float volume)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            return source;
        }

        private void ScheduleStageAudio()
        {
            if (countInTone != null)
            {
                var beatSeconds = 60.0 / CurrentBpm();
                for (var index = 0; index < countInSources.Length; index++)
                {
                    countInSources[index].clip = countInTone;
                    countInSources[index].pitch = index == 0 ? 1.25f : 1f;
                    countInSources[index].PlayScheduled(countInStartDsp + beatSeconds * index);
                }
            }

            if (backingSource == null) return;
            try
            {
                if (backingLoop != null) Destroy(backingLoop);
                backingLoop = CreateBackingLoop(CurrentBpm());
                backingSource.clip = backingLoop;
                backingSource.loop = true;
                backingSource.PlayScheduled(stageStartDsp);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Gate 1 accompaniment unavailable; visual play continues. " + error.Message);
            }
        }

        private void StopScheduledAudio()
        {
            if (backingSource != null) backingSource.Stop();
            if (countInSources == null) return;
            foreach (var source in countInSources)
                if (source != null) source.Stop();
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

        private static AudioClip CreateBackingLoop(int bpm)
        {
            const int sampleRate = 44100;
            var beatSeconds = 60f / bpm;
            var samples = new float[Mathf.RoundToInt(sampleRate * beatSeconds * 4f)];
            var bassNotes = new[] { 164.81f, 196f, 220f, 196f };
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)sampleRate;
                var beatIndex = Mathf.Min(3, Mathf.FloorToInt(time / beatSeconds));
                var beatTime = time - beatIndex * beatSeconds;
                var bassEnvelope = Mathf.Exp(-beatTime * 4.5f);
                var pulseEnvelope = Mathf.Exp(-beatTime * 12f);
                var bass = Mathf.Sin(2f * Mathf.PI * bassNotes[beatIndex] * time);
                var overtone = Mathf.Sin(2f * Mathf.PI * bassNotes[beatIndex] * 2f * time);
                var pulse = Mathf.Sin(2f * Mathf.PI * 659.25f * beatTime);
                samples[index] = .16f * bassEnvelope * (bass + .24f * overtone) + .055f * pulseEnvelope * pulse;
            }
            var clip = AudioClip.Create("Gate 1 prototype accompaniment " + bpm + " BPM", samples.Length, 1, sampleRate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("반주음을 만들 수 없습니다.");
            return clip;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
