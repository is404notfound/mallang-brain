using System.Collections;
using Mallang.Gate0.Runtime.Smoke;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Mallang.Gate0.Tests.PlayMode
{
    public sealed class SmokeTapSceneTests
    {
        private GameObject root;
        private SmokeTapScene probe;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Smoke under test", typeof(Camera), typeof(AudioListener), typeof(AudioSource));
            probe = root.AddComponent<SmokeTapScene>();
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }

        [UnityTest] public IEnumerator ActualMouseInputChangesColorAndCountsOneTap()
        {
            // Batch Play Mode has no focused Game View. Route this synthetic test device
            // to the player; do not change the runtime's real focus/pause handling.
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var before = probe.CurrentColor;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) }.WithButton(MouseButton.Left));
                // The test coroutine can resume before MonoBehaviour.Update in the input frame.
                yield return null;
                yield return null;
                Assert.IsTrue(mouse.enabled, "Synthetic test mouse must receive player events in batch mode.");
                Assert.AreEqual(1, probe.LeftTaps);
                Assert.AreNotEqual(before, probe.CurrentColor);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) });
                yield return null;
                yield return null;
                Assert.AreEqual(1, probe.LeftTaps, "Release must not count as another tap.");
                Assert.AreEqual(0, probe.RightTaps);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [Test] public void ConsecutiveSameSideTapsEachChangeColor()
        {
            probe.Tap(false);
            var first = probe.CurrentColor;
            probe.Tap(false);
            Assert.AreEqual(2, probe.RightTaps);
            Assert.AreNotEqual(first, probe.CurrentColor);
        }

        [Test] public void PauseAndFocusMustBothRecoverBeforeInputResumes()
        {
            root.SendMessage("OnApplicationPause", true);
            root.SendMessage("OnApplicationFocus", false);
            root.SendMessage("OnApplicationPause", false);
            probe.Tap(true);
            Assert.AreEqual(0, probe.LeftTaps);
            root.SendMessage("OnApplicationFocus", true);
            probe.Tap(true);
            Assert.AreEqual(1, probe.LeftTaps);
        }

        [Test] public void GeneratedToneIsFiniteNonSilentAndEndsAtZero()
        {
            var clip = SmokeTapScene.CreateTestTone();
            try
            {
                var samples = new float[clip.samples];
                Assert.IsTrue(clip.GetData(samples, 0));
                var peak = 0f;
                foreach (var sample in samples)
                {
                    Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                Assert.That(peak, Is.GreaterThan(.01f).And.LessThanOrEqualTo(1));
                Assert.AreEqual(0, samples[0]);
                Assert.AreEqual(0, samples[samples.Length - 1]);
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }
}
