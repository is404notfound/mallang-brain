using System;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Immutable per-run record. A new file is written for every run and never overwritten. A run is only a
    // comparable Gate result when every required field, the scenario/policy hashes, and the raw-sample checksum
    // are present (test-plan E14). gateEligible additionally requires a clean linked build; recording a device
    // measurement is a separate step and a deferred measurement is never treated as a pass.
    [Serializable]
    public sealed class RunRecord
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string runId;
        public string buildId;

        public string scenarioId;
        public string scenarioHash;
        public string policyId;
        public string policyHash;

        public string deviceModel;
        public string os;
        public string tossAppVersion; // empty for Android runs
        public string network;        // wifi / mobile
        public int dprMilli;
        public int refreshHz;
        public string loadState;      // cold / warm
        public string cacheState;     // cleared / warm

        public string[] phaseOrder = Array.Empty<string>();
        public string startedUtc;
        public string endedUtc;

        public string status;         // valid / invalid / failed
        public string statusReason;

        public string[] externalSamplePaths = Array.Empty<string>();
        public string rawChecksum;    // SHA-256 hex of the raw sample bytes

        // Set true only by the recorder when this run is linked to a clean, final-eligible build AND status=valid.
        // Defaults false so an incomplete or diagnostic run cannot masquerade as a Gate pass.
        public bool gateEligible;

        // Measured metrics, filled in by whatever produced this run. Left at zero when a metric was not
        // exercised by that run (e.g. a lifecycle-only run has no crowd/frame data). A run with all zeros is
        // still schema-valid but is not evidence of anything: check frameCount/duplicateCount+perfect+good+miss
        // before reading the metrics as real measurements.
        public double meanFps;
        public double frameP95Ms;
        public double frameP99Ms;
        public int framesOver33Ms;
        public int framesOver50Ms;
        public double longestStallMs;
        public int frameCount;

        public int perfectCount;
        public int goodCount;
        public int missCount;
        public int duplicateCount;

        public long coldStartMs; // 0 when this run did not measure a cold start

        private static readonly string[] AllowedStatuses = { "valid", "invalid", "failed" };

        public bool IsComparableGateResult()
        {
            try { Validate(); return true; } catch (InvalidOperationException) { return false; }
        }

        // Fail closed: throw on any missing required field so an incomplete record cannot be compared.
        public void Validate()
        {
            if (schemaVersion != CurrentSchemaVersion) throw new InvalidOperationException("Unsupported RunRecord schema.");
            Require(runId, nameof(runId));
            Require(buildId, nameof(buildId));
            Require(scenarioId, nameof(scenarioId));
            Require(scenarioHash, nameof(scenarioHash));
            Require(policyId, nameof(policyId));
            Require(policyHash, nameof(policyHash));
            Require(deviceModel, nameof(deviceModel));
            Require(os, nameof(os));
            Require(network, nameof(network));
            Require(loadState, nameof(loadState));
            Require(cacheState, nameof(cacheState));
            Require(startedUtc, nameof(startedUtc));
            Require(endedUtc, nameof(endedUtc));
            Require(rawChecksum, nameof(rawChecksum));
            if (Array.IndexOf(AllowedStatuses, status) < 0)
                throw new InvalidOperationException("RunRecord status must be valid, invalid, or failed.");
            if (phaseOrder == null || phaseOrder.Length == 0)
                throw new InvalidOperationException("RunRecord must record the phase order.");
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing required RunRecord field: " + field);
        }
    }
}
