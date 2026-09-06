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
    public DragSource currentSource = DragSource.LootList; // ⭐ 출처 추적
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
    /// 무기 드래그 시작 (출처 지정)
    /// </summary>
    public void StartDragWeapon(WeaponDataSO weapon, DragSource source = DragSource.LootList)
    {
        selectedWeapon = weapon;
        currentSource = source;
        isDragging = true;

        if (currentSource == DragSource.LootList && PlayerInventory.Instance != null)
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

        Debug.Log($"WeaponDragHandler: Selected weapon {weapon.weaponName} from {source}.");
    }

    /// <summary>
    /// 장착 무기 슬롯에 무기 드롭 처리
    /// </summary>
    public void DropOnWeaponSlot(int slotIndex)
    {
        if (!isDragging || selectedWeapon == null || PlayerInventory.Instance == null) return;

        WeaponDataSO newWeapon = selectedWeapon;
        WeaponDataSO oldWeapon = PlayerInventory.Instance.equippedWeapons[slotIndex];

        PlayerInventory.Instance.equippedWeapons[slotIndex] = newWeapon;

        if (oldWeapon != null)
        {
            StartDragWeapon(oldWeapon, DragSource.EquipSlot);
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
    /// 금고 슬롯에 무기 드롭 처리
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

            EndDrag();

            if (oldWeapon != null)
            {
                StartDragWeapon(oldWeapon, DragSource.VaultSlot);
            }
            else if (oldModule != null)
            {
                if (ModuleDragHandler.Instance != null)
                    ModuleDragHandler.Instance.StartDragModule(oldModule, DragSource.VaultSlot);
            }

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }
        }
    }

    /// <summary>
    /// 드래그 취소 시 출처에 따른 복귀 처리 (금고 출처는 금고 보존 ⭐)
    /// </summary>
    public void CancelDrag()
    {
        if (selectedWeapon != null)
        {
            // 금고 출처인 경우 금고 빈 슬롯에 우선 복귀 시도
            if (currentSource == DragSource.VaultSlot && MaintenanceManager.Instance != null)
            {
                bool restoredToVault = false;
                foreach (var slot in MaintenanceManager.Instance.vaultSlots)
                {
                    if (slot.IsEmpty)
                    {
                        slot.weapon = selectedWeapon;
                        restoredToVault = true;
                        break;
                    }
                }

                if (!restoredToVault && PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.collectedWeapons.Add(selectedWeapon);
                }
            }
            else if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.collectedWeapons.Add(selectedWeapon);
            }

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }
        }

        EndDrag();
    }

    public void EndDrag()
    {
        isDragging = false;
        selectedWeapon = null;

        if (weaponGhostObject != null)
        {
            weaponGhostObject.SetActive(false);
        }
    }
}