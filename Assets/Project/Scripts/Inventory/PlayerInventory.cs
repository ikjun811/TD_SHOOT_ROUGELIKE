using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Current Staged Items (Round Loot)")]
    public List<WeaponDataSO> collectedWeapons = new List<WeaponDataSO>();
    public List<ModuleDataSO> collectedModules = new List<ModuleDataSO>();

    [Header("Equipped Items")]
    public WeaponDataSO[] equippedWeapons = new WeaponDataSO[2]; // 최대 2개 장착

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 무기 획득
    public void AddWeapon(WeaponDataSO weapon)
    {
        collectedWeapons.Add(weapon);
        Debug.Log($" [인벤토리] 무기 획득: {weapon.weaponName} ({weapon.rarity})");
    }

    // 강화모듈 획득
    public void AddModule(ModuleDataSO module)
    {
        collectedModules.Add(module);
        Debug.Log($" [인벤토리] 강화모듈 획득: {module.moduleName} ({module.rarity})");
    }

    // 라운드 종료 후 정비 단계 진입 시 소멸 처리용
    public void ClearUnusedLoot()
    {
        collectedWeapons.Clear();
        collectedModules.Clear();
        Debug.Log("🧹 [정비 완료] 미사용한 필드 획득 아이템이 소멸되었습니다.");
    }
}