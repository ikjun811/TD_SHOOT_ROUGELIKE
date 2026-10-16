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

    [Header("Starting Weapon")]
    [SerializeField] private WeaponDataSO startingWeapon; // 게임 시작 시 지급 (UMP)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 장착 슬롯이 모두 비어 있으면 시작 무기를 1번 슬롯에 지급
        if (startingWeapon != null && equippedWeapons[0] == null && equippedWeapons[1] == null)
        {
            equippedWeapons[0] = startingWeapon;
            Debug.Log($" [인벤토리] 시작 무기 지급: {startingWeapon.weaponName}");
        }
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