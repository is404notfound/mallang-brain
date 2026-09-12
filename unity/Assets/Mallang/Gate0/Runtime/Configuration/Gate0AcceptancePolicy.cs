using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Configuration
{
    [CreateAssetMenu(menuName = "Mallang/Gate0/Acceptance Policy")]
    public sealed class Gate0AcceptancePolicy : ScriptableObject
    {
        [Serializable] public sealed class Device
        {
            public string role, model, os, network, cacheMode;
            public int refreshHz, width, height, dprMilli, qualityLevel;
        }
        [Serializable] public sealed class Specification
        {
            public int schemaVersion = 1;
            public string policyId = "gate0-original-thresholds-v1";
            public int validRuns = 5, clockErrorUs = 5000, androidColdMs = 5000, tossColdMs = 10000;
            public int meanFps = 50, tapP95Us = 60000, avOffsetUs = 60000, avDriftUs = 20000;
            public int avJitterP95Us = 20000, internalExternalGapUs = 16700;
            public int externalFps = 240, tapsAtStart = 20, tapsAtEnd = 20, avAtStart = 10, avAtEnd = 10;
            public string[] invalidationReasons = { "call", "notification-overlay", "manual-input-error", "network-loss", "lifecycle-interrupt", "clock-error" };
            public Device[] devices = Array.Empty<Device>();
        }
        public Specification specification = new Specification();
        public string contentHash;
        public bool frozen;
        public string ComputeHash() => ContractHash.Of(specification);

        public void Validate(bool requireFrozen = true)
        {
            var s = specification;
            if (s == null || s.schemaVersion != 1 || string.IsNullOrWhiteSpace(s.policyId))
                throw new InvalidOperationException("Missing or unsupported policy schema.");
            // Existing acceptance values are locked, including sample counts. A policy change requires a reviewed version.
            if (s.validRuns != 5 || s.clockErrorUs != 5000 || s.androidColdMs != 5000 || s.tossColdMs != 10000
                || s.meanFps != 50 || s.tapP95Us != 60000 || s.avOffsetUs != 60000 || s.avDriftUs != 20000
                || s.avJitterP95Us != 20000 || s.internalExternalGapUs != 16700 || s.externalFps != 240
                || s.tapsAtStart != 20 || s.tapsAtEnd != 20 || s.avAtStart != 10 || s.avAtEnd != 10)
                throw new InvalidOperationException("Acceptance thresholds/sample counts differ from reviewed Gate 0 policy.");
            var reasons = new[] { "call", "notification-overlay", "manual-input-error", "network-loss", "lifecycle-interrupt", "clock-error" };
            if (s.invalidationReasons == null || string.Join("|", s.invalidationReasons) != string.Join("|", reasons))
                throw new InvalidOperationException("Invalidation rules differ from reviewed policy.");
            var roles = new HashSet<string>();
            if (s.devices == null) throw new InvalidOperationException("Device matrix missing.");
            foreach (var d in s.devices)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.role) || !roles.Add(d.role)
                    || string.IsNullOrWhiteSpace(d.model) || string.IsNullOrWhiteSpace(d.os)
                    || string.IsNullOrWhiteSpace(d.network) || string.IsNullOrWhiteSpace(d.cacheMode)
                    || d.refreshHz <= 0 || d.width <= 0 || d.height <= 0 || d.dprMilli <= 0 || d.qualityLevel < 0)
                    throw new InvalidOperationException("Incomplete or duplicate device condition.");
                if (d.role == "toss-gate" && (d.network != "wifi" || d.cacheMode != "cleared"))
                    throw new InvalidOperationException("Toss Gate requires cache-cleared Wi-Fi.");
                if (d.role == "toss-diagnostic-warm" && d.cacheMode != "warm")
                    throw new InvalidOperationException("Warm diagnostic requires warm cache.");
                if (d.role == "toss-diagnostic-mobile" && d.network != "mobile")
                    throw new InvalidOperationException("Mobile diagnostic requires mobile network.");
            }
            foreach (var role in new[] { "android-gate-mid", "android-diagnostic-low", "android-diagnostic-high", "toss-gate", "toss-diagnostic-warm", "toss-diagnostic-mobile" })
                if (!roles.Contains(role)) throw new InvalidOperationException("Unresolved device condition: " + role);
            if (requireFrozen && (!frozen || string.IsNullOrEmpty(contentHash) || contentHash != ComputeHash()))
                throw new InvalidOperationException("Policy is not frozen or hash mismatch; measurement blocked.");
        }
        public void Freeze() { Validate(false); contentHash = ComputeHash(); frozen = true; }
        public static void RequireMeasurementReady(Gate0Scenario scenario, Gate0AcceptancePolicy policy)
        {
            if (scenario == null || policy == null) throw new InvalidOperationException("Required measurement asset missing; no fallback.");
            scenario.Validate(); policy.Validate();
        }
    }
}
