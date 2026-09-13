using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Collects one manual run or one nine-phase automatic run. A storage failure is kept in memory and
    // returned to the caller so the automatic measurement cannot look successful when its evidence is gone.
    public sealed class MeasurementBatch
    {
        [Serializable]
        public sealed class Entry
        {
            public string phaseId;
            public string storageStatus; // saved / failed
            public string failureReason;
            public RunRecord record;
            public string rawSamplesJson;
        }

        [Serializable]
        private sealed class Bundle
        {
            public int schemaVersion = 1;
            public string sessionId;
            public string buildId;
            public string platform;
            public string startedUtc;
            public string updatedUtc;
            public int expectedPhaseCount;
            public int savedCount;
            public int invalidCount;
            public int failedCount;
            public List<Entry> entries = new List<Entry>();
        }

        private readonly string rootDirectory;
        private readonly Action<string, RunRecord, byte[]> writeRun;
        private readonly Bundle bundle;

        public string SessionId => bundle.sessionId;
        public string BundlePath => Path.Combine(rootDirectory, "Gate0RunBundle.json");
        public int ExpectedPhaseCount => bundle.expectedPhaseCount;
        public int SavedCount => bundle.savedCount;
        public int InvalidCount => bundle.invalidCount;
        public int FailedCount => bundle.failedCount;
        public int AttemptedCount => bundle.entries.Count;

        public MeasurementBatch(
            string runsRoot,
            int expectedPhaseCount,
            string buildId,
            string platform,
            Action<string, RunRecord, byte[]> writer = null)
        {
            if (string.IsNullOrWhiteSpace(runsRoot)) throw new ArgumentException("Runs root required.");
            if (expectedPhaseCount <= 0) throw new ArgumentOutOfRangeException(nameof(expectedPhaseCount));

            writeRun = writer ?? MeasurementWriter.WriteRun;
            bundle = new Bundle
            {
                sessionId = Guid.NewGuid().ToString("N"),
                buildId = buildId,
                platform = platform,
                startedUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o"),
                expectedPhaseCount = expectedPhaseCount,
            };
            rootDirectory = Path.Combine(runsRoot, bundle.sessionId);
        }

        public bool TrySave(RunRecord record, byte[] rawSamples, out string error)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (rawSamples == null) throw new ArgumentNullException(nameof(rawSamples));

            // Stamp this before the injected writer runs so even an in-memory failure entry can prove which
            // raw bytes belonged to the attempted record.
            record.rawChecksum = MeasurementWriter.Sha256Hex(rawSamples);
            var entry = new Entry
            {
                phaseId = record.phaseOrder != null && record.phaseOrder.Length > 0 ? record.phaseOrder[0] : "unknown",
                storageStatus = "saved",
                record = record,
                rawSamplesJson = Encoding.UTF8.GetString(rawSamples),
            };

            try
            {
                writeRun(Path.Combine(rootDirectory, record.runId), record, rawSamples);
            }
            catch (Exception exception)
            {
                entry.storageStatus = "failed";
                entry.failureReason = exception.Message;
            }

            bundle.entries.Add(entry);
            RefreshCounts();

            try
            {
                Directory.CreateDirectory(rootDirectory);
                File.WriteAllText(BundlePath, ToJson(), Encoding.UTF8);
            }
            catch (Exception exception)
            {
                if (entry.storageStatus == "saved")
                {
                    entry.storageStatus = "failed";
                    entry.failureReason = "Bundle write failed: " + exception.Message;
                    RefreshCounts();
                }
            }

            error = entry.failureReason;
            return entry.storageStatus == "saved";
        }

        public string ToJson()
        {
            bundle.updatedUtc = DateTime.UtcNow.ToString("o");
            return JsonUtility.ToJson(bundle, true);
        }

        private void RefreshCounts()
        {
            bundle.savedCount = 0;
            bundle.invalidCount = 0;
            bundle.failedCount = 0;
            foreach (var entry in bundle.entries)
            {
                if (entry.storageStatus == "failed") bundle.failedCount++;
                else
                {
                    bundle.savedCount++;
                    if (entry.record != null && entry.record.status == "invalid") bundle.invalidCount++;
                }
            }
        }
    }
}
