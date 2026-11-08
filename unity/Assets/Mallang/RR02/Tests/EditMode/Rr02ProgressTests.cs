using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L8 라운드 진행 전구 매핑. NormalCleared(0~9)를 전구 점등 상태로 바꾸는 순수 로직.
    [TestFixture]
    public class Rr02ProgressTests
    {
        [Test]
        public void Compute_none_all_off()
        {
            var lit = StageLamps.Compute(0, 9);
            Assert.AreEqual(9, lit.Length);
            foreach (var b in lit) Assert.IsFalse(b);
        }

        [Test]
        public void Compute_partial_lights_left_to_right()
        {
            var lit = StageLamps.Compute(3, 9);
            Assert.AreEqual(
                new[] { true, true, true, false, false, false, false, false, false },
                lit);
        }

        [Test]
        public void Compute_full_all_on()
        {
            var lit = StageLamps.Compute(9, 9);
            Assert.AreEqual(9, lit.Length);
            foreach (var b in lit) Assert.IsTrue(b);
        }

        [Test]
        public void Compute_overflow_clamps_to_total()
        {
            var lit = StageLamps.Compute(12, 9);
            Assert.AreEqual(9, lit.Length);
            foreach (var b in lit) Assert.IsTrue(b);
        }

        [Test]
        public void Compute_negative_clamps_to_zero()
        {
            var lit = StageLamps.Compute(-1, 9);
            Assert.AreEqual(9, lit.Length);
            foreach (var b in lit) Assert.IsFalse(b);
        }

        [Test]
        public void Compute_zero_total_is_empty()
        {
            Assert.AreEqual(0, StageLamps.Compute(5, 0).Length);
        }

        [Test]
        public void JustLit_returns_newly_lit_index()
        {
            Assert.AreEqual(2, StageLamps.JustLitIndex(2, 3, 9));
        }

        [Test]
        public void JustLit_none_when_no_progress()
        {
            Assert.AreEqual(-1, StageLamps.JustLitIndex(3, 3, 9));
        }

        [Test]
        public void JustLit_jump_returns_latest_index()
        {
            Assert.AreEqual(4, StageLamps.JustLitIndex(2, 5, 9));
        }

        [Test]
        public void JustLit_clamps_to_total()
        {
            Assert.AreEqual(8, StageLamps.JustLitIndex(8, 12, 9));
        }

        [Test]
        public void JustLit_none_when_already_full()
        {
            Assert.AreEqual(-1, StageLamps.JustLitIndex(9, 12, 9));
        }
    }
}
