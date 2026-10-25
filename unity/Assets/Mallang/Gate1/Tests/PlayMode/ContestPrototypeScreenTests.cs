using System.Collections;
using System.Collections.Generic;
using Mallang.Gate1.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Mallang.Gate1.Tests.PlayMode
{
    public sealed class ContestPrototypeScreenTests
    {
        private readonly List<string> logLines = new List<string>();
        private GameObject root;
        private ContestPrototypeScreen screen;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [SetUp]
        public void SetUp()
        {
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();

            root = new GameObject("gate1-mini-contest-under-test");
            root.SetActive(false);
            root.AddComponent<AudioListener>();
            screen = root.AddComponent<ContestPrototypeScreen>();
            screen.runLogWriterOverride = line => logLines.Add(line);
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            if (root != null) Object.DestroyImmediate(root);
            logLines.Clear();
        }

        [UnityTest]
        public IEnumerator CalibrationReachesReadyAndRawKeyboardInputStartsCountIn()
        {
            yield return WaitForPhase(MiniContestPhase.Ready);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return null;
            yield return null;

            Assert.AreEqual(MiniContestPhase.CountIn, screen.Phase);
            Assert.IsTrue(logLines.Exists(line => line.Contains("\"event_name\":\"run_start\"")));
        }

        [UnityTest]
        public IEnumerator PauseDuringCountInInvalidatesRunAndRestartsCalibration()
        {
            yield return WaitForPhase(MiniContestPhase.Ready);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return null;
            Assert.AreEqual(MiniContestPhase.CountIn, screen.Phase);

            root.SendMessage("OnApplicationPause", true);

            Assert.AreEqual(MiniContestPhase.Calibrating, screen.Phase);
            Assert.IsTrue(logLines.Exists(line => line.Contains("\"event_name\":\"run_end\"")
                && line.Contains("\"end_reason\":\"invalid\"")
                && line.Contains("\"eligible_human_run\":0")));
        }

        private IEnumerator WaitForPhase(MiniContestPhase expected, float timeoutSeconds = 10f)
        {
            var started = Time.realtimeSinceStartup;
            while (screen.Phase != expected)
            {
                Assert.Less(Time.realtimeSinceStartup - started, timeoutSeconds,
                    "Timed out waiting for " + expected + "; phase is " + screen.Phase + ".");
                yield return null;
            }
        }
    }
}
