using UnityEngine;
using UnityEngine.EventSystems;

public class LootPanelDropHandler : MonoBehaviour, IPointerClickHandler
{
    // 좌측 전리품 가방 패널 영역 클릭 시
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 1. 손에 모듈을 들고 있는 경우 ➔ 가방으로 안전 반납!
            if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
            {
                ModuleDragHandler.Instance.CancelDrag();
                Debug.Log("🎒 [가방 반납] 모듈이 전리품 가방으로 무사히 반납되었습니다.");
            }

            // 2. 손에 무기를 들고 있는 경우 ➔ 가방으로 안전 반납!
            if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            {
                WeaponDragHandler.Instance.CancelDrag();
                Debug.Log("🎒 [가방 반납] 무기가 전리품 가방으로 무사히 반납되었습니다.");
            }
        }
    }
}