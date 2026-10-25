using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mallang.Gate1.Editor.Build
{
    /// <summary>Creates the installable Android tutorial APK; keeps contest and Gate 0 build paths untouched.</summary>
    public static class TutorialAndroidBuild
    {
        public const string ScenePath = "Assets/Mallang/Gate1/Scenes/ListenRepeatTutorial.unity";
        public const string DefaultOutputPath = "Builds/Android/MallangBrainTutorial.apk";

        public static void BuildAndroidTutorial()
        {
            if (!File.Exists(ScenePath)) throw new BuildFailedException("Tutorial scene is missing: " + ScenePath);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is not installed for this Unity editor.");

            PlayerSettings.companyName = "is404notfound";
            PlayerSettings.productName = "말랑브레인음악대";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.is404notfound.mallangbrain");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputPath = Environment.GetEnvironmentVariable("MALLANG_ANDROID_APK");
            if (string.IsNullOrWhiteSpace(outputPath)) outputPath = Path.Combine(projectRoot, DefaultOutputPath);
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Tutorial Android build failed: " + report.summary.result);
            Debug.Log("Tutorial Android APK created: " + outputPath + " (" + report.summary.totalSize + " bytes)");
        }

        [MenuItem("Mallang Brain/Build Android Tutorial APK")]
        private static void BuildAndroidTutorialFromMenu()
        {
            BuildAndroidTutorial();
        }
    }

    // The request file is consumed exactly once by the already-open editor.
    // It avoids closing the user's editor or reusing its authentication command-line arguments.
    [InitializeOnLoad]
    internal static class TutorialAndroidBuildRequest
    {
        static TutorialAndroidBuildRequest()
        {
            EditorApplication.update += ConsumeRequest;
        }

        private static void ConsumeRequest()
        {
            var requestPath = Path.Combine(Application.dataPath, "..", "Library", "MallangTutorialAndroidBuild.request");
            if (!File.Exists(requestPath) || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= ConsumeRequest;
            File.Delete(requestPath);
            try { TutorialAndroidBuild.BuildAndroidTutorial(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
