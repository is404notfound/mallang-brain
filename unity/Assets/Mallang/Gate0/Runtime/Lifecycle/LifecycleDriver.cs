namespace Mallang.Gate0.Runtime.Lifecycle
{
    // Bridges a coalesced "suspended" signal to the LifecycleController and runs the resume path:
    // recalibrate the clock and, when audio is ready, start a fresh four-beat count-in; otherwise hold in
    // Error until a retry. Both platform adapters share this so resume behaviour is identical.
    public sealed class LifecycleDriver
    {
        private readonly LifecycleController controller;
        private readonly InterruptCoalescer coalescer = new InterruptCoalescer();

        public LifecycleDriver(LifecycleController controller) => this.controller = controller;

        public LifecycleController Controller => controller;
        public bool IsSuspended => coalescer.IsSuspended;

        // Feed the folded platform signal. audioReady reflects whether the audio clock can be recalibrated now
        // (Android: normally true on resume; WebGL: only once the AudioContext is running again).
        public void SetSuspended(bool suspended, bool audioReady = true)
        {
            switch (coalescer.Update(suspended))
            {
                case InterruptCoalescer.Signal.Interrupt:
                    controller.TryFire(LifecycleTrigger.Interrupt);
                    break;
                case InterruptCoalescer.Signal.Resume:
                    Resume(audioReady);
                    break;
            }
        }

        // Retry after audio failed to reinitialise (Error → Recalibrating → CountIn or back to Error).
        public void Retry(bool audioReady)
        {
            if (controller.State == GameState.Error) controller.Fire(LifecycleTrigger.Retry);
            Recalibrate(audioReady);
        }

        private void Resume(bool audioReady)
        {
            if (controller.State == GameState.Interrupted) controller.Fire(LifecycleTrigger.Resume);
            Recalibrate(audioReady);
        }

        private void Recalibrate(bool audioReady)
        {
            if (controller.State != GameState.Recalibrating) return;
            controller.Fire(audioReady ? LifecycleTrigger.RecalibrateSucceeded : LifecycleTrigger.RecalibrateFailed);
        }
    }
}
