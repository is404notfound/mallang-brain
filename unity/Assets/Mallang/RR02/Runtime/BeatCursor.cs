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
            throw new NotImplementedException();
        }

        // 첫음~끝음 사이 진행도 0..1(클램프). lastUs<=firstUs면 0.
        public static float Progress(long nowUs, long firstUs, long lastUs)
        {
            throw new NotImplementedException();
        }
    }
}
