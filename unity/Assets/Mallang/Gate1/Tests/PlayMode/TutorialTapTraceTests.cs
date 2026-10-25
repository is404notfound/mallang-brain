using System.Collections;
using System.Reflection;
using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate1.Tests.PlayMode
{
    public sealed class TutorialTapTraceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void TracesUseRawInputTimeForBothHandsAndResetBetweenPatterns()
        {
            var root = new GameObject("tap-trace-test");
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            try
            {
                var type = screen.GetType();
                var press = type.GetMethod("Press", PrivateInstance);
                var release = type.GetMethod("Release", PrivateInstance);
                var traces = (IList)type.GetField("tapTraces", PrivateInstance).GetValue(screen);
                var flow = (ListenRepeatTutorialFlow)type.GetField("flow", PrivateInstance).GetValue(screen);
                press.Invoke(screen, new object[] { Hand.Left, 71, 100.123 });
                Assert.That(traces.Count, Is.Zero, "PatternReady input must not leave a trace.");
                release.Invoke(screen, new object[] { 71, 100.124, false });
                flow.BeginDemonstration();
                press.Invoke(screen, new object[] { Hand.Left, 71, 100.123 });
                Assert.That(traces.Count, Is.Zero, "Demonstration input must not leave a trace.");
                release.Invoke(screen, new object[] { 71, 100.124, false });
                flow.BeginCountdown();
                press.Invoke(screen, new object[] { Hand.Left, 71, 100.123 });
                Assert.That(traces.Count, Is.Zero, "Countdown input must not leave a trace.");
                release.Invoke(screen, new object[] { 71, 100.124, false });
                flow.BeginPerformance();
                // No frames advance in this test. Offset is zero; calibration affects judgement only.
                type.GetField("performanceStartDsp", PrivateInstance).SetValue(screen, 100.0);
                type.GetField("userCalibrationSeconds", PrivateInstance).SetValue(screen, 0.05);
                press.Invoke(screen, new object[] { Hand.Left, 71, 100.123 });
                press.Invoke(screen, new object[] { Hand.Right, 72, 100.123 });
                Assert.That(traces.Count, Is.EqualTo(2), "Simultaneous hands need independent traces.");
                var traceType = traces[0].GetType();
                var expected = 123000f / flow.PatternDurationUs;
                Assert.That((float)traceType.GetField("Progress").GetValue(traces[0]), Is.EqualTo(expected).Within(0.00001f),
                    "Trace must use input position, not a target note or user timing correction.");
                Assert.That(traceType.GetField("Hand").GetValue(traces[0]), Is.EqualTo(Hand.Left));
                Assert.That(traceType.GetField("Hand").GetValue(traces[1]), Is.EqualTo(Hand.Right));
                press.Invoke(screen, new object[] { Hand.Left, 71, 100.124 });
                Assert.That(traces.Count, Is.EqualTo(2), "Duplicate held pointer must not create a second tap.");
                type.GetMethod("ResetForPattern", PrivateInstance).Invoke(screen, null);
                Assert.That(traces.Count, Is.Zero, "Pattern changes must clear old visual traces.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
