using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Measurement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mallang.Gate0.Editor.Build
{
    public static class SmokeBuild
    {
        private const string SettingsDirectory = "Assets/Mallang/Gate0/Settings/";
        public static void BuildWebGL()
        {
            SmokeSceneSetup.Prepare();
            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "Mallang Brain";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.template = "PROJECT:Gate0Smoke";

            Build(
                BuildTarget.WebGL,
                OutputPath("WebGL"));
        }

        public static void BuildAndroid()
        {
            SmokeSceneSetup.Prepare();
            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "Mallang Brain";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                "com.is404notfound.mallangbrain");

            Build(
                BuildTarget.Android,
                OutputPath("mallang-brain-smoke.apk"));
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("At least one enabled scene is required.");
            }

            var outputDirectory = target == BuildTarget.Android
                ? Path.GetDirectoryName(outputPath)
                : outputPath;

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new BuildFailedException("The build output directory is invalid.");
            }

            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.Development
            });

            var buildReportPath = Path.Combine(outputDirectory, "BuildReport.json");
            File.WriteAllText(buildReportPath, JsonUtility.ToJson(new SmokeReport
            {
                target = target.ToString(), result = report.summary.result.ToString(),
                unityVersion = Application.unityVersion, outputPath = outputPath,
                sizeBytes = report.summary.totalSize.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, durationSeconds = report.summary.totalTime.TotalSeconds,
                scope = "T2 launch smoke only; not a Gate pass candidate"
            }, true));

            WriteBuildManifest(target, outputPath, outputDirectory, buildReportPath, report);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"{target} smoke build failed: {report.summary.result}");
            }

            Debug.Log(
                $"{target} smoke build succeeded: {report.summary.totalSize} bytes at {outputPath}");
        }

        // Writes an immutable BuildManifest.json alongside BuildReport.json for every build, success or
        // failure. A missing/unfrozen scenario or policy does not block this — that would break routine
        // iteration builds — it just falls back to an explicit placeholder so this manifest can never be
        // mistaken for a final Gate-pass candidate (see BuildManifest.IsFinalGateEligible, which additionally
        // requires a clean commit and real artifact/report checksums).
        private static void WriteBuildManifest(BuildTarget target, string outputPath, string outputDirectory, string buildReportPath, BuildReport report)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<Gate0Scenario>(SettingsDirectory + "Gate0Scenario.asset");
            var policy = AssetDatabase.LoadAssetAtPath<Gate0AcceptancePolicy>(SettingsDirectory + "Gate0AcceptancePolicy.asset");

            var commitHash = RunGit("rev-parse HEAD");
            // Not derived from outputDirectory's basename: WebGL nests its output one level deeper ("WebGL/"
            // under the unique per-invocation folder) than Android does, which would otherwise make every
            // WebGL build's buildId collide on the literal string "WebGL".
            var timestamp = DateTime.UtcNow;
            var manifest = new BuildManifest
            {
                buildId = target + "-" + timestamp.ToString("yyyyMMddTHHmmssfffZ"),
                sourceCommit = string.IsNullOrEmpty(commitHash) ? "unknown" : commitHash,
                dirty = !string.IsNullOrEmpty(RunGit("status --porcelain")),
                unityVersion = Application.unityVersion,
                buildTarget = target.ToString(),
                buildProfile = "development",
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    target == BuildTarget.Android ? NamedBuildTarget.Android : NamedBuildTarget.WebGL).ToString(),
                qualityLevel = QualitySettings.GetQualityLevel(),
                logicalWidth = PlayerSettings.defaultScreenWidth,
                logicalHeight = PlayerSettings.defaultScreenHeight,
                scenarioId = scenario != null ? scenario.specification.scenarioId : "unset-draft",
                scenarioSchema = scenario != null ? scenario.specification.schemaVersion : 0,
                scenarioHash = !string.IsNullOrEmpty(scenario?.contentHash) ? scenario.contentHash : "unset-draft",
                policyId = policy != null ? policy.specification.policyId : "unset-draft",
                policySchema = policy != null ? policy.specification.schemaVersion : 0,
                policyHash = !string.IsNullOrEmpty(policy?.contentHash) ? policy.contentHash : "unset-draft",
                artifactSha256 = HashPath(outputPath),
                buildReportSha256 = HashFile(buildReportPath),
                buildTimestampUtc = timestamp.ToString("o"),
                buildCommand = target == BuildTarget.Android ? "gate0:build-android" : "gate0:build-webgl",
                buildResult = report.summary.result.ToString(),
            };

            try { MeasurementWriter.WriteBuildManifest(outputDirectory, manifest); }
            catch (Exception e) { Debug.LogError("Failed to write BuildManifest.json: " + e.Message); throw; }
        }

        private static string RunGit(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = ProjectRoot(),
                };
                using var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch (Exception e) { Debug.LogWarning("git " + args + " failed: " + e.Message); return ""; }
        }

        private static string HashFile(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // Deterministic hash over a whole directory (WebGL output): sorted relative-path + per-file hash pairs,
        // then hashed again, so identical file sets always produce the same artifactSha256.
        private static string HashPath(string path)
        {
            if (File.Exists(path)) return HashFile(path);
            if (!Directory.Exists(path)) throw new InvalidOperationException("Build artifact not found: " + path);
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(path.Length + 1).Replace('\\', '/') + ":" + HashFile(f))
                .OrderBy(entry => entry, StringComparer.Ordinal);
            var combined = string.Join("\n", files);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(combined))).Replace("-", "").ToLowerInvariant();
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string OutputPath(string name)
        {
            var directory = Environment.GetEnvironmentVariable("GATE0_BUILD_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(ProjectRoot(), "Builds", "T2-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            return Path.Combine(directory, name);
        }

        [Serializable]
        private sealed class SmokeReport
        {
            public string target, result, unityVersion, outputPath, sizeBytes, scope;
            public int errors, warnings;
            public double durationSeconds;
        }
    }
}
