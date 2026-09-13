using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Mallang.Gate0.Runtime.Clock;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Input;
using Mallang.Gate0.Runtime.Lifecycle;
using Mallang.Gate0.Runtime.Smoke;
using Mallang.Gate0.Runtime.Workload;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Wires T4 (clock calibration + raw timing judgement), T5 (lifecycle interrupt/resume + four-beat recount),
    // T6 (crowd workload + frame stats), and T7 (RunRecord writing) into one real, on-device scene.
    //
    // This is still a Gate 0 technical-validation harness, not Gate 1 gameplay: there is no score, combo,
    // hold, final art, or marimba tone here. A human picks one of the scenario's nine phases, starts it, taps
    // left/right along with the metronome, and the phase writes a RunRecord when it finishes. The scenario's
    // fixed synthetic input schedule is also auto-replayed every run so the cross-platform band comparison
    // does not depend on a human tapping for the full three minutes. Values shown or recorded are technical
    // measurements, not a Gate 0 pass — gateEligible is always false here because a final pass additionally
    // requires the full T9 device/network matrix and a frozen Gate0AcceptancePolicy.
    //
    // Known limitations (tracked, not silently ignored):
    //  - buildId/loadState/cacheState/coldStartMs are placeholders until this is driven by the real T9
    //    protocol; buildId should be supplied by the BuildManifest once a run is tied to a specific build.
    [RequireComponent(typeof(AudioSource))]
    public sealed class Gate0MeasurementController : MonoBehaviour
    {
        private const double ScheduleLookaheadSeconds = 0.5;
        private const double CalibrationWindowSeconds = 30.0;
        private const int UiUpdatesPerBeat = 4;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void Gate0InstallAudioContextBridge(string objectName, string methodName);
        [DllImport("__Internal")] private static extern double Gate0GetDevicePixelRatio();
        [DllImport("__Internal")] private static extern void Gate0DownloadText(string fileName, string contents);
#endif

        public Gate0Scenario scenario;
        public Gate0AcceptancePolicy policy; // may be an unfrozen draft; this controller never requires it frozen

        // Assigned by the scene setup script from Assets/Mallang/Gate0/Settings/CrowdProxy.controller. Left
        // null falls back to a manual Transform rotation so the controller still runs without the asset.
        public RuntimeAnimatorController crowdAnimatorController;

        public LifecycleController Lifecycle { get; private set; }
        public string StatusMessage { get; private set; } = "Ready.";
        public int PhaseIndex { get; private set; }
        public PhaseSession CurrentSession { get; private set; }
        public LoadDriver LoadDriver => loadDriver;
        public MeasurementBatch Batch => measurementBatch;

        // Test seam for a storage failure. Player builds leave this null and use MeasurementWriter.WriteRun.
        [NonSerialized] public Action<string, RunRecord, byte[]> runWriterOverride;

        private readonly InterruptCoalescer coalescer = new InterruptCoalescer();
        private readonly IRhythmClock clock = new SystemRhythmClock();
        private ClockCalibrator liveCalibrator = new ClockCalibrator();
        private readonly CalibrationState calibrationState = new CalibrationState();
        private double lastCalibrationFinalizeRealtime;

        // Two AudioSources, used alternately per beat. AudioSource.PlayScheduled has only one pending slot per
        // source: scheduling a new clip on a source that is still playing (or has a play still pending) cuts
        // the previous one off mid-waveform, which is exactly what produced an audible "tak-tak-tak" click on
        // every beat on a real device even though the clip itself fades to zero at both ends. Alternating
        // sources guarantees a full beat interval (500ms) of silence on a given source before it is reused,
        // well clear of the ~200ms click clip.
        private AudioSource[] beatSources;
        private int clickCounter;
        // The contest only lets the player hear their own marimba. Competitors are visual, so the workload uses
        // one player sound source rather than simulating 99 sounds that the real game will never mix.
        private AudioSource playerSoundSource;
        private AudioClip clickTone;
        private LoadDriver loadDriver;

        private BeatScheduler countInScheduler;
        private double countInStartRealtime;
        private double? phaseStartDsp;
        private int nextScheduledBeat;
        private string phaseStartedUtc;
        private int flashFramesRemaining;
        private int leftFlashFrames, rightFlashFrames;
        private bool paused, focused = true, audioContextRunning = true;
        private RawMeasurementTrace rawTrace;
        private AutoPhaseRunner autoRunner;
        private bool autoNextQueued;
        private double autoNextRealtime;
        private MeasurementBatch measurementBatch;
        // True while waiting for a fresh calibration burst (ClockCalibrator.DefaultRequiredSamples readings,
        // ~2.7s at 60fps) to fill up before starting a phase. See BeginSelectedPhase for why this can no longer
        // be a single instantaneous sample.
        private bool startPending;

        public bool AutoRunActive => autoRunner != null && autoRunner.Active;

        // Live per-beat UI readout, refreshed exactly UiUpdatesPerBeat times per beat to exercise the
        // scenario's declared UI-update load rather than only redrawing reactively on state changes.
        private int uiElapsedBeats;
        private double uiLastElapsedMs;
        private int uiActiveCrowd;
        private int uiJudgedTotal;

        public Gate0Scenario.Phase CurrentPhaseSpec =>
            scenario != null && scenario.specification.phases != null && PhaseIndex < scenario.specification.phases.Length
                ? scenario.specification.phases[PhaseIndex]
                : null;

        private void Awake()
        {
            if (scenario == null) throw new InvalidOperationException("Gate0MeasurementController requires a Gate0Scenario asset.");
            scenario.Validate();
            if (policy != null)
            {
                try { policy.Validate(requireFrozen: false); }
                catch (InvalidOperationException e) { Debug.LogWarning("Gate0AcceptancePolicy is incomplete; running without threshold reference: " + e.Message); policy = null; }
            }

            var primary = GetComponent<AudioSource>();
            primary.playOnAwake = false;
            beatSources = new[] { primary, CreateAudioSource("Gate0BeatAudioB") };
            playerSoundSource = CreateAudioSource("Gate0PlayerSound");
            clickTone = SmokeTapScene.CreateTestTone();

            var crowdHost = new GameObject("Gate0Crowd");
            crowdHost.transform.SetParent(transform, false);
            loadDriver = crowdHost.AddComponent<LoadDriver>();
            loadDriver.animatorController = crowdAnimatorController;

            Lifecycle = new LifecycleController();
            Lifecycle.Transitioned += OnLifecycleTransitioned;
            Lifecycle.Fire(LifecycleTrigger.BootSucceeded);
            autoRunner = new AutoPhaseRunner(scenario.specification.phases.Length);

            InputSystem.onEvent += OnInputEvent;

#if UNITY_WEBGL && !UNITY_EDITOR
            // Gate0AudioContextBridge.jslib calls SetWebGLAudioContextRunning(int) on this GameObject by name
            // whenever the browser's own audio-suspend state changes (see the jslib for why this uses an
            // independent monitoring AudioContext rather than Unity's internal audio object).
            Gate0InstallAudioContextBridge(gameObject.name, nameof(SetWebGLAudioContextRunning));
#endif

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private AudioSource CreateAudioSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            return src;
        }

        private void OnLifecycleTransitioned(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.CountIn: StartAttempt(); break;
                case GameState.Playing: phaseStartDsp = clock.DspSeconds; StatusMessage = "Playing."; break;
                case GameState.Interrupted: StatusMessage = "Interrupted."; AbortAttempt(); break;
                case GameState.Recalibrating: StatusMessage = "Recalibrating..."; break;
                case GameState.Error: StatusMessage = "Audio/clock error - tap Retry."; break;
            }
        }

        public void SelectPhase(int index)
        {
            if (Lifecycle.State != GameState.Idle && Lifecycle.State != GameState.Completed) return;
            var count = scenario.specification.phases?.Length ?? 1;
            PhaseIndex = Mathf.Clamp(index, 0, count - 1);
        }

        public void BeginPhase()
        {
            autoRunner.Cancel();
            StartMeasurementBatch(1);
            BeginSelectedPhase();
        }

        public void StartAutoRun()
        {
            if (Lifecycle.State != GameState.Idle && Lifecycle.State != GameState.Completed) return;
            PhaseIndex = autoRunner.Start();
            autoNextQueued = false;
            StartMeasurementBatch(scenario.specification.phases.Length);
            BeginSelectedPhase();
        }

        private void StartMeasurementBatch(int expectedPhaseCount)
        {
            var buildId = string.IsNullOrWhiteSpace(Application.buildGUID) ? "diagnostic-unlinked" : Application.buildGUID;
            measurementBatch = new MeasurementBatch(
                Path.Combine(Application.persistentDataPath, "Gate0Runs"),
                expectedPhaseCount,
                buildId,
                Application.platform.ToString(),
                runWriterOverride);
        }

        public void CancelAutoRun()
        {
            autoRunner.Cancel();
            autoNextQueued = false;
            StatusMessage = "Automatic run cancelled; current phase will finish.";
        }

        private void BeginSelectedPhase()
        {
            if (Lifecycle.State != GameState.Idle && Lifecycle.State != GameState.Completed) return;
            var phase = CurrentPhaseSpec;
            if (phase == null) { StatusMessage = "No phase selected."; return; }

            // Do not fire Begin yet: a single instantaneous sample was found (by pulling real device clock
            // traces) to be meaningless here — dspTime only advances once per audio callback while realtime
            // advances every video frame, so any individual reading carries up to about one audio-buffer of
            // quantization noise. Wait for Update() to accumulate a full calibration burst (ClockCalibrator.
            // DefaultRequiredSamples) instead, which is what actually lets Build() distinguish a genuine clock
            // problem from that per-sample jitter. In practice Idle has usually already been sampling for a
            // while, so this resolves within the same frame the button was pressed.
            rawTrace = new RawMeasurementTrace(phase.id);
            startPending = true;
            StatusMessage = "Calibrating...";
        }

        public void Retry()
        {
            if (Lifecycle.State != GameState.Error) return;
            Lifecycle.Fire(LifecycleTrigger.Retry); // -> Recalibrating; Update() drives the fresh burst below
            BeginRecalibrationBurst();
        }

        // Called by every entry into CountIn: the first Begin from Idle/Completed, and every fresh
        // recalibration-succeeded resume. A single attempt is always started clean — no partial pre-interrupt
        // taps, frames, or crowd state carry over (see item: "중단·복구·재시도 때 ... 모두 초기화").
        private void StartAttempt()
        {
            var phase = CurrentPhaseSpec;
            if (phase == null) return;

            CurrentSession = new PhaseSession(phase);
            loadDriver.BeginPhase(phase);
            nextScheduledBeat = 0;
            clickCounter = 0;
            phaseStartDsp = null;
            uiElapsedBeats = 0;
            uiLastElapsedMs = 0;
            uiActiveCrowd = loadDriver.ActiveCrowd;
            uiJudgedTotal = 0;

            countInScheduler = new BeatScheduler(WorkloadSchedule.BeatIntervalUs, LifecycleController.CountInBeats);
            countInStartRealtime = clock.RealtimeSeconds;
            StatusMessage = "Count-in...";
        }

        // Discards the in-progress attempt entirely: stops all scheduled/voice audio, clears the flash and
        // crowd, and drops the current session so no pre-interrupt measurement bleeds into the post-resume
        // run. Called on every entry into Interrupted.
        private void AbortAttempt()
        {
            foreach (var s in beatSources) s.Stop();
            if (playerSoundSource != null) playerSoundSource.Stop();
            flashFramesRemaining = 0;
            leftFlashFrames = 0;
            rightFlashFrames = 0;
            loadDriver.EndPhase();
            CurrentSession = null;
            phaseStartDsp = null;
            rawTrace = null;
        }

        private void Update()
        {
            if (Lifecycle.State == GameState.Boot) return;

            var accumulatingCalibration = Lifecycle.State == GameState.Idle || Lifecycle.State == GameState.CountIn
                || Lifecycle.State == GameState.Playing || Lifecycle.State == GameState.Completed
                || Lifecycle.State == GameState.Recalibrating;
            if (accumulatingCalibration)
            {
                AddCalibrationSample();

                if (Lifecycle.State == GameState.Recalibrating)
                {
                    TryCompleteRecalibrationBurst();
                }
                else if (clock.RealtimeSeconds - lastCalibrationFinalizeRealtime >= CalibrationWindowSeconds)
                {
                    var wasValid = calibrationState.Current.Valid;
                    if (!FinalizeCalibration() && wasValid && CurrentSession != null)
                    {
                        // A previously-good calibration just failed mid-run: stop judging immediately and mark
                        // this run's data invalid rather than silently keep using a stale/drifted conversion.
                        CurrentSession.Invalidate("clock-error");
                        StatusMessage = "Clock calibration failed mid-run; segment invalidated.";
                    }
                }
            }

            if (startPending && liveCalibrator.SampleCount >= liveCalibrator.RequiredSamples)
            {
                startPending = false;
                if (FinalizeCalibration())
                {
                    phaseStartedUtc = DateTime.UtcNow.ToString("o");
                    Lifecycle.Fire(LifecycleTrigger.Begin); // -> CountIn, which triggers StartAttempt() below
                }
                else
                {
                    StatusMessage = "Clock calibration invalid; cannot start.";
                }
            }

            switch (Lifecycle.State)
            {
                case GameState.CountIn: AdvanceCountIn(); break;
                case GameState.Playing: AdvancePlaying(); break;
            }

            if (autoNextQueued && clock.RealtimeSeconds >= autoNextRealtime)
            {
                autoNextQueued = false;
                BeginSelectedPhase();
            }

            if (flashFramesRemaining > 0) flashFramesRemaining--;
            if (leftFlashFrames > 0) leftFlashFrames--;
            if (rightFlashFrames > 0) rightFlashFrames--;
        }

        private void AdvanceCountIn()
        {
            var elapsedUs = (long)((clock.RealtimeSeconds - countInStartRealtime) * 1_000_000);
            foreach (var _ in countInScheduler.ConsumeDueBeats(elapsedUs))
            {
                PlayClickNow();
                Lifecycle.AdvanceCountInBeat();
                if (Lifecycle.State != GameState.CountIn) break; // count-in completed mid-loop
            }
        }

        private void AdvancePlaying()
        {
            if (CurrentSession == null || phaseStartDsp == null) return;
            var elapsedUs = (long)((clock.DspSeconds - phaseStartDsp.Value) * 1_000_000);
            CurrentSession.RecordFrame(Time.unscaledDeltaTime);
            rawTrace?.AddFrame(elapsedUs, Time.unscaledDeltaTime);
            CurrentSession.ConsumeDueSyntheticInputs(elapsedUs, RecordSyntheticInput);

            foreach (var beat in CurrentSession.ConsumeDueBeats(elapsedUs))
            {
                loadDriver.Tick(beat);
                RefreshBeatUi(beat, elapsedUs);
                rawTrace?.AddBeat(beat, elapsedUs);
                flashFramesRemaining = 3;
            }
            ScheduleUpcomingClicks();

            if (CurrentSession.IsComplete(elapsedUs)) FinishPhase();
        }

        private void RecordSyntheticInput(long inputUs, bool leftHand, RawTimingProbe.Result result)
        {
            var dsp = phaseStartDsp.GetValueOrDefault() + inputUs / 1_000_000.0;
            rawTrace?.AddInput("synthetic", leftHand, 0, dsp, inputUs, result);
            if (result.Accepted) playerSoundSource.PlayOneShot(clickTone, 0.4f);
        }

        // Refreshes exactly UiUpdatesPerBeat tracked values per beat, matching the scenario's uiUpdatesPerBeat
        // load instead of only redrawing reactively when something else already changed.
        private void RefreshBeatUi(int beat, long elapsedUs)
        {
            uiElapsedBeats = beat;
            uiLastElapsedMs = elapsedUs / 1000.0;
            uiActiveCrowd = loadDriver.ActiveCrowd;
            uiJudgedTotal = CurrentSession.Tally.TotalJudged;
            _ = UiUpdatesPerBeat; // four fields above == UiUpdatesPerBeat; kept as a named constant for clarity
        }

        // Look-ahead audio scheduling: clicks are queued against AudioSettings.dspTime directly, not fired
        // immediately in Update, so playback timing does not inherit frame jitter. Alternates AudioSources per
        // beat (see beatSources) so a source is never re-scheduled before its previous click has finished.
        private void ScheduleUpcomingClicks()
        {
            if (CurrentSession == null || phaseStartDsp == null) return;
            var schedule = CurrentSession.Schedule;
            while (nextScheduledBeat < schedule.BeatCount)
            {
                var beatDsp = phaseStartDsp.Value + schedule.BeatTimeUs(nextScheduledBeat) / 1_000_000.0;
                if (beatDsp > AudioSettings.dspTime + ScheduleLookaheadSeconds) break;
                PlayClickAt(beatDsp);
                nextScheduledBeat++;
            }
        }

        private void PlayClickAt(double dspTime)
        {
            var src = beatSources[clickCounter % beatSources.Length];
            clickCounter++;
            src.clip = clickTone;
            src.PlayScheduled(dspTime);
        }

        private void PlayClickNow()
        {
            PlayClickAt(AudioSettings.dspTime + 0.05);
            flashFramesRemaining = 3;
        }

        // Reads real Input System event timestamps (InputEvent.time, the same realtime-since-startup timeline
        // the clock calibration uses) instead of polling in Update, removing the extra frame of latency
        // uncertainty polling would add on top of the ±60ms/±120ms judgement. Runs for every input event
        // system-wide; the state checks below make it a no-op outside an active, calibrated Playing segment.
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!Lifecycle.AcceptsGameInput || CurrentSession == null || phaseStartDsp == null || !calibrationState.Current.Valid) return;
            if (!eventPtr.valid) return;

            if (device is Touchscreen touchscreen)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!WasPressedThisEvent(touch.press, eventPtr)) continue;
                    // An OS-cancelled touch (e.g. palm rejection) must not be treated as a real tap.
                    if (touch.phase.ReadValueFromEvent(eventPtr) == UnityEngine.InputSystem.TouchPhase.Canceled) continue;
                    var x = touch.position.ReadValueFromEvent(eventPtr).x;
                    Judge(eventPtr.time, x < Screen.width * 0.5f);
                }
                return;
            }
            if (device is Mouse mouse)
            {
                if (WasPressedThisEvent(mouse.leftButton, eventPtr))
                    Judge(eventPtr.time, mouse.position.ReadValueFromEvent(eventPtr).x < Screen.width * 0.5f);
                return;
            }
            if (device is Keyboard keyboard)
            {
                if (WasPressedThisEvent(keyboard.aKey, eventPtr) || WasPressedThisEvent(keyboard.leftArrowKey, eventPtr))
                    Judge(eventPtr.time, true);
                if (WasPressedThisEvent(keyboard.lKey, eventPtr) || WasPressedThisEvent(keyboard.rightArrowKey, eventPtr))
                    Judge(eventPtr.time, false);
            }
        }

        // True only for the event that transitions a button from not-pressed to pressed: `control.isPressed`
        // still reflects the state *before* this event is applied (onEvent runs during processing, ahead of
        // the device's live state commit), so comparing it against this event's own value gives a clean edge.
        private static bool WasPressedThisEvent(ButtonControl control, InputEventPtr eventPtr)
            => !control.isPressed && control.ReadValueFromEvent(eventPtr, out var value) && value >= 0.5f;

        private void Judge(double realtimeSeconds, bool leftHand)
        {
            var dspSeconds = calibrationState.Current.ToDspSeconds(realtimeSeconds);
            var offsetUs = (long)((dspSeconds - phaseStartDsp.Value) * 1_000_000);
            var result = CurrentSession.JudgeTap(offsetUs, leftHand);
            rawTrace?.AddInput("device", leftHand, realtimeSeconds, dspSeconds, offsetUs, result);
            if (!result.Accepted) return; // duplicate or invalidated segment: no immediate feedback
            // Immediate visual feedback tied to the accepted input itself (distinct from the beat-cue flash),
            // for the external tap-to-visible-change latency protocol.
            if (leftHand) leftFlashFrames = 4; else rightFlashFrames = 4;
            playerSoundSource.PlayOneShot(clickTone, 0.4f);
        }

        private void FinishPhase()
        {
            var wasAutoRun = AutoRunActive;
            Lifecycle.Fire(LifecycleTrigger.Finish);
            var recordSaved = WriteRunRecord();
            loadDriver.EndPhase();
            CurrentSession = null;
            phaseStartDsp = null;
            rawTrace = null;

            if (!recordSaved)
            {
                autoRunner.Cancel();
                autoNextQueued = false;
                return;
            }

            if (autoRunner.TryAdvance(out var nextPhase))
            {
                PhaseIndex = nextPhase;
                autoNextQueued = true;
                autoNextRealtime = clock.RealtimeSeconds + 1.0;
                StatusMessage = $"Phase complete. Next automatic phase: {nextPhase + 1}/9";
            }
            else
            {
                StatusMessage = wasAutoRun
                    ? $"Automatic run complete. Saved {measurementBatch.SavedCount}/{measurementBatch.ExpectedPhaseCount}."
                    : "Phase complete.";
            }
        }

        private bool WriteRunRecord()
        {
            var frames = CurrentSession.FrameStats.Compute();
            var invalid = CurrentSession.Invalidated;
            var record = new RunRecord
            {
                runId = Guid.NewGuid().ToString("N"),
                buildId = string.IsNullOrWhiteSpace(Application.buildGUID) ? "diagnostic-unlinked" : Application.buildGUID,
                scenarioId = scenario.specification.scenarioId,
                scenarioHash = scenario.contentHash,
                policyId = policy != null ? policy.specification.policyId : "unset-draft",
                policyHash = !string.IsNullOrEmpty(policy?.contentHash) ? policy.contentHash : "unset-draft",
                deviceModel = SystemInfo.deviceModel,
                os = SystemInfo.operatingSystem,
                tossAppVersion = "",
                network = MapNetwork(Application.internetReachability),
                dprMilli = Mathf.RoundToInt(ReadDevicePixelRatio() * 1000f),
                refreshHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value),
                loadState = "unspecified",
                cacheState = "unspecified",
                phaseOrder = new[] { CurrentSession.Phase.id },
                startedUtc = phaseStartedUtc,
                endedUtc = DateTime.UtcNow.ToString("o"),
                status = invalid ? "invalid" : "valid",
                statusReason = invalid ? CurrentSession.InvalidReason : null,
                externalSamplePaths = Array.Empty<string>(),
                gateEligible = false,
                meanFps = frames.MeanFps,
                frameP95Ms = frames.P95Ms,
                frameP99Ms = frames.P99Ms,
                framesOver33Ms = frames.FramesOver33Ms,
                framesOver50Ms = frames.FramesOver50Ms,
                longestStallMs = frames.LongestStallMs,
                frameCount = frames.FrameCount,
                perfectCount = CurrentSession.Tally.Perfect,
                goodCount = CurrentSession.Tally.Good,
                missCount = CurrentSession.Tally.Miss,
                duplicateCount = CurrentSession.Tally.Duplicate,
            };
            var raw = rawTrace != null ? rawTrace.ToUtf8Json() : Encoding.UTF8.GetBytes("{}");
            if (measurementBatch == null) StartMeasurementBatch(1);
            if (measurementBatch.TrySave(record, raw, out var error))
            {
                StatusMessage = $"Saved {measurementBatch.SavedCount}/{measurementBatch.ExpectedPhaseCount}: {measurementBatch.BundlePath}";
                return true;
            }

            StatusMessage = $"RECORD FAILED in {CurrentSession.Phase.id}. Auto run stopped. {error}";
            Debug.LogError(StatusMessage);
            return false;
        }

        public void ExportMeasurementBundle()
        {
            if (measurementBatch == null || measurementBatch.AttemptedCount == 0)
            {
                StatusMessage = "No measurement results to export.";
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Gate0DownloadText("Gate0RunBundle-" + measurementBatch.SessionId + ".json", measurementBatch.ToJson());
            StatusMessage = "Result download requested. Check the browser download list.";
#else
            StatusMessage = "Results saved: " + measurementBatch.BundlePath;
#endif
        }

        private static string MapNetwork(NetworkReachability reachability) => reachability switch
        {
            NetworkReachability.NotReachable => "none",
            NetworkReachability.ReachableViaCarrierDataNetwork => "mobile",
            _ => "wifi",
        };

        private bool FinalizeCalibration()
        {
            var result = liveCalibrator.Build();
            liveCalibrator = new ClockCalibrator();
            lastCalibrationFinalizeRealtime = clock.RealtimeSeconds;
            return calibrationState.Apply(result);
        }

        // Resets the calibrator to collect an entirely fresh burst across the Recalibrating state, rather than
        // reusing whatever was accumulated before the interrupt — the whole point of resuming is to check
        // whether something changed (jump, backward clock, audio device reinit) across the suspend boundary,
        // which stale pre-interrupt samples would dilute.
        private void BeginRecalibrationBurst()
        {
            if (rawTrace == null && CurrentPhaseSpec != null) rawTrace = new RawMeasurementTrace(CurrentPhaseSpec.id);
            liveCalibrator = new ClockCalibrator();
        }

        // Called every frame while Recalibrating; only decides once a full burst has accumulated (see
        // ClockCalibrator's required-sample-count rationale).
        private void TryCompleteRecalibrationBurst()
        {
            if (liveCalibrator.SampleCount < liveCalibrator.RequiredSamples) return;
            var clockOk = FinalizeCalibration();
            // On WebGL, a resume must not be reported successful while the browser's Web Audio context is
            // still suspended (see SetWebGLAudioContextRunning) — otherwise a fresh count-in would start with
            // no audio device actually ready, contradicting "오디오 준비가 끝난 뒤 네 박자를 새로 시작한다".
            var ok = clockOk && audioContextRunning;
            Lifecycle.Fire(ok ? LifecycleTrigger.RecalibrateSucceeded : LifecycleTrigger.RecalibrateFailed);
        }

        private void AddCalibrationSample()
        {
            var realtime = clock.RealtimeSeconds;
            var dsp = clock.DspSeconds;
            liveCalibrator.AddSample(realtime, dsp);
            rawTrace?.AddClock(realtime, dsp);
        }

        private static float ReadDevicePixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Max(0.1f, (float)Gate0GetDevicePixelRatio());
#elif UNITY_ANDROID && !UNITY_EDITOR
            return Screen.dpi > 0 ? Mathf.Max(0.1f, Screen.dpi / 160f) : 1f;
#else
            return 1f;
#endif
        }

        private void OnApplicationPause(bool value) { paused = value; ApplySuspendState(); }
        private void OnApplicationFocus(bool value) { focused = value; ApplySuspendState(); }

        // Called from unity/Assets/Plugins/WebGL/Gate0AudioContextBridge.jslib via SendMessage whenever the
        // browser's Web Audio context changes state. SendMessage only supports string/int/float/void
        // signatures, hence the int rather than bool.
        public void SetWebGLAudioContextRunning(int running)
        {
            audioContextRunning = running != 0;
            ApplySuspendState();
        }

        private void ApplySuspendState()
        {
            var suspended = paused || !focused || !audioContextRunning;
            switch (coalescer.Update(suspended))
            {
                case InterruptCoalescer.Signal.Interrupt:
                    Lifecycle.TryFire(LifecycleTrigger.Interrupt);
                    break;
                case InterruptCoalescer.Signal.Resume:
                    if (Lifecycle.TryFire(LifecycleTrigger.Resume)) BeginRecalibrationBurst();
                    break;
            }
        }

        // Fixed row below SmokeTapScene's "LEFT/A 0 taps" labels (which end around y=260) and above its audio
        // status text (which starts around y=340), so the two OnGUI blocks never overlap on the same 960x540
        // canvas.
        private const float ControlsY = 270f;

        private void OnGUI()
        {
            var y = 10f;
            GUI.Label(new Rect(10, y, 920, 24), $"Gate0 Measurement — phase {PhaseIndex + 1}/{scenario.specification.phases.Length} ({CurrentPhaseSpec?.id}) — {Lifecycle.State}"); y += 24;
            GUI.Label(new Rect(10, y, 920, 24), $"Perfect {CurrentSession?.Tally.Perfect ?? 0}  Good {CurrentSession?.Tally.Good ?? 0}  Miss {CurrentSession?.Tally.Miss ?? 0}  Dup {CurrentSession?.Tally.Duplicate ?? 0}"); y += 24;
            GUI.Label(new Rect(10, y, 920, 24), $"beat {uiElapsedBeats}  t={uiLastElapsedMs:0}ms  crowd {uiActiveCrowd}  judged {uiJudgedTotal}"); y += 24;
            var statusLine = StatusMessage;
            if (startPending || Lifecycle.State == GameState.Recalibrating)
                statusLine += $" ({liveCalibrator.SampleCount}/{liveCalibrator.RequiredSamples})";
            GUI.Label(new Rect(10, y, 920, 24), statusLine);
            if (measurementBatch != null)
                GUI.Label(new Rect(10, y + 24, 920, 24), $"records {measurementBatch.SavedCount}/{measurementBatch.ExpectedPhaseCount}  invalid {measurementBatch.InvalidCount}  failed {measurementBatch.FailedCount}");

            if (Lifecycle.State == GameState.Idle || Lifecycle.State == GameState.Completed)
            {
                if (GUI.Button(new Rect(10, ControlsY, 140, 36), "< Prev")) SelectPhase(PhaseIndex - 1);
                if (GUI.Button(new Rect(160, ControlsY, 140, 36), "Next >")) SelectPhase(PhaseIndex + 1);
                if (GUI.Button(new Rect(310, ControlsY, 220, 36), "Start phase")) BeginPhase();
                if (GUI.Button(new Rect(540, ControlsY, 250, 36), "Auto run all 9 phases")) StartAutoRun();
            }
            else if (Lifecycle.State == GameState.Error)
            {
                if (GUI.Button(new Rect(10, ControlsY, 220, 36), "Retry")) Retry();
            }
            if (AutoRunActive && GUI.Button(new Rect(800, ControlsY, 150, 36), "Stop auto")) CancelAutoRun();
            if (measurementBatch != null && measurementBatch.AttemptedCount > 0
                && GUI.Button(new Rect(800, ControlsY + 42, 150, 36), "Export results"))
                ExportMeasurementBundle();

            if (flashFramesRemaining > 0) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            var oldColor = GUI.color;
            if (leftFlashFrames > 0) { GUI.color = new Color(1, 1, 1, 0.5f); GUI.DrawTexture(new Rect(0, 0, Screen.width * 0.5f, Screen.height), Texture2D.whiteTexture); }
            if (rightFlashFrames > 0) { GUI.color = new Color(1, 1, 1, 0.5f); GUI.DrawTexture(new Rect(Screen.width * 0.5f, 0, Screen.width * 0.5f, Screen.height), Texture2D.whiteTexture); }
            GUI.color = oldColor;
        }

        private void OnDestroy()
        {
            InputSystem.onEvent -= OnInputEvent;
            if (clickTone != null) Destroy(clickTone);
        }
    }
}
