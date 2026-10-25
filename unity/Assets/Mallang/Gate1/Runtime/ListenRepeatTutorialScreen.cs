using System;
using System.Collections.Generic;
using Mallang.Gate0.Runtime.Clock;
using Mallang.RhythmCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Mallang.Gate1.Runtime
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ListenRepeatTutorialScreen : MonoBehaviour
    {
        private const double ReadySeconds = 0.55;
        private const double ResultSeconds = 1.15;
        private const double TransitionSeconds = 0.35;
        private const double ScheduledLeadSeconds = 0.12;
        private const int CountdownBeats = 3;
        private const int PreparationRestBeats = 1;

        private static readonly Color Cream = Hex("FAF7EF");
        private static readonly Color Ink = Hex("332923");
        private static readonly Color Pink = Hex("EC6582");
        private static readonly Color SoftPink = Hex("F8BABB");
        private static readonly Color Blue = Hex("DDA886");
        private static readonly Color Yellow = Hex("F5C96B");

        [SerializeField] private Texture2D rabbitAtlas;
        [SerializeField] private Font koreanFont;
        // Optional replacements. The original rabbit/brain remain separate from presentation state.
        [SerializeField] private Texture2D paperBackground;
        [SerializeField] private Texture2D leftInstrument;
        [SerializeField] private Texture2D rightInstrument;
        // Visible artwork bounds in source pixels, measured from the top-left; PNGs stay unchanged.
        [SerializeField] private Rect leftInstrumentPixels = new Rect(196, 248, 621, 442);
        [SerializeField] private Rect rightInstrumentPixels = new Rect(204, 346, 633, 309);
        [SerializeField] private AudioClip dongSample;
        [SerializeField] private AudioClip takSample;
        [SerializeField, Range(0, 10000)] private int passAccuracyBasisPoints = 6000;
        [SerializeField] private double userCalibrationSeconds;

        private readonly SystemRhythmClock clock = new SystemRhythmClock();
        private readonly List<JudgementEvent> judgements = new List<JudgementEvent>(16);
        private readonly HashSet<int> activePointers = new HashSet<int>();
        private struct PerformanceInput
        {
            public Hand Hand;
            public int PointerId;
            public double Realtime;
        }
        private readonly List<PerformanceInput> pendingPerformanceInputs = new List<PerformanceInput>(16);
        private const double TapGlowSeconds = 0.55;
        private const int MaximumTapTraces = 64;
        private struct TapTrace
        {
            public Hand Hand;
            public float Progress;
            public double InputRealtime;
        }
        private readonly List<TapTrace> tapTraces = new List<TapTrace>(MaximumTapTraces);
        private ListenRepeatTutorialFlow flow;
        private ClockCalibrator calibrator;
        private ClockCalibration calibration;
        private double readySinceRealtime;
        private double demonstrationStartDsp;
        private double countdownStartDsp;
        private double startCueDsp;
        private double performanceStartDsp;
        private double resultSinceRealtime;
        private double transitionSinceRealtime;
        private double feedbackUntilRealtime;
        private double leftFlashUntilRealtime;
        private double rightFlashUntilRealtime;
        private float brainVisualScale = ListenRepeatTutorialFlow.InitialBrainScale;
        private string feedback = "";
        private string warning = "";
        private bool inputSubscribed;

        private AudioSource leftPlayer;
        private AudioSource rightPlayer;
        private AudioSource[] demonstrationVoices;
        private AudioSource[] countdownVoices;
        private AudioSource startVoice;
        private AudioClip dongClip;
        private AudioClip takClip;
        private AudioClip countClip;
        private AudioClip startClip;
        private Texture2D discTexture;
        private Texture2D roundedTexture;
        private Texture2D paperTexture;
        private Texture2D tapRingTexture;
        private Texture2D tapGlowTexture;
        private Texture2D padHalfTexture;
        private GUIStyle labelStyle;
        private GUIStyle boldStyle;
        private GUIStyle buttonStyle;

        public TutorialPhase Phase => flow.Phase;
        public int PatternIndex => flow.PatternIndex;
        public float BrainScale => flow.BrainScale;
        public RunSnapshot LastResult => flow.Result;
        public bool HasRabbitArtwork => rabbitAtlas != null;

        public void ReplayTutorial()
        {
            if (flow.Phase != TutorialPhase.Transition || !flow.IsLastPattern) return;
            StopAllVoices();
            flow.Replay();
            ResetForPattern();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (rabbitAtlas == null) rabbitAtlas = Resources.Load<Texture2D>("lop_rabbit_atlas");
            if (rabbitAtlas == null)
            {
                warning = "토끼 원본을 불러오지 못했어요. 장면의 Rabbit Atlas 연결을 확인해 주세요.";
                Debug.LogError("Tutorial rabbit atlas unavailable: serialized reference and Resources/lop_rabbit_atlas are both missing.", this);
            }
            flow = new ListenRepeatTutorialFlow(passAccuracyBasisPoints);
            readySinceRealtime = clock.RealtimeSeconds;
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);

            leftPlayer = NewSource("Player dong", 0.55f, 0);
            rightPlayer = NewSource("Player tak", 0.55f, 0);
            demonstrationVoices = NewSources("Demonstration", 8, 0.55f);
            countdownVoices = NewSources("Count", 2, 0.22f);
            startVoice = NewSource("Start cue", 0.22f, 0);
            discTexture = ShapeTexture(true);
            roundedTexture = ShapeTexture(false);
            paperTexture = MakePaperTexture();
            tapRingTexture = MakeTapLightTexture(true);
            tapGlowTexture = MakeTapLightTexture(false);
            padHalfTexture = MakePadHalfTexture();
            try
            {
                dongClip = dongSample != null ? dongSample : MakePercussion("Dong", 145, 0.26f, false);
                takClip = takSample != null ? takSample : MakeWoodHit();
                countClip = MakePercussion("Count", 950, 0.09f, true);
                startClip = MakePercussion("Start", 680, 0.17f, true);
            }
            catch (Exception error)
            {
                warning = "소리를 준비하지 못했어요. 화면으로 계속 연주할 수 있어요.";
                Debug.LogWarning("Tutorial audio unavailable: " + error.Message);
            }
        }

        private void OnEnable()
        {
            if (!inputSubscribed)
            {
                InputSystem.onEvent += OnInputEvent;
                InputSystem.onAfterUpdate += FlushPerformanceInputs;
                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
                inputSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (inputSubscribed)
            {
                InputSystem.onEvent -= OnInputEvent;
                InputSystem.onAfterUpdate -= FlushPerformanceInputs;
                AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
                inputSubscribed = false;
            }
            StopAllVoices();
            activePointers.Clear();
            pendingPerformanceInputs.Clear();
            tapTraces.Clear();
        }

        private void Update()
        {
            for (var i = tapTraces.Count - 1; i >= 0; i--)
                if (clock.RealtimeSeconds - tapTraces[i].InputRealtime >= TapGlowSeconds) tapTraces.RemoveAt(i);
            brainVisualScale = Mathf.MoveTowards(brainVisualScale, flow.BrainScale, Time.unscaledDeltaTime * 0.7f);
            if (flow.Phase == TutorialPhase.PatternReady)
            {
                if (!calibration.Valid)
                {
                    calibrator.AddSample(clock);
                    calibration = calibrator.Build();
                }
                if (calibration.Valid && clock.RealtimeSeconds - readySinceRealtime >= ReadySeconds)
                    BeginDemonstration();
                return;
            }

            if (flow.Phase == TutorialPhase.Demonstration)
            {
                if (clock.DspSeconds >= demonstrationStartDsp + flow.PatternDurationUs / 1000000.0)
                    BeginCountdown();
                return;
            }

            if (flow.Phase == TutorialPhase.Countdown)
            {
                if (clock.DspSeconds >= performanceStartDsp - ContestRun.GoodWindowUs / 1000000.0)
                {
                    activePointers.Clear();
                    flow.BeginPerformance();
                }
                return;
            }

            if (flow.Phase == TutorialPhase.PlayerPerformance)
            {
                // Input events may arrive after a newer frame. Never move the judge to
                // wall-clock "now" before processing their original timestamps.
                FlushPerformanceInputs();
                var finishUs = flow.Chart.Stages[0].DurationUs;
                if (clock.DspSeconds >= performanceStartDsp + finishUs / 1000000.0)
                {
                    judgements.Clear();
                    flow.Finish(judgements);
                    CaptureJudgements();
                    StopAllVoices();
                    activePointers.Clear();
                    resultSinceRealtime = clock.RealtimeSeconds;
                }
                return;
            }

            if (flow.Phase == TutorialPhase.Result && clock.RealtimeSeconds - resultSinceRealtime >= ResultSeconds)
            {
                flow.BeginTransition();
                transitionSinceRealtime = clock.RealtimeSeconds;
                return;
            }

            if (flow.Phase == TutorialPhase.Transition && !flow.IsLastPattern
                && clock.RealtimeSeconds - transitionSinceRealtime >= TransitionSeconds)
            {
                StopAllVoices();
                flow.NextPattern();
                ResetForPattern();
            }

        }

        private void BeginDemonstration()
        {
            StopAllVoices();
            flow.BeginDemonstration();
            demonstrationStartDsp = clock.DspSeconds + ScheduledLeadSeconds;
            var notes = flow.Chart.Stages[0].Notes;
            for (var index = 0; index < notes.Count && index < demonstrationVoices.Length; index++)
            {
                var source = demonstrationVoices[index];
                source.clip = notes[index].Hand == Hand.Left ? dongClip : takClip;
                if (source.clip != null) source.PlayScheduled(demonstrationStartDsp + notes[index].TargetUs / 1000000.0);
            }
        }

        private void BeginCountdown()
        {
            StopAllVoices();
            flow.BeginCountdown();
            var beatSeconds = 60.0 / flow.Chart.Stages[0].Bpm;
            countdownStartDsp = clock.DspSeconds + ScheduledLeadSeconds;
            startCueDsp = countdownStartDsp + (CountdownBeats - 1) * beatSeconds;
            performanceStartDsp = countdownStartDsp + (CountdownBeats + PreparationRestBeats) * beatSeconds;
            for (var index = 0; index < countdownVoices.Length; index++)
            {
                countdownVoices[index].clip = countClip;
                if (countClip != null) countdownVoices[index].PlayScheduled(countdownStartDsp + index * beatSeconds);
            }
            startVoice.clip = startClip;
            if (startClip != null) startVoice.PlayScheduled(startCueDsp);
        }

        private void ResetForPattern()
        {
            activePointers.Clear();
            pendingPerformanceInputs.Clear();
            tapTraces.Clear();
            judgements.Clear();
            feedback = "";
            feedbackUntilRealtime = 0;
            leftFlashUntilRealtime = 0;
            rightFlashUntilRealtime = 0;
            readySinceRealtime = clock.RealtimeSeconds;
        }

        private void RetryAfterInterruption()
        {
            if (flow == null) return;
            StopAllVoices();
            flow.AbortAndRetryCurrentPattern();
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);
            ResetForPattern();
        }

        private void OnApplicationPause(bool paused) { if (paused) RetryAfterInterruption(); }
        private void OnApplicationFocus(bool focused) { if (!focused) RetryAfterInterruption(); }
        private void OnAudioConfigurationChanged(bool _) { RetryAfterInterruption(); }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.valid || (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())) return;
            if (device is Touchscreen touchscreen)
            {
                // Native Android events contain one TouchState, not the whole touchscreen.
                // Input System maps that event only to primaryTouch for ReadValueFromEvent.
                // This is an event-data adapter: every finger keeps its own touchId/time.
                ReadTouchEvent(touchscreen.primaryTouch, eventPtr);
                // Preserve support for full touchscreen state events (editor/synthetic input).
                foreach (var touch in touchscreen.touches) ReadTouchEvent(touch, eventPtr);
                return;
            }

            if (device is Mouse mouse)
            {
                if (WasPressed(mouse.leftButton, eventPtr) && TryHand(mouse.position.ReadValueFromEvent(eventPtr).x, out var hand))
                    Press(hand, 900, eventPtr.time);
                if (WasReleased(mouse.leftButton, eventPtr)) Release(900, eventPtr.time, false);
                return;
            }

            if (!(device is Keyboard keyboard)) return;
            KeyboardHand(keyboard.aKey, eventPtr, Hand.Left, 901);
            KeyboardHand(keyboard.fKey, eventPtr, Hand.Left, 903);
            KeyboardHand(keyboard.leftArrowKey, eventPtr, Hand.Left, 905);
            KeyboardHand(keyboard.lKey, eventPtr, Hand.Right, 902);
            KeyboardHand(keyboard.jKey, eventPtr, Hand.Right, 904);
            KeyboardHand(keyboard.rightArrowKey, eventPtr, Hand.Right, 906);
        }

        private void ReadTouchEvent(TouchControl touch, InputEventPtr eventPtr)
        {
            if (!touch.phase.ReadValueFromEvent(eventPtr, out var phase)) return;
            var id = touch.touchId.ReadValueFromEvent(eventPtr);
            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                var x = touch.position.ReadValueFromEvent(eventPtr).x;
                if (TryHand(x, out var hand)) Press(hand, id, eventPtr.time);
            }
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended) Release(id, eventPtr.time, false);
            else if (phase == UnityEngine.InputSystem.TouchPhase.Canceled) Release(id, eventPtr.time, true);
        }

        private void KeyboardHand(ButtonControl button, InputEventPtr eventPtr, Hand hand, int id)
        {
            if (WasPressed(button, eventPtr)) Press(hand, id, eventPtr.time);
            if (WasReleased(button, eventPtr)) Release(id, eventPtr.time, false);
        }

        private static bool WasPressed(ButtonControl button, InputEventPtr eventPtr)
            => !button.isPressed && button.ReadValueFromEvent(eventPtr, out var value) && value >= 0.5f;
        private static bool WasReleased(ButtonControl button, InputEventPtr eventPtr)
            => button.isPressed && button.ReadValueFromEvent(eventPtr, out var value) && value < 0.5f;

        private static bool TryHand(float x, out Hand hand)
        {
            var safe = Screen.safeArea;
            if (x >= safe.xMin && x <= safe.xMin + safe.width * 0.30f) { hand = Hand.Left; return true; }
            if (x >= safe.xMax - safe.width * 0.30f && x <= safe.xMax) { hand = Hand.Right; return true; }
            hand = default;
            return false;
        }

        private void Press(Hand hand, int pointerId, double inputRealtime)
        {
            if (!activePointers.Add(pointerId)) return;
            var source = hand == Hand.Left ? leftPlayer : rightPlayer;
            var clip = hand == Hand.Left ? dongClip : takClip;
            if (clip != null) source.PlayOneShot(clip);
            if (hand == Hand.Left) leftFlashUntilRealtime = clock.RealtimeSeconds + 0.28;
            else rightFlashUntilRealtime = clock.RealtimeSeconds + 0.28;
            if (flow.Phase != TutorialPhase.PlayerPerformance) return;
            // Record the actual event time, independently of target notes, judgement and user calibration.
            var rawInputUs = (calibration.ToDspSeconds(inputRealtime) - performanceStartDsp) * 1000000.0;
            if (tapTraces.Count == MaximumTapTraces) tapTraces.RemoveAt(0);
            tapTraces.Add(new TapTrace
            {
                Hand = hand,
                Progress = Mathf.Clamp01((float)(rawInputUs / Math.Max(1, flow.PatternDurationUs))),
                InputRealtime = inputRealtime
            });
            pendingPerformanceInputs.Add(new PerformanceInput { Hand = hand, PointerId = pointerId, Realtime = inputRealtime });
        }

        private void FlushPerformanceInputs()
        {
            if (flow == null || pendingPerformanceInputs.Count == 0) return;
            pendingPerformanceInputs.Sort((a, b) => a.Realtime.CompareTo(b.Realtime));
            if (flow.Phase == TutorialPhase.PlayerPerformance)
            {
                foreach (var input in pendingPerformanceInputs)
                {
                    judgements.Clear();
                    flow.Press(input.Hand, input.PointerId, ToPerformanceUs(input.Realtime), judgements);
                    CaptureJudgements();
                }
            }
            pendingPerformanceInputs.Clear();
        }

        private void Release(int pointerId, double inputRealtime, bool canceled)
        {
            // These tutorial patterns are taps. Releases manage physical pointers only;
            // they must not advance/invalidate the tap judge's input timeline.
            activePointers.Remove(pointerId);
        }

        private long ToPerformanceUs(double realtimeSeconds)
        {
            var us = (calibration.ToDspSeconds(realtimeSeconds) - performanceStartDsp - userCalibrationSeconds) * 1000000.0;
            return us >= 0 ? (long)Math.Floor(us + 0.5) : (long)Math.Ceiling(us - 0.5);
        }

        private void CaptureJudgements()
        {
            foreach (var entry in judgements)
            {
                if (entry.Kind == JudgementEventKind.UnmatchedRelease) continue;
                feedback = entry.Kind == JudgementEventKind.Note ? entry.Judgement.ToString().ToUpperInvariant() : "MISS";
                feedbackUntilRealtime = clock.RealtimeSeconds + 0.45;
            }
        }

        private AudioSource NewSource(string name, float volume, float pan)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            source.panStereo = pan;
            return source;
        }

        private AudioSource[] NewSources(string prefix, int count, float volume)
        {
            var sources = new AudioSource[count];
            for (var index = 0; index < count; index++) sources[index] = NewSource(prefix + " " + index, volume, 0);
            return sources;
        }

        private void StopAllVoices()
        {
            if (leftPlayer != null) leftPlayer.Stop();
            if (rightPlayer != null) rightPlayer.Stop();
            if (demonstrationVoices != null) foreach (var source in demonstrationVoices) if (source != null) source.Stop();
            if (countdownVoices != null) foreach (var source in countdownVoices) if (source != null) source.Stop();
            if (startVoice != null) startVoice.Stop();
        }

        private static AudioClip MakePercussion(string name, float frequency, float seconds, bool bright)
        {
            const int sampleRate = 44100;
            var samples = new float[Mathf.RoundToInt(sampleRate * seconds)];
            var phase = 0f;
            for (var index = 0; index < samples.Length; index++)
            {
                var t = index / (float)sampleRate;
                var pitch = frequency * (bright ? 1f - 0.28f * Mathf.Min(1, t * 18f) : 1f - 0.35f * Mathf.Min(1, t * 9f));
                phase += 2f * Mathf.PI * pitch / sampleRate;
                var body = Mathf.Sin(phase);
                var overtone = Mathf.Sin(phase * (bright ? 2.8f : 1.5f));
                var noise = Mathf.Sin(index * 2.39996f) * Mathf.Sin(index * 0.47839f);
                var decay = Mathf.Exp(-t * (bright ? 27f : 13f));
                var attack = Mathf.Clamp01(t / 0.0015f);
                samples[index] = attack * decay * (bright ? 0.46f * body + 0.22f * overtone + 0.22f * noise * Mathf.Exp(-t * 65f)
                    : 0.64f * body + 0.18f * overtone + 0.11f * noise * Mathf.Exp(-t * 60f));
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Could not create " + name);
            return clip;
        }

        private static AudioClip MakeWoodHit()
        {
            const int rate = 44100;
            var samples = new float[rate / 6];
            uint seed = 731;
            var filtered = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                seed = 1664525u * seed + 1013904223u;
                var noise = (seed >> 8) / 8388607.5f - 1f;
                filtered += 0.32f * (noise - filtered);
                var attack = Mathf.Min(1f, t / 0.0007f);
                var tail = Mathf.Clamp01((samples.Length - 1 - i) / (rate * 0.01f));
                samples[i] = attack * tail * (0.36f * Mathf.Sin(2 * Mathf.PI * 780 * t) * Mathf.Exp(-t * 72)
                    + 0.22f * Mathf.Sin(2 * Mathf.PI * 1270 * t) * Mathf.Exp(-t * 100)
                    + 0.25f * (noise - filtered) * Mathf.Exp(-t * 150));
            }
            var clip = AudioClip.Create("Tak wood placeholder", samples.Length, 1, rate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Could not create Tak");
            return clip;
        }

        private void OnGUI()
        {
            if (flow == null) return;
            EnsureStyles();
            var safe = SafeGuiRect();
            var unit = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            Fill(new Rect(0, 0, Screen.width, Screen.height), Cream);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), paperBackground != null ? paperBackground : paperTexture,
                ScaleMode.ScaleAndCrop);
            DrawPadTouchWash(safe, true, leftFlashUntilRealtime);
            DrawPadTouchWash(safe, false, rightFlashUntilRealtime);
            DrawText(new Rect(safe.x, safe.y + 8 * unit, safe.width, 46 * unit), "말랑브레인음악대", boldStyle, Ink, 24 * unit);
            DrawText(new Rect(safe.x, safe.y + 49 * unit, safe.width, 30 * unit), "리듬 " + (flow.PatternIndex + 1) + " / " + flow.PatternCount,
                labelStyle, Ink, 16 * unit);
            if (flow.SetCombo > 0)
                DrawText(new Rect(safe.x + 12 * unit, safe.y + 49 * unit, 180 * unit, 30 * unit),
                    flow.SetCombo + "세트 연속 성공", labelStyle, Ink, 14 * unit);
            if (flow.Phase == TutorialPhase.PlayerPerformance || flow.Phase == TutorialPhase.Result)
                DrawText(new Rect(safe.xMax - 185 * unit, safe.y + 16 * unit, 170 * unit, 28 * unit),
                    flow.CurrentSnapshot.RankScore + "점", boldStyle, Ink, 16 * unit);

            var leftPad = new Rect(safe.x + safe.width * 0.02f, safe.y + safe.height * 0.23f,
                safe.width * 0.27f, safe.height * 0.64f);
            var rightPad = new Rect(safe.xMax - safe.width * 0.29f, leftPad.y, leftPad.width, leftPad.height);
            DrawPad(leftPad, true, clock.RealtimeSeconds < leftFlashUntilRealtime || DemonstrationHitActive(Hand.Left), unit);
            DrawPad(rightPad, false, clock.RealtimeSeconds < rightFlashUntilRealtime || DemonstrationHitActive(Hand.Right), unit);

            var center = new Rect(safe.x + safe.width * 0.31f, safe.y + safe.height * 0.13f,
                safe.width * 0.38f, safe.height * 0.78f);
            DrawRabbit(center, unit);
            DrawScore(new Rect(center.x, center.y + center.height * 0.46f, center.width, center.height * 0.31f), unit);
            DrawState(new Rect(center.x, center.y + center.height * 0.79f, center.width, center.height * 0.20f), unit);

            if (!string.IsNullOrEmpty(warning))
                DrawText(new Rect(safe.x, safe.yMax - 25 * unit, safe.width, 20 * unit), warning, labelStyle, Ink, 12 * unit);
        }

        private bool DemonstrationHitActive(Hand hand)
        {
            if (flow.Phase != TutorialPhase.Demonstration) return false;
            var elapsedUs = (clock.DspSeconds - demonstrationStartDsp) * 1000000.0;
            foreach (var note in flow.Chart.Stages[0].Notes)
                if (note.Hand == hand && elapsedUs >= note.TargetUs && elapsedUs < note.TargetUs + 120000)
                    return true;
            return false;
        }

        private void DrawPadTouchWash(Rect safe, bool left, double flashUntil)
        {
            var remaining = flashUntil - clock.RealtimeSeconds;
            if (remaining <= 0) return;
            var amount = Mathf.Clamp01((float)(remaining / 0.28));
            var area = new Rect(left ? safe.x : safe.xMax - safe.width * 0.29f,
                safe.y + safe.height * 0.15f, safe.width * 0.29f, safe.height * 0.77f);
            GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.22f * amount);
            GUI.DrawTextureWithTexCoords(area, padHalfTexture, left ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1), true);
            GUI.color = Color.white;
        }

        private void DrawPad(Rect rect, bool left, bool active, float unit)
        {
            var size = Mathf.Min(rect.width * 0.87f, rect.height * 0.65f);
            var instrument = new Rect(rect.center.x - size / 2, rect.center.y - size * 0.4f, size, size * 0.8f);
            if (active)
            {
                instrument.y += 5 * unit;
                instrument.height *= 0.96f;
            }
            var art = left ? leftInstrument : rightInstrument;
            if (art != null)
            {
                DrawInstrumentArtwork(instrument, art, left ? leftInstrumentPixels : rightInstrumentPixels);
            }
            else if (left)
            {
                DrawRounded(new Rect(instrument.x + size * 0.04f, instrument.y + size * 0.25f,
                    size * 0.92f, size * 0.45f), Ink);
                DrawRounded(new Rect(instrument.x + size * 0.065f, instrument.y + size * 0.26f,
                    size * 0.87f, size * 0.40f), Blue);
                for (var i = 0; i < 4; i++)
                    Fill(new Rect(instrument.x + size * (0.16f + i * 0.22f), instrument.y + size * 0.32f,
                        3 * unit, size * 0.27f), Hex("F1D2A6"));
                DrawDisc(new Rect(instrument.x, instrument.y, size, size * 0.43f), Ink);
                DrawDisc(new Rect(instrument.x + 5 * unit, instrument.y + 5 * unit,
                    size - 10 * unit, size * 0.43f - 10 * unit), active ? Hex("F5D29A") : Hex("EBD7B4"));
                DrawDisc(new Rect(instrument.x + size * 0.14f, instrument.y + size * 0.065f,
                    size * 0.68f, size * 0.22f), Hex("F7E8CE"));
            }
            else
            {
                var wood = new Rect(instrument.x + size * 0.02f, instrument.y + size * 0.14f, size * 0.96f, size * 0.42f);
                DrawRounded(wood, Ink);
                wood = new Rect(wood.x + 5 * unit, wood.y + 5 * unit, wood.width - 10 * unit, wood.height - 10 * unit);
                DrawRounded(wood, active ? Hex("DDA879") : Hex("C79164"));
                for (var i = 0; i < 3; i++)
                    Fill(new Rect(wood.x + 12 * unit, wood.y + wood.height * (0.25f + i * 0.23f),
                        wood.width - 24 * unit, 2 * unit), Hex("AD764F"));
                var stick = new Rect(instrument.x + size * 0.23f, instrument.y + size * 0.69f, size * 0.61f, size * 0.075f);
                DrawRounded(stick, Ink);
                DrawRounded(new Rect(stick.x + 3 * unit, stick.y + 2 * unit, stick.width - 6 * unit, stick.height - 4 * unit), Hex("EBD7B4"));
            }
            if (flow.PatternIndex == 0 && flow.Phase != TutorialPhase.PlayerPerformance
                && flow.Phase != TutorialPhase.Result && flow.Phase != TutorialPhase.Transition)
                DrawText(new Rect(rect.x, instrument.yMax + 14 * unit, rect.width, 28 * unit),
                    left ? "왼손 · 낮은 북" : "오른손 · 나무 타격", labelStyle, Ink, 16 * unit);
#if UNITY_EDITOR
            DrawText(new Rect(rect.x, rect.yMax - 18 * unit, rect.width, 22 * unit), left ? "A / F" : "J / L",
                labelStyle, Ink, 12 * unit);
#endif
        }

        private static void DrawInstrumentArtwork(Rect area, Texture2D texture, Rect pixels)
        {
            // Trim only the draw UVs so generous transparent margins don't shrink the visible instrument.
            if (pixels.width <= 0 || pixels.height <= 0)
                pixels = new Rect(0, 0, texture.width, texture.height);
            var aspect = pixels.width / pixels.height;
            var width = Mathf.Min(area.width, area.height * aspect);
            var height = width / aspect;
            var fitted = new Rect(area.center.x - width / 2, area.center.y - height / 2, width, height);
            var uv = new Rect(pixels.x / texture.width, 1 - pixels.yMax / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(fitted, texture, uv, true);
        }

        private void DrawRabbit(Rect center, float unit)
        {
            var bob = flow.Phase == TutorialPhase.Demonstration ? Mathf.Sin((float)(clock.DspSeconds - demonstrationStartDsp) * 10f) * 2 * unit
                : flow.Phase == TutorialPhase.PatternReady ? Mathf.Sin((float)clock.RealtimeSeconds * 4f) * unit : 0;
            var rabbit = new Rect(center.center.x - 116 * unit, center.y - 9 * unit + bob, 232 * unit, 170 * unit);
            if (rabbitAtlas != null)
            {
                DrawAtlas(rabbit, new Rect(16f / 1774f, 1f - 483f / 887f, 655f / 1774f, 468f / 887f));
                var scale = Mathf.Clamp(brainVisualScale, ListenRepeatTutorialFlow.MinimumBrainScale,
                    ListenRepeatTutorialFlow.MaximumBrainScale);
                var brain = new Rect(rabbit.center.x - 41 * unit * scale, rabbit.y + 22 * unit,
                    82 * unit * scale, 48 * unit * scale);
                DrawAtlas(brain, new Rect(714f / 1774f, 1f - 363f / 887f, 355f / 1774f, 205f / 887f));
            }
            else
            {
                DrawText(rabbit, "토끼 이미지 연결 필요", labelStyle, Ink, 16 * unit);
            }
        }

        private void DrawScore(Rect rect, float unit)
        {
            DrawRounded(new Rect(rect.x - 2 * unit, rect.y - 2 * unit, rect.width + 4 * unit, rect.height + 4 * unit), Hex("D6BFA5"));
            DrawRounded(rect, Hex("FCF5E9"));
            var inset = 18 * unit;
            var x0 = rect.x + inset;
            var width = rect.width - inset * 2;
            var topY = rect.y + rect.height * 0.36f;
            var bottomY = rect.y + rect.height * 0.72f;
            Fill(new Rect(x0, topY, width, 2 * unit), Hex("B49D87"));
            Fill(new Rect(x0, bottomY, width, 2 * unit), Hex("B49D87"));
            var totalUs = Math.Max(1, flow.PatternDurationUs);
            foreach (var note in flow.Chart.Stages[0].Notes)
            {
                var x = x0 + width * note.TargetUs / totalUs;
                var y = note.Hand == Hand.Left ? topY : bottomY;
                var noteRect = new Rect(x - 10 * unit, y - 10 * unit, 20 * unit, 20 * unit);
                // Shape and row distinguish hands even without colour.
                if (note.Hand == Hand.Left) DrawDisc(noteRect, Ink);
                else DrawRounded(noteRect, Ink);
                noteRect = new Rect(x - 7 * unit, y - 7 * unit, 14 * unit, 14 * unit);
                if (note.Hand == Hand.Left) DrawDisc(noteRect, Blue);
                else DrawRounded(noteRect, Pink);
            }
            var progress = 0f;
            if (flow.Phase == TutorialPhase.Demonstration)
                progress = (float)((clock.DspSeconds - demonstrationStartDsp) * 1000000.0 / totalUs);
            else if (flow.Phase == TutorialPhase.PlayerPerformance)
                progress = (float)(ToPerformanceUs(clock.RealtimeSeconds) / (double)totalUs);
            if (flow.Phase == TutorialPhase.Demonstration || flow.Phase == TutorialPhase.PlayerPerformance)
                Fill(new Rect(x0 + width * Mathf.Clamp01(progress) - 2 * unit, rect.y + 9 * unit,
                    4 * unit, rect.height - 18 * unit), Yellow);
            DrawTapTraces(rect, x0, width, topY, bottomY, unit);
        }

        private void DrawTapTraces(Rect score, float x0, float width, float topY, float bottomY, float unit)
        {
            GUI.BeginGroup(score); // Clip the spreading light to the score, never over the rabbit or pads.
            foreach (var trace in tapTraces)
            {
                var age = Mathf.Clamp01((float)((clock.RealtimeSeconds - trace.InputRealtime) / TapGlowSeconds));
                if (age >= 1) continue;
                var eased = 1 - (1 - age) * (1 - age);
                var center = new Vector2(x0 - score.x + width * trace.Progress,
                    (trace.Hand == Hand.Left ? topY : bottomY) - score.y);
                var tint = trace.Hand == Hand.Left ? Hex("E5AC69") : Hex("EC6582");
                var radius = Mathf.Lerp(6, 25, eased) * unit;
                tint.a = 0.70f * (1 - age) * (1 - age);
                GUI.color = tint;
                GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), tapRingTexture);
                tint.a = 0.48f * (1 - age);
                GUI.color = tint;
                var glow = (12 + 10 * eased) * unit;
                GUI.DrawTexture(new Rect(center.x - glow, center.y - glow, glow * 2, glow * 2), tapGlowTexture);
                DrawDisc(new Rect(center.x - 3 * unit, center.y - 3 * unit, 6 * unit, 6 * unit),
                    new Color(1f, 0.98f, 0.90f, 1 - age));
            }
            GUI.color = Color.white;
            GUI.EndGroup();
        }

        private void DrawState(Rect rect, float unit)
        {
            string message;
            switch (flow.Phase)
            {
                case TutorialPhase.PatternReady:
                    message = calibration.Valid ? "토끼가 준비 중이에요" : "박자 맞추는 중…";
                    break;
                case TutorialPhase.Demonstration: message = "먼저 들어 봐!"; break;
                case TutorialPhase.Countdown:
                    message = clock.DspSeconds >= countdownStartDsp + CountdownBeats * (60.0 / flow.Chart.Stages[0].Bpm)
                        ? "한 박자 쉬고…" : clock.DspSeconds >= startCueDsp ? "Start!" : "준비!";
                    break;
                case TutorialPhase.PlayerPerformance:
                    message = clock.RealtimeSeconds < feedbackUntilRealtime ? feedback : "같이 연주해요!";
                    break;
                case TutorialPhase.Result:
                    message = (flow.Passed ? "성공!  " : "괜찮아!  ") + "정확도 "
                        + (flow.Result.AccuracyBasisPoints / 100f).ToString("0") + "%";
                    break;
                default: message = flow.IsLastPattern ? "10개 리듬을 마쳤어요!" : "다음 리듬!"; break;
            }
            var color = message == "PERFECT" ? Blue : message == "GOOD" ? Yellow : message == "MISS" ? Pink : Ink;
            DrawText(rect, message, boldStyle, color, 22 * unit);
            if (flow.Phase == TutorialPhase.Transition && flow.IsLastPattern)
            {
                var button = new Rect(rect.center.x - 100 * unit, rect.yMax - 14 * unit, 200 * unit, 38 * unit);
                DrawRounded(new Rect(button.x - 2 * unit, button.y - 2 * unit, button.width + 4 * unit, button.height + 4 * unit), Ink);
                DrawRounded(button, SoftPink);
                buttonStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(18 * unit));
                GUI.color = Color.white;
                if (GUI.Button(button, "다시 연주", buttonStyle)) ReplayTutorial();
            }
        }

        private void DrawAtlas(Rect rect, Rect uv)
        {
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, rabbitAtlas, uv, true);
        }

        private static Rect SafeGuiRect()
        {
            var area = Screen.safeArea;
            return new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { font = koreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            labelStyle.normal.textColor = Color.white;
            boldStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
            buttonStyle.normal.textColor = Ink;
            buttonStyle.hover.textColor = Ink;
            buttonStyle.active.textColor = Ink;
            buttonStyle.focused.textColor = Ink;
            buttonStyle.normal.background = null;
            buttonStyle.hover.background = null;
            buttonStyle.active.background = null;
            buttonStyle.focused.background = null;
        }

        private static void DrawText(Rect rect, string message, GUIStyle style, Color color, float size)
        {
            style.fontSize = Mathf.Max(10, Mathf.RoundToInt(size));
            GUI.color = color;
            GUI.Label(rect, message, style);
            GUI.color = Color.white;
        }

        private void DrawRounded(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, roundedTexture);
            GUI.color = Color.white;
        }

        private void DrawDisc(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, discTexture);
            GUI.color = Color.white;
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static Texture2D ShapeTexture(bool circle)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = circle ? x + 0.5f - size / 2f : Mathf.Max(0, 10 - Mathf.Min(x, size - 1 - x));
                var dy = circle ? y + 0.5f - size / 2f : Mathf.Max(0, 10 - Mathf.Min(y, size - 1 - y));
                var alpha = circle ? Mathf.Clamp01(size / 2f - Mathf.Sqrt(dx * dx + dy * dy))
                    : Mathf.Clamp01(10.5f - Mathf.Sqrt(dx * dx + dy * dy));
                colors[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(colors);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakePaperTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            uint seed = 149;
            for (var i = 0; i < pixels.Length; i++)
            {
                seed = 1664525u * seed + 1013904223u;
                var grain = ((seed >> 8) / 16777215f - 0.5f) * 0.028f;
                pixels[i] = new Color(Cream.r + grain, Cream.g + grain, Cream.b + grain);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakeTapLightTexture(bool ring)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var radius = new Vector2((x + 0.5f - size / 2f) / (size / 2f),
                    (y + 0.5f - size / 2f) / (size / 2f)).magnitude;
                var distance = ring ? (radius - 0.74f) / 0.075f : radius / 0.39f;
                var alpha = Mathf.Exp(-distance * distance * 0.5f) * Mathf.Clamp01((1 - radius) * 12);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakePadHalfTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var px = (x + 0.5f) / size;
                var py = (y + 0.5f) / size * 2 - 1;
                var radius = Mathf.Sqrt(px * px + py * py);
                pixels[y * size + x] = new Color(1, 1, 1,
                    Mathf.Clamp01((1 - radius) * 12) * (0.25f + 0.75f * (1 - px)));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color Hex(string code)
        {
            ColorUtility.TryParseHtmlString("#" + code, out var color);
            return color;
        }

        private void OnDestroy()
        {
            StopAllVoices();
            if (dongClip != null && dongClip != dongSample) Destroy(dongClip);
            if (takClip != null && takClip != takSample) Destroy(takClip);
            if (countClip != null) Destroy(countClip);
            if (startClip != null) Destroy(startClip);
            if (discTexture != null) Destroy(discTexture);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (paperTexture != null) Destroy(paperTexture);
            if (tapRingTexture != null) Destroy(tapRingTexture);
            if (tapGlowTexture != null) Destroy(tapGlowTexture);
            if (padHalfTexture != null) Destroy(padHalfTexture);
        }
    }
}
