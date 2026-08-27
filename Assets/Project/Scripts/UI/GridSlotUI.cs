using UnityEngine;
using UnityEngine.EventSystems;

public class GridSlotUI : MonoBehaviour, IPointerClickHandler
{
    public int gridX;
    public int gridY;

    public void SetupCoordinates(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    // 타일 클릭 시 동작
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 드래그 중인 모듈이 있다면 이 타일에 배치 시도
            if (ModuleDragHandler.Instance != null)
            {
                ModuleDragHandler.Instance.TryPlaceOnGrid(gridX, gridY);
            }
        }
    }
}