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

    // ⭐ 타일 마우스 올라감 (미리보기 하이라이트 OR 장착 모듈 툴팁 출력)
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 1. 모듈을 들고 있는 상태 ➔ 그리드 안착 미리보기 (초록/빨강)
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            var handler = ModuleDragHandler.Instance;
            if (GridPanelUI.Instance != null)
            {
                GridPanelUI.Instance.HighlightPlacementPreview(gridX, gridY, handler.currentWidth, handler.currentHeight, handler.currentShape);
            }
        }
        // 2. 맨손 상태이고 타일에 이미 모듈이 장착되어 있으면 ➔ 장착 모듈 툴팁 출력! ⭐
        else if (GridInventorySystem.Instance != null && ItemTooltipUI.Instance != null)
        {
            var placed = GridInventorySystem.Instance.GetPlacedModuleAt(gridX, gridY);
            if (placed != null)
            {
                ItemTooltipUI.Instance.ShowModuleTooltip(placed.moduleData);
            }
        }
    }

    // ⭐ 타일 마우스 나감 (하이라이트 및 툴팁 숨김)
    public void OnPointerExit(PointerEventData eventData)
    {
        if (GridPanelUI.Instance != null)
        {
            GridPanelUI.Instance.RefreshGridVisuals();
        }

        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }
    }

    // 타일 클릭 (배치 또는 장착된 모듈 다시 집어 올리기)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (ModuleDragHandler.Instance == null) return;

            // 1. 모듈을 들고 있는 상태 ➔ 안착 시도
            if (ModuleDragHandler.Instance.IsDragging)
            {
                ModuleDragHandler.Instance.TryPlaceOnGrid(gridX, gridY);
            }
            // 2. 맨손 상태 ➔ 장착된 모듈 다시 집기
            else
            {
                if (GridInventorySystem.Instance != null)
                {
                    var placed = GridInventorySystem.Instance.GetPlacedModuleAt(gridX, gridY);
                    if (placed != null)
                    {
                        GridInventorySystem.Instance.RemoveModule(placed);
                        GridPanelUI.Instance.RefreshGridVisuals();

                        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();

                        ModuleDragHandler.Instance.StartDragModule(placed.moduleData);
                        Debug.Log($"🧩 [모듈 재해제] {placed.moduleData.moduleName} 모듈을 다시 집어 올렸습니다.");
                    }
                }
            }
        }
    }
}