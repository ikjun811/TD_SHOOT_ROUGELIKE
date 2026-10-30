using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 좌측 전리품 가방 영역 클릭 시 손에 든 아이템을 가방으로 명시적으로 이관 보관하는 클래스입니다.
/// 가방 안 아이템 카드를 눌러도 같은 처리 (LootItemButtonUI → DepositHeldItem)
/// </summary>
public class LootPanelDropHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        DepositHeldItem();
    }

    // 손에 든 모듈·무기를 가방에 넣음. 들고 있던 게 있으면 true
    public static bool DepositHeldItem()
    {
        bool deposited = false;

        // 1. 손에 모듈을 든 경우
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDataSO module = ModuleDragHandler.Instance.selectedModule;
            if (module != null && PlayerInventory.Instance != null)
            {
                // 중복 검사 없이 무조건 가방 리스트에 안전 추가
                PlayerInventory.Instance.collectedModules.Add(module);
            }

            ModuleDragHandler.Instance.EndDrag();
            deposited = true;
            Debug.Log("LootPanelDropHandler: Deposited module from hand into loot inventory.");
        }

        // 2. 손에 무기를 든 경우
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDataSO weapon = WeaponDragHandler.Instance.selectedWeapon;
            if (weapon != null && PlayerInventory.Instance != null)
            {
                // 중복 검사 없이 무조건 가방 리스트에 안전 추가 (소멸 차단)
                PlayerInventory.Instance.collectedWeapons.Add(weapon);
            }

            WeaponDragHandler.Instance.EndDrag();
            deposited = true;
            Debug.Log("LootPanelDropHandler: Deposited weapon from hand into loot inventory.");
        }

        if (deposited && MaintenanceUI.Instance != null)
        {
            MaintenanceUI.Instance.RefreshLootPanel();
            MaintenanceUI.Instance.RefreshEquipAndVaultUI();
        }
        return deposited;
    }
}
