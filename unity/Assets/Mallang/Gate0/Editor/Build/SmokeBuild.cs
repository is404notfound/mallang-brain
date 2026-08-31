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
            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "Mallang Brain";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            Build(
                BuildTarget.WebGL,
                Path.Combine(ProjectRoot(), "Builds", "Smoke", "WebGL"));
        }

        public static void BuildAndroid()
        {
            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "Mallang Brain";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                "com.is404notfound.mallangbrain");

            Build(
                BuildTarget.Android,
                Path.Combine(ProjectRoot(), "Builds", "Smoke", "Android", "mallang-brain-smoke.apk"));
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
    }
}
