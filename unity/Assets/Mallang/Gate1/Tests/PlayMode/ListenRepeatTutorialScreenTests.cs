using System.Collections;
using Mallang.Gate1.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mallang.Gate1.Tests.PlayMode
{
    public sealed class ListenRepeatTutorialScreenTests
    {
        [UnityTest]
        public IEnumerator TenPatternsReachReplayAndStartCleanlyAgain()
        {
            var root = new GameObject("listen-repeat-tutorial-under-test");
            root.SetActive(false);
            root.AddComponent<AudioListener>();
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            root.SetActive(true);
            try
            {
                yield return WaitFor(screen, TutorialPhase.Demonstration, 10f);
                Assert.That(screen.PatternIndex, Is.Zero);
                yield return WaitFor(screen, TutorialPhase.PlayerPerformance, 10f);
                yield return WaitFor(screen, TutorialPhase.Result, 10f);
                Assert.That(screen.LastResult.AccuracyBasisPoints, Is.Zero);
                Assert.That(screen.BrainScale, Is.LessThan(ListenRepeatTutorialFlow.InitialBrainScale));
                for (var index = 1; index < 10; index++)
                {
                    yield return WaitForPattern(screen, index, 5f);
                    yield return WaitFor(screen, TutorialPhase.Result, 15f);
                    yield return WaitFor(screen, TutorialPhase.Transition, 5f);
                }
                Assert.That(screen.PatternIndex, Is.EqualTo(9));
                screen.ReplayTutorial();
                Assert.That(screen.Phase, Is.EqualTo(TutorialPhase.PatternReady));
                Assert.That(screen.PatternIndex, Is.Zero);
                Assert.That(screen.BrainScale, Is.EqualTo(ListenRepeatTutorialFlow.InitialBrainScale));
                yield return WaitFor(screen, TutorialPhase.Demonstration, 5f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CountdownAddsOneFullRestBeatAndDemoHasRoomForAllEightTaps()
        {
            var root = new GameObject("countdown-rest-test");
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            try
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var type = screen.GetType();
                var flow = (ListenRepeatTutorialFlow)type.GetField("flow", flags).GetValue(screen);
                flow.BeginDemonstration();
                type.GetMethod("BeginCountdown", flags).Invoke(screen, null);
                var cue = (double)type.GetField("startCueDsp", flags).GetValue(screen);
                var performance = (double)type.GetField("performanceStartDsp", flags).GetValue(screen);
                var beat = 60.0 / flow.Chart.Stages[0].Bpm;
                Assert.That(performance - cue, Is.EqualTo(2 * beat).Within(0.000001));
                var cueClip = (AudioClip)type.GetField("startClip", flags).GetValue(screen);
                Assert.That(performance - cue - cueClip.length, Is.GreaterThanOrEqualTo(beat));
                var voices = (AudioSource[])type.GetField("demonstrationVoices", flags).GetValue(screen);
                Assert.That(voices.Length, Is.GreaterThanOrEqualTo(8));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static IEnumerator WaitFor(ListenRepeatTutorialScreen screen, TutorialPhase phase, float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (screen.Phase != phase)
            {
                Assert.Less(Time.realtimeSinceStartup - start, seconds,
                    "Timed out waiting for " + phase + " at pattern " + screen.PatternIndex + "; current " + screen.Phase);
                yield return null;
            }
        }

        private static IEnumerator WaitForPattern(ListenRepeatTutorialScreen screen, int index, float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (screen.PatternIndex != index)
            {
                Assert.Less(Time.realtimeSinceStartup - start, seconds, "Timed out waiting for pattern " + index);
                yield return null;
            }
        }
    }
}
