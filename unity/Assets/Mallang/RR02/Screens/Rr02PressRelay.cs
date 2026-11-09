using UnityEngine;
using UnityEngine.EventSystems;

namespace Mallang.RR02.Screens
{
    // 누르는 순간(pointer down) 콜백. 리듬 입력은 손 뗄 때(Button.onClick=release)가 아니라
    // 누르는 즉시 발동해야 반응이 빠르다. 비눗방울 동물 이미지에 붙인다.
    public class Rr02PressRelay : MonoBehaviour, IPointerDownHandler
    {
        public System.Action onDown;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (onDown != null) onDown();
        }
    }
}
