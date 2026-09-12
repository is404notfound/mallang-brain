namespace Mallang.Gate0.Runtime.Input
{
    // Accumulates RawTimingProbe results for one phase/run so the totals can be shown on screen live and
    // folded into a RunRecord at phase end. Technical-validation counts only, never a game score.
    public sealed class JudgementTally
    {
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Duplicate { get; private set; }
        public int TotalJudged => Perfect + Good + Miss;

        public void Add(RawTimingProbe.Result result)
        {
            if (!result.Accepted) { Duplicate++; return; }
            switch (result.Band)
            {
                case JudgementBand.Perfect: Perfect++; break;
                case JudgementBand.Good: Good++; break;
                default: Miss++; break;
            }
        }
    }
}
