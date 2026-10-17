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

        RarityType selectedRarity = DetermineRarityByRound(currentRound);

        bool dropWeapon = Random.value > 0.5f;

        if (dropWeapon)
        {
            WeaponDataSO weapon = GetRandomWeaponOfRarity(selectedRarity);
            if (weapon != null) SpawnDrop(spawnPosition, weapon, null);
        }
        else
        {
            ModuleDataSO module = GetRandomModuleOfRarity(selectedRarity);
            if (module != null) SpawnDrop(spawnPosition, null, module);
        }
    }

    private RarityType DetermineRarityByRound(int round)
    {
        float rand = Random.value * 100f;

        float legendaryChance = Mathf.Clamp((round - 10) * 1.5f, 0f, 20f);
        float eliteChance = Mathf.Clamp(round * 1.5f, 5f, 35f);
        float rareChance = Mathf.Clamp(30f + round, 30f, 40f);

        if (rand < legendaryChance) return RarityType.Legendary;
        if (rand < legendaryChance + eliteChance) return RarityType.Elite;
        if (rand < legendaryChance + eliteChance + rareChance) return RarityType.Rare;
        return RarityType.Common;
    }

    private WeaponDataSO GetRandomWeaponOfRarity(RarityType rarity)
    {
        List<WeaponDataSO> filtered = allWeapons.FindAll(w => w.rarity == rarity);
        if (filtered.Count == 0) filtered = allWeapons;
        return filtered.Count > 0 ? filtered[Random.Range(0, filtered.Count)] : null;
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