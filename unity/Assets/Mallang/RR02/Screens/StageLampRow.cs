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

        // 켜진 전구는 환하게 + 조금 크게, 꺼진 전구는 어둑하게 + 작게 — 대비를 뚜렷하게.
        static readonly Color LitColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color OffColor = new Color(0.5f, 0.5f, 0.56f, 0.8f);
        const float LitScale = 1.06f, OffScale = 0.86f;

        void Apply(int i, bool isLit)
        {
            if (lamps[i] == null) return;
            lamps[i].sprite = isLit ? onSprite : offSprite;
            lamps[i].color = isLit ? LitColor : OffColor;
            lamps[i].rectTransform.localScale = Vector3.one * (isLit ? LitScale : OffScale);
        }

        // 전체 즉시 반영(초기화/점프). 연출 없음.
        public void SetClearedInstant(int cleared)
        {
            var lit = StageLamps.Compute(cleared, Total);
            for (int i = 0; i < lamps.Length && i < lit.Length; i++) Apply(i, lit[i]);
            _prevCleared = cleared;
        }

        // 진행 갱신: 새로 켜진 전구가 있으면 그 전구에 퐁 연출.
        public void SetCleared(int cleared)
        {
            int justLit = StageLamps.JustLitIndex(_prevCleared, cleared, Total);
            var lit = StageLamps.Compute(cleared, Total);
            for (int i = 0; i < lamps.Length && i < lit.Length; i++) Apply(i, lit[i]);
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
            // 보잉: 크게 튀어나왔다가 ease-out-back으로 반동하며 '켜짐 크기'로 안착(쫀득).
            float rest = LitScale;
            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / popDuration);
                float eob = EaseOutBack(u);                 // 0 -> 1(끝에서 오버슈트)
                float s = popScale + (rest - popScale) * eob; // popScale -> rest
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one * rest;
            _pop = null;
        }

        static float EaseOutBack(float x)
        {
            float c1 = 1.70158f, c3 = c1 + 1f, p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
