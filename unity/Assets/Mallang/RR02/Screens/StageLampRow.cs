using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mallang.RR02.Screens
{
    // 라운드 진행 전구 줄의 뷰 바인딩. 점등 상태 계산은 로직 레인의 Mallang.RR02.StageLamps가 하고,
    // 이 컴포넌트는 그 결과를 스프라이트로 반영하며 새로 켜진 전구에 '퐁'(스케일 펀치) 연출을 줍니다.
    // 플레이 루프는 라운드가 올라갈 때 SetCleared(run.NormalCleared)만 호출하면 됩니다.
    public class StageLampRow : MonoBehaviour
    {
        [Tooltip("왼쪽부터 순서대로 담은 전구 Image들(Lamp0..N)")]
        public Image[] lamps;
        public Sprite onSprite;
        public Sprite offSprite;

        [Header("미리보기 (플레이 시작 시 이 값으로 표시)")]
        public int previewCleared = 3;

        [Header("퐁 연출")]
        public float popScale = 1.4f;
        public float popDuration = 0.28f;

        int _prevCleared;
        Coroutine _pop;

        int Total => (lamps != null) ? lamps.Length : 0;

        void OnEnable()
        {
            if (lamps != null && lamps.Length > 0)
                SetClearedInstant(previewCleared);
        }

        // 전체 즉시 반영(초기화/점프). 연출 없음.
        public void SetClearedInstant(int cleared)
        {
            var lit = StageLamps.Compute(cleared, Total);
            for (int i = 0; i < lamps.Length && i < lit.Length; i++)
            {
                if (lamps[i] == null) continue;
                lamps[i].sprite = lit[i] ? onSprite : offSprite;
                lamps[i].rectTransform.localScale = Vector3.one;
            }
            _prevCleared = cleared;
        }

        // 진행 갱신: 새로 켜진 전구가 있으면 그 전구에 퐁 연출.
        public void SetCleared(int cleared)
        {
            int justLit = StageLamps.JustLitIndex(_prevCleared, cleared, Total);
            var lit = StageLamps.Compute(cleared, Total);
            for (int i = 0; i < lamps.Length && i < lit.Length; i++)
            {
                if (lamps[i] == null) continue;
                lamps[i].sprite = lit[i] ? onSprite : offSprite;
            }
            _prevCleared = cleared;
            if (justLit >= 0 && justLit < lamps.Length && lamps[justLit] != null)
                Pop(lamps[justLit].rectTransform);
        }

        void Pop(RectTransform rt)
        {
            if (_pop != null) StopCoroutine(_pop);
            _pop = StartCoroutine(PopRoutine(rt));
        }

        IEnumerator PopRoutine(RectTransform rt)
        {
            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / popDuration);
                float punch = Mathf.Sin(u * Mathf.PI); // 0 -> 1 -> 0
                float s = 1f + (popScale - 1f) * punch;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _pop = null;
        }
    }
}
