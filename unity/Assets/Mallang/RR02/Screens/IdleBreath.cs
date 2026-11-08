using UnityEngine;

namespace Mallang.RR02.Screens
{
    // 대기 숨쉬기(절차적). 2프레임(대기/타격)만으로 정적인 캐릭터에 생기를 줍니다.
    // 캐릭터 Image에 붙이며, 버블 그룹의 펄스(펀치)와는 대상이 달라 서로 겹쳐도 됩니다.
    public class IdleBreath : MonoBehaviour
    {
        public float amp = 0.035f;   // 숨 크기
        public float speed = 2.2f;   // 숨 속도
        public float phase = 0f;     // 캐릭터별 위상차(제각각 숨쉬게)

        RectTransform _rt;

        void Awake() { _rt = (RectTransform)transform; }

        void Update()
        {
            float s = 1f + amp * Mathf.Sin((Time.time + phase) * speed);
            // 세로로 살짝 더 늘고 가로로 살짝 눌리는 숨(말랑한 느낌)
            _rt.localScale = new Vector3(1f - amp * 0.4f * Mathf.Sin((Time.time + phase) * speed), s, 1f);
        }
    }
}
