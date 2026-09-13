using System;
using System.IO;
using System.Text;
using Mallang.Gate0.Runtime.Measurement;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate0.Tests.EditMode
{
    // Covers immutable records: test-plan E13 (BuildManifest) and E14 (RunRecord), plus the no-overwrite and
    // checksum guarantees of the writer.
    public sealed class RecordsTests
    {
        private string tempRoot;

        [SetUp] public void SetUp()
        {
            tempRoot = Path.Combine(Application.temporaryCachePath, "gate0-records-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }

        private static RunRecord CompleteRun() => new RunRecord
        {
            runId = "run-1", buildId = "build-1",
            scenarioId = "gate0-nine-workloads-v1", scenarioHash = "abc",
            policyId = "gate0-original-thresholds-v1", policyHash = "def",
            deviceModel = "SM-XXXX", os = "Android 16", network = "wifi",
            loadState = "cold", cacheState = "cleared",
            startedUtc = "2026-09-12T00:00:00Z", endedUtc = "2026-09-12T00:03:00Z",
            status = "valid", phaseOrder = new[] { "30-idle" }, rawChecksum = "seeded",
        };

        // E14 — a record missing a required field is not a comparable Gate result.
        [Test] public void RunRecordMissingFieldIsNotComparable()
        {
            var record = CompleteRun();
            record.policyHash = null;
            Assert.IsFalse(record.IsComparableGateResult());
            Assert.Throws<InvalidOperationException>(() => record.Validate());

            record.policyHash = "def";
            record.status = "unknown"; // outside valid/invalid/failed
            Assert.Throws<InvalidOperationException>(() => record.Validate());
        }

        // Measured metric fields default to zero and round-trip through JsonUtility (used by MeasurementWriter).
        [Test] public void MetricFieldsDefaultToZeroAndRoundTripThroughJson()
        {
            var fresh = CompleteRun();
            Assert.AreEqual(0, fresh.meanFps);
            Assert.AreEqual(0, fresh.frameCount);
            Assert.AreEqual(0, fresh.perfectCount + fresh.goodCount + fresh.missCount + fresh.duplicateCount);

            fresh.meanFps = 58.5; fresh.frameP95Ms = 18.2; fresh.perfectCount = 300; fresh.missCount = 4;
            var roundTripped = JsonUtility.FromJson<RunRecord>(JsonUtility.ToJson(fresh));
            Assert.AreEqual(58.5, roundTripped.meanFps, 1e-9);
            Assert.AreEqual(18.2, roundTripped.frameP95Ms, 1e-9);
            Assert.AreEqual(300, roundTripped.perfectCount);
            Assert.AreEqual(4, roundTripped.missCount);
        }

        [Test] public void WriterStampsChecksumAndValidatesBeforeWriting()
        {
            var raw = Encoding.UTF8.GetBytes("raw-sample-bytes");
            var dir = Path.Combine(tempRoot, "run-1");
            var record = CompleteRun();
            MeasurementWriter.WriteRun(dir, record, raw);

            Assert.AreEqual(MeasurementWriter.Sha256Hex(raw), record.rawChecksum);
            Assert.IsTrue(File.Exists(Path.Combine(dir, MeasurementWriter.RunRecordFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(dir, MeasurementWriter.RawSamplesFileName)));
        }

        // Records are immutable: a second write to the same directory is refused.
        [Test] public void WriterRefusesToOverwriteExistingRecord()
        {
            var raw = Encoding.UTF8.GetBytes("x");
            var dir = Path.Combine(tempRoot, "run-1");
            MeasurementWriter.WriteRun(dir, CompleteRun(), raw);
            Assert.Throws<InvalidOperationException>(() => MeasurementWriter.WriteRun(dir, CompleteRun(), raw));
        }

        // An incomplete record throws before any file is written.
        [Test] public void WriterDoesNotWriteIncompleteRecord()
        {
            var incomplete = CompleteRun();
            incomplete.deviceModel = null;
            var dir = Path.Combine(tempRoot, "run-bad");
            Assert.Throws<InvalidOperationException>(() => MeasurementWriter.WriteRun(dir, incomplete, new byte[0]));
            Assert.IsFalse(Directory.Exists(dir) && File.Exists(Path.Combine(dir, MeasurementWriter.RunRecordFileName)));
        }

        [Test] public void MeasurementBatchMakesStorageFailureVisibleAndKeepsRawEvidenceInMemory()
        {
            var record = CompleteRun();
            var raw = Encoding.UTF8.GetBytes("{\"sample\":42}");
            var batch = new MeasurementBatch(
                tempRoot,
                9,
                "build-1",
                "Android",
                (_, __, ___) => throw new IOException("disk full"));

            Assert.IsFalse(batch.TrySave(record, raw, out var error));
            StringAssert.Contains("disk full", error);
            Assert.AreEqual(0, batch.SavedCount);
            Assert.AreEqual(1, batch.FailedCount);
            Assert.AreEqual(1, batch.AttemptedCount);
            StringAssert.Contains("\"storageStatus\": \"failed\"", batch.ToJson());
            StringAssert.Contains("\\\"sample\\\":42", batch.ToJson());
        }

        // E13 — dirty or checksum-missing builds are not final Gate candidates; a clean, complete build is.
        [Test] public void BuildManifestEligibilityRules()
        {
            var clean = new BuildManifest
            {
                buildId = "b1", sourceCommit = "deadbeef", dirty = false,
                unityVersion = "6000.3.23f1", buildTarget = "Android",
                scenarioId = "gate0-nine-workloads-v1", scenarioSchema = 1, scenarioHash = "abc",
                policyId = "gate0-original-thresholds-v1", policySchema = 1, policyHash = "def",
                artifactSha256 = "aa", buildReportSha256 = "bb",
                buildTimestampUtc = "2026-09-12T00:00:00Z", buildResult = "Succeeded",
            };
            Assert.IsTrue(clean.IsFinalGateEligible);
            clean.Validate();

            clean.dirty = true;
            Assert.IsFalse(clean.IsFinalGateEligible);

            clean.dirty = false;
            clean.buildReportSha256 = null;
            Assert.IsFalse(clean.IsFinalGateEligible);
            clean.buildReportSha256 = "bb";

            // Bug found in review: an unfrozen policy (or missing scenario) writes a placeholder like
            // "unset-draft" rather than leaving the field empty, so a plain emptiness check would not catch
            // it — and a clean working tree alone must not be enough to call this build final-Gate-eligible.
            clean.policyHash = "unset-draft";
            Assert.IsFalse(clean.IsFinalGateEligible, "A placeholder policyHash must block final Gate eligibility even on a clean commit.");
            clean.policyHash = "def";
            Assert.IsTrue(clean.IsFinalGateEligible);

            clean.scenarioId = "unset-draft";
            Assert.IsFalse(clean.IsFinalGateEligible, "A placeholder scenarioId must block final Gate eligibility.");

            var missing = new BuildManifest { schemaVersion = BuildManifest.CurrentSchemaVersion };
            Assert.Throws<InvalidOperationException>(() => missing.Validate());
        }
    }
}
