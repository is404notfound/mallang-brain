using System;
using Mallang.Gate0.Runtime.Lifecycle;
using UnityEngine;

namespace Mallang.Gate0.Platform.Android
{
    // Translates Android's OnApplicationPause / OnApplicationFocus callbacks into one coalesced Interrupt/Resume.
    // Android can deliver these overlapping or in reverse order; folding both into a single "suspended" boolean
    // means one interrupt on the way out and one four-beat recount on the way back — never a double recount.
    public sealed class AndroidLifecycleAdapter : MonoBehaviour
    {
        private bool paused;
        private bool focused = true;

        public LifecycleController Controller { get; private set; }
        public LifecycleDriver Driver { get; private set; }

        // Whether the audio device is ready to recalibrate on resume. Defaults to available; a harness can
        // point this at the real AudioSource/DSP readiness check.
        public Func<bool> AudioReady = () => true;

        private void Awake()
        {
            Controller = new LifecycleController();
            Driver = new LifecycleDriver(Controller);
            Controller.Fire(LifecycleTrigger.BootSucceeded); // Boot → Idle
        }

        private void OnApplicationPause(bool value) { paused = value; Apply(); }
        private void OnApplicationFocus(bool value) { focused = value; Apply(); }

        private void Apply() => Driver.SetSuspended(paused || !focused, AudioReady());
    }
}
