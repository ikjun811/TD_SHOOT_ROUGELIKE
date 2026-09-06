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
            if (WeaponDragHandler.Instance == null) return;

            // 1. 손에 무기를 들고 있는 상태 ➔ 이 슬롯에 드롭/스왑
            if (WeaponDragHandler.Instance.IsDragging)
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
            // 2. 맨손 상태 ➔ 이미 장착된 이 슬롯의 무기를 즉시 비우고 손으로 집어 올림! ⭐
            else if (slotType == SlotType.WeaponSlot && PlayerInventory.Instance != null)
            {
                WeaponDataSO currentWeapon = PlayerInventory.Instance.equippedWeapons[weaponSlotIndex];
                if (currentWeapon != null)
                {
                    PlayerInventory.Instance.equippedWeapons[weaponSlotIndex] = null; // 슬롯 즉시 비우기!

                    if (MaintenanceUI.Instance != null)
                    {
                        MaintenanceUI.Instance.RefreshEquipAndVaultUI();
                    }

                    WeaponDragHandler.Instance.StartDragWeapon(currentWeapon);
                    Debug.Log($"⚔️ [슬롯 무기 집기] 슬롯 {weaponSlotIndex + 1}번에서 {currentWeapon.weaponName}을 손으로 들었습니다.");
                }
            }
        }
    }
}