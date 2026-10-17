using System.Collections.Generic;
using UnityEngine;

// 아이템 드랍 및 확률 보장 매니저 클래스
public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Drop Prefab")]
    [SerializeField] private GameObject droppedItemPrefab;

    [Header("Item Pools")]
    [SerializeField] private List<WeaponDataSO> allWeapons = new List<WeaponDataSO>();
    [SerializeField] private List<ModuleDataSO> allModules = new List<ModuleDataSO>();

    // 라운드 구간별 레어도 확률 (%) — fromRound 이상인 마지막 구간을 사용 (GDD 8-4 #15)
    [System.Serializable]
    public class RarityTier
    {
        public int fromRound;
        public float common, rare, elite, legendary;

        public RarityTier(int fromRound, float common, float rare, float elite, float legendary)
        {
            this.fromRound = fromRound;
            this.common = common;
            this.rare = rare;
            this.elite = elite;
            this.legendary = legendary;
        }
    }

    [Header("Rarity Table (라운드 구간별 %, 합계 100 권장)")]
    [SerializeField] private List<RarityTier> rarityTable = new List<RarityTier>
    {
        new RarityTier(1,  70f, 27f, 3f,  0f),
        new RarityTier(4,  50f, 35f, 13f, 2f),
        new RarityTier(7,  35f, 38f, 22f, 5f),
        new RarityTier(10, 20f, 40f, 30f, 10f),
        new RarityTier(13, 10f, 35f, 40f, 15f),
    };

    [Header("Supply Crate Weapon (GDD 8-6 #13)")]
    [Tooltip("장착 슬롯·금고에 이미 있는 무기군이 뽑힐 가중치 (1 = 억제 없음)")]
    [Range(0f, 1f)][SerializeField] private float ownedWeaponTypeWeight = 0.3f;

    [Header("Fallback Drop Settings")]
    [Range(0f, 1f)][SerializeField] private float overallDropChance = 0.35f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 적 사망 시 드랍 시도 (실시간 감쇄 드랍률 및 천장 연동)
    public void TryDropItem(Vector3 spawnPosition, int currentRound)
    {
        StageDifficultyManager sdm = StageDifficultyManager.Instance;

        // StageDifficultyManager의 실시간 드랍률 값 가져오기
        float activeDropChance = sdm != null ? sdm.currentDropChance : overallDropChance;

        bool shouldDrop = Random.value <= activeDropChance;
        bool isPityDrop = false;

        // 천장: 발동 횟수가 남아 있고 연속 미드랍이 기준(3회)에 도달했으면 확정 드랍 (GDD 6-2)
        if (!shouldDrop && sdm != null
            && sdm.currentPityUsedCount < sdm.maxEarlyPityCount
            && sdm.consecutiveMissCount >= sdm.pityMissThreshold)
        {
            shouldDrop = true;
            isPityDrop = true;
        }

        if (!shouldDrop)
        {
            if (sdm != null) sdm.consecutiveMissCount++;
            return;
        }

        if (sdm != null)
        {
            sdm.consecutiveMissCount = 0;

            if (isPityDrop)
            {
                // 천장 드랍은 발동 횟수만 올리고 드랍률 감쇄는 하지 않음
                sdm.currentPityUsedCount++;
                Debug.Log($"ItemDropManager: Pity drop ({sdm.currentPityUsedCount}/{sdm.maxEarlyPityCount}).");
            }
            else
            {
                // 일반 드랍 시 드랍률 감쇄 (-5%)
                sdm.RegisterItemObtained();
            }
        }

        // 적은 모듈만 드랍 (무기는 보급 상자에서 획득, GDD 8-6 #5·#13)
        RarityType selectedRarity = DetermineRarityByRound(currentRound);

        ModuleDataSO module = GetRandomModuleOfRarity(selectedRarity);
        if (module != null) SpawnDrop(spawnPosition, null, module);
    }

    // 레어도 표에서 현재 라운드 구간을 찾아 확률대로 레어도 결정
    private RarityType DetermineRarityByRound(int round)
    {
        RarityTier tier = null;
        foreach (RarityTier t in rarityTable)
        {
            if (t != null && round >= t.fromRound && (tier == null || t.fromRound >= tier.fromRound)) tier = t;
        }
        if (tier == null) return RarityType.Common;

        float total = tier.common + tier.rare + tier.elite + tier.legendary;
        if (total <= 0f) return RarityType.Common;

        float rand = Random.value * total;
        if (rand < tier.legendary) return RarityType.Legendary;
        rand -= tier.legendary;
        if (rand < tier.elite) return RarityType.Elite;
        rand -= tier.elite;
        if (rand < tier.rare) return RarityType.Rare;
        return RarityType.Common;
    }

    // 보급 상자 무기 드랍: 레어도 표 기준, 장착·금고에 있는 무기군은 낮은 가중치 (중복 억제)
    public void DropCrateWeapon(Vector3 position, int currentRound)
    {
        RarityType rarity = DetermineRarityByRound(currentRound);

        List<WeaponDataSO> candidates = allWeapons.FindAll(w => w != null && w.rarity == rarity);
        if (candidates.Count == 0) candidates = allWeapons.FindAll(w => w != null);
        if (candidates.Count == 0) return;

        HashSet<WeaponType> ownedTypes = GetOwnedWeaponTypes();

        float totalWeight = 0f;
        foreach (WeaponDataSO w in candidates) totalWeight += ownedTypes.Contains(w.weaponType) ? ownedWeaponTypeWeight : 1f;

        WeaponDataSO picked = candidates[candidates.Count - 1];
        if (totalWeight > 0f)
        {
            float rand = Random.value * totalWeight;
            foreach (WeaponDataSO w in candidates)
            {
                rand -= ownedTypes.Contains(w.weaponType) ? ownedWeaponTypeWeight : 1f;
                if (rand <= 0f) { picked = w; break; }
            }
        }

        SpawnDrop(position, picked, null);
        Debug.Log($"ItemDropManager: Supply crate -> {picked.weaponName} [{picked.rarity}]");
    }

    // 장착 슬롯과 금고에 있는 무기군
    private HashSet<WeaponType> GetOwnedWeaponTypes()
    {
        HashSet<WeaponType> owned = new HashSet<WeaponType>();

        if (PlayerInventory.Instance != null)
        {
            foreach (WeaponDataSO w in PlayerInventory.Instance.equippedWeapons)
            {
                if (w != null) owned.Add(w.weaponType);
            }
        }

        if (MaintenanceManager.Instance != null && MaintenanceManager.Instance.vaultSlots != null)
        {
            foreach (MaintenanceManager.VaultSlotItem slot in MaintenanceManager.Instance.vaultSlots)
            {
                if (slot != null && slot.weapon != null) owned.Add(slot.weapon.weaponType);
            }
        }

        return owned;
    }

    private ModuleDataSO GetRandomModuleOfRarity(RarityType rarity)
    {
        List<ModuleDataSO> filtered = allModules.FindAll(m => m.rarity == rarity);
        if (filtered.Count == 0) filtered = allModules;
        return filtered.Count > 0 ? filtered[Random.Range(0, filtered.Count)] : null;
    }

    private void SpawnDrop(Vector3 pos, WeaponDataSO weapon, ModuleDataSO module)
    {
        if (droppedItemPrefab == null) return;

        GameObject dropObj = Instantiate(droppedItemPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
        DroppedItem droppedItem = dropObj.GetComponent<DroppedItem>();

        if (droppedItem != null)
        {
            if (weapon != null) droppedItem.SetupWeapon(weapon);
            else if (module != null) droppedItem.SetupModule(module);
        }
    }

    public void CollectAllRemainingDropsOnField()
    {
        DroppedItem[] remainingItems = FindObjectsByType<DroppedItem>(FindObjectsSortMode.None);
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null || remainingItems.Length == 0) return;

        foreach (var item in remainingItems)
        {
            item.StartFlyingToPlayer(playerObj.transform);
        }
    }

    public void EnsureAllItemsCollected()
    {
        DroppedItem[] remainingItems = FindObjectsByType<DroppedItem>(FindObjectsSortMode.None);
        foreach (var item in remainingItems)
        {
            item.ForceInstantCollect();
        }
    }
}