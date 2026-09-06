using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정비 단계 상태 및 금고(Vault) 보관함 데이터를 통합 관리하는 매니저 클래스입니다.
/// </summary>
public class MaintenanceManager : MonoBehaviour
{
    public static MaintenanceManager Instance { get; private set; }

    /// <summary>
    /// 금고 단일 슬롯에 보관되는 무기 또는 모듈 데이터 구조체입니다.
    /// </summary>
    [System.Serializable]
    public class VaultSlotItem
    {
        public WeaponDataSO weapon;
        public ModuleDataSO module;

        public bool IsEmpty => weapon == null && module == null;

        public void Clear()
        {
            weapon = null;
            module = null;
        }
    }

    [Header("Vault Settings")]
    public int maxVaultCapacity = 3;
    public VaultSlotItem[] vaultSlots = new VaultSlotItem[3];

    [Header("State")]
    public bool isInMaintenance = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeVaultSlots();
    }

    /// <summary>
    /// 금고 슬롯 배열 데이터 초기화
    /// </summary>
    private void InitializeVaultSlots()
    {
        if (vaultSlots == null || vaultSlots.Length != maxVaultCapacity)
        {
            vaultSlots = new VaultSlotItem[maxVaultCapacity];
        }

        for (int i = 0; i < maxVaultCapacity; i++)
        {
            if (vaultSlots[i] == null)
            {
                vaultSlots[i] = new VaultSlotItem();
            }
        }
    }

    /// <summary>
    /// 라운드 클리어 후 정비 단계 진입 시 호출
    /// </summary>
    public void EnterMaintenanceStage()
    {
        isInMaintenance = true;

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.EnsureAllItemsCollected();
        }

        Time.timeScale = 0f;

        MaintenanceUI ui = MaintenanceUI.Instance;
        if (ui == null) ui = FindObjectOfType<MaintenanceUI>(true);
        if (ui != null) ui.OpenUI();

        Debug.Log("MaintenanceManager: Enter Maintenance Stage.");
    }

    /// <summary>
    /// 정비 완료 후 다음 라운드 진입 처리
    /// </summary>
    public void CompleteMaintenanceAndStartNextRound()
    {
        // 1. 드래그 중인 아이템 안전 원복
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDragHandler.Instance.CancelDrag();
        }
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDragHandler.Instance.CancelDrag();
        }

        // 2. 무기 최소 장착 여부 검증
        if (PlayerInventory.Instance != null)
        {
            if (PlayerInventory.Instance.equippedWeapons[0] == null && PlayerInventory.Instance.equippedWeapons[1] == null)
            {
                if (PlayerInventory.Instance.collectedWeapons.Count > 0)
                {
                    PlayerInventory.Instance.equippedWeapons[0] = PlayerInventory.Instance.collectedWeapons[0];
                    PlayerInventory.Instance.collectedWeapons.RemoveAt(0);
                    Debug.LogWarning("MaintenanceManager: Auto-equipped weapon to Slot 1 because all slots were empty.");
                }
            }
        }

        isInMaintenance = false;
        Time.timeScale = 1f;

        // 3. 미사용 잔여 아이템 소멸 (금고에 보관된 아이템은 유지가 보장됨)
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearUnusedLoot();
        }

        ApplyMaintenanceResultsTo3DPlayer();

        if (WaveManager.Instance != null)
        {
            int currentR = WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null ?
                (int)WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(WaveManager.Instance) : 1;

            WaveManager.Instance.StartRound(currentR + 1);
        }
    }

    /// <summary>
    /// 3D 플레이어 개체에 정비 스탯 및 무기 적용
    /// </summary>
    private void ApplyMaintenanceResultsTo3DPlayer()
    {
        if (GridInventorySystem.Instance != null)
        {
            GridInventorySystem.Instance.RecalculateTotalStats();
        }

        Debug.Log("MaintenanceManager: Applied maintenance setup to 3D Player.");
    }
}