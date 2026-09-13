using System.Collections;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Lifecycle;
using Mallang.Gate0.Runtime.Measurement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Mallang.Gate0.Tests.PlayMode
{
    // Exercises the real MonoBehaviour that wires T4-T7 into an actual scene: real audio-scheduled count-in,
    // real crowd spawn, real tap judging through the Input System, and real pause/resume recovery.
    //
    // Starting a phase (or resuming after an interrupt) now waits for a full ClockCalibrator calibration burst
    // (DefaultRequiredSamples, ~2.7s at 60fps) rather than firing immediately — a single instantaneous sample
    // was found, by pulling real device clock traces, to be meaningless (dspTime only advances once per audio
    // buffer, so one reading carries up to a whole buffer of quantization noise). Tests below wait for the
    // actual state transition (with a generous timeout) instead of a fixed real-time delay, so they stay
    // correct regardless of the exact required-sample count or the test runner's actual frame rate.
    public sealed class Gate0MeasurementControllerTests
    {
        private GameObject root;
        private Gate0Scenario scenario;
        private Gate0MeasurementController controller;

        [SetUp] public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<Gate0Scenario>();
            scenario.CreateDraft(20260911);
            root = new GameObject("gate0-measurement-under-test");
            root.SetActive(false); // defer Awake so scenario can be assigned before it runs
            root.AddComponent<AudioListener>();
            root.AddComponent<AudioSource>();
            controller = root.AddComponent<Gate0MeasurementController>();
            controller.scenario = scenario;
            root.SetActive(true);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(scenario);
        }

        private static IEnumerator WaitUntilState(Gate0MeasurementController c, GameState state, float timeoutSeconds = 10f)
        {
            var start = Time.realtimeSinceStartup;
            while (c.Lifecycle.State != state)
            {
                Assert.Less(Time.realtimeSinceStartup - start, timeoutSeconds,
                    $"Timed out waiting for {state}; state is still {c.Lifecycle.State}.");
                yield return null;
            }
        }

        [UnityTest] public IEnumerator BeginPhaseCalibratesThenSpawnsCrowdOnCountIn()
        {
            Assert.AreEqual(GameState.Idle, controller.Lifecycle.State);
            controller.BeginPhase(); // phase 0 = "30-idle"
            yield return WaitUntilState(controller, GameState.CountIn);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);
        }

        // Each real phase runs 180s, so a full nine-phase auto run cannot be exercised end-to-end in an
        // automated test (27+ minutes). This covers the entry/cancel wiring instead; AutoPhaseRunnerTests
        // (EditMode) covers the phase-advance state machine itself in isolation.
        [UnityTest] public IEnumerator StartAutoRunBeginsFirstPhaseAndCancelStopsIt()
        {
            Assert.IsFalse(controller.AutoRunActive);
            controller.StartAutoRun();
            Assert.IsTrue(controller.AutoRunActive);
            yield return WaitUntilState(controller, GameState.CountIn);
            Assert.AreEqual(0, controller.PhaseIndex);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);

            controller.CancelAutoRun();
            Assert.IsFalse(controller.AutoRunActive, "Cancelling must stop the auto-advance without interrupting the phase already in progress.");
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State, "The in-progress phase itself keeps running after a cancel.");
        }

        [UnityTest] public IEnumerator RecordFailureStopsAutoRunAndRemainsVisible()
        {
            controller.runWriterOverride = (_, __, ___) => throw new System.IO.IOException("test storage unavailable");
            controller.StartAutoRun();
            yield return WaitUntilState(controller, GameState.Playing);

            LogAssert.Expect(LogType.Error, "RECORD FAILED in 30-idle. Auto run stopped. test storage unavailable");
            root.SendMessage("FinishPhase");

            Assert.IsFalse(controller.AutoRunActive);
            Assert.AreEqual(1, controller.Batch.FailedCount);
            StringAssert.Contains("RECORD FAILED", controller.StatusMessage);
            StringAssert.Contains("30-idle", controller.StatusMessage);
        }

        // BeginPhase (the manual "Start phase" button) must not silently continue an auto run in the
        // background: a manual start is understood as taking over from wherever auto-run left off.
        [UnityTest] public IEnumerator ManualBeginPhaseCancelsAnActiveAutoRun()
        {
            controller.StartAutoRun();
            Assert.IsTrue(controller.AutoRunActive);
            yield return WaitUntilState(controller, GameState.Playing);

            // Interrupt cancels the running attempt; a manual BeginPhase afterwards must not resume auto-run.
            root.SendMessage("OnApplicationPause", true);
            root.SendMessage("OnApplicationPause", false);
            var start = Time.realtimeSinceStartup;
            while (controller.Lifecycle.State == GameState.Error || controller.Lifecycle.State == GameState.Recalibrating)
            {
                Assert.Less(Time.realtimeSinceStartup - start, 10f, "Timed out waiting to leave Error/Recalibrating.");
                if (controller.Lifecycle.State == GameState.Error) controller.Retry();
                yield return null;
            }
            controller.BeginPhase();
            Assert.IsFalse(controller.AutoRunActive);
        }

        [UnityTest] public IEnumerator CountInAdvancesToPlayingAfterFourRealBeats()
        {
            controller.BeginPhase();
            yield return WaitUntilState(controller, GameState.Playing);
            Assert.IsNotNull(controller.CurrentSession);
        }

        [UnityTest] public IEnumerator RealKeyboardTapDuringPlayingIsJudged()
        {
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                controller.BeginPhase();
                yield return WaitUntilState(controller, GameState.Playing);

                // The scenario's synthetic input schedule auto-replays in the background (see PhaseSession.
                // ConsumeDueSyntheticInputs), so a real key press must be checked as an *increase*, not an
                // exact count — a synthetic input may also have come due by this point in real time.
                var before = controller.CurrentSession.Tally.TotalJudged + controller.CurrentSession.Tally.Duplicate;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return null;
                yield return null;

                Assert.Greater(controller.CurrentSession.Tally.TotalJudged + controller.CurrentSession.Tally.Duplicate, before);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest] public IEnumerator PauseDuringCountInInterruptsAndResumeRecounts()
        {
            controller.BeginPhase();
            yield return WaitUntilState(controller, GameState.CountIn);

            root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Interrupted, controller.Lifecycle.State);

            root.SendMessage("OnApplicationPause", false);
            // Resume enters Recalibrating and needs a fresh calibration burst before reaching CountIn again;
            // Retry recovers if it lands in Error while the audio/clock is still settling (test-plan P07).
            var start = Time.realtimeSinceStartup;
            while (controller.Lifecycle.State != GameState.CountIn)
            {
                Assert.Less(Time.realtimeSinceStartup - start, 10f, "Timed out waiting for CountIn after resume.");
                if (controller.Lifecycle.State == GameState.Error) controller.Retry();
                yield return null;
            }
        }

        // An interrupt must throw away the whole in-progress attempt — crowd, session, tally, frame stats —
        // not just pause it, so nothing from before the interrupt bleeds into the post-resume run.
        [UnityTest] public IEnumerator InterruptDuringPlayingResetsCrowdAndSessionThenResumeStartsFresh()
        {
            controller.BeginPhase();
            yield return WaitUntilState(controller, GameState.Playing);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);

            root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Interrupted, controller.Lifecycle.State);
            Assert.IsNull(controller.CurrentSession, "The in-progress session must be discarded on interrupt.");
            Assert.AreEqual(0, controller.LoadDriver.ActiveCrowd, "The crowd must be cleared on interrupt.");

            root.SendMessage("OnApplicationPause", false);
            // Resume recalibrates via a fresh sample burst; Retry is the documented recovery path if it lands
            // in Error while the audio/clock is still settling (test-plan P07) — exercise it for real rather
            // than asserting away real-world timing variance.
            var start = Time.realtimeSinceStartup;
            while (controller.Lifecycle.State != GameState.CountIn)
            {
                Assert.Less(Time.realtimeSinceStartup - start, 10f, "Timed out waiting for CountIn after resume.");
                if (controller.Lifecycle.State == GameState.Error) controller.Retry();
                yield return null;
            }
            // A fresh attempt is started immediately on re-entering CountIn: crowd respawns, new session.
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);
            Assert.IsNotNull(controller.CurrentSession);
            Assert.AreEqual(0, controller.CurrentSession.Tally.TotalJudged);
        }
    }
}
