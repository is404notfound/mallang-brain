using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mallang.Gate1.Editor.Build
{
    // Gate 1 build entry point. It deliberately does not use Gate 0's measurement manifest or smoke scene.
    public static class ContestBuild
    {
        public const string ScenePath = "Assets/Mallang/Gate1/Scenes/Gate1ContestPrototype.unity";

        public static void BuildWebGL()
        {
            if (!File.Exists(ScenePath)) throw new BuildFailedException("Gate 1 contest scene is missing: " + ScenePath);

            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "Mallang Brain · Gate 1";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.template = "PROJECT:Gate1Prototype";

            var outputDirectory = Environment.GetEnvironmentVariable("GATE1_BUILD_DIR");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = Path.Combine(ProjectRoot(), "Builds", "Gate1-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            var outputPath = Path.Combine(outputDirectory, "WebGL");
            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = outputPath,
                target = BuildTarget.WebGL, options = BuildOptions.Development,
            });
            File.WriteAllText(Path.Combine(outputPath, "BuildReport.json"), JsonUtility.ToJson(new ContestBuildReport
            {
                target = BuildTarget.WebGL.ToString(), result = report.summary.result.ToString(),
                unityVersion = Application.unityVersion, outputPath = outputPath,
                scope = "Gate 1 playable prototype; not a Gate 0 measurement artifact",
            }, true));

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Gate 1 WebGL build failed: " + report.summary.result);
            Debug.Log("Gate 1 WebGL build succeeded: " + outputPath);
        }

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [Serializable]
        private sealed class ContestBuildReport
        {
            public string target, result, unityVersion, outputPath, scope;
        }
    }
}
