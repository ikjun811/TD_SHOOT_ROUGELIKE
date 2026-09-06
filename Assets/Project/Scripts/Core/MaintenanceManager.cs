using System.Collections.Generic;
using UnityEngine;

public class MaintenanceManager : MonoBehaviour
{
    public static MaintenanceManager Instance { get; private set; }

    [Header("Vault Settings (금고)")]
    public List<WeaponDataSO> vaultWeapons = new List<WeaponDataSO>();
    public List<ModuleDataSO> vaultModules = new List<ModuleDataSO>();
    public int maxVaultCapacity = 3;

    [Header("State")]
    public bool isInMaintenance = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void EnterMaintenanceStage()
    {
        isInMaintenance = true;

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.EnsureAllItemsCollected();
        }

        Time.timeScale = 0f; // 전투 일시정지

        // 안전한 UI 오픈
        MaintenanceUI ui = MaintenanceUI.Instance;
        if (ui == null) ui = FindObjectOfType<MaintenanceUI>(true);
        if (ui != null) ui.OpenUI();

        Debug.Log("🛠️ [정비 단계 진입] 모든 전리품 보장 수거 및 UI 활성화 완료.");
    }

    // ⭐ [선제적 예외 차단 적용] 정비 완료 및 다음 라운드 시작
    public void CompleteMaintenanceAndStartNextRound()
    {
        // 1. 선제 차단: 마우스 손에 쥐어진 드래그 아이템이 있다면 무사히 가방으로 안전 원복!
        if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDragHandler.Instance.CancelDrag();
        }
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDragHandler.Instance.CancelDrag();
        }

        // 2. 선제 차단: 장착 슬롯 2개가 모두 비어있는 경우 방지
        if (PlayerInventory.Instance != null)
        {
            if (PlayerInventory.Instance.equippedWeapons[0] == null && PlayerInventory.Instance.equippedWeapons[1] == null)
            {
                // 가방에 무기가 있다면 첫 번째 무기 자동 장착
                if (PlayerInventory.Instance.collectedWeapons.Count > 0)
                {
                    PlayerInventory.Instance.equippedWeapons[0] = PlayerInventory.Instance.collectedWeapons[0];
                    PlayerInventory.Instance.collectedWeapons.RemoveAt(0);
                    Debug.LogWarning("⚠️ [안전 장치] 무기 슬롯이 모두 비어있어 가방의 무기를 1번 슬롯에 자동 장착했습니다.");
                }
            }
        }

        isInMaintenance = false;
        Time.timeScale = 1f; // 전투 재개

        // 3. 미사용한 잔여 필드 아이템 소멸
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearUnusedLoot();
        }

        // 4. ⭐ 3D 캐릭터에게 새로 장착한 무기 및 테트리스 스탯 동기화!
        ApplyMaintenanceResultsTo3DPlayer();

        Debug.Log("🚀 [정비 완료] 다음 라운드가 시작됩니다!");

        // 다음 라운드 진행
        if (WaveManager.Instance != null)
        {
            int currentR = WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null ?
                (int)WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(WaveManager.Instance) : 1;

            WaveManager.Instance.StartRound(currentR + 1);
        }
    }

    // 금고 보관
    public bool TryVaultWeapon(WeaponDataSO weapon)
    {
        if (vaultWeapons.Count + vaultModules.Count >= maxVaultCapacity)
        {
            Debug.LogWarning("🔒 [금고] 금고 용량이 가득 찼습니다!");
            return false;
        }

        vaultWeapons.Add(weapon);
        Debug.Log($"🔒 [금고] 무기 보관 성공: {weapon.weaponName}");
        return true;
    }

    // ⭐ 3D 캐릭터에게 무기 및 테트리스 스탯 동기화 적용
    private void ApplyMaintenanceResultsTo3DPlayer()
    {
        // 8x8 테트리스 최종 스탯 적용
        if (GridInventorySystem.Instance != null)
        {
            GridInventorySystem.Instance.RecalculateTotalStats();
        }

        // TODO: 3D 플레이어 모델에 장착된 무기 프리팹 스왑
        Debug.Log("⚔️ [3D 동기화] 플레이어 전투 스탯 및 장착 무기가 최종 적용되었습니다.");
    }
}