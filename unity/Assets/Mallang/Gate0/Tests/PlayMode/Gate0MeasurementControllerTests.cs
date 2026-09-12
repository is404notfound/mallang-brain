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

        [UnityTest] public IEnumerator BeginPhaseSpawnsCrowdImmediately()
        {
            Assert.AreEqual(GameState.Idle, controller.Lifecycle.State);
            controller.BeginPhase(); // phase 0 = "30-idle"
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);
            yield return null;
        }

        // Each real phase runs 180s, so a full nine-phase auto run cannot be exercised end-to-end in an
        // automated test (27+ minutes). This covers the entry/cancel wiring instead; AutoPhaseRunnerTests
        // (EditMode) covers the phase-advance state machine itself in isolation.
        [UnityTest] public IEnumerator StartAutoRunBeginsFirstPhaseAndCancelStopsIt()
        {
            Assert.IsFalse(controller.AutoRunActive);
            controller.StartAutoRun();
            Assert.IsTrue(controller.AutoRunActive);
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State);
            Assert.AreEqual(0, controller.PhaseIndex);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);

            controller.CancelAutoRun();
            Assert.IsFalse(controller.AutoRunActive, "Cancelling must stop the auto-advance without interrupting the phase already in progress.");
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State, "The in-progress phase itself keeps running after a cancel.");
            yield return null;
        }

        // BeginPhase (the manual "Start phase" button) must not silently continue an auto run in the
        // background: a manual start is understood as taking over from wherever auto-run left off.
        [UnityTest] public IEnumerator ManualBeginPhaseCancelsAnActiveAutoRun()
        {
            controller.StartAutoRun();
            Assert.IsTrue(controller.AutoRunActive);
            yield return new WaitForSecondsRealtime(2.2f); // let the first phase reach Playing
            Assert.AreEqual(GameState.Playing, controller.Lifecycle.State);

            // Interrupt cancels the running attempt; a manual BeginPhase afterwards must not resume auto-run.
            root.SendMessage("OnApplicationPause", true);
            root.SendMessage("OnApplicationPause", false);
            for (var attempt = 0; attempt < 10 && controller.Lifecycle.State == GameState.Error; attempt++)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                controller.Retry();
            }
            controller.BeginPhase();
            Assert.IsFalse(controller.AutoRunActive);
        }

        [UnityTest] public IEnumerator CountInAdvancesToPlayingAfterFourRealBeats()
        {
            controller.BeginPhase();
            yield return new WaitForSecondsRealtime(2.2f); // 4 beats @ 500ms
            Assert.AreEqual(GameState.Playing, controller.Lifecycle.State);
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
                yield return new WaitForSecondsRealtime(2.2f);
                Assert.AreEqual(GameState.Playing, controller.Lifecycle.State);

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
            yield return null;
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State);

            root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Interrupted, controller.Lifecycle.State);

            root.SendMessage("OnApplicationPause", false);
            // Resume recalibrates synchronously and starts a fresh count-in.
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State);
            yield return null;
        }

        // An interrupt must throw away the whole in-progress attempt — crowd, session, tally, frame stats —
        // not just pause it, so nothing from before the interrupt bleeds into the post-resume run.
        [UnityTest] public IEnumerator InterruptDuringPlayingResetsCrowdAndSessionThenResumeStartsFresh()
        {
            controller.BeginPhase();
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.AreEqual(GameState.Playing, controller.Lifecycle.State);
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);

            root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Interrupted, controller.Lifecycle.State);
            Assert.IsNull(controller.CurrentSession, "The in-progress session must be discarded on interrupt.");
            Assert.AreEqual(0, controller.LoadDriver.ActiveCrowd, "The crowd must be cleared on interrupt.");

            root.SendMessage("OnApplicationPause", false);
            // Resume recalibrates immediately using a short recent sample burst. In a batch-mode CI
            // environment the audio clock can occasionally still be settling, which sends this through Error
            // instead of straight to CountIn — Retry is the documented recovery path for exactly that
            // (test-plan P07), so exercise it for real rather than asserting away real-world flakiness.
            for (var attempt = 0; attempt < 10 && controller.Lifecycle.State == GameState.Error; attempt++)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                controller.Retry();
            }
            Assert.AreEqual(GameState.CountIn, controller.Lifecycle.State);
            // A fresh attempt is started immediately on re-entering CountIn: crowd respawns, new session.
            Assert.AreEqual(30, controller.LoadDriver.ActiveCrowd);
            Assert.IsNotNull(controller.CurrentSession);
            Assert.AreEqual(0, controller.CurrentSession.Tally.TotalJudged);
            yield return null;
        }
    }
}
