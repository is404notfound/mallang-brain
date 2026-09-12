using Mallang.Gate0.Runtime.Configuration;
using Mallang.Gate0.Runtime.Measurement;
using Mallang.Gate0.Runtime.Workload;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate0.Tests.EditMode
{
    // Deterministic crowd/VFX/elimination schedule (30/60/100) and the frame-stability metrics.
    public sealed class WorkloadTests
    {
        private Gate0Scenario scenario;

        [SetUp] public void SetUp() { scenario = ScriptableObject.CreateInstance<Gate0Scenario>(); scenario.CreateDraft(20260911); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(scenario); }

        [Test] public void CrowdCountsFollowThreeByThreeMatrix()
        {
            // Phases are ordered 30/60/100 × idle/simultaneous_vfx/mass_elimination.
            var idle30 = new WorkloadSchedule(scenario.specification.phases[0]);
            var vfx30 = new WorkloadSchedule(scenario.specification.phases[1]);
            var mass100 = new WorkloadSchedule(scenario.specification.phases[8]);

            Assert.AreEqual("30-idle", idle30.PhaseId);
            Assert.AreEqual(30, idle30.CrowdCount);
            Assert.AreEqual(360, idle30.BeatCount); // 180s at 500ms beats
            Assert.AreEqual(0, idle30.VfxOnBeat(0));
            Assert.AreEqual(0, idle30.EliminationsOnBeat(0));

            Assert.AreEqual(30, vfx30.VfxOnBeat(0));         // one VFX per crowd member each beat
            Assert.AreEqual(0, vfx30.EliminationsOnBeat(0));
            Assert.AreEqual(30L * 360, vfx30.TotalVfx);

            Assert.AreEqual(100, mass100.CrowdCount);
            Assert.AreEqual(100, mass100.EliminationsOnBeat(0));
            Assert.AreEqual(100L * 360, mass100.TotalEliminations);
        }

        [Test] public void ScheduleIsDeterministicAndBoundedToPhase()
        {
            var a = new WorkloadSchedule(scenario.specification.phases[4]);
            var b = new WorkloadSchedule(scenario.specification.phases[4]);
            Assert.AreEqual(a.VfxOnBeat(10), b.VfxOnBeat(10));
            Assert.AreEqual(a.BeatTimeUs(2), 2 * WorkloadSchedule.BeatIntervalUs);
            Assert.AreEqual(0, a.VfxOnBeat(-1));           // out of range
            Assert.AreEqual(0, a.VfxOnBeat(a.BeatCount));  // out of range
        }

        [Test] public void FrameStatsComputeExpectedMetrics()
        {
            var stats = new FrameStatsCollector();
            foreach (var ms in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
                stats.AddFrameSeconds(ms / 1000.0);
            var s = stats.Compute();

            Assert.AreEqual(10, s.FrameCount);
            Assert.AreEqual(7, s.FramesOver33Ms);
            Assert.AreEqual(5, s.FramesOver50Ms);
            Assert.AreEqual(100.0, s.P95Ms, 1e-9);
            Assert.AreEqual(100.0, s.P99Ms, 1e-9);
            Assert.AreEqual(400.0, s.LongestStallMs, 1e-9); // 60+70+80+90+100 in a row
            Assert.AreEqual(10 / 0.55, s.MeanFps, 1e-6);
        }

        [Test] public void FrameStatsIgnoreInvalidDeltasAndEmptyIsZero()
        {
            var stats = new FrameStatsCollector();
            stats.AddFrameSeconds(0);
            stats.AddFrameSeconds(-1);
            stats.AddFrameSeconds(double.NaN);
            Assert.AreEqual(0, stats.FrameCount);
            var s = stats.Compute();
            Assert.AreEqual(0, s.FrameCount);
            Assert.AreEqual(0, s.MeanFps);
        }
    }
}
