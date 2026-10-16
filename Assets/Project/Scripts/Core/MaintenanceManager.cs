using System.Collections.Generic;
using UnityEngine;

// 정비 단계 상태, 전투 HUD 토글 및 다음 라운드 리셋 제어 클래스
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

        Time.timeScale = 0f;

        // 전투 HUD 비활성화
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.SetHUDActive(false);
        }

        MaintenanceUI ui = MaintenanceUI.Instance;
        if (ui == null) ui = FindObjectOfType<MaintenanceUI>(true);
        if (ui != null) ui.OpenUI();

        Debug.Log("MaintenanceManager: Enter Maintenance Stage.");
    }

    // 정비 종료 및 다음 라운드 시작 (장착 무기가 하나도 없으면 시작하지 않고 false 반환)
    public bool CompleteMaintenanceAndStartNextRound()
    {
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDragHandler.Instance.CancelDrag();
        }
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDragHandler.Instance.CancelDrag();
        }

        if (PlayerInventory.Instance != null)
        {
            if (PlayerInventory.Instance.equippedWeapons[0] == null && PlayerInventory.Instance.equippedWeapons[1] == null)
            {
                if (PlayerInventory.Instance.collectedWeapons.Count > 0)
                {
                    PlayerInventory.Instance.equippedWeapons[0] = PlayerInventory.Instance.collectedWeapons[0];
                    PlayerInventory.Instance.collectedWeapons.RemoveAt(0);
                }
            }

            // 맨손 시작 방지: 가방에도 무기가 없어 장착 슬롯이 모두 비면 정비 유지
            if (PlayerInventory.Instance.equippedWeapons[0] == null && PlayerInventory.Instance.equippedWeapons[1] == null)
            {
                Debug.LogWarning("MaintenanceManager: No weapon equipped. Next round blocked.");
                return false;
            }
        }

        isInMaintenance = false;
        Time.timeScale = 1f;

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearUnusedLoot();
        }

        ApplyMaintenanceResultsTo3DPlayer();

        // 전투 HUD 다시 활성화
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.SetHUDActive(true);
        }

        // 다음 라운드 리셋 및 플레이어/탈출지점 재배치
        ResetAndStartNextRound();

        Debug.Log("MaintenanceManager: Next round started successfully.");
        return true;
    }

    private void ResetAndStartNextRound()
    {
        // 1. 플레이어 위치 및 물리 속도 초기화
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerObj.transform.position = new Vector3(0f, 1f, 0f); // 시작 좌표 이동
            Rigidbody rb = playerObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        // 2. 탈출 구역 무작위 재배치 및 타이머 리셋
        ExtractionZone ez = FindObjectOfType<ExtractionZone>();
        if (ez != null)
        {
            ez.RelocateAndResetZone();
        }

        // 3. 난이도 경과 시간 및 드랍률 리셋
        if (StageDifficultyManager.Instance != null)
        {
            StageDifficultyManager.Instance.ResetStageDifficulty();
        }

        // 4. 웨이브 적 무한 스폰 코루틴 재가동
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