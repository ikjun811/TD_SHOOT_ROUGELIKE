using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 정비 UI 상에서 무기 아이템의 마우스 드래그, 장착 슬롯 스왑, 금고 슬롯 배치를 제어하는 매니저 클래스입니다.
/// </summary>
public class WeaponDragHandler : MonoBehaviour
{
    public static WeaponDragHandler Instance { get; private set; }

    [Header("Current Selected Weapon")]
    public WeaponDataSO selectedWeapon;
    public bool isFromLootList = false;
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

        if (weaponGhostObject != null)
        {
            weaponGhostObject.transform.position = Input.mousePosition;
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }

    /// <summary>
    /// 전리품 가방 또는 장착 슬롯에서 무기 드래그 시작
    /// </summary>
    public void StartDragWeapon(WeaponDataSO weapon, bool fromLootList = false)
    {
        selectedWeapon = weapon;
        isFromLootList = fromLootList;
        isDragging = true;

        if (isFromLootList && PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.collectedWeapons.Remove(weapon);
            if (MaintenanceUI.Instance != null) MaintenanceUI.Instance.RefreshLootPanel();
        }

        if (weaponGhostObject != null)
        {
            weaponGhostObject.SetActive(true);
            Image bg = weaponGhostObject.GetComponent<Image>();
            if (bg != null) bg.color = weapon.GetRarityColor();
            if (ghostNameText != null) ghostNameText.text = weapon.weaponName;
        }

        Debug.Log($"WeaponDragHandler: Selected weapon {weapon.weaponName}.");
    }

    /// <summary>
    /// 장착 무기 슬롯에 무기 드롭 처리 (스왑 지원)
    /// </summary>
    public void DropOnWeaponSlot(int slotIndex)
    {
        if (!isDragging || selectedWeapon == null || PlayerInventory.Instance == null) return;

        WeaponDataSO newWeapon = selectedWeapon;
        WeaponDataSO oldWeapon = PlayerInventory.Instance.equippedWeapons[slotIndex];

        PlayerInventory.Instance.equippedWeapons[slotIndex] = newWeapon;

        if (oldWeapon != null)
        {
            StartDragWeapon(oldWeapon, false);
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

    /// <summary>
    /// 금고 슬롯에 무기 드롭 처리 (개편된 금고 3슬롯 구조 반영)
    /// </summary>
    public void DropOnVaultSlot(int vaultIndex = 0)
    {
        if (!isDragging || selectedWeapon == null || MaintenanceManager.Instance == null) return;

        if (vaultIndex >= 0 && vaultIndex < MaintenanceManager.Instance.vaultSlots.Length)
        {
            var slotData = MaintenanceManager.Instance.vaultSlots[vaultIndex];
            WeaponDataSO oldWeapon = slotData.weapon;
            ModuleDataSO oldModule = slotData.module;

            slotData.weapon = selectedWeapon;
            slotData.module = null;

            if (oldWeapon != null)
            {
                StartDragWeapon(oldWeapon, false);
            }
            else if (oldModule != null)
            {
                EndDrag();
                if (ModuleDragHandler.Instance != null)
                    ModuleDragHandler.Instance.StartDragModule(oldModule, false);
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
    }

    /// <summary>
    /// 드래그 취소 시 전리품 가방으로 무기 안전 복귀
    /// </summary>
    public void CancelDrag()
    {
        if (selectedWeapon != null && PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.collectedWeapons.Add(selectedWeapon);

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
            }

            Debug.Log($"WeaponDragHandler: Returned weapon {selectedWeapon.weaponName} to loot list.");
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