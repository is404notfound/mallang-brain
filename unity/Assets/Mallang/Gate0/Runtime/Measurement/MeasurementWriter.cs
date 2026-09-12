using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Writes a RunRecord plus its raw sample bytes to a per-run directory. Refuses to overwrite an existing
    // record so results can never be silently replaced, and stamps the raw-sample SHA-256 into the record before
    // validating it. The same guard applies to a BuildManifest.
    public static class MeasurementWriter
    {
        public const string RunRecordFileName = "RunRecord.json";
        public const string RawSamplesFileName = "raw-samples.bin";
        public const string BuildManifestFileName = "BuildManifest.json";

        public static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        // Write a run. rawSamples may be empty but not null; its checksum is always recorded.
        public static void WriteRun(string runDirectory, RunRecord record, byte[] rawSamples)
        {
            if (string.IsNullOrWhiteSpace(runDirectory)) throw new ArgumentException("Run directory required.");
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (rawSamples == null) throw new ArgumentNullException(nameof(rawSamples));

            var recordPath = Path.Combine(runDirectory, RunRecordFileName);
            if (File.Exists(recordPath))
                throw new InvalidOperationException("RunRecord already exists; records are immutable: " + recordPath);

            record.rawChecksum = Sha256Hex(rawSamples);
            record.Validate(); // throws before any file is written if the record is incomplete

            Directory.CreateDirectory(runDirectory);
            File.WriteAllBytes(Path.Combine(runDirectory, RawSamplesFileName), rawSamples);
            File.WriteAllText(recordPath, JsonUtility.ToJson(record, true), Encoding.UTF8);
        }

        public static void WriteBuildManifest(string buildDirectory, BuildManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(buildDirectory)) throw new ArgumentException("Build directory required.");
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            var path = Path.Combine(buildDirectory, BuildManifestFileName);
            if (File.Exists(path))
                throw new InvalidOperationException("BuildManifest already exists; manifests are immutable: " + path);

            manifest.Validate();
            Directory.CreateDirectory(buildDirectory);
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
        }
    }
}
