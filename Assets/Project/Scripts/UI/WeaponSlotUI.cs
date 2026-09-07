using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 장착 무기 슬롯 및 금고 슬롯의 클릭 이벤트를 처리하는 클래스입니다.
/// </summary>
public class WeaponSlotUI : MonoBehaviour, IPointerClickHandler
{
    public enum SlotType { WeaponSlot, VaultSlot }

    public SlotType slotType = SlotType.WeaponSlot;
    public int weaponSlotIndex = 0;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (WeaponDragHandler.Instance == null) return;

        // 1. 손에 무기를 들고 있는 경우 드롭 또는 스왑
        if (WeaponDragHandler.Instance.IsDragging)
        {
            if (slotType == SlotType.WeaponSlot)
            {
                WeaponDragHandler.Instance.DropOnWeaponSlot(weaponSlotIndex);
            }
            else if (slotType == SlotType.VaultSlot)
            {
                WeaponDragHandler.Instance.DropOnVaultSlot(weaponSlotIndex);
            }
        }
        // 2. 맨손 상태인 경우 장착된 무기 집어 올리기
        else if (slotType == SlotType.WeaponSlot && PlayerInventory.Instance != null)
        {
            WeaponDataSO currentWeapon = PlayerInventory.Instance.equippedWeapons[weaponSlotIndex];
            if (currentWeapon != null)
            {
                PlayerInventory.Instance.equippedWeapons[weaponSlotIndex] = null;

                // 마우스 손에 무기 들기 상태 전환
                WeaponDragHandler.Instance.StartDragWeapon(currentWeapon, DragSource.EquipSlot);

                if (MaintenanceUI.Instance != null)
                {
                    MaintenanceUI.Instance.RefreshEquipAndVaultUI();
                    MaintenanceUI.Instance.RefreshLootPanel();
                }

                Debug.Log($"WeaponSlotUI: Picked up weapon {currentWeapon.weaponName} from slot {weaponSlotIndex + 1}.");
            }
        }
    }
}