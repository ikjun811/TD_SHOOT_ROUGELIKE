using UnityEngine;
using UnityEngine.EventSystems;

public class WeaponSlotUI : MonoBehaviour, IPointerClickHandler
{
    public enum SlotType { WeaponSlot, VaultSlot }

    public SlotType slotType = SlotType.WeaponSlot;
    public int weaponSlotIndex = 0; // 0: 슬롯1, 1: 슬롯2

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 1. 마우스에 무기를 들고 있는 상태 ➔ 이 슬롯에 드롭/스왑!
            if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            {
                if (slotType == SlotType.WeaponSlot)
                {
                    WeaponDragHandler.Instance.DropOnWeaponSlot(weaponSlotIndex);
                }
                else if (slotType == SlotType.VaultSlot)
                {
                    WeaponDragHandler.Instance.DropOnVaultSlot();
                }
            }
            // 2. 맨손 상태 ➔ 이미 장착된 이 슬롯의 무기를 다시 손으로 들어 올림! ⭐
            else if (slotType == SlotType.WeaponSlot && PlayerInventory.Instance != null)
            {
                WeaponDataSO currentWeapon = PlayerInventory.Instance.equippedWeapons[weaponSlotIndex];
                if (currentWeapon != null)
                {
                    // 슬롯을 비우고 손으로 집어 올림
                    PlayerInventory.Instance.equippedWeapons[weaponSlotIndex] = null;
                    WeaponDragHandler.Instance.StartDragWeapon(currentWeapon);

                    if (MaintenanceUI.Instance != null)
                    {
                        MaintenanceUI.Instance.RefreshEquipAndVaultUI();
                    }
                }
            }
        }
    }
}