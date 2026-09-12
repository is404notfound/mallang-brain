using System;
using Mallang.Gate0.Runtime.Lifecycle;
using NUnit.Framework;

namespace Mallang.Gate0.Tests.EditMode
{
    // Covers the lifecycle state machine: test-plan P01/P02/P06/P07 logic and E15 (undefined transitions),
    // plus the interrupt coalescing that the Android/WebGL adapters rely on.
    public sealed class LifecycleTests
    {
        private static LifecycleController Booted()
        {
            var c = new LifecycleController();
            c.Fire(LifecycleTrigger.BootSucceeded);
            return c;
        }

        // P01 — normal boot flow reaches Playing after a four-beat count-in.
        [Test] public void BootFlowReachesPlayingAfterFourBeats()
        {
            var c = Booted();
            Assert.AreEqual(GameState.Idle, c.State);
            c.Fire(LifecycleTrigger.Begin);
            Assert.AreEqual(GameState.CountIn, c.State);
            Assert.AreEqual(4, c.BeatsRemaining);
            for (var i = 0; i < 4; i++) c.AdvanceCountInBeat();
            Assert.AreEqual(GameState.Playing, c.State);
        }

        // P02 — input is only live in Playing; count-in taps are not game input.
        [Test] public void CountInDoesNotAcceptGameInput()
        {
            var c = Booted();
            c.Fire(LifecycleTrigger.Begin);
            Assert.IsFalse(c.AcceptsGameInput);
            for (var i = 0; i < 4; i++) c.AdvanceCountInBeat();
            Assert.IsTrue(c.AcceptsGameInput);
        }

        // P06 — an interrupt while Playing flushes, excludes the segment, and resumes with a fresh count-in.
        [Test] public void InterruptFlushesExcludesAndRecountsOnResume()
        {
            var c = Booted();
            c.Fire(LifecycleTrigger.Begin);
            for (var i = 0; i < 4; i++) c.AdvanceCountInBeat();
            var flushed = 0;
            c.FlushRequested += () => flushed++;

            c.Fire(LifecycleTrigger.Interrupt);
            Assert.AreEqual(GameState.Interrupted, c.State);
            Assert.AreEqual(1, flushed);
            Assert.AreEqual(1, c.ExcludedSegments);

            c.Fire(LifecycleTrigger.Resume);
            Assert.AreEqual(GameState.Recalibrating, c.State);
            c.Fire(LifecycleTrigger.RecalibrateSucceeded);
            Assert.AreEqual(GameState.CountIn, c.State);
            Assert.AreEqual(4, c.BeatsRemaining); // recount starts over
        }

        // A repeated/continuous interrupt does not flush or exclude twice.
        [Test] public void RepeatedInterruptDoesNotDoubleCount()
        {
            var c = Booted();
            c.Fire(LifecycleTrigger.Begin);
            var flushed = 0; c.FlushRequested += () => flushed++;
            c.Fire(LifecycleTrigger.Interrupt);
            c.Fire(LifecycleTrigger.Interrupt);
            Assert.AreEqual(GameState.Interrupted, c.State);
            Assert.AreEqual(1, flushed);
            Assert.AreEqual(1, c.ExcludedSegments);
        }

        // Re-interrupt during the resume path cancels recalibration and returns to Interrupted.
        [Test] public void ReInterruptDuringRecalibratingReturnsToInterrupted()
        {
            var c = Booted();
            c.Fire(LifecycleTrigger.Begin);
            c.Fire(LifecycleTrigger.Interrupt);
            c.Fire(LifecycleTrigger.Resume);
            Assert.AreEqual(GameState.Recalibrating, c.State);
            c.Fire(LifecycleTrigger.Interrupt);
            Assert.AreEqual(GameState.Interrupted, c.State);
        }

        // P07 — audio reinit failure holds in Error; retry recovers once audio is ready.
        [Test] public void AudioFailureHoldsErrorThenRetryRecovers()
        {
            var c = Booted();
            c.Fire(LifecycleTrigger.Begin);
            c.Fire(LifecycleTrigger.Interrupt);
            c.Fire(LifecycleTrigger.Resume);
            c.Fire(LifecycleTrigger.RecalibrateFailed);
            Assert.AreEqual(GameState.Error, c.State);
            c.Fire(LifecycleTrigger.Retry);
            Assert.AreEqual(GameState.Recalibrating, c.State);
            c.Fire(LifecycleTrigger.RecalibrateSucceeded);
            Assert.AreEqual(GameState.CountIn, c.State);
        }

        // E15 — a transition outside the table is rejected (fail closed).
        [Test] public void UndefinedTransitionThrows()
        {
            var c = Booted();
            Assert.Throws<InvalidOperationException>(() => c.Fire(LifecycleTrigger.Finish));
            Assert.IsFalse(c.TryFire(LifecycleTrigger.Finish));
            Assert.AreEqual(GameState.Idle, c.State);
        }

        // The driver runs the full resume path: audio-not-ready → Error, later retry → CountIn.
        [Test] public void DriverResumeWithoutAudioGoesToErrorThenRetry()
        {
            var c = Booted();
            var driver = new LifecycleDriver(c);
            c.Fire(LifecycleTrigger.Begin);
            driver.SetSuspended(true);
            Assert.AreEqual(GameState.Interrupted, c.State);
            driver.SetSuspended(false, audioReady: false);
            Assert.AreEqual(GameState.Error, c.State);
            driver.Retry(audioReady: true);
            Assert.AreEqual(GameState.CountIn, c.State);
        }

        // Overlapping/reversed suspend signals collapse to one Interrupt and one Resume.
        [Test] public void CoalescerCollapsesOverlappingSignals()
        {
            var coalescer = new InterruptCoalescer();
            Assert.AreEqual(InterruptCoalescer.Signal.Interrupt, coalescer.Update(true));
            Assert.AreEqual(InterruptCoalescer.Signal.None, coalescer.Update(true));
            Assert.AreEqual(InterruptCoalescer.Signal.Resume, coalescer.Update(false));
            Assert.AreEqual(InterruptCoalescer.Signal.None, coalescer.Update(false));
        }
    }
}
