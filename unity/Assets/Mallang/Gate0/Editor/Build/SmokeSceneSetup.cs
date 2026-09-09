using System.IO;
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

        public static void Prepare()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Gate0Smoke", typeof(Camera), typeof(AudioListener), typeof(AudioSource), typeof(SmokeTapScene));
            root.tag = "MainCamera";
            root.transform.position = new Vector3(0, 0, -10);
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
    }
}
