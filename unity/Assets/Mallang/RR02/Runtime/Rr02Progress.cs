using System;

namespace Mallang.RR02
{
    // L8 라운드 진행 전구 매핑(순수 로직). 클리어한 라운드 수를 전구 점등 상태로 바꿉니다.
    // 전구 줄 그리기·퐁 연출은 장면 레인 몫이고, 여기는 "몇 번째 전구까지 켜지는가"만 다룹니다.
    public static class StageLamps
    {
        // 전구 점등 배열을 돌려줍니다. lit[i] = (i < clamp(cleared,0,total)). total<=0이면 빈 배열.
        public static bool[] Compute(int cleared, int total)
        {
            throw new NotImplementedException();
        }

        // 방금 켜진 전구의 인덱스(퐁 연출 트리거용). 진행이 없으면 -1.
        public static int JustLitIndex(int prevCleared, int newCleared, int total)
        {
            throw new NotImplementedException();
        }
    }
}
