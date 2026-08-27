using UnityEngine;
using UnityEngine.EventSystems;

public class GridSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int gridX;
    public int gridY;

    public void SetupCoordinates(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    // ⭐ 타일 마우스 올라감 (미리보기 하이라이트)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            var handler = ModuleDragHandler.Instance;
            if (GridPanelUI.Instance != null)
            {
                GridPanelUI.Instance.HighlightPlacementPreview(gridX, gridY, handler.currentWidth, handler.currentHeight, handler.currentShape);
            }
        }
    }

    // ⭐ 타일 마우스 나감
    public void OnPointerExit(PointerEventData eventData)
    {
        if (GridPanelUI.Instance != null)
        {
            GridPanelUI.Instance.RefreshGridVisuals();
        }
    }

    // ⭐ 타일 클릭 (배치 또는 장착된 모듈 다시 집어 올리기)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (ModuleDragHandler.Instance == null) return;

            // 1. 이미 모듈을 잡고 있는 상태 ➔ 그리드에 안착 시도
            if (ModuleDragHandler.Instance.IsDragging)
            {
                ModuleDragHandler.Instance.TryPlaceOnGrid(gridX, gridY);
            }
            // 2. 맨손 상태 ➔ 이 타일에 이미 장착되어 있는 모듈을 다시 쓱 들어 올림! ⭐
            else
            {
                if (GridInventorySystem.Instance != null)
                {
                    var placed = GridInventorySystem.Instance.GetPlacedModuleAt(gridX, gridY);
                    if (placed != null)
                    {
                        // 그리드에서 제거 후 다시 손으로 들어 올림!
                        GridInventorySystem.Instance.RemoveModule(placed);
                        GridPanelUI.Instance.RefreshGridVisuals();

                        ModuleDragHandler.Instance.StartDragModule(placed.moduleData);
                        Debug.Log($"🧩 [모듈 재해제] {placed.moduleData.moduleName} 모듈을 다시 집어 올렸습니다.");
                    }
                }
            }
        }
    }
}