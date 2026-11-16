using System;

namespace Mallang.RR02
{
    // L9 박자 커서 위치(순수 로직). 재생 시각을 노트 시퀀스 상의 '커서 위치'로 바꿉니다.
    // 커서 선을 그려 움직이는 것은 장면 레인 몫이고, 여기는 "지금 몇 번째 음 위치인가/진행도 얼마인가"만.
    public static class BeatCursor
    {
        // 정렬된 목표시각 배열에 대해 now의 연속 위치를 0..(n-1) 보간값으로 돌려줍니다.
        // now<=targets[0] -> 0, now>=targets[n-1] -> n-1, 그 사이는 구간 선형보간.
        // 배열이 null/비었으면 0, 원소 1개면 0.
        public static float FloatIndex(long nowUs, long[] targetsUs)
        {
            if (targetsUs == null || targetsUs.Length == 0) return 0f;
            if (targetsUs.Length == 1) return 0f;
            if (nowUs <= targetsUs[0]) return 0f;

            int n = targetsUs.Length;
            if (nowUs >= targetsUs[n - 1]) return n - 1f;

            // 정렬된 배열이므로 now를 포함하는 구간 i(>=targets[i]이고 <targets[i+1])는 하나만 처음으로 걸린다.
            for (int i = 0; i < n - 1; i++)
            {
                if (nowUs < targetsUs[i + 1])
                {
                    long span = targetsUs[i + 1] - targetsUs[i];
                    return i + (nowUs - targetsUs[i]) / (float)span;
                }
            }
            return n - 1f; // 이론상 도달하지 않는 방어 값
        }

        // 첫음~끝음 사이 진행도 0..1(클램프). lastUs<=firstUs면 0.
        public static float Progress(long nowUs, long firstUs, long lastUs)
        {
            if (lastUs <= firstUs) return 0f;
            float t = (nowUs - firstUs) / (float)(lastUs - firstUs);
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }
    }
}
