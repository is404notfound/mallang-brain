using UnityEngine;

namespace Mallang.RR02.Screens
{
    // 대기 숨쉬기(절차적). 2프레임(대기/타격)만으로 정적인 캐릭터에 생기를 줍니다.
    // 캐릭터 Image에 붙이며, 버블 그룹의 펄스(펀치)와는 대상이 달라 서로 겹쳐도 됩니다.
    public class IdleBreath : MonoBehaviour
    {
        public float amp = 0.08f;    // 숨 크기(위아래 늘어남 — 티나게)
        public float speed = 2.2f;   // 숨 속도
        public float phase = 0f;     // 캐릭터별 위상차(제각각 숨쉬게)

        RectTransform _rt;

        void Awake() { _rt = (RectTransform)transform; }

        void Update()
        {
            float wave = Mathf.Sin((Time.time + phase) * speed);
            // 세로로 뚜렷하게 늘고 가로로 그만큼 눌리는 숨(말랑·부피 보존 느낌)
            float sy = 1f + amp * wave;
            float sx = 1f - amp * 0.6f * wave;
            _rt.localScale = new Vector3(sx, sy, 1f);
        }
    }
}
