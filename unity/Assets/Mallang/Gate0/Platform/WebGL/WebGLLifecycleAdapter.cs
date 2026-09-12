using Mallang.Gate0.Runtime.Lifecycle;
using UnityEngine;

namespace Mallang.Gate0.Platform.WebGL
{
    // Translates two independent web signals into the common lifecycle:
    //   - document visibility (hidden tab)
    //   - Web Audio AudioContext suspend/resume
    // Either one suspends the game. On return, the count-in only restarts once audio is actually running; if the
    // tab is visible again but the AudioContext is still suspended, recalibration fails and the game holds in
    // Error until CallRetry() succeeds. The real build wires SetVisibility / SetAudioContextRunning to a JS
    // plugin; kept as plain methods here so lifecycle behaviour is testable without the browser.
    public sealed class WebGLLifecycleAdapter : MonoBehaviour
    {
        private bool visible = true;
        private bool audioRunning = true;

        public LifecycleController Controller { get; private set; }
        public LifecycleDriver Driver { get; private set; }

        private void Awake()
        {
            Controller = new LifecycleController();
            Driver = new LifecycleDriver(Controller);
            Controller.Fire(LifecycleTrigger.BootSucceeded); // Boot → Idle
        }

        public void SetVisibility(bool isVisible) { visible = isVisible; Apply(); }
        public void SetAudioContextRunning(bool isRunning) { audioRunning = isRunning; Apply(); }

        // Retry after an audio-context reinitialisation failure left the game in Error.
        public void CallRetry() => Driver.Retry(audioRunning);

        private void Apply() => Driver.SetSuspended(!visible || !audioRunning, audioReady: audioRunning);
    }
}
