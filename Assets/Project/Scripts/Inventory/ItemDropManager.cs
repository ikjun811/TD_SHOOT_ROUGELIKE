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

    [Header("Module Drop Weights (GDD 8-6 #17 A5, 계열 합계 기준 스탯:기능 = 2:1)")]
    [SerializeField] private float statCategoryWeight = 67f;
    [SerializeField] private float mechanismCategoryWeight = 13f;
    [SerializeField] private float specialCategoryWeight = 20f;
    [Tooltip("무기군 전용 모듈 가중치: 장착 중인 무기군")]
    [SerializeField] private float equippedWeaponTypeWeight = 3f;
    [Tooltip("무기군 전용 모듈 가중치: 금고에만 있는 무기군")]
    [SerializeField] private float vaultWeaponTypeWeight = 1f;
    [Tooltip("무기군 전용 모듈 가중치: 장착·금고 모두 없는 무기군")]
    [SerializeField] private float unownedWeaponTypeWeight = 0.3f;
    [Tooltip("레전더리 드랍 시 유니크로 바뀔 확률 (유니크 에셋이 없으면 무시)")]
    [Range(0f, 1f)][SerializeField] private float uniqueChanceOnLegendary = 0.15f;

    [Header("Elite Drop Bonus (GDD 8-6 #14, 임시값)")]
    [Range(0f, 1f)][SerializeField] private float eliteUpgradeChance = 0.5f;       // 레어도 +1단계 확률
    [Range(0f, 1f)][SerializeField] private float eliteDoubleUpgradeChance = 0.3f; // +1단계 성공 시 추가 +1단계 확률

    [Header("Fallback Drop Settings")]
    [Range(0f, 1f)][SerializeField] private float overallDropChance = 0.35f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 적 사망 시 드랍 시도 (실시간 감쇄 드랍률 및 천장 연동)
    public void TryDropItem(Vector3 spawnPosition, int currentRound, bool isElite = false)
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

        // 엘리트: 드랍 판정은 일반 적과 같고, 드랍 시 확률적으로 레어도 상향
        if (isElite && Random.value < eliteUpgradeChance)
        {
            RarityType baseRarity = selectedRarity;
            int steps = Random.value < eliteDoubleUpgradeChance ? 2 : 1;
            selectedRarity = (RarityType)Mathf.Min((int)selectedRarity + steps, (int)RarityType.Legendary);
            Debug.Log($"ItemDropManager: Elite drop upgrade {baseRarity} -> {selectedRarity}");
        }

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

    // 모듈 선택 (GDD 8-6 #17 A5): 레전더리면 일부 유니크 → 계열 비율로 계열 선택 → 계열에 있는 판본 중 뽑힌 레어도에 가장 가까운 것 → 무기군 가중치로 1개
    private ModuleDataSO GetRandomModuleOfRarity(RarityType rarity)
    {
        HashSet<WeaponType> equippedTypes = GetEquippedWeaponTypes();
        HashSet<WeaponType> ownedTypes = GetOwnedWeaponTypes();

        if (rarity == RarityType.Legendary && Random.value < uniqueChanceOnLegendary)
        {
            ModuleDataSO unique = PickWeighted(GetCategoryCandidates(ModuleCategory.Unique, rarity), equippedTypes, ownedTypes);
            if (unique != null)
            {
                Debug.Log($"ItemDropManager: Module drop -> {unique.moduleName} [Unique]");
                return unique;
            }
        }

        // 계열 선택 (후보가 없는 계열은 제외)
        ModuleCategory[] categories = { ModuleCategory.Stat, ModuleCategory.Mechanism, ModuleCategory.Special };
        float[] weights = { statCategoryWeight, mechanismCategoryWeight, specialCategoryWeight };
        List<ModuleDataSO>[] candidates = new List<ModuleDataSO>[categories.Length];
        float total = 0f;
        for (int i = 0; i < categories.Length; i++)
        {
            candidates[i] = GetCategoryCandidates(categories[i], rarity);
            if (candidates[i].Count == 0) weights[i] = 0f;
            total += Mathf.Max(0f, weights[i]);
        }
        if (total <= 0f) return null;

        float rand = Random.value * total;
        int picked = categories.Length - 1;
        for (int i = 0; i < categories.Length; i++)
        {
            rand -= Mathf.Max(0f, weights[i]);
            if (rand <= 0f && weights[i] > 0f) { picked = i; break; }
        }

        ModuleDataSO module = PickWeighted(candidates[picked], equippedTypes, ownedTypes);
        if (module != null) Debug.Log($"ItemDropManager: Module drop -> {module.moduleName} [{categories[picked]}, roll {rarity} -> {module.rarity}]");
        return module;
    }

    // 계열의 판본 중 뽑힌 레어도에 가장 가까운 레어도의 모듈들 (같은 거리면 높은 쪽)
    // 작동은 엘리트판만 → 항상 엘리트 / 특수는 희귀·레전더리 → 일반·희귀는 희귀판, 엘리트·레전더리는 레전더리판
    private List<ModuleDataSO> GetCategoryCandidates(ModuleCategory category, RarityType rarity)
    {
        List<ModuleDataSO> inCategory = allModules.FindAll(m => m != null && m.category == category);
        if (inCategory.Count == 0) return inCategory;

        int best = -1;
        int bestDistance = int.MaxValue;
        foreach (ModuleDataSO m in inCategory)
        {
            int value = (int)m.rarity;
            int distance = Mathf.Abs(value - (int)rarity);
            if (distance < bestDistance || (distance == bestDistance && value > best))
            {
                best = value;
                bestDistance = distance;
            }
        }
        return inCategory.FindAll(m => (int)m.rarity == best);
    }

    // 무기군 전용 모듈은 장착 x3 / 금고에만 있음 x1 / 없음 x0.3, 그 외 대상(공용·수류탄·캐릭터)은 x1
    private ModuleDataSO PickWeighted(List<ModuleDataSO> list, HashSet<WeaponType> equippedTypes, HashSet<WeaponType> ownedTypes)
    {
        if (list == null || list.Count == 0) return null;

        float total = 0f;
        foreach (ModuleDataSO m in list) total += GetModuleWeight(m, equippedTypes, ownedTypes);
        if (total <= 0f) return list[Random.Range(0, list.Count)];

        float rand = Random.value * total;
        foreach (ModuleDataSO m in list)
        {
            rand -= GetModuleWeight(m, equippedTypes, ownedTypes);
            if (rand <= 0f) return m;
        }
        return list[list.Count - 1];
    }

    private float GetModuleWeight(ModuleDataSO module, HashSet<WeaponType> equippedTypes, HashSet<WeaponType> ownedTypes)
    {
        bool weaponSpecific = false;
        foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
        {
            if (!module.TargetsWeapon(type)) continue;
            weaponSpecific = true;
            if (equippedTypes.Contains(type)) return equippedWeaponTypeWeight;
            if (ownedTypes.Contains(type)) return vaultWeaponTypeWeight;
        }
        return weaponSpecific ? unownedWeaponTypeWeight : 1f;
    }

    // 장착 슬롯에 있는 무기군
    private HashSet<WeaponType> GetEquippedWeaponTypes()
    {
        HashSet<WeaponType> equipped = new HashSet<WeaponType>();
        if (PlayerInventory.Instance != null)
        {
            foreach (WeaponDataSO w in PlayerInventory.Instance.equippedWeapons)
            {
                if (w != null) equipped.Add(w.weaponType);
            }
        }
        return equipped;
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