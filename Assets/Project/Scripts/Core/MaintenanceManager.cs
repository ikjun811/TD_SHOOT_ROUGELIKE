using System.Collections.Generic;
using UnityEngine;

public class MaintenanceManager : MonoBehaviour
{
    public static MaintenanceManager Instance { get; private set; }

    [Header("Vault Settings (금고)")]
    public List<WeaponDataSO> vaultWeapons = new List<WeaponDataSO>();
    public List<ModuleDataSO> vaultModules = new List<ModuleDataSO>();
    public int maxVaultCapacity = 3; // 금고 기본 3칸 (영구 강화로 확장 가능)

    [Header("State")]
    public bool isInMaintenance = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // WaveManager에서 라운드 클리어 시 호출됨
    public void EnterMaintenanceStage()
    {
        isInMaintenance = true;

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.EnsureAllItemsCollected();
        }

        Time.timeScale = 0f; // 전투 일시정지

        MaintenanceUI ui = MaintenanceUI.Instance;
        if (ui == null)
        {
            ui = FindObjectOfType<MaintenanceUI>(true);
        }

        if (ui != null)
        {
            ui.OpenUI();
        }
        else
        {
            Debug.LogError("[Error] 씬에 MaintenanceUI 스크립트가 없습니다!");
        }

        Debug.Log("[정비 단계 진입] 모든 전리품 보장 수거 및 UI 활성화 완료.");
    }

    // 정비 완료 버튼 클릭 시 호출
    public void CompleteMaintenanceAndStartNextRound()
    {
        isInMaintenance = false;
        Time.timeScale = 1f; // 전투 재개

        // 미사용한 획득 아이템 소멸 (금고에 넣은 것은 유지)
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearUnusedLoot();
        }

        Debug.Log("🚀 [정비 완료] 다음 라운드를 시작합니다!");

        // 다음 라운드 진행
        if (WaveManager.Instance != null)
        {
            // 다음 라운드 시작
            WaveManager.Instance.StartRound(WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null ?
                (int)WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(WaveManager.Instance) + 1 : 2);
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


}