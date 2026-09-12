using UnityEngine;

namespace Mallang.Gate0.Runtime.Clock
{
    // Two timelines Gate 0 must reconcile. RealtimeSeconds shares InputEvent.time's clock
    // (Time.realtimeSinceStartup). DspSeconds is the audio schedule clock (AudioSettings.dspTime).
    // Judgement converts an input on the realtime timeline into the DSP timeline via ClockCalibration.
    public interface IRhythmClock
    {
        double RealtimeSeconds { get; }
        double DspSeconds { get; }
    }

    // Production clock. Reads Unity's real clocks; never used in Edit Mode timing tests.
    public sealed class SystemRhythmClock : IRhythmClock
    {
        public double RealtimeSeconds => Time.realtimeSinceStartupAsDouble;
        public double DspSeconds => AudioSettings.dspTime;
    }

    // Deterministic clock for Edit Mode. The test sets both timelines and can jump either one
    // to reproduce forward jumps, backward (rewound) clocks, and calibration drift.
    public sealed class ManualRhythmClock : IRhythmClock
    {
        public double RealtimeSeconds { get; private set; }
        public double DspSeconds { get; private set; }

        public ManualRhythmClock(double realtime = 0, double dsp = 0) { RealtimeSeconds = realtime; DspSeconds = dsp; }

        // Advance both timelines by the same amount, the normal case with no drift.
        public void Advance(double seconds) { RealtimeSeconds += seconds; DspSeconds += seconds; }

        // Advance each timeline independently to inject drift, jumps, or a rewound clock.
        public void AdvanceRealtime(double seconds) => RealtimeSeconds += seconds;
        public void AdvanceDsp(double seconds) => DspSeconds += seconds;
        public void Set(double realtime, double dsp) { RealtimeSeconds = realtime; DspSeconds = dsp; }
    }
}
