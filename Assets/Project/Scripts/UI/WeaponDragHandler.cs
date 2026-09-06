using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponDragHandler : MonoBehaviour
{
    public static WeaponDragHandler Instance { get; private set; }

    [Header("Current Selected Weapon")]
    public WeaponDataSO selectedWeapon;
    public bool IsDragging => isDragging;

    [Header("Drag Ghost UI")]
    [SerializeField] private GameObject weaponGhostObject;
    [SerializeField] private TextMeshProUGUI ghostNameText;

    private bool isDragging = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (weaponGhostObject != null)
        {
            weaponGhostObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isDragging || selectedWeapon == null) return;

        // 마우스 커서 추종
        if (weaponGhostObject != null)
        {
            weaponGhostObject.transform.position = Input.mousePosition;
        }

        // 우클릭 시 드래그 취소 (가방으로 복귀)
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }

    // 가방 또는 슬롯에서 무기 들어 올리기
    public void StartDragWeapon(WeaponDataSO weapon)
    {
        selectedWeapon = weapon;
        isDragging = true;

        if (weaponGhostObject != null)
        {
            weaponGhostObject.SetActive(true);
            Image bg = weaponGhostObject.GetComponent<Image>();
            if (bg != null) bg.color = weapon.GetRarityColor();
            if (ghostNameText != null) ghostNameText.text = weapon.weaponName;
        }

        Debug.Log($"⚔️ [무기 선택] {weapon.weaponName} 손에 쥠");
    }

    // 슬롯에 무기 드롭 (스왑 지원)
    public void DropOnWeaponSlot(int slotIndex)
    {
        if (!isDragging || selectedWeapon == null || PlayerInventory.Instance == null) return;

        WeaponDataSO newWeapon = selectedWeapon;
        WeaponDataSO oldWeapon = PlayerInventory.Instance.equippedWeapons[slotIndex];

        // 1. 새 무기를 지정 슬롯에 안착
        PlayerInventory.Instance.equippedWeapons[slotIndex] = newWeapon;
        Debug.Log($"⚔️ [무기 장착] 슬롯 {slotIndex + 1}번에 {newWeapon.weaponName} 장착 완료!");

        // 2. 기존 슬롯에 무기가 있었다면 그 무기를 마우스 손으로 스왑!
        if (oldWeapon != null)
        {
            Debug.Log($"🔄 [손 스왑] 기존 무기 {oldWeapon.weaponName} 손으로 집어 올림!");
            StartDragWeapon(oldWeapon);
        }
        else
        {
            EndDrag();
        }

        if (MaintenanceUI.Instance != null)
        {
            MaintenanceUI.Instance.RefreshLootPanel();
            MaintenanceUI.Instance.RefreshEquipAndVaultUI();
        }
    }

    // 금고 슬롯에 무기 드롭
    public void DropOnVaultSlot()
    {
        if (!isDragging || selectedWeapon == null || MaintenanceManager.Instance == null) return;

        bool success = MaintenanceManager.Instance.TryVaultWeapon(selectedWeapon);
        if (success)
        {
            PlayerInventory.Instance.collectedWeapons.Remove(selectedWeapon);
            EndDrag();

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }
        }
    }

    // 드래그 취소 (우클릭 또는 가방 드롭 시 안전하게 가방으로 복귀)
    public void CancelDrag()
    {
        if (selectedWeapon != null && PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.collectedWeapons.Add(selectedWeapon); // 가방에 안전하게 추가

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
            }

            Debug.Log($"❌ [무기 취소] {selectedWeapon.weaponName} 가방으로 복귀.");
        }

        EndDrag();
    }

    private void EndDrag()
    {
        isDragging = false;
        selectedWeapon = null;

        if (weaponGhostObject != null)
        {
            weaponGhostObject.SetActive(false);
        }
    }
}