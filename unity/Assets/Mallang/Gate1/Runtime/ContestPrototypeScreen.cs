using System;
using System.Collections.Generic;
using Mallang.Gate0.Runtime.Clock;
using Mallang.RhythmCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mallang.Gate1.Runtime
{
    // "리듬을 듣고, 악보를 보며, 다음 마디에 그대로 따라 연주한다" — listen-and-repeat rhythm minigame.
    // No falling notes, no moving judgement line, no BGM. Judgement/scoring is entirely delegated to the
    // unmodified Mallang.RhythmCore.ContestRun (same tap/hold windows, combo and scoring rules as Gate 1's
    // marimba contest); this screen only supplies a fresh one-bar chart per pattern via PatternCompiler and
    // presents it. See docs/03-development/gate-1-implementation-plan.md for the reused judgement contract.
    [RequireComponent(typeof(AudioSource))]
    public sealed class ContestPrototypeScreen : MonoBehaviour
    {
        private enum ScreenPhase { Calibrating, Ready, CountIn, Listen, YourTurn, Result }

        private sealed class RawInput
        {
            public Hand Hand;
            public long PressUs;
            public long? ReleaseUs;
        }

        private const int Bpm = 90;
        private const double ResultHoldSeconds = 1.4;

        private static readonly Color Cream = Hex("FFF9F0");
        private static readonly Color Ink = Hex("183A55");
        private static readonly Color Teal = Hex("7ABF7E");
        private static readonly Color Mint = Hex("9ED6F6");
        private static readonly Color Coral = Hex("FF8F8F");
        private static readonly Color Gold = Hex("F6C445");
        private static readonly Color Walnut = Hex("F3C68B");

        [Header("Gate 1 visual assets")]
        [SerializeField] private Texture2D stageBackground;
        [SerializeField] private Font koreanFont;
        [SerializeField] private double userCalibrationSeconds;

        private readonly List<JudgementEvent> events = new List<JudgementEvent>(16);
        private readonly List<RawInput> rawInputLog = new List<RawInput>(16);
        private readonly Dictionary<int, RawInput> openRawInputs = new Dictionary<int, RawInput>(8);
        private readonly Dictionary<int, Hand> activePointerHands = new Dictionary<int, Hand>(8);
        private readonly SystemRhythmClock clock = new SystemRhythmClock();

        private ClockCalibrator calibrator;
        private ClockCalibration calibration;
        private int patternIndex;
        private PatternBlock currentPattern;
        private ContestChart chart;
        private ContestRun run;
        private RunSnapshot lastSnapshot;
        private ScreenPhase phase;
        private double countInStartDsp;
        private double listenStartDsp;
        private double yourTurnStartDsp;
        private double resultShownRealtime;
        private double feedbackUntil;
        private Hand mouseHand;
        private string feedback = "";

        private AudioSource leftSource;
        private AudioSource rightSource;
        private AudioSource[] metronomeSources;
        private AudioSource[] previewSources;
        private AudioClip leftTapClip;
        private AudioClip rightTapClip;
        private AudioClip leftHoldReleaseClip;
        private AudioClip rightHoldReleaseClip;
        private AudioClip metronomeClip;
        private AudioClip metronomeAccentClip;
        private Texture2D roundedTexture;
        private Texture2D circleTexture;
        private GUIStyle displayStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;

        private double BeatSeconds => 60.0 / Bpm;
        private double BarSeconds => BeatSeconds * 4.0;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            leftSource = CreateAudioSource("Left tone", .55f, -.35f);
            rightSource = CreateAudioSource("Right tone", .55f, .35f);
            metronomeSources = new AudioSource[12];
            for (var index = 0; index < metronomeSources.Length; index++)
                metronomeSources[index] = CreateAudioSource("Metronome " + index, .12f, 0f);
            previewSources = new AudioSource[10];
            for (var index = 0; index < previewSources.Length; index++)
                previewSources[index] = CreateAudioSource("Listen preview " + index, .5f, 0f);
            roundedTexture = CreateShapeTexture(false);
            circleTexture = CreateShapeTexture(true);
            try
            {
                leftTapClip = CreatePercussionTone("Left tap", 220f, 14f);
                rightTapClip = CreatePercussionTone("Right tap", 440f, 16f);
                leftHoldReleaseClip = CreatePercussionTone("Left hold release", 165f, 30f);
                rightHoldReleaseClip = CreatePercussionTone("Right hold release", 330f, 30f);
                metronomeClip = CreatePercussionTone("Metronome tick", 1500f, 55f, .05f);
                metronomeAccentClip = CreatePercussionTone("Metronome accent", 2000f, 45f, .08f);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Listen & Repeat audio unavailable; visual play continues. " + error.Message);
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
            if (phase == ScreenPhase.Ready) return;

            if (phase == ScreenPhase.CountIn && clock.DspSeconds >= listenStartDsp)
            {
                phase = ScreenPhase.Listen;
                return;
            }

            if (phase == ScreenPhase.Listen && clock.DspSeconds >= yourTurnStartDsp)
            {
                phase = ScreenPhase.YourTurn;
                run = new ContestRun(chart);
                return;
            }

            if (phase == ScreenPhase.YourTurn)
            {
                if (run == null) return;
                var stage = chart.Stages[0];
                events.Clear();
                var finalUs = stage.DurationUs + ContestRun.HoldLateGoodWindowUs + 1;
                run.AdvanceTime(Math.Min(YourTurnUs(), finalUs), events);
                CaptureFeedback();
                if (clock.DspSeconds < yourTurnStartDsp + stage.DurationUs / 1000000.0) return;
                events.Clear();
                run.FinalizeCurrentStage(events);
                CaptureFeedback();
                lastSnapshot = run.Snapshot();
                StopScheduledAudio();
                phase = ScreenPhase.Result;
                resultShownRealtime = clock.RealtimeSeconds;
                return;
            }

            if (phase == ScreenPhase.Result && clock.RealtimeSeconds - resultShownRealtime >= ResultHoldSeconds)
            {
                patternIndex = (patternIndex + 1) % ListenAndRepeatPatterns.All.Count;
                BeginProblem();
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
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) HandlePress(Hand.Left, 901, EventTime());
            if (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.jKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) HandlePress(Hand.Right, 902, EventTime());
            if (Keyboard.current.aKey.wasReleasedThisFrame || Keyboard.current.fKey.wasReleasedThisFrame || Keyboard.current.leftArrowKey.wasReleasedThisFrame) HandleRelease(901, EventTime());
            if (Keyboard.current.lKey.wasReleasedThisFrame || Keyboard.current.jKey.wasReleasedThisFrame || Keyboard.current.rightArrowKey.wasReleasedThisFrame) HandleRelease(902, EventTime());
        }

        private void HandlePress(Hand hand, int pointerId, double realtime)
        {
            activePointerHands[pointerId] = hand;
            if (phase == ScreenPhase.Ready) { BeginProblem(); return; }
            if (phase != ScreenPhase.YourTurn || run == null) return;

            var inputUs = ToChartUs(realtime);
            events.Clear();
            run.ProcessPress(hand, pointerId, inputUs, events);
            CaptureFeedback();
            openRawInputs[pointerId] = new RawInput { Hand = hand, PressUs = inputUs, ReleaseUs = null };
            PlayTapTone(hand);
        }

        private void HandleRelease(int pointerId, double realtime)
        {
            activePointerHands.Remove(pointerId);
            if (phase != ScreenPhase.YourTurn || run == null) return;

            var inputUs = ToChartUs(realtime);
            events.Clear();
            run.ProcessRelease(pointerId, inputUs, events);
            CaptureFeedback();
            if (!openRawInputs.TryGetValue(pointerId, out var raw)) return;
            raw.ReleaseUs = inputUs;
            rawInputLog.Add(raw);
            openRawInputs.Remove(pointerId);
            if (IsHoldReleaseEvent(events)) PlayHoldReleaseTone(raw.Hand);
        }

        private bool IsHoldReleaseEvent(List<JudgementEvent> evs)
        {
            foreach (var candidate in evs)
                if (candidate.Kind == JudgementEventKind.Note && IsHoldNote(candidate.NoteId)) return true;
            return false;
        }

        private bool IsHoldNote(int noteId)
        {
            if (chart == null) return false;
            foreach (var note in chart.Stages[0].Notes)
                if (note.Id == noteId) return note.Kind == NoteKind.Hold;
            return false;
        }

        private void CaptureFeedback()
        {
            if (events.Count == 0) return;
            var result = events[events.Count - 1];
            feedback = result.Kind == JudgementEventKind.Guidance ? "왼손부터!"
                : result.Kind == JudgementEventKind.GhostMiss ? "MISS"
                : result.Kind == JudgementEventKind.UnmatchedRelease ? ""
                : result.Judgement.ToString().ToUpperInvariant();
            feedbackUntil = clock.RealtimeSeconds + .6;
        }

        private void BeginProblem()
        {
            var entry = ListenAndRepeatPatterns.All[patternIndex];
            currentPattern = entry.Block;
            chart = PatternCompiler.BuildSingleBarChart("listen-repeat-" + entry.Id, currentPattern, Bpm);
            run = null;
            StopScheduledAudio();
            rawInputLog.Clear();
            openRawInputs.Clear();

            countInStartDsp = clock.DspSeconds + .15;
            listenStartDsp = countInStartDsp + BarSeconds;
            yourTurnStartDsp = listenStartDsp + BarSeconds;
            ScheduleMetronome(countInStartDsp);
            ScheduleListenPreview(listenStartDsp, currentPattern);

            phase = ScreenPhase.CountIn;
            feedback = "";
        }

        private void StartCalibration()
        {
            StopScheduledAudio();
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);
            run = null;
            phase = ScreenPhase.Calibrating;
            feedback = "";
        }

        private void OnApplicationPause(bool paused) { if (paused) StartCalibration(); }
        private void OnApplicationFocus(bool focused) { if (!focused) StartCalibration(); }

        private void OnDestroy()
        {
            StopScheduledAudio();
            DestroyClip(leftTapClip);
            DestroyClip(rightTapClip);
            DestroyClip(leftHoldReleaseClip);
            DestroyClip(rightHoldReleaseClip);
            DestroyClip(metronomeClip);
            DestroyClip(metronomeAccentClip);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (circleTexture != null) Destroy(circleTexture);
        }

        private static void DestroyClip(AudioClip clip) { if (clip != null) Destroy(clip); }

        // realtime -> chart-relative microseconds, measured from the start of the current YOUR TURN bar.
        private long ToChartUs(double realtime)
        {
            var microseconds = (calibration.ToDspSeconds(realtime) - yourTurnStartDsp - userCalibrationSeconds) * 1000000.0;
            return microseconds >= 0 ? (long)Math.Floor(microseconds + .5) : (long)Math.Ceiling(microseconds - .5);
        }

        private long YourTurnUs() => ToChartUs(clock.RealtimeSeconds);
        private static double EventTime() => InputState.currentTime;
        private double TickSeconds(int tick) => tick / (double)PatternBlock.TicksPerBeat * BeatSeconds;

        // ---------- Audio ----------

        private void ScheduleMetronome(double startDsp)
        {
            for (var index = 0; index < metronomeSources.Length; index++)
            {
                var isAccent = index % 4 == 0;
                var source = metronomeSources[index];
                source.clip = isAccent ? metronomeAccentClip : metronomeClip;
                source.volume = isAccent ? .16f : .10f;
                if (source.clip != null) source.PlayScheduled(startDsp + index * BeatSeconds);
            }
        }

        private void ScheduleListenPreview(double barStartDsp, PatternBlock pattern)
        {
            var cursor = 0;
            foreach (var note in pattern.Notes)
            {
                var pan = note.Hand == Hand.Left ? -.35f : .35f;
                var startClip = note.Hand == Hand.Left ? leftTapClip : rightTapClip;
                cursor = PlayPreviewAt(cursor, barStartDsp + TickSeconds(note.StartTick), startClip, pan);
                if (note.Kind == NoteKind.Hold)
                {
                    var endClip = note.Hand == Hand.Left ? leftHoldReleaseClip : rightHoldReleaseClip;
                    cursor = PlayPreviewAt(cursor, barStartDsp + TickSeconds(note.EndTick), endClip, pan);
                }
            }
        }

        private int PlayPreviewAt(int cursor, double dsp, AudioClip clip, float pan)
        {
            if (clip == null || cursor >= previewSources.Length) return cursor;
            var source = previewSources[cursor];
            source.clip = clip;
            source.panStereo = pan;
            source.volume = .5f;
            source.PlayScheduled(dsp);
            return cursor + 1;
        }

        private void StopScheduledAudio()
        {
            if (metronomeSources != null) foreach (var source in metronomeSources) if (source != null) source.Stop();
            if (previewSources != null) foreach (var source in previewSources) if (source != null) source.Stop();
        }

        private void PlayTapTone(Hand hand)
        {
            var source = hand == Hand.Left ? leftSource : rightSource;
            var clip = hand == Hand.Left ? leftTapClip : rightTapClip;
            if (source != null && clip != null) source.PlayOneShot(clip);
        }

        private void PlayHoldReleaseTone(Hand hand)
        {
            var source = hand == Hand.Left ? leftSource : rightSource;
            var clip = hand == Hand.Left ? leftHoldReleaseClip : rightHoldReleaseClip;
            if (source != null && clip != null) source.PlayOneShot(clip);
        }

        private AudioSource CreateAudioSource(string objectName, float volume, float pan)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            source.panStereo = pan;
            return source;
        }

        private static AudioClip CreatePercussionTone(string clipName, float frequency, float decayRate, float overtoneMix = .18f)
        {
            const int sampleRate = 44100;
            var length = Mathf.Clamp(Mathf.RoundToInt(sampleRate * 5f / decayRate), 512, sampleRate / 2);
            var samples = new float[length];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)sampleRate;
                var attack = Mathf.Min(1f, time / .003f);
                var release = Mathf.Exp(-time * decayRate);
                samples[index] = .85f * attack * release * (Mathf.Sin(2f * Mathf.PI * frequency * time) + overtoneMix * Mathf.Sin(2f * Mathf.PI * frequency * 2f * time));
            }
            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException(clipName + " 음을 만들 수 없습니다.");
            return clip;
        }

        // ---------- Rendering ----------

        private void OnGUI()
        {
            EnsureStyles();
            var safe = GuiSafeArea();
            DrawBackdrop();
            if (phase == ScreenPhase.Calibrating) DrawCalibrating(safe);
            else if (phase == ScreenPhase.Ready) DrawReady(safe);
            else DrawPlayField(safe);
        }

        private void DrawBackdrop()
        {
            GUI.color = Color.white;
            if (stageBackground != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), stageBackground, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, Screen.width, Screen.height), Walnut);
        }

        private void DrawCalibrating(Rect safe)
        {
            var unit = Unit(safe);
            var panel = CenterRect(safe, Mathf.Min(520 * unit, safe.width * .82f), 168 * unit, -.02f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 24 * unit, panel.width - 40 * unit, 44 * unit), "박자 맞추는 중…", headingStyle, Ink, TextAnchor.MiddleCenter, 26 * unit);
            DrawText(new Rect(panel.x + 24 * unit, panel.y + 80 * unit, panel.width - 48 * unit, 28 * unit), "잠시만 기다려 주세요", bodyStyle, Ink, TextAnchor.MiddleCenter, 18 * unit);
            DrawProgress(new Rect(panel.x + 50 * unit, panel.y + 124 * unit, panel.width - 100 * unit, 10 * unit), calibrator.SampleCount / (float)calibrator.RequiredSamples, Mint);
        }

        private void DrawReady(Rect safe)
        {
            var unit = Unit(safe);
            var panelWidth = Mathf.Min(570 * unit, safe.width * .78f);
            var panel = new Rect(safe.center.x - panelWidth / 2, safe.y + safe.height * .13f, panelWidth, 194 * unit);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);
            DrawText(new Rect(panel.x + 24 * unit, panel.y + 16 * unit, panel.width - 48 * unit, 52 * unit), "리듬 듣고 따라하기", displayStyle, Ink, TextAnchor.MiddleCenter, 32 * unit);
            DrawText(new Rect(panel.x + 26 * unit, panel.y + 68 * unit, panel.width - 52 * unit, 60 * unit), "악보를 보고, 한 번 들은 뒤\n다음 마디에 그대로 연주하세요", bodyStyle, Ink, TextAnchor.MiddleCenter, 18 * unit);
            var button = new Rect(panel.center.x - 142 * unit, panel.yMax - 64 * unit, 284 * unit, 48 * unit);
            GUI.color = Teal;
            if (GUI.Button(button, "시작", buttonStyle)) BeginProblem();
            GUI.color = Color.white;
        }

        private void DrawPlayField(Rect safe)
        {
            var unit = Unit(safe);
            DrawInputZones(safe);
            DrawHeader(safe, unit);
            DrawNotation(new Rect(safe.x + safe.width * .28f, safe.y + safe.height * .34f, safe.width * .44f, safe.height * .15f), unit, currentPattern);
            if (phase == ScreenPhase.CountIn) DrawCountIn(safe, unit);
            else if (phase == ScreenPhase.YourTurn && clock.RealtimeSeconds < feedbackUntil) DrawFeedback(safe, unit);
            if (phase == ScreenPhase.Result) DrawResult(safe, unit);
        }

        private string PhaseLabel()
        {
            if (phase == ScreenPhase.CountIn) return "준비";
            if (phase == ScreenPhase.Listen) return "듣기";
            if (phase == ScreenPhase.YourTurn) return "연주";
            return "결과";
        }

        private void DrawHeader(Rect safe, float unit)
        {
            var entry = ListenAndRepeatPatterns.All[patternIndex];
            var patternLabel = entry.Id.StartsWith("LR", StringComparison.Ordinal) ? entry.Id.Substring(2) : entry.Id;
            var leftBadge = new Rect(safe.x + 28 * unit, safe.y + 22 * unit, 92 * unit, 52 * unit);
            var rightBadge = new Rect(safe.xMax - 128 * unit, safe.y + 22 * unit, 100 * unit, 52 * unit);
            DrawOutlinedPanel(leftBadge, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);
            DrawOutlinedPanel(rightBadge, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);
            DrawText(leftBadge, patternLabel, displayStyle, Ink, TextAnchor.MiddleCenter, 27 * unit);
            DrawText(rightBadge, PhaseLabel(), headingStyle, Ink, TextAnchor.MiddleCenter, 20 * unit);
        }

        private void DrawInputZones(Rect safe)
        {
            var zoneTop = safe.y + safe.height * .68f;
            var zoneHeight = Mathf.Max(0, safe.yMax - zoneTop);
            var half = Screen.width * .5f;
            var leftRect = new Rect(0, zoneTop, half, zoneHeight);
            var rightRect = new Rect(half, zoneTop, Screen.width - half, zoneHeight);
            var leftPressed = IsHandPressed(Hand.Left);
            var rightPressed = IsHandPressed(Hand.Right);
            if (leftPressed) Fill(leftRect, new Color(Mint.r, Mint.g, Mint.b, .13f));
            if (rightPressed) Fill(rightRect, new Color(Coral.r, Coral.g, Coral.b, .13f));

            var unit = Unit(safe);
            var badgeSize = 44 * unit;
            var badgeY = safe.yMax - 72 * unit;
            var leftBadge = new Rect(safe.x + 34 * unit, badgeY, badgeSize, badgeSize);
            var rightBadge = new Rect(safe.xMax - 34 * unit - badgeSize, badgeY, badgeSize, badgeSize);
            DrawDisc(leftBadge, Mint);
            DrawDisc(rightBadge, Coral);
            DrawText(leftBadge, "L", headingStyle, Ink, TextAnchor.MiddleCenter, 20 * unit);
            DrawText(rightBadge, "R", headingStyle, Ink, TextAnchor.MiddleCenter, 20 * unit);
        }

        private bool IsHandPressed(Hand hand)
        {
            foreach (var pair in activePointerHands) if (pair.Value == hand) return true;
            return false;
        }

        private void DrawNotation(Rect panel, float unit, PatternBlock pattern)
        {
            if (pattern == null) return;
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);

            var laneY = panel.center.y;
            var laneX0 = panel.x + 30 * unit;
            var laneX1 = panel.xMax - 30 * unit;
            var laneWidth = Mathf.Max(1f, laneX1 - laneX0);

            for (var beat = 0; beat <= 4; beat++)
            {
                var x = laneX0 + laneWidth * (beat / 4f);
                Fill(new Rect(x - 1, panel.y + 13 * unit, 2, panel.height - 26 * unit), new Color(Ink.r, Ink.g, Ink.b, .10f));
            }

            foreach (var note in pattern.Notes) DrawNote(note.Hand, note.Kind, note.StartTick, note.EndTick, laneX0, laneWidth, laneY, unit);
        }

        private void DrawNote(Hand hand, NoteKind kind, int startTick, int endTick, float laneX0, float laneWidth, float laneY, float unit)
        {
            var color = hand == Hand.Left ? Mint : Coral;
            var label = hand == Hand.Left ? "L" : "R";
            var x0 = laneX0 + laneWidth * (startTick / (float)PatternBlock.TicksPerBlock);
            var size = 36 * unit;
            if (kind == NoteKind.Hold)
            {
                var x1 = laneX0 + laneWidth * (endTick / (float)PatternBlock.TicksPerBlock);
                var tailHeight = 20 * unit;
                DrawPanel(new Rect(x0, laneY - tailHeight / 2, Mathf.Max(x1 - x0, tailHeight), tailHeight), color);
            }

            var noteRect = new Rect(x0 - size / 2, laneY - size / 2, size, size);
            DrawDisc(noteRect, color);
            DrawText(noteRect, label, headingStyle, Ink, TextAnchor.MiddleCenter, 19 * unit);
        }

        private void DrawCountIn(Rect safe, float unit)
        {
            var elapsed = Math.Max(0, clock.DspSeconds - countInStartDsp);
            var count = Mathf.Clamp(4 - Mathf.FloorToInt((float)(elapsed / BeatSeconds)), 1, 4);
            var panel = new Rect(safe.center.x - 46 * unit, safe.y + safe.height * .42f, 92 * unit, 92 * unit);
            DrawDisc(panel, Gold);
            DrawText(panel, count.ToString(), displayStyle, Ink, TextAnchor.MiddleCenter, 40 * unit);
        }

        private void DrawFeedback(Rect safe, float unit)
        {
            var color = feedback == "PERFECT" ? Mint : feedback == "GOOD" ? Gold : feedback.StartsWith("MISS", StringComparison.Ordinal) ? Coral : Cream;
            DrawText(new Rect(safe.center.x - 200 * unit, safe.y + safe.height * .42f, 400 * unit, 50 * unit), feedback, displayStyle, color, TextAnchor.MiddleCenter, 34 * unit);
        }

        private void DrawResult(Rect safe, float unit)
        {
            var panelWidth = Mathf.Min(560 * unit, safe.width * .86f);
            var panel = CenterRect(safe, panelWidth, 240 * unit, .02f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .97f), 3 * unit);
            var headline = lastSnapshot.AccuracyBasisPoints >= 9000 ? "PERFECT!" : lastSnapshot.AccuracyBasisPoints >= 6000 ? "GOOD!" : "다시 들어볼까요?";
            DrawText(new Rect(panel.x + 16 * unit, panel.y + 14 * unit, panel.width - 32 * unit, 40 * unit), headline, displayStyle, Ink, TextAnchor.MiddleCenter, 28 * unit);
            DrawText(new Rect(panel.x + 16 * unit, panel.y + 58 * unit, panel.width - 32 * unit, 32 * unit), lastSnapshot.RankScore.ToString("N0") + "점  ·  정확도 " + (lastSnapshot.AccuracyBasisPoints / 100f).ToString("0.0") + "%", headingStyle, Ink, TextAnchor.MiddleCenter, 19 * unit);

            var compareY = panel.y + 104 * unit;
            DrawMiniLane(new Rect(panel.x + 16 * unit, compareY, panel.width - 32 * unit, 56 * unit), "ORIGINAL", Ink, DrawOriginalLane);
            DrawMiniLane(new Rect(panel.x + 16 * unit, compareY + 62 * unit, panel.width - 32 * unit, 56 * unit), "YOU", Gold, DrawYouLane);
        }

        private void DrawMiniLane(Rect rect, string label, Color labelColor, Action<Rect, float, float, float, float> draw)
        {
            DrawText(new Rect(rect.x, rect.y, 68, rect.height), label, smallStyle, labelColor, TextAnchor.MiddleLeft, 13);
            var laneX0 = rect.x + 72;
            var laneX1 = rect.xMax - 8;
            var laneWidth = Mathf.Max(1f, laneX1 - laneX0);
            var laneLeftY = rect.y + rect.height * .30f;
            var laneRightY = rect.y + rect.height * .74f;
            draw(rect, laneX0, laneWidth, laneLeftY, laneRightY);
        }

        private void DrawOriginalLane(Rect rect, float laneX0, float laneWidth, float laneLeftY, float laneRightY)
        {
            if (currentPattern == null) return;
            foreach (var note in currentPattern.Notes)
            {
                var y = note.Hand == Hand.Left ? laneLeftY : laneRightY;
                var color = note.Hand == Hand.Left ? Mint : Coral;
                var x0 = laneX0 + laneWidth * (note.StartTick / (float)PatternBlock.TicksPerBlock);
                if (note.Kind == NoteKind.Tap) { DrawDisc(new Rect(x0 - 8, y - 8, 16, 16), color); continue; }
                var x1 = laneX0 + laneWidth * (note.EndTick / (float)PatternBlock.TicksPerBlock);
                DrawPanel(new Rect(x0, y - 6, Mathf.Max(x1 - x0, 10), 12), color);
            }
        }

        private void DrawYouLane(Rect rect, float laneX0, float laneWidth, float laneLeftY, float laneRightY)
        {
            var barUs = (long)(BarSeconds * 1000000.0);
            foreach (var raw in rawInputLog)
            {
                var y = raw.Hand == Hand.Left ? laneLeftY : laneRightY;
                var color = raw.Hand == Hand.Left ? Mint : Coral;
                var x0 = laneX0 + laneWidth * Mathf.Clamp01(raw.PressUs / (float)barUs);
                if (raw.ReleaseUs.HasValue && raw.ReleaseUs.Value - raw.PressUs > 150000)
                {
                    var x1 = laneX0 + laneWidth * Mathf.Clamp01(raw.ReleaseUs.Value / (float)barUs);
                    DrawPanel(new Rect(x0, y - 6, Mathf.Max(x1 - x0, 10), 12), color);
                }
                else DrawDisc(new Rect(x0 - 8, y - 8, 16, 16), color);
            }
        }

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

        private void DrawOutlinedPanel(Rect rect, Color color, float border)
        {
            DrawPanel(rect, Ink);
            DrawPanel(new Rect(rect.x + border, rect.y + border, rect.width - border * 2, rect.height - border * 2), color);
        }

        private void DrawDisc(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, circleTexture, ScaleMode.StretchToFill, true);
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

        private static Texture2D CreateShapeTexture(bool circle)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "Listen&Repeat circle" : "Listen&Repeat rounded panel" };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float alpha;
                if (circle)
                {
                    var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f));
                    alpha = Mathf.Clamp01(31f - distance);
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

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
