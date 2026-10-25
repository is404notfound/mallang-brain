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
    // Gate 1 representative build: three rhythm stages against 19 disclosed synthetic trainee records.
    [RequireComponent(typeof(AudioSource))]
    public sealed class ContestPrototypeScreen : MonoBehaviour
    {
        private const int CountInBeats = 4;
        private const double StageResultSeconds = .85;
        private const double HitVisualSeconds = .09;

        private static readonly Color Cream = Hex("FFF9F0");
        private static readonly Color Ink = Hex("183A55");
        private static readonly Color Teal = Hex("7ABF7E");
        private static readonly Color Mint = Hex("9ED6F6");
        private static readonly Color Coral = Hex("FF8F8F");
        private static readonly Color Gold = Hex("F6C445");
        private static readonly Color Walnut = Hex("F3C68B");
        private static readonly Color Faded = Hex("A9B6BD");

        [Header("Gate 1 visual assets")]
        [SerializeField] private Texture2D stageBackground;
        [SerializeField] private Font koreanFont;
        [SerializeField] private double userCalibrationSeconds;

        // Play Mode tests replace only the storage edge; normal builds keep the persistent JSON Lines writer.
        [NonSerialized] public Action<string> runLogWriterOverride;

        private readonly List<JudgementEvent> judgementEvents = new List<JudgementEvent>(64);
        private readonly Dictionary<int, Hand> activePointerHands = new Dictionary<int, Hand>(8);
        private readonly SystemRhythmClock clock = new SystemRhythmClock();
        private readonly MiniContestFlow flow = new MiniContestFlow();

        private ClockCalibrator calibrator;
        private ClockCalibration calibration;
        private ContestChart chart;
        private ContestRun run;
        private MiniContestRoster roster;
        private Gate1LocalRunLog runLog;
        private RunSnapshot lastSnapshot;
        private string previousRunId;
        private string technicalMessage;
        private string rankError;
        private string feedback = "";
        private int playerRank;
        private int remainingParticipants = MiniContestRoster.ParticipantCount;
        private int stageResultFrom = MiniContestRoster.ParticipantCount;
        private int stageResultTo = MiniContestRoster.ParticipantCount;
        private double countInStartDsp;
        private double stageStartDsp;
        private double stageResultStartRealtime;
        private double resultShownRealtime;
        private double runStartRealtime;
        private double feedbackUntilRealtime;
        private double leftHitUntilRealtime;
        private double rightHitUntilRealtime;
        private bool rankAvailable;
        private bool inputSubscribed;

        private AudioSource leftSource;
        private AudioSource rightSource;
        private AudioSource effectSource;
        private AudioSource[] countInSources;
        private AudioClip leftMarimbaClip;
        private AudioClip rightMarimbaClip;
        private AudioClip countClip;
        private AudioClip countAccentClip;
        private AudioClip eliminationClip;
        private AudioClip successClip;
        private AudioClip failureClip;
        private Texture2D roundedTexture;
        private Texture2D circleTexture;
        private GUIStyle displayStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;

        public MiniContestPhase Phase => flow.Phase;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            chart = Gate1ChartFactory.CreateMiniContest();
            roster = MiniContestRoster.CreateDefault();
            runLog = new Gate1LocalRunLog(runLogWriterOverride);
            if (!roster.Validate(out rankError)) technicalMessage = "연습 기록 오류 · 순위 제외";

            leftSource = CreateAudioSource("Player marimba left", .42f, -.28f);
            rightSource = CreateAudioSource("Player marimba right", .42f, .28f);
            effectSource = CreateAudioSource("Contest effects", .18f, 0f);
            countInSources = new AudioSource[CountInBeats];
            for (var index = 0; index < countInSources.Length; index++)
                countInSources[index] = CreateAudioSource("Count in " + (index + 1), .10f, 0f);

            roundedTexture = CreateShapeTexture(false);
            circleTexture = CreateShapeTexture(true);
            CreateAudioClips();
            StartCalibration();
        }

        private void OnEnable()
        {
            if (inputSubscribed) return;
            InputSystem.onEvent += OnInputEvent;
            inputSubscribed = true;
        }

        private void OnDisable()
        {
            if (!inputSubscribed) return;
            InputSystem.onEvent -= OnInputEvent;
            inputSubscribed = false;
            activePointerHands.Clear();
        }

        private void Update()
        {
            if (flow.Phase == MiniContestPhase.Calibrating)
            {
                calibrator.AddSample(clock);
                calibration = calibrator.Build();
                if (calibration.Valid) flow.CompleteCalibration();
                return;
            }

            if (flow.Phase == MiniContestPhase.CountIn)
            {
                if (clock.DspSeconds >= stageStartDsp) flow.StartPlaying();
                return;
            }

            if (flow.Phase == MiniContestPhase.Playing)
            {
                UpdatePlayingStage();
                return;
            }

            if (flow.Phase == MiniContestPhase.StageResult
                && clock.RealtimeSeconds - stageResultStartRealtime >= StageResultSeconds)
            {
                remainingParticipants = stageResultTo;
                flow.CompleteStageResult();
                if (flow.Phase == MiniContestPhase.CountIn) BeginCountIn();
                else resultShownRealtime = clock.RealtimeSeconds;
            }
        }

        private void UpdatePlayingStage()
        {
            if (run == null || !run.IsActive) return;
            var stage = CurrentStage();
            var finalUs = stage.DurationUs + ContestRun.HoldLateGoodWindowUs + 1;
            judgementEvents.Clear();
            run.AdvanceTime(Math.Min(ToStageUs(clock.RealtimeSeconds), finalUs), judgementEvents);
            CaptureFeedback();
            if (clock.DspSeconds < stageStartDsp + stage.DurationUs / 1000000.0) return;

            judgementEvents.Clear();
            run.FinalizeCurrentStage(judgementEvents);
            CaptureFeedback();
            lastSnapshot = run.Snapshot();
            ResolveStage(lastSnapshot);
        }

        private void ResolveStage(RunSnapshot snapshot)
        {
            StopScheduledAudio();
            flow.ResolveStage(snapshot);

            if (snapshot.EndReason == RunEndReason.Failed || snapshot.EndReason == RunEndReason.Invalid)
            {
                if (failureClip != null) effectSource.PlayOneShot(failureClip, .7f);
                FinishRun(snapshot);
                resultShownRealtime = clock.RealtimeSeconds;
                return;
            }

            stageResultFrom = remainingParticipants;
            stageResultTo = MiniContestRoster.RemainingParticipantsAfterStage(snapshot.LastCompletedStage);
            stageResultStartRealtime = clock.RealtimeSeconds;
            var effect = snapshot.EndReason == RunEndReason.Completed ? successClip : eliminationClip;
            if (effect != null) effectSource.PlayOneShot(effect, .75f);
            if (!runLog.RecordCrowdDrop(snapshot.LastCompletedStage, stageResultTo))
                technicalMessage = "기록 저장 실패 · 플레이는 계속됩니다";
            if (snapshot.EndReason == RunEndReason.Completed) FinishRun(snapshot);
        }

        private void FinishRun(RunSnapshot snapshot)
        {
            rankAvailable = roster.TryGetPlayerRank(snapshot, out playerRank, out rankError);
            if (!rankAvailable) technicalMessage = "연습 기록 오류 · 순위 제외";
            if (!runLog.EndRun(snapshot, clock.RealtimeSeconds - runStartRealtime))
                technicalMessage = "기록 저장 실패 · 결과는 유지됩니다";
            previousRunId = runLog.CurrentRunId;
        }

        private void BeginRun(bool isRetry)
        {
            if (flow.Phase != MiniContestPhase.Ready && flow.Phase != MiniContestPhase.Result) return;
            if (!ChartValidator.Validate(chart).IsValid)
            {
                technicalMessage = "차트 오류 · 시작할 수 없습니다";
                return;
            }

            if (isRetry) runLog.RecordResultAction("retry");
            flow.StartRun();
            run = new ContestRun(chart);
            lastSnapshot = run.Snapshot();
            activePointerHands.Clear();
            remainingParticipants = MiniContestRoster.ParticipantCount;
            stageResultFrom = remainingParticipants;
            stageResultTo = remainingParticipants;
            rankAvailable = false;
            playerRank = 0;
            rankError = null;
            feedback = "";
            technicalMessage = roster.Validate(out rankError) ? null : "연습 기록 오류 · 순위 제외";
            runStartRealtime = clock.RealtimeSeconds;
            if (!runLog.StartRun(chart, isRetry ? previousRunId : null))
                technicalMessage = "기록 저장 실패 · 플레이는 계속됩니다";
            BeginCountIn();
        }

        private void BeginCountIn()
        {
            StopScheduledAudio();
            var stage = CurrentStage();
            var beatSeconds = 60.0 / stage.Bpm;
            countInStartDsp = clock.DspSeconds + .15;
            stageStartDsp = countInStartDsp + CountInBeats * beatSeconds;
            for (var index = 0; index < CountInBeats; index++)
            {
                var source = countInSources[index];
                source.clip = index == 0 ? countAccentClip : countClip;
                source.volume = index == 0 ? .14f : .09f;
                if (source.clip != null) source.PlayScheduled(countInStartDsp + index * beatSeconds);
            }
        }

        private void StartCalibration()
        {
            if (run != null && run.IsActive && flow.Phase != MiniContestPhase.Calibrating && flow.Phase != MiniContestPhase.Ready)
            {
                var snapshot = run.Snapshot();
                var invalid = new RunSnapshot(
                    RunEndReason.Invalid,
                    snapshot.LastCompletedStage,
                    run.CurrentStageNumber,
                    snapshot.BaseScore,
                    snapshot.AccuracyScore,
                    snapshot.RankScore,
                    snapshot.AccuracyBasisPoints,
                    snapshot.Combo,
                    snapshot.GhostMissCount);
                if (!runLog.EndRun(invalid, clock.RealtimeSeconds - runStartRealtime))
                    technicalMessage = "기록 저장 실패 · 플레이는 계속됩니다";
                previousRunId = runLog.CurrentRunId;
            }
            StopScheduledAudio();
            activePointerHands.Clear();
            run = null;
            calibrator = new ClockCalibrator();
            calibration = ClockCalibration.Invalid("not-started", 0);
            flow.BeginCalibration();
            feedback = "";
        }

        private void OnApplicationPause(bool paused) { if (paused) StartCalibration(); }
        private void OnApplicationFocus(bool focused) { if (!focused) StartCalibration(); }

        // Raw event timestamps avoid adding a frame of polling uncertainty to rhythm judgement.
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.valid) return;
            if (flow.Phase != MiniContestPhase.Ready && flow.Phase != MiniContestPhase.Playing) return;

            if (device is Touchscreen touchscreen)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.phase.ReadValueFromEvent(eventPtr, out var touchPhase)) continue;
                    var pointerId = touch.touchId.ReadValueFromEvent(eventPtr);
                    if (touchPhase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        var x = touch.position.ReadValueFromEvent(eventPtr).x;
                        HandlePress(x < Screen.width * .5f ? Hand.Left : Hand.Right, pointerId, eventPtr.time);
                    }
                    else if (touchPhase == UnityEngine.InputSystem.TouchPhase.Ended) HandleRelease(pointerId, eventPtr.time);
                    else if (touchPhase == UnityEngine.InputSystem.TouchPhase.Canceled) HandleCancel(pointerId, eventPtr.time);
                }
                return;
            }

            if (device is Mouse mouse)
            {
                if (WasPressedThisEvent(mouse.leftButton, eventPtr))
                {
                    var x = mouse.position.ReadValueFromEvent(eventPtr).x;
                    HandlePress(x < Screen.width * .5f ? Hand.Left : Hand.Right, 900, eventPtr.time);
                }
                if (WasReleasedThisEvent(mouse.leftButton, eventPtr)) HandleRelease(900, eventPtr.time);
                return;
            }

            if (!(device is Keyboard keyboard)) return;
            HandleKeyboardButton(keyboard.aKey, eventPtr, Hand.Left, 901);
            HandleKeyboardButton(keyboard.fKey, eventPtr, Hand.Left, 903);
            HandleKeyboardButton(keyboard.leftArrowKey, eventPtr, Hand.Left, 905);
            HandleKeyboardButton(keyboard.lKey, eventPtr, Hand.Right, 902);
            HandleKeyboardButton(keyboard.jKey, eventPtr, Hand.Right, 904);
            HandleKeyboardButton(keyboard.rightArrowKey, eventPtr, Hand.Right, 906);
        }

        private void HandleKeyboardButton(ButtonControl control, InputEventPtr eventPtr, Hand hand, int pointerId)
        {
            if (WasPressedThisEvent(control, eventPtr)) HandlePress(hand, pointerId, eventPtr.time);
            if (WasReleasedThisEvent(control, eventPtr)) HandleRelease(pointerId, eventPtr.time);
        }

        private static bool WasPressedThisEvent(ButtonControl control, InputEventPtr eventPtr)
            => !control.isPressed && control.ReadValueFromEvent(eventPtr, out var value) && value >= .5f;

        private static bool WasReleasedThisEvent(ButtonControl control, InputEventPtr eventPtr)
            => control.isPressed && control.ReadValueFromEvent(eventPtr, out var value) && value < .5f;

        private void HandlePress(Hand hand, int pointerId, double realtimeSeconds)
        {
            if (flow.Phase == MiniContestPhase.Ready)
            {
                BeginRun(false);
                return;
            }
            if (flow.Phase != MiniContestPhase.Playing || run == null) return;

            activePointerHands[pointerId] = hand;
            judgementEvents.Clear();
            run.ProcessPress(hand, pointerId, ToStageUs(realtimeSeconds), judgementEvents);
            CaptureFeedback();
            PlayMarimba(hand);
            if (hand == Hand.Left) leftHitUntilRealtime = clock.RealtimeSeconds + HitVisualSeconds;
            else rightHitUntilRealtime = clock.RealtimeSeconds + HitVisualSeconds;
        }

        private void HandleRelease(int pointerId, double realtimeSeconds)
        {
            activePointerHands.Remove(pointerId);
            if (flow.Phase != MiniContestPhase.Playing || run == null) return;
            judgementEvents.Clear();
            run.ProcessRelease(pointerId, ToStageUs(realtimeSeconds), judgementEvents);
            CaptureFeedback();
        }

        private void HandleCancel(int pointerId, double realtimeSeconds)
        {
            activePointerHands.Remove(pointerId);
            if (flow.Phase != MiniContestPhase.Playing || run == null) return;
            judgementEvents.Clear();
            run.ProcessCancel(pointerId, ToStageUs(realtimeSeconds), judgementEvents);
            CaptureFeedback();
        }

        private void CaptureFeedback()
        {
            runLog.CountJudgements(judgementEvents);
            string nextFeedback = null;
            foreach (var entry in judgementEvents)
            {
                if (entry.Kind == JudgementEventKind.UnmatchedRelease) continue;
                if (entry.Kind == JudgementEventKind.Guidance) nextFeedback = "반대 손!";
                else if (entry.Kind == JudgementEventKind.GhostMiss) nextFeedback = "MISS";
                else if (entry.Kind == JudgementEventKind.RunInvalid) nextFeedback = "입력 오류";
                else if (entry.Kind == JudgementEventKind.Note) nextFeedback = entry.Judgement.ToString().ToUpperInvariant();
            }
            if (nextFeedback == null) return;
            feedback = nextFeedback;
            feedbackUntilRealtime = clock.RealtimeSeconds + .55;
        }

        private StageDefinition CurrentStage()
        {
            var stageNumber = run != null && run.IsActive ? run.CurrentStageNumber : Math.Max(1, lastSnapshot.LastCompletedStage);
            return chart.Stages[Mathf.Clamp(stageNumber - 1, 0, chart.Stages.Count - 1)];
        }

        private long ToStageUs(double realtimeSeconds)
        {
            var microseconds = (calibration.ToDspSeconds(realtimeSeconds) - stageStartDsp - userCalibrationSeconds) * 1000000.0;
            return microseconds >= 0 ? (long)Math.Floor(microseconds + .5) : (long)Math.Ceiling(microseconds - .5);
        }

        // ---------- Audio ----------

        private void CreateAudioClips()
        {
            try
            {
                leftMarimbaClip = CreateMarimbaTone("Marimba left", 220f);
                rightMarimbaClip = CreateMarimbaTone("Marimba right", 329.63f);
                countClip = CreatePercussionTone("Count", 1150f, .07f);
                countAccentClip = CreatePercussionTone("Count accent", 1550f, .09f);
                eliminationClip = CreateMarimbaTone("Trainee drop", 164.81f, .34f);
                successClip = CreateMarimbaTone("Contest clear", 523.25f, .55f);
                failureClip = CreateMarimbaTone("Contest fail", 130.81f, .38f);
            }
            catch (Exception error)
            {
                technicalMessage = "오디오 오류 · 시각 플레이만 가능합니다";
                Debug.LogWarning("Gate 1 audio unavailable; visual play continues. " + error.Message);
            }
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

        private void PlayMarimba(Hand hand)
        {
            var source = hand == Hand.Left ? leftSource : rightSource;
            var clip = hand == Hand.Left ? leftMarimbaClip : rightMarimbaClip;
            if (source != null && clip != null) source.PlayOneShot(clip);
        }

        private void StopScheduledAudio()
        {
            if (countInSources == null) return;
            foreach (var source in countInSources) if (source != null) source.Stop();
        }

        private static AudioClip CreateMarimbaTone(string clipName, float frequency, float seconds = .62f)
        {
            const int sampleRate = 44100;
            var length = Mathf.Max(512, Mathf.RoundToInt(sampleRate * seconds));
            var samples = new float[length];
            for (var index = 0; index < length; index++)
            {
                var time = index / (float)sampleRate;
                var attack = Mathf.Clamp01(time / .0025f);
                var fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time) * Mathf.Exp(-time * 5.2f);
                var woodenPartial = .28f * Mathf.Sin(2f * Mathf.PI * frequency * 3.99f * time) * Mathf.Exp(-time * 13f);
                var brightPartial = .10f * Mathf.Sin(2f * Mathf.PI * frequency * 9.15f * time) * Mathf.Exp(-time * 24f);
                var malletClick = .08f * Mathf.Sin(2f * Mathf.PI * 2100f * time) * Mathf.Exp(-time * 90f);
                samples[index] = .62f * attack * (fundamental + woodenPartial + brightPartial + malletClick);
            }
            return CreateClip(clipName, samples, sampleRate);
        }

        private static AudioClip CreatePercussionTone(string clipName, float frequency, float seconds)
        {
            const int sampleRate = 44100;
            var length = Mathf.Max(256, Mathf.RoundToInt(sampleRate * seconds));
            var samples = new float[length];
            for (var index = 0; index < length; index++)
            {
                var time = index / (float)sampleRate;
                samples[index] = .45f * Mathf.Sin(2f * Mathf.PI * frequency * time) * Mathf.Exp(-time * 42f);
            }
            return CreateClip(clipName, samples, sampleRate);
        }

        private static AudioClip CreateClip(string clipName, float[] samples, int sampleRate)
        {
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
            if (flow.Phase == MiniContestPhase.Calibrating) DrawCalibrating(safe);
            else if (flow.Phase == MiniContestPhase.Ready) DrawReady(safe);
            else DrawContest(safe);
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
            var panel = CenterRect(safe, Mathf.Min(520 * unit, safe.width * .82f), 184 * unit, 0);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .96f), 3 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 22 * unit, panel.width - 40 * unit, 44 * unit), "박자 맞추는 중…", headingStyle, Ink, TextAnchor.MiddleCenter, 26 * unit);
            DrawText(new Rect(panel.x + 24 * unit, panel.y + 72 * unit, panel.width - 48 * unit, 34 * unit), "입력과 소리의 시계를 맞추고 있어요", bodyStyle, Ink, TextAnchor.MiddleCenter, 17 * unit);
            DrawProgress(new Rect(panel.x + 50 * unit, panel.y + 124 * unit, panel.width - 100 * unit, 10 * unit), calibrator.SampleCount / (float)calibrator.RequiredSamples, Mint);
            if (calibration.InvalidReason != null && calibration.InvalidReason != "not-started" && calibration.InvalidReason != "insufficient-samples")
                DrawText(new Rect(panel.x + 20 * unit, panel.y + 142 * unit, panel.width - 40 * unit, 28 * unit), "조율 오류 · 화면을 다시 열어 주세요", smallStyle, Coral, TextAnchor.MiddleCenter, 13 * unit);
        }

        private void DrawReady(Rect safe)
        {
            var unit = Unit(safe);
            var panelWidth = Mathf.Min(600 * unit, safe.width * .84f);
            var panel = CenterRect(safe, panelWidth, 260 * unit, -.02f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .97f), 3 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 18 * unit, panel.width - 40 * unit, 52 * unit), "미니 브레인 콘테스트", displayStyle, Ink, TextAnchor.MiddleCenter, 32 * unit);
            DrawText(new Rect(panel.x + 28 * unit, panel.y + 72 * unit, panel.width - 56 * unit, 72 * unit), "3개 스테이지를 연주하고\n20명 중 내 순위를 확인하세요", bodyStyle, Ink, TextAnchor.MiddleCenter, 19 * unit);
            DrawText(new Rect(panel.x + 28 * unit, panel.y + 146 * unit, panel.width - 56 * unit, 26 * unit), "연습생 기록 19명 포함 · 실제 실시간 대전이 아닙니다", smallStyle, Ink, TextAnchor.MiddleCenter, 13 * unit);
            var button = new Rect(panel.center.x - 146 * unit, panel.yMax - 66 * unit, 292 * unit, 50 * unit);
            GUI.color = Teal;
            if (GUI.Button(button, "콘테스트 참가", buttonStyle)) BeginRun(false);
            GUI.color = Color.white;
            DrawTechnicalMessage(safe, unit);
        }

        private void DrawContest(Rect safe)
        {
            var unit = Unit(safe);
            DrawHeader(safe, unit);
            DrawCrowd(safe, unit);
            if (flow.Phase == MiniContestPhase.CountIn || flow.Phase == MiniContestPhase.Playing)
            {
                DrawStageLane(safe, unit);
                DrawInputPads(safe, unit);
                if (flow.Phase == MiniContestPhase.CountIn) DrawCountIn(safe, unit);
                else if (clock.RealtimeSeconds < feedbackUntilRealtime) DrawFeedback(safe, unit);
            }
            else if (flow.Phase == MiniContestPhase.StageResult) DrawStageResult(safe, unit);
            else if (flow.Phase == MiniContestPhase.Result) DrawResult(safe, unit);
            DrawTechnicalMessage(safe, unit);
        }

        private void DrawHeader(Rect safe, float unit)
        {
            var stageNumber = flow.Phase == MiniContestPhase.StageResult
                ? Math.Max(1, lastSnapshot.LastCompletedStage)
                : run != null && run.IsActive
                    ? run.CurrentStageNumber
                    : Math.Max(1, lastSnapshot.LastCompletedStage);
            var snapshot = run == null ? lastSnapshot : run.Snapshot();
            var badgeWidth = 150 * unit;
            var left = new Rect(safe.x + 16 * unit, safe.y + 10 * unit, badgeWidth, 42 * unit);
            var center = new Rect(safe.center.x - badgeWidth / 2, safe.y + 10 * unit, badgeWidth, 42 * unit);
            var right = new Rect(safe.xMax - 16 * unit - badgeWidth, safe.y + 10 * unit, badgeWidth, 42 * unit);
            DrawOutlinedPanel(left, new Color(Cream.r, Cream.g, Cream.b, .95f), 2 * unit);
            DrawOutlinedPanel(center, new Color(Cream.r, Cream.g, Cream.b, .95f), 2 * unit);
            DrawOutlinedPanel(right, new Color(Cream.r, Cream.g, Cream.b, .95f), 2 * unit);
            DrawText(left, "STAGE " + stageNumber + "/3", headingStyle, Ink, TextAnchor.MiddleCenter, 17 * unit);
            DrawText(center, "남은 인원 " + DisplayedRemaining(), headingStyle, Ink, TextAnchor.MiddleCenter, 17 * unit);
            DrawText(right, snapshot.RankScore.ToString("N0") + "점", headingStyle, Ink, TextAnchor.MiddleCenter, 17 * unit);
            DrawText(new Rect(safe.center.x - 180 * unit, safe.y + 54 * unit, 360 * unit, 20 * unit), "연습생 기록 19명 포함", smallStyle, Ink, TextAnchor.MiddleCenter, 12 * unit);
        }

        private void DrawCrowd(Rect safe, float unit)
        {
            var panel = new Rect(safe.x + safe.width * .08f, safe.y + 78 * unit, safe.width * .84f, safe.height * .27f);
            DrawPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .88f));
            var columns = safe.width / safe.height >= 1.5f ? 10 : 5;
            var rows = MiniContestRoster.ParticipantCount / columns;
            var cellWidth = panel.width / columns;
            var cellHeight = panel.height / rows;
            var baseSize = Mathf.Min(cellWidth, cellHeight) * .56f;
            var progress = StageResultProgress();
            for (var index = 0; index < MiniContestRoster.ParticipantCount; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var center = new Vector2(panel.x + cellWidth * (column + .5f), panel.y + cellHeight * (row + .5f));
                var scale = CrowdSlotScale(index, progress);
                var size = baseSize * scale;
                var rect = new Rect(center.x - size / 2, center.y - size / 2, size, size);
                var playerFailed = index == 0 && flow.Phase == MiniContestPhase.Result && lastSnapshot.EndReason == RunEndReason.Failed;
                if (playerFailed)
                {
                    var settle = Mathf.Clamp01((float)((clock.RealtimeSeconds - resultShownRealtime) / .35));
                    rect = ScaleAroundCenter(rect, 1f + .12f * (1f - settle), .72f + .28f * settle);
                    rect.y += 7 * unit * (1f - settle);
                }
                var alive = index < DisplayedRemaining() && !playerFailed;
                var color = index == 0 ? Gold : alive ? Teal : new Color(Faded.r, Faded.g, Faded.b, .22f);
                if (size > 2) DrawDisc(rect, color);
                if (index == 0 && size > 16)
                {
                    var labelRect = playerFailed ? new Rect(rect.x, rect.y + rect.height * .23f, rect.width, rect.height * .54f) : rect;
                    DrawText(labelRect, "나", headingStyle, Ink, TextAnchor.MiddleCenter, Mathf.Max(10, 14 * unit * scale));
                }
            }
        }

        private float CrowdSlotScale(int index, float stageProgress)
        {
            if (flow.Phase != MiniContestPhase.StageResult || index < stageResultTo || index >= stageResultFrom)
                return index < remainingParticipants ? 1f : .72f;
            var eliminatedCount = Math.Max(1, stageResultFrom - stageResultTo);
            var order = (index - stageResultTo) / (float)eliminatedCount;
            var t = Mathf.Clamp01(stageProgress * 1.5f - order * .5f);
            return Mathf.Lerp(1f, .1f, t * t * (3f - 2f * t));
        }

        private int DisplayedRemaining()
        {
            if (flow.Phase != MiniContestPhase.StageResult) return remainingParticipants;
            return Mathf.RoundToInt(Mathf.Lerp(stageResultFrom, stageResultTo, StageResultProgress()));
        }

        private float StageResultProgress()
            => flow.Phase == MiniContestPhase.StageResult
                ? Mathf.Clamp01((float)((clock.RealtimeSeconds - stageResultStartRealtime) / StageResultSeconds))
                : 0f;

        private void DrawStageLane(Rect safe, float unit)
        {
            var stage = CurrentStage();
            var panel = new Rect(safe.x + safe.width * .14f, safe.y + safe.height * .43f, safe.width * .72f, safe.height * .15f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .96f), 2 * unit);
            var x0 = panel.x + 28 * unit;
            var width = panel.width - 56 * unit;
            var leftY = panel.y + panel.height * .34f;
            var rightY = panel.y + panel.height * .72f;
            Fill(new Rect(x0, leftY, width, 2), new Color(Ink.r, Ink.g, Ink.b, .14f));
            Fill(new Rect(x0, rightY, width, 2), new Color(Ink.r, Ink.g, Ink.b, .14f));
            var playableUs = Math.Max(1, stage.DurationUs - ChartValidator.StageBoundaryExclusionUs);
            foreach (var note in stage.Notes)
            {
                var x = x0 + width * Mathf.Clamp01(note.TargetUs / (float)playableUs);
                var y = note.Hand == Hand.Left ? leftY : rightY;
                var color = note.Hand == Hand.Left ? Mint : Coral;
                if (note.Kind == NoteKind.Hold)
                {
                    var endX = x0 + width * Mathf.Clamp01(note.EndUs / (float)playableUs);
                    DrawPanel(new Rect(x, y - 5 * unit, Mathf.Max(8 * unit, endX - x), 10 * unit), color);
                }
                DrawDisc(new Rect(x - 8 * unit, y - 8 * unit, 16 * unit, 16 * unit), color);
            }
            if (flow.Phase == MiniContestPhase.Playing)
            {
                var now = Mathf.Clamp01(ToStageUs(clock.RealtimeSeconds) / (float)playableUs);
                var markerX = x0 + width * now;
                Fill(new Rect(markerX - 1 * unit, panel.y + 8 * unit, 2 * unit, panel.height - 16 * unit), Gold);
            }
        }

        private void DrawInputPads(Rect safe, float unit)
        {
            var gap = 16 * unit;
            var width = (safe.width - gap * 3) / 2;
            var height = safe.height * .18f;
            var y = safe.yMax - height - 18 * unit;
            var leftActive = IsHandHeld(Hand.Left) || clock.RealtimeSeconds < leftHitUntilRealtime;
            var rightActive = IsHandHeld(Hand.Right) || clock.RealtimeSeconds < rightHitUntilRealtime;
            DrawInputPad(new Rect(safe.x + gap, y, width, height), Hand.Left, Mint, leftActive, unit);
            DrawInputPad(new Rect(safe.x + gap * 2 + width, y, width, height), Hand.Right, Coral, rightActive, unit);
        }

        private bool IsHandHeld(Hand hand)
        {
            foreach (var activeHand in activePointerHands.Values)
                if (activeHand == hand) return true;
            return false;
        }

        private void DrawInputPad(Rect rect, Hand hand, Color color, bool hit, float unit)
        {
            var scaled = ScaleAroundCenter(rect, hit ? .92f : 1f, hit ? .86f : 1f);
            DrawOutlinedPanel(scaled, new Color(color.r, color.g, color.b, hit ? .98f : .86f), 3 * unit);
            DrawText(scaled, hand == Hand.Left ? "왼손  L" : "오른손  R", headingStyle, Ink, TextAnchor.MiddleCenter, 22 * unit);
        }

        private void DrawCountIn(Rect safe, float unit)
        {
            var beatSeconds = 60.0 / CurrentStage().Bpm;
            var elapsed = Math.Max(0, clock.DspSeconds - countInStartDsp);
            var count = Mathf.Clamp(4 - Mathf.FloorToInt((float)(elapsed / beatSeconds)), 1, 4);
            var rect = new Rect(safe.center.x - 43 * unit, safe.y + safe.height * .57f, 86 * unit, 86 * unit);
            DrawDisc(rect, Gold);
            DrawText(rect, count.ToString(), displayStyle, Ink, TextAnchor.MiddleCenter, 38 * unit);
        }

        private void DrawFeedback(Rect safe, float unit)
        {
            var remaining = Mathf.Clamp01((float)((feedbackUntilRealtime - clock.RealtimeSeconds) / .55));
            var pop = 1f + .12f * Mathf.Sin((1f - remaining) * Mathf.PI);
            var color = feedback == "PERFECT" ? Mint : feedback == "GOOD" ? Gold : feedback == "MISS" ? Coral : Cream;
            DrawText(new Rect(safe.center.x - 210 * unit, safe.y + safe.height * .57f, 420 * unit, 52 * unit), feedback, displayStyle, color, TextAnchor.MiddleCenter, 32 * unit * pop);
        }

        private void DrawStageResult(Rect safe, float unit)
        {
            var panel = CenterRect(safe, Mathf.Min(500 * unit, safe.width * .78f), 156 * unit, .16f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .97f), 3 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 18 * unit, panel.width - 40 * unit, 48 * unit), "STAGE " + lastSnapshot.LastCompletedStage + " 통과!", displayStyle, Teal, TextAnchor.MiddleCenter, 30 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 76 * unit, panel.width - 40 * unit, 42 * unit), stageResultFrom + "명  →  " + stageResultTo + "명", headingStyle, Ink, TextAnchor.MiddleCenter, 24 * unit);
        }

        private void DrawResult(Rect safe, float unit)
        {
            var panelWidth = Mathf.Min(610 * unit, safe.width * .88f);
            var panel = CenterRect(safe, panelWidth, 292 * unit, .12f);
            DrawOutlinedPanel(panel, new Color(Cream.r, Cream.g, Cream.b, .98f), 3 * unit);
            var completed = lastSnapshot.EndReason == RunEndReason.Completed;
            var headline = completed ? "3스테이지 완주!" : "STAGE " + lastSnapshot.FailStage + "에서 탈락";
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 16 * unit, panel.width - 40 * unit, 46 * unit), headline, displayStyle, completed ? Teal : Coral, TextAnchor.MiddleCenter, 29 * unit);
            var rankText = rankAvailable ? "20명 중 " + playerRank + "위" : "연습 기록 오류 · 순위 제외";
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 66 * unit, panel.width - 40 * unit, 44 * unit), rankText, displayStyle, Ink, TextAnchor.MiddleCenter, 27 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 112 * unit, panel.width - 40 * unit, 32 * unit), "정확도 " + (lastSnapshot.AccuracyBasisPoints / 100f).ToString("0.0") + "%  ·  " + lastSnapshot.RankScore.ToString("N0") + "점", headingStyle, Ink, TextAnchor.MiddleCenter, 18 * unit);
            DrawText(new Rect(panel.x + 20 * unit, panel.y + 150 * unit, panel.width - 40 * unit, 24 * unit), "연습생 기록 19명 포함 · 실제 실시간 대전이 아닙니다", smallStyle, Ink, TextAnchor.MiddleCenter, 13 * unit);
            var button = new Rect(panel.center.x - 140 * unit, panel.yMax - 72 * unit, 280 * unit, 50 * unit);
            GUI.color = Teal;
            if (GUI.Button(button, "다시 참가", buttonStyle)) BeginRun(true);
            GUI.color = Color.white;
        }

        private void DrawTechnicalMessage(Rect safe, float unit)
        {
            if (string.IsNullOrEmpty(technicalMessage)) return;
            DrawText(new Rect(safe.x + 16 * unit, safe.yMax - 22 * unit, safe.width - 32 * unit, 18 * unit), technicalMessage, smallStyle, Coral, TextAnchor.MiddleCenter, 12 * unit);
        }

        private static Rect GuiSafeArea()
        {
            var area = Screen.safeArea;
            return new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
        }

        private static float Unit(Rect safe) => Mathf.Clamp(Mathf.Min(safe.width / 1280f, safe.height / 720f), .68f, 2.5f);
        private static Rect CenterRect(Rect safe, float width, float height, float yOffsetRatio) => new Rect(safe.center.x - width / 2, safe.center.y - height / 2 + safe.height * yOffsetRatio, width, height);

        private static Rect ScaleAroundCenter(Rect rect, float xScale, float yScale)
        {
            var width = rect.width * xScale;
            var height = rect.height * yScale;
            return new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height);
        }

        private void EnsureStyles()
        {
            if (displayStyle != null) return;
            displayStyle = Style(FontStyle.Bold, false);
            headingStyle = Style(FontStyle.Bold, false);
            bodyStyle = Style(FontStyle.Normal, true);
            smallStyle = Style(FontStyle.Normal, true);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = koreanFont, fontStyle = FontStyle.Bold, fontSize = 22, alignment = TextAnchor.MiddleCenter, border = new RectOffset(20, 20, 20, 20) };
            buttonStyle.normal.background = roundedTexture;
            buttonStyle.hover.background = roundedTexture;
            buttonStyle.active.background = roundedTexture;
            buttonStyle.normal.textColor = Cream;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Cream;
        }

        private GUIStyle Style(FontStyle fontStyle, bool wordWrap)
            => new GUIStyle(GUI.skin.label) { font = koreanFont, fontStyle = fontStyle, alignment = TextAnchor.MiddleLeft, wordWrap = wordWrap, clipping = TextClipping.Clip };

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
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "Contest circle" : "Contest rounded panel" };
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

        private void OnDestroy()
        {
            OnDisable();
            StopScheduledAudio();
            DestroyClip(leftMarimbaClip);
            DestroyClip(rightMarimbaClip);
            DestroyClip(countClip);
            DestroyClip(countAccentClip);
            DestroyClip(eliminationClip);
            DestroyClip(successClip);
            DestroyClip(failureClip);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (circleTexture != null) Destroy(circleTexture);
        }

        private static void DestroyClip(AudioClip clip) { if (clip != null) Destroy(clip); }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
