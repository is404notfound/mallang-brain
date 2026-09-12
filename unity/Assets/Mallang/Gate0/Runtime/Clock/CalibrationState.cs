namespace Mallang.Gate0.Runtime.Clock
{
    // Keeps only the newest calibration. An invalid result must replace an older valid result so a clock
    // failure can never continue judging with stale timing data.
    public sealed class CalibrationState
    {
        public ClockCalibration Current { get; private set; }

        public bool Apply(ClockCalibration next)
        {
            Current = next;
            return next.Valid;
        }
    }
}
