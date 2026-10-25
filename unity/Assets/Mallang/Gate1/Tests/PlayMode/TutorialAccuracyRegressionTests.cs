using System;
using System.Reflection;
using Mallang.Gate1.Runtime;
using Mallang.RhythmCore;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate1.Tests.PlayMode
{
    public sealed class TutorialAccuracyRegressionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TouchFromBeforeLatestFrameStillScoresAtItsOriginalTime()
        {
            var root = new GameObject("delayed-input-test");
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            try
            {
                var type = screen.GetType();
                var flow = (ListenRepeatTutorialFlow)type.GetField("flow", Private).GetValue(screen);
                flow.BeginDemonstration(); flow.BeginCountdown(); flow.BeginPerformance();
                var start = Time.realtimeSinceStartupAsDouble - .02;
                var dspStart = AudioSettings.dspTime - .02;
                type.GetField("performanceStartDsp", Private).SetValue(screen, dspStart);
                var calibration = type.GetField("calibration", Private);
                calibration.SetValue(screen, Activator.CreateInstance(calibration.FieldType, new object[] { dspStart - start, 0.0, start, 160 }));
                type.GetMethod("Update", Private).Invoke(screen, null);
                type.GetMethod("Press", Private).Invoke(screen, new object[] { Hand.Left, 31, start });
                type.GetMethod("FlushPerformanceInputs", Private)?.Invoke(screen, null);
                flow.Finish(null);
                Assert.That(flow.Result.EndReason, Is.Not.EqualTo(RunEndReason.Invalid));
                Assert.That(flow.Result.AccuracyBasisPoints, Is.EqualTo(5000), "One perfect note out of two must score 50%, even if delivered after a newer frame.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void InstrumentSoundsPlayInEveryPhaseWithoutScoringOutsidePerformance()
        {
            var root = new GameObject("free-instrument-test");
            var screen = root.AddComponent<ListenRepeatTutorialScreen>();
            try
            {
                var type = screen.GetType();
                var flow = (ListenRepeatTutorialFlow)type.GetField("flow", Private).GetValue(screen);
                for (var phase = 0; phase < 6; phase++)
                {
                    foreach (var hand in new[] { Hand.Left, Hand.Right })
                    {
                        var source = (AudioSource)type.GetField(hand == Hand.Left ? "leftPlayer" : "rightPlayer", Private).GetValue(screen);
                        source.Stop();
                        type.GetMethod("Press", Private).Invoke(screen, new object[] { hand, 40, Time.realtimeSinceStartupAsDouble });
                        Assert.That(source.isPlaying, Is.True, "Instrument must sound during " + flow.Phase);
                        type.GetMethod("Release", Private).Invoke(screen, new object[] { 40, Time.realtimeSinceStartupAsDouble, false });
                    }
                    if (phase == 0) flow.BeginDemonstration();
                    else if (phase == 1) flow.BeginCountdown();
                    else if (phase == 2) flow.BeginPerformance();
                    else if (phase == 3) flow.Finish(null);
                    else if (phase == 4) flow.BeginTransition();
                    if (phase < 3) Assert.That(flow.CurrentSnapshot.AccuracyBasisPoints, Is.Zero);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
