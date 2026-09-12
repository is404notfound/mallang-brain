using System.IO;
using Mallang.Gate0.Runtime.Configuration;
using UnityEditor;
using UnityEngine;

namespace Mallang.Gate0.Editor.Build
{
    public static class Gate0ContractSetup
    {
        public static void ValidateMeasurement()
        {
            const string directory = "Assets/Mallang/Gate0/Settings/";
            Gate0AcceptancePolicy.RequireMeasurementReady(
                AssetDatabase.LoadAssetAtPath<Gate0Scenario>(directory + "Gate0Scenario.asset"),
                AssetDatabase.LoadAssetAtPath<Gate0AcceptancePolicy>(directory + "Gate0AcceptancePolicy.asset"));
            Debug.Log("Measurement contracts valid. This is not a measured Gate result.");
        }
        public static void Prepare()
        {
            const string directory = "Assets/Mallang/Gate0/Settings";
            Directory.CreateDirectory(directory);
            var scenario = AssetDatabase.LoadAssetAtPath<Gate0Scenario>(directory + "/Gate0Scenario.asset");
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<Gate0Scenario>();
                scenario.CreateDraft(20260911);
                AssetDatabase.CreateAsset(scenario, directory + "/Gate0Scenario.asset");
            }
            // Never overwrite user-selected devices or freeze a guessed device matrix.
            if (AssetDatabase.LoadAssetAtPath<Gate0AcceptancePolicy>(directory + "/Gate0AcceptancePolicy.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Gate0AcceptancePolicy>(), directory + "/Gate0AcceptancePolicy.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("T3 draft contracts prepared. Policy must be completed and frozen before measurement.");
        }
    }
}
