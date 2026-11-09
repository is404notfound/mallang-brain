using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mallang.RR02.Screens
{
    // 버튼 눌림 쫀득 피드백: 누르면 0.9로 쏙 들어가고, 떼면 오버슈트(ease-out-back)로 통통 복귀.
    // 베이크된 버튼 아트 위 투명 히트박스에 붙이면 스케일 변화가 안 보이므로, 실제 스프라이트 버튼에 붙인다.
    public class Rr02ButtonPunch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float pressScale = 0.9f;
        Vector3 _base = Vector3.one;
        bool _init;
        Coroutine _co;

        void OnEnable() { if (!_init) { _base = transform.localScale; _init = true; } }

        public void OnPointerDown(PointerEventData e)
        {
            if (!_init) { _base = transform.localScale; _init = true; }
            if (_co != null) StopCoroutine(_co);
            transform.localScale = _base * pressScale;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_co != null) StopCoroutine(_co);
            if (isActiveAndEnabled) _co = StartCoroutine(Spring());
            else transform.localScale = _base;
        }

        IEnumerator Spring()
        {
            float t = 0f, dur = 0.32f;
            Vector3 from = transform.localScale;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float e = 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f); // ease-out-back
                transform.localScale = Vector3.LerpUnclamped(from, _base, e);
                yield return null;
            }
            transform.localScale = _base;
        }
    }
}
