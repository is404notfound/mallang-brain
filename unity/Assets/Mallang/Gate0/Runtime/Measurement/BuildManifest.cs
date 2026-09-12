using System;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Immutable per-build record, written once and never modified. A final Gate-pass build must come from a
    // known commit with a clean working tree and carry every checksum (test-plan E13). A dirty build may be used
    // for diagnostics but can never be a final-pass candidate.
    [Serializable]
    public sealed class BuildManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string buildId;
        public string sourceCommit;
        public bool dirty;

        public string unityVersion;
        public string inputSystemVersion;
        public string aitSdkVersion;

        public string buildTarget;      // Android / WebGL
        public string buildProfile;
        public string scriptingBackend;
        public int qualityLevel;
        public int logicalWidth;
        public int logicalHeight;

        public string scenarioId;
        public int scenarioSchema;
        public string scenarioHash;
        public string policyId;
        public int policySchema;
        public string policyHash;

        public string artifactSha256;
        public string buildReportSha256;

        public string buildTimestampUtc;
        public string buildCommand;
        public string buildResult;      // Succeeded / Failed

        // Final Gate pass requires a clean, known, successful build with both checksums present.
        public bool IsFinalGateEligible =>
            !dirty
            && !string.IsNullOrWhiteSpace(sourceCommit)
            && sourceCommit != "unknown"
            && scenarioSchema > 0
            && policySchema > 0
            && !IsPlaceholder(scenarioId)
            && !IsPlaceholder(scenarioHash)
            && !IsPlaceholder(policyId)
            && !IsPlaceholder(policyHash)
            && !string.IsNullOrWhiteSpace(artifactSha256)
            && !string.IsNullOrWhiteSpace(buildReportSha256)
            && string.Equals(buildResult, "Succeeded", StringComparison.Ordinal);

        private static bool IsPlaceholder(string value) =>
            string.IsNullOrWhiteSpace(value) || value.StartsWith("unset-", StringComparison.Ordinal);

        // Fail closed on missing required build metadata.
        public void Validate()
        {
            if (schemaVersion != CurrentSchemaVersion) throw new InvalidOperationException("Unsupported BuildManifest schema.");
            Require(buildId, nameof(buildId));
            Require(sourceCommit, nameof(sourceCommit));
            Require(unityVersion, nameof(unityVersion));
            Require(buildTarget, nameof(buildTarget));
            Require(scenarioHash, nameof(scenarioHash));
            Require(policyHash, nameof(policyHash));
            Require(buildTimestampUtc, nameof(buildTimestampUtc));
            Require(buildResult, nameof(buildResult));
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing required BuildManifest field: " + field);
        }
    }
}
