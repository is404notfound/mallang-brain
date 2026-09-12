namespace Mallang.Gate0.Runtime.Lifecycle
{
    // Collapses noisy platform signals into a single edge. Android delivers overlapping and reversed
    // pause/focus callbacks; WebGL delivers separate visibility and AudioContext events. Each adapter folds
    // its signals into one "suspended" boolean and feeds it here, so the controller sees exactly one
    // Interrupt on the first suspend and one Resume on the return — never a double count.
    public sealed class InterruptCoalescer
    {
        public enum Signal { None, Interrupt, Resume }

        private bool suspended;
        public bool IsSuspended => suspended;

        public Signal Update(bool nowSuspended)
        {
            if (nowSuspended == suspended) return Signal.None;
            suspended = nowSuspended;
            return nowSuspended ? Signal.Interrupt : Signal.Resume;
        }
    }
}
