using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mallang.Gate0.Runtime.Smoke
{
    // T2 launch probe only. Counts are input observations, never game scores.
    [RequireComponent(typeof(Camera), typeof(AudioSource))]
    public sealed class SmokeTapScene : MonoBehaviour
    {
        public int LeftTaps { get; private set; }
        public int RightTaps { get; private set; }
        public string AudioStatus { get; private set; } = "Tap to request test tone";
        public bool Suspended => paused || !focused;
        public Color CurrentColor => view.backgroundColor;
        private Camera view;
        private AudioSource source;
        private AudioClip tone;
        private bool paused;
        private bool focused = true;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private static readonly Color IdleColor = new Color(0.10f, 0.15f, 0.21f);

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = IdleColor;
            view.orthographic = true;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = 0.3f;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Update()
        {
            if (Suspended) return;
            var hasTouch = false;
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    hasTouch = true;
                    Tap(touch.position.ReadValue().x < Screen.width * 0.5f);
                }
            }
            // Avoid the synthetic mouse event produced by a touchscreen.
            if (!hasTouch && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Tap(Mouse.current.position.ReadValue().x < Screen.width * 0.5f);
            if (Keyboard.current == null) return;
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) Tap(true);
            if (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) Tap(false);
        }

        public void Tap(bool left)
        {
            if (Suspended) return;
            if (left) LeftTaps++; else RightTaps++;
            var odd = (LeftTaps + RightTaps) % 2 == 1;
            view.backgroundColor = left
                ? (odd ? new Color(.12f, .39f, .36f) : new Color(.08f, .28f, .26f))
                : (odd ? new Color(.43f, .26f, .16f) : new Color(.32f, .18f, .12f));
            try
            {
                if (tone == null) tone = CreateTestTone();
                source.PlayOneShot(tone);
                AudioStatus = "Tone requested - confirm sound on device";
            }
            catch (Exception error)
            {
                AudioStatus = "Audio unavailable - tap to retry: " + error.Message;
                Debug.LogWarning(AudioStatus);
            }
        }

        public static AudioClip CreateTestTone()
        {
            const int sampleRate = 44100;
            var samples = new float[sampleRate / 5];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var attack = Mathf.Min(1, time / .004f);
                var release = Mathf.Min(1, (samples.Length - 1 - i) / (sampleRate * .01f));
                samples[i] = .5f * attack * release * Mathf.Exp(-time * 22)
                    * (Mathf.Sin(2 * Mathf.PI * 523.25f * time) + .22f * Mathf.Sin(2 * Mathf.PI * 1569.75f * time));
            }
            var clip = AudioClip.Create("Generated T2 test tone (not final marimba)", samples.Length, 1, sampleRate, false);
            if (!clip.SetData(samples, 0))
            {
                Destroy(clip);
                throw new InvalidOperationException("Could not initialize test tone");
            }
            return clip;
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (Suspended && source != null) source.Stop();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (Suspended && source != null) source.Stop();
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 960f, Screen.height / 540f, 1));
            GUI.Label(new Rect(30, 42, 900, 48), "MALLANG / GATE 0", titleStyle);
            GUI.Label(new Rect(30, 94, 900, 34), "DEVICE LAUNCH CHECK - NOT THE GAME", smallStyle);
            GUI.Label(new Rect(20, 185, 460, 75), "LEFT / A\n" + LeftTaps + " taps", textStyle);
            GUI.Label(new Rect(480, 185, 460, 75), "RIGHT / L\n" + RightTaps + " taps", textStyle);
            GUI.Label(new Rect(30, 340, 900, 50), Suspended ? "Paused" : AudioStatus, smallStyle);
            GUI.Label(new Rect(30, 404, 900, 50), "Every tap changes the background and requests a short test tone.\nTiming, performance and Toss device verification are still pending.", smallStyle);
            GUI.Label(new Rect(30, 480, 900, 30), Application.platform + " / " + Screen.width + " x " + Screen.height, smallStyle);
            GUI.matrix = oldMatrix;
        }

        private void OnDestroy()
        {
            if (tone != null) Destroy(tone);
        }
    }
}
