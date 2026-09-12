using System;
using Mallang.Gate0.Runtime.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate0.Tests.EditMode
{
    public sealed class Gate0ContractTests
    {
        private Gate0Scenario scenario;
        private Gate0AcceptancePolicy policy;
        [SetUp] public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<Gate0Scenario>(); scenario.CreateDraft(20260911);
            policy = ScriptableObject.CreateInstance<Gate0AcceptancePolicy>();
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(scenario); UnityEngine.Object.DestroyImmediate(policy); }

        [Test] public void SameSeedAndSerializationProduceSameHash()
        {
            var original = scenario.ComputeHash();
            var timestamps = (long[])scenario.specification.phases[0].syntheticInputUs.Clone();
            scenario.CreateDraft(20260911);
            Assert.That(scenario.ComputeHash(), Is.EqualTo(original));
            Assert.That(scenario.specification.phases[0].syntheticInputUs, Is.EqualTo(timestamps));
            var json = JsonUtility.ToJson(scenario.specification);
            scenario.specification = JsonUtility.FromJson<Gate0Scenario.Specification>(json);
            scenario.Validate();
            Assert.That(scenario.ComputeHash(), Is.EqualTo(original));
            scenario.CreateDraft(42);
            Assert.That(scenario.ComputeHash(), Is.Not.EqualTo(original));
        }
        [Test] public void ScenarioMutationRejectsOldHash()
        {
            scenario.specification.seed++;
            Assert.Throws<InvalidOperationException>(() => scenario.Validate());
        }
        [Test] public void ScenarioSchemaAndMissingPhaseFailClosed()
        {
            scenario.specification.schemaVersion = 2;
            Assert.Throws<InvalidOperationException>(() => scenario.Seal());
            scenario.CreateDraft(1); scenario.specification.phases[3] = null;
            Assert.Throws<InvalidOperationException>(() => scenario.Seal());
        }
        [Test] public void DuplicateOrOutOfBoundsInputRejected()
        {
            var p = scenario.specification.phases[0];
            p.syntheticInputUs[1] = p.syntheticInputUs[0];
            Assert.Throws<InvalidOperationException>(() => scenario.Seal());
            p.syntheticInputUs[1] = 180000000;
            Assert.Throws<InvalidOperationException>(() => scenario.Seal());
        }
        [Test] public void MissingAssetsAndDraftDevicesCannotStartMeasurement()
        {
            Assert.Throws<InvalidOperationException>(() => Gate0AcceptancePolicy.RequireMeasurementReady(null, policy));
            Assert.Throws<InvalidOperationException>(() => Gate0AcceptancePolicy.RequireMeasurementReady(scenario, null));
            Assert.Throws<InvalidOperationException>(() => policy.Freeze());
            Assert.That(policy.frozen, Is.False);
        }
        [Test] public void CompletePolicyAcceptsThenRejectsChangedDevice()
        {
            FillSyntheticDeviceMatrix(); policy.Freeze();
            Gate0AcceptancePolicy.RequireMeasurementReady(scenario, policy);
            policy.specification.devices[0].refreshHz = 120;
            Assert.Throws<InvalidOperationException>(() => Gate0AcceptancePolicy.RequireMeasurementReady(scenario, policy));
        }
        [Test] public void RelaxedThresholdAndSampleCountCannotBeFrozen()
        {
            FillSyntheticDeviceMatrix(); policy.specification.tapP95Us = 61000;
            Assert.Throws<InvalidOperationException>(() => policy.Freeze());
            policy.specification.tapP95Us = 60000; policy.specification.validRuns = 4;
            Assert.Throws<InvalidOperationException>(() => policy.Freeze());
        }
        [Test] public void WrongTossNetworkAndDuplicateRoleRejected()
        {
            FillSyntheticDeviceMatrix(); policy.specification.devices[3].network = "mobile";
            Assert.Throws<InvalidOperationException>(() => policy.Freeze());
            FillSyntheticDeviceMatrix(); policy.specification.devices[1].role = "android-gate-mid";
            Assert.Throws<InvalidOperationException>(() => policy.Freeze());
        }
        private void FillSyntheticDeviceMatrix()
        {
            // Unit-test fixtures only: never written to project assets or used as measured devices.
            var roles = new[] { "android-gate-mid", "android-diagnostic-low", "android-diagnostic-high", "toss-gate", "toss-diagnostic-warm", "toss-diagnostic-mobile" };
            policy.specification.devices = Array.ConvertAll(roles, role => new Gate0AcceptancePolicy.Device {
                role = role, model = "UNIT-TEST-ONLY", os = "test", network = role == "toss-diagnostic-mobile" ? "mobile" : "wifi", cacheMode = role == "toss-diagnostic-warm" ? "warm" : "cleared",
                refreshHz = 60, width = 960, height = 540, dprMilli = 1000 });
        }
    }
}
