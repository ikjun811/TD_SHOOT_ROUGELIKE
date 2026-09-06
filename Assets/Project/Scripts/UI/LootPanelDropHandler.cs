using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 좌측 전리품 가방 영역 클릭 시 손에 든 아이템을 가방으로 명시적으로 이관 보관하는 클래스입니다.
/// </summary>
public class LootPanelDropHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // 1. 손에 모듈을 들고 좌측 가방 영역을 클릭한 경우
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDataSO module = ModuleDragHandler.Instance.selectedModule;
            if (module != null && PlayerInventory.Instance != null)
            {
                if (!PlayerInventory.Instance.collectedModules.Contains(module))
                {
                    PlayerInventory.Instance.collectedModules.Add(module);
                }
            }

            // 가방에 이관 저장 후 드래그 깔끔히 종료
            ModuleDragHandler.Instance.EndDrag();

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }

            Debug.Log("LootPanelDropHandler: Deposited module from hand into loot inventory.");
        }

        // 2. 손에 무기를 들고 좌측 가방 영역을 클릭한 경우
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDataSO weapon = WeaponDragHandler.Instance.selectedWeapon;
            if (weapon != null && PlayerInventory.Instance != null)
            {
                if (!PlayerInventory.Instance.collectedWeapons.Contains(weapon))
                {
                    PlayerInventory.Instance.collectedWeapons.Add(weapon);
                }
            }

            // 가방에 이관 저장 후 드래그 깔끔히 종료
            WeaponDragHandler.Instance.EndDrag();

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }

            Debug.Log("LootPanelDropHandler: Deposited weapon from hand into loot inventory.");
        }
    }
}