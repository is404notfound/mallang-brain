using System.Collections;
using System.Reflection;
using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Mallang.Gate1.Tests.PlayMode
{
    public sealed class TutorialAndroidTouchTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void NativeTouchEventsReachBothInputSoundsAndAllowRepeatedTaps()
        {
            var device = InputSystem.AddDevice<Touchscreen>();
            var root = new GameObject("android-touch-regression");
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            try
            {
                var type = screen.GetType();
                var flow = (ListenRepeatTutorialFlow)type.GetField("flow", Fields).GetValue(screen);
                var traces = (IList)type.GetField("tapTraces", Fields).GetValue(screen);
                var safe = Screen.safeArea;
                var left = new Vector2(safe.xMin + safe.width * .15f, safe.center.y);
                var right = new Vector2(safe.xMin + safe.width * .85f, safe.center.y);
                Send(device, 71, left, TouchPhase.Began);
                Send(device, 71, left, TouchPhase.Ended);
                Assert.That(traces.Count, Is.Zero);
                flow.BeginDemonstration();
                Send(device, 71, left, TouchPhase.Began);
                Send(device, 71, left, TouchPhase.Ended);
                Assert.That(traces.Count, Is.Zero);
                flow.BeginCountdown();
                Send(device, 71, left, TouchPhase.Began);
                Send(device, 71, left, TouchPhase.Ended);
                Assert.That(traces.Count, Is.Zero);
                flow.BeginPerformance();
                // Queue both hands in the same update, as native Android TouchState events.
                InputSystem.QueueStateEvent(device, new TouchState { touchId = 71, position = left, phase = TouchPhase.Began });
                InputSystem.QueueStateEvent(device, new TouchState { touchId = 72, position = right, phase = TouchPhase.Began });
                InputSystem.Update();
                Assert.That(traces.Count, Is.EqualTo(2), "Native touch must reach Press for each hand.");
                Assert.That(traces[0].GetType().GetField("Hand").GetValue(traces[0]), Is.EqualTo(Hand.Left));
                Assert.That(traces[1].GetType().GetField("Hand").GetValue(traces[1]), Is.EqualTo(Hand.Right));
                Assert.That(((AudioSource)type.GetField("leftPlayer", Fields).GetValue(screen)).isPlaying, Is.True, "Left input must start its audio source.");
                Assert.That(((AudioSource)type.GetField("rightPlayer", Fields).GetValue(screen)).isPlaying, Is.True, "Right input must start its audio source.");
                Send(device, 71, left, TouchPhase.Ended);
                Send(device, 72, right, TouchPhase.Canceled);
                Send(device, 71, left, TouchPhase.Began);
                Send(device, 72, right, TouchPhase.Began);
                Assert.That(traces.Count, Is.EqualTo(4), "Release and cancellation must allow another tap.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                InputSystem.RemoveDevice(device);
            }
        }

        private static void Send(Touchscreen device, int id, Vector2 position, TouchPhase phase)
        {
            InputSystem.QueueStateEvent(device, new TouchState { touchId = id, position = position, phase = phase });
            InputSystem.Update();
        }
    }
}
