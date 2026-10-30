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

    // 튜토리얼(TutorialDirector)용: NEXT ROUND 시 호출, 막을 이유(글)를 돌려주면 시작하지 않음 (null = 통과)
    public System.Func<string> NextRoundBlocker { get; set; }
    // 마지막으로 NEXT ROUND가 막힌 이유 (MaintenanceUI 경고 글)
    public string LastBlockReason { get; private set; }

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

        // 정비 중 클릭이 사격으로 처리되지 않도록 플레이어 입력 차단 (다음 라운드 시작 시 복구)
        if (PlayerStatAndWeaponSync.Instance != null)
        {
            PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(false);
        }

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
        if (ui == null) ui = FindAnyObjectByType<MaintenanceUI>(FindObjectsInactive.Include);
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

        LastBlockReason = NextRoundBlocker?.Invoke();
        if (!string.IsNullOrEmpty(LastBlockReason))
        {
            Debug.Log($"MaintenanceManager: Next round blocked — {LastBlockReason}");
            return false;
        }

        if (PlayerInventory.Instance != null)
        {
            // 맨손 시작 방지: 장착 슬롯이 모두 비면 정비 유지 (사용자 2026-10-30: 가방 무기 자동 장착은 모르는 사이 진행돼 제거)
            if (PlayerInventory.Instance.equippedWeapons[0] == null && PlayerInventory.Instance.equippedWeapons[1] == null)
            {
                Debug.LogWarning("MaintenanceManager: No weapon equipped. Next round blocked.");
                LastBlockReason = "무기를 1개 이상 장착하세요";
                return false;
            }

            // 같은 종류(같은 JU 총) 2개는 한 자루로 처리돼 한 슬롯이 무시됨 (장착은 WeaponDragHandler가 막지만, 취소 복귀 등 다른 경로 대비)
            WeaponDataSO a = PlayerInventory.Instance.equippedWeapons[0];
            WeaponDataSO b = PlayerInventory.Instance.equippedWeapons[1];
            if (a != null && b != null && a.juItemName == b.juItemName)
            {
                LastBlockReason = "같은 종류의 무기 2개는 함께 장착할 수 없습니다";
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
        // 1. 맵 재생성 (BSP 생성기가 있으면 새 맵 + NavMesh 재베이크 + 플레이어를 새 시작 구역으로 이동)
        BspArenaGenerator arenaGenerator = FindAnyObjectByType<BspArenaGenerator>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (arenaGenerator != null)
        {
            arenaGenerator.Generate();
        }
        // 생성기가 없으면(기존 고정 맵) 플레이어 위치 및 물리 속도 초기화
        else if (playerObj != null)
        {
            playerObj.transform.position = new Vector3(0f, 1f, 0f); // 시작 좌표 이동
            Rigidbody rb = playerObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        // 2. 탈출 구역 무작위 재배치 및 타이머 리셋
        ExtractionZone ez = FindAnyObjectByType<ExtractionZone>();
        if (ez != null)
        {
            ez.RelocateAndResetZone();
        }

        // 3. 난이도 경과 시간 및 드랍률 리셋
        if (StageDifficultyManager.Instance != null)
        {
            StageDifficultyManager.Instance.ResetStageDifficulty();
        }

        // 3-1. 보급 상자 재생성 (플레이어·탈출 구역 재배치 이후라 거리 조건이 맞게 적용됨)
        if (SupplyCrateManager.Instance != null)
        {
            SupplyCrateManager.Instance.RespawnCrates();
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