using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mallang.Gate0.Editor.Build
{
    // Generates the minimal AnimatorController the crowd workload uses so each crowd member carries genuine
    // Mecanim overhead (matching the scenario's "animatorCount") rather than a manual Transform rotation.
    // AnimatorController assets can only be authored through UnityEditor APIs, but the resulting
    // RuntimeAnimatorController asset is fully usable in Android/WebGL player builds.
    public static class CrowdAnimatorSetup
    {
        public const string AssetPath = "Assets/Mallang/Gate0/Settings/CrowdProxy.controller";

        // Idempotent: reuses the asset if it already exists rather than regenerating it on every prepare.
        public static RuntimeAnimatorController Prepare()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetPath);
            if (existing != null) return existing;

            var clip = new AnimationClip { name = "CrowdSpin" };
            clip.SetCurve("", typeof(Transform), "localEulerAngles.z", AnimationCurve.Linear(0, 0, 1, 360));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(AssetPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Spin");
            state.motion = clip;
            stateMachine.defaultState = state;

            AssetDatabase.AddObjectToAsset(clip, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            return controller;
        }
    }
}
