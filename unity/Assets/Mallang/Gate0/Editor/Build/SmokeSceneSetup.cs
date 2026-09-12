using System.IO;
using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Measurement;
using Mallang.Gate0.Runtime.Smoke;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mallang.Gate0.Editor.Build
{
    public static class SmokeSceneSetup
    {
        public const string ScenePath = "Assets/Mallang/Gate0/Scenes/SpikeScene.unity";
        private const string SettingsDirectory = "Assets/Mallang/Gate0/Settings/";

        public static void Prepare()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Gate0Smoke", typeof(Camera), typeof(AudioListener), typeof(AudioSource), typeof(SmokeTapScene));
            root.tag = "MainCamera";
            root.transform.position = new Vector3(0, 0, -10);
            AddMeasurementController();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("T2 common launch scene saved: " + ScenePath);
        }

        // Adds the T4-T7 measurement harness alongside the existing tap/color/sound smoke object. Skipped
        // (with a warning) if the scenario draft has not been prepared yet, so plain `gate0:prepare` still
        // works before `prepare-contracts` has run.
        private static void AddMeasurementController()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<Gate0Scenario>(SettingsDirectory + "Gate0Scenario.asset");
            if (scenario == null)
            {
                Debug.LogWarning("Gate0Scenario.asset not found; skipping Gate0MeasurementController. Run gate0:prepare-contracts first.");
                return;
            }
            var policy = AssetDatabase.LoadAssetAtPath<Gate0AcceptancePolicy>(SettingsDirectory + "Gate0AcceptancePolicy.asset");

            var measurement = new GameObject("Gate0Measurement");
            measurement.SetActive(false); // defer Awake until scenario/policy are assigned
            measurement.AddComponent<AudioSource>();
            var controller = measurement.AddComponent<Gate0MeasurementController>();
            controller.scenario = scenario;
            controller.policy = policy;
            controller.crowdAnimatorController = CrowdAnimatorSetup.Prepare();
            measurement.SetActive(true);
        }
    }
}
