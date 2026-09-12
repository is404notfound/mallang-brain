using Mallang.Gate0.Platform.Android;
using Mallang.Gate0.Platform.WebGL;
using Mallang.Gate0.Runtime.Lifecycle;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate0.Tests.PlayMode
{
    // Device-signal adapters as real MonoBehaviours. Verifies that noisy Android pause/focus callbacks and the
    // WebGL AudioContext-only suspend both resolve to a single interrupt and one four-beat recount.
    public sealed class LifecycleAdapterTests
    {
        private GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [Test] public void AndroidDuplicateAndReversedCallbacksCoalesceToOneRecount()
        {
            root = new GameObject("android-adapter");
            var adapter = root.AddComponent<AndroidLifecycleAdapter>();
            var flushed = 0;
            adapter.Controller.FlushRequested += () => flushed++;
            adapter.Controller.Fire(LifecycleTrigger.Begin);

            // Overlapping, reversed, and duplicate signals for a single interruption.
            root.SendMessage("OnApplicationFocus", false);
            root.SendMessage("OnApplicationPause", true);
            root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Interrupted, adapter.Controller.State);
            Assert.AreEqual(1, flushed);
            Assert.AreEqual(1, adapter.Controller.ExcludedSegments);

            // Only when both focus and pause clear does the game resume — once.
            root.SendMessage("OnApplicationPause", false);
            Assert.AreEqual(GameState.Interrupted, adapter.Controller.State, "Still unfocused: must stay interrupted.");
            root.SendMessage("OnApplicationFocus", true);
            Assert.AreEqual(GameState.CountIn, adapter.Controller.State);
            Assert.AreEqual(4, adapter.Controller.BeatsRemaining);
        }

        [Test] public void WebGLAudioContextSuspendInterruptsAndRetryRecovers()
        {
            root = new GameObject("webgl-adapter");
            var adapter = root.AddComponent<WebGLLifecycleAdapter>();
            adapter.Controller.Fire(LifecycleTrigger.Begin);

            // Tab stays visible but the AudioContext suspends → common interrupt.
            adapter.SetAudioContextRunning(false);
            Assert.AreEqual(GameState.Interrupted, adapter.Controller.State);

            // Resume attempted while audio is still suspended → recalibration fails, hold in Error.
            adapter.SetVisibility(true);
            Assert.AreEqual(GameState.Interrupted, adapter.Controller.State);

            // Audio comes back and a retry restarts the count-in.
            adapter.SetAudioContextRunning(true);
            Assert.AreEqual(GameState.CountIn, adapter.Controller.State);
            Assert.AreEqual(4, adapter.Controller.BeatsRemaining);
        }
    }
}
