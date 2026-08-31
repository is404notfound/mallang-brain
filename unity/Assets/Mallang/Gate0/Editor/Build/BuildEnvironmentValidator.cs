using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Mallang.Gate0.Editor.Build
{
    public static class BuildEnvironmentValidator
    {
        public static void Validate()
        {
            ValidateTarget(BuildTargetGroup.Android, BuildTarget.Android, "Android");
            ValidateTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL, "WebGL");

            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.externalVersionControl = "Visible Meta Files";

            Debug.Log($"Gate 0 Unity environment verified with Unity {Application.unityVersion}.");
        }

        private static void ValidateTarget(
            BuildTargetGroup targetGroup,
            BuildTarget target,
            string displayName)
        {
            if (!BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                throw new BuildFailedException($"{displayName} Build Support is not installed.");
            }

            Debug.Log($"{displayName} Build Support is installed.");
        }
    }
}
