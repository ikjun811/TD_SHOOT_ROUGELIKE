using System.Collections.Generic;
using UnityEngine;

// 정비 단계 상태 및 금고(Vault) 보관함 데이터를 통합 관리하는 매니저 클래스
public class MaintenanceManager : MonoBehaviour
{
    public static MaintenanceManager Instance { get; private set; }

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

    public void EnterMaintenanceStage()
    {
        isInMaintenance = true;

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.EnsureAllItemsCollected();
        }

        Time.timeScale = 0f; // 전투 일시정지

        MaintenanceUI ui = MaintenanceUI.Instance;
        if (ui == null) ui = FindObjectOfType<MaintenanceUI>(true);
        if (ui != null) ui.OpenUI();

        Debug.Log("MaintenanceManager: Enter Maintenance Stage.");
    }

    // ⭐ [개편] 정비 완료 후 다음 라운드 시스템 전체 리셋 및 스폰 재개
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
                    Debug.LogWarning("MaintenanceManager: Auto-equipped weapon to Slot 1.");
                }
            }
        }

        isInMaintenance = false;
        Time.timeScale = 1f; // 전투 재개

        // 3. 미사용 잔여 전리품 가방 소멸
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearUnusedLoot();
        }

        // 4. 3D 캐릭터에게 무기 및 테트리스 스탯 실전 적용
        ApplyMaintenanceResultsTo3DPlayer();

        // 5. ⭐ 다음 라운드 시스템 전체 리셋 및 전투 시작!
        ResetAndStartNextRound();

        Debug.Log("MaintenanceManager: Next round started successfully.");
    }

    // 다음 라운드 전체 시스템 리셋 함수
    private void ResetAndStartNextRound()
    {
        // 탈출 구역 60초 타이머 0초로 초기화
        ExtractionZone ez = FindObjectOfType<ExtractionZone>();
        if (ez != null)
        {
            ez.ResetExtractionZone();
        }

        // 난이도 경과 시간 및 드랍률(35%) 리셋
        if (StageDifficultyManager.Instance != null)
        {
            StageDifficultyManager.Instance.ResetStageDifficulty();
        }

        // 웨이브 적 무한 스폰 코루틴 재가동
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.RestartWaveSpawning();
        }
    }

    private void ApplyMaintenanceResultsTo3DPlayer()
    {
        if (PlayerStatAndWeaponSync.Instance != null)
        {
            PlayerStatAndWeaponSync.Instance.ApplyMaintenanceResultsToPlayer();
        }
    }
}