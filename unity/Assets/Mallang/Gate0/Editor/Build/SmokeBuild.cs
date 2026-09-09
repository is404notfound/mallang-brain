using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mallang.Gate0.Editor.Build
{
    public static class SmokeBuild
    {
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

            File.WriteAllText(Path.Combine(outputDirectory, "BuildReport.json"), JsonUtility.ToJson(new SmokeReport
            {
                target = target.ToString(), result = report.summary.result.ToString(),
                unityVersion = Application.unityVersion, outputPath = outputPath,
                sizeBytes = report.summary.totalSize.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, durationSeconds = report.summary.totalTime.TotalSeconds,
                scope = "T2 launch smoke only; not a Gate pass candidate"
            }, true));

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"{target} smoke build failed: {report.summary.result}");
            }

            Debug.Log(
                $"{target} smoke build succeeded: {report.summary.totalSize} bytes at {outputPath}");
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
