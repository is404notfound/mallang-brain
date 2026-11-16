using NUnit.Framework;
using Mallang.RR02;

namespace Mallang.RR02.Tests.EditMode
{
    // L9 박자 커서 위치 매핑. 재생 시각 -> 노트 시퀀스 상의 연속 위치/진행도.
    [TestFixture]
    public class BeatCursorTests
    {
        static long[] Targets() => new long[] { 0, 400000, 800000, 1200000, 1600000, 2000000, 2400000, 2800000 };

        [Test]
        public void FloatIndex_before_first_is_zero()
        {
            Assert.AreEqual(0f, BeatCursor.FloatIndex(-50000, Targets()), 1e-4f);
        }

        [Test]
        public void FloatIndex_on_a_target_is_that_index()
        {
            Assert.AreEqual(2f, BeatCursor.FloatIndex(800000, Targets()), 1e-4f);
        }

        [Test]
        public void FloatIndex_midway_interpolates()
        {
            // 400000(idx1)과 800000(idx2) 사이 중간 -> 1.5
            Assert.AreEqual(1.5f, BeatCursor.FloatIndex(600000, Targets()), 1e-4f);
        }

        [Test]
        public void FloatIndex_after_last_is_last_index()
        {
            Assert.AreEqual(7f, BeatCursor.FloatIndex(9999999, Targets()), 1e-4f);
        }

        [Test]
        public void FloatIndex_empty_is_zero()
        {
            Assert.AreEqual(0f, BeatCursor.FloatIndex(123, new long[0]), 1e-4f);
        }

        [Test]
        public void FloatIndex_single_is_zero()
        {
            Assert.AreEqual(0f, BeatCursor.FloatIndex(500000, new long[] { 400000 }), 1e-4f);
        }

        [Test]
        public void Progress_at_first_is_zero()
        {
            Assert.AreEqual(0f, BeatCursor.Progress(0, 0, 2800000), 1e-4f);
        }

        [Test]
        public void Progress_at_last_is_one()
        {
            Assert.AreEqual(1f, BeatCursor.Progress(2800000, 0, 2800000), 1e-4f);
        }

        [Test]
        public void Progress_midway_is_half()
        {
            Assert.AreEqual(0.5f, BeatCursor.Progress(1400000, 0, 2800000), 1e-4f);
        }

        [Test]
        public void Progress_clamps_outside()
        {
            Assert.AreEqual(0f, BeatCursor.Progress(-100, 0, 2800000), 1e-4f);
            Assert.AreEqual(1f, BeatCursor.Progress(9999999, 0, 2800000), 1e-4f);
        }

        [Test]
        public void Progress_degenerate_span_is_zero()
        {
            Assert.AreEqual(0f, BeatCursor.Progress(500000, 800000, 800000), 1e-4f);
        }
    }
}
