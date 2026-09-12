using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Configuration
{
    [CreateAssetMenu(menuName = "Mallang/Gate0/Scenario")]
    public sealed class Gate0Scenario : ScriptableObject
    {
        [Serializable] public sealed class Phase
        {
            public string id;
            public int durationMs = 180000, crowdCount, animatorCount, spriteLayers = 2;
            public int vfxSpawnCount, eliminationCount, audioVoiceCount = 1, uiUpdatesPerBeat = 4;
            public long[] syntheticInputUs;
        }
        [Serializable] public sealed class Specification
        {
            public int schemaVersion = 1;
            public string scenarioId = "gate0-nine-workloads-v1";
            public int seed = 20260911, logicalWidth = 960, logicalHeight = 540, qualityLevel;
            public int targetRefreshHz = 60;
            public Phase[] phases;
        }
        public Specification specification = new Specification();
        public string contentHash;

        public string ComputeHash() => ContractHash.Of(specification);
        public void Seal() { Validate(false); contentHash = ComputeHash(); }
        public void Validate(bool verifyHash = true)
        {
            var s = specification;
            if (s == null || s.schemaVersion != 1 || string.IsNullOrWhiteSpace(s.scenarioId))
                throw new InvalidOperationException("Missing or unsupported scenario schema.");
            if (s.logicalWidth <= 0 || s.logicalHeight <= 0 || s.qualityLevel < 0 || s.targetRefreshHz <= 0)
                throw new InvalidOperationException("Invalid scenario display settings.");
            if (s.phases == null || s.phases.Length != 9) throw new InvalidOperationException("Nine workload phases required.");
            var ids = new HashSet<string>();
            for (var i = 0; i < 9; i++)
            {
                var p = s.phases[i];
                var crowd = new[] { 30, 60, 100 }[i / 3];
                var kind = new[] { "idle", "simultaneous_vfx", "mass_elimination" }[i % 3];
                if (p == null || p.id != crowd + "-" + kind || !ids.Add(p.id) || p.durationMs < 180000
                    || p.crowdCount != crowd || p.animatorCount != crowd || p.spriteLayers != 2
                    || p.audioVoiceCount != 1 || p.uiUpdatesPerBeat != 4
                    || p.vfxSpawnCount != (i % 3 == 1 ? crowd : 0)
                    || p.eliminationCount != (i % 3 == 2 ? crowd : 0))
                    throw new InvalidOperationException("Incomplete or inconsistent workload phase " + i);
                if (p.syntheticInputUs == null || p.syntheticInputUs.Length == 0)
                    throw new InvalidOperationException("Synthetic input schedule missing.");
                long previous = -1;
                foreach (var time in p.syntheticInputUs)
                {
                    if (time < 0 || time <= previous || time >= (long)p.durationMs * 1000)
                        throw new InvalidOperationException("Input timestamps must be ordered and inside phase.");
                    previous = time;
                }
            }
            if (verifyHash && (string.IsNullOrEmpty(contentHash) || contentHash != ComputeHash()))
                throw new InvalidOperationException("Scenario hash mismatch; do not combine runs.");
        }

        public void CreateDraft(int seed)
        {
            specification = new Specification { seed = seed, phases = new Phase[9] };
            uint random = unchecked((uint)seed);
            if (random == 0) random = 0x9e3779b9u;
            for (var i = 0; i < 9; i++)
            {
                var crowd = new[] { 30, 60, 100 }[i / 3];
                var kind = new[] { "idle", "simultaneous_vfx", "mass_elimination" }[i % 3];
                var times = new long[359];
                for (var n = 0; n < times.Length; n++)
                {
                    // Defined integer PRNG, independent of UnityEngine.Random global state.
                    random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                    times[n] = (n + 1L) * 500000 + (long)(random % 20001) - 10000;
                }
                specification.phases[i] = new Phase { id = crowd + "-" + kind, crowdCount = crowd,
                    animatorCount = crowd, vfxSpawnCount = i % 3 == 1 ? crowd : 0, audioVoiceCount = 1,
                    eliminationCount = i % 3 == 2 ? crowd : 0, syntheticInputUs = times };
            }
            Seal();
        }
    }

    internal static class ContractHash
    {
        // Explicit serializable DTOs: no asset path, Unity instance ID, or stored hash in payload.
        public static string Of(object payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
