using System.Collections.Generic;
using UnityEngine;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Drop Prefab")]
    [SerializeField] private GameObject droppedItemPrefab;

    [Header("Item Pools")]
    [SerializeField] private List<WeaponDataSO> allWeapons = new List<WeaponDataSO>();
    [SerializeField] private List<ModuleDataSO> allModules = new List<ModuleDataSO>();

    [Header("Drop Settings")]
    [Range(0f, 1f)][SerializeField] private float overallDropChance = 0.4f; // 적 사망 시 40% 확률로 드랍

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 적이 사망했을 때 호출
    public void TryDropItem(Vector3 spawnPosition, int currentRound)
    {
        // 1. 드랍 확률 체크
        if (Random.value > overallDropChance) return;

        // 2. 라운드에 따른 레어도 결정
        RarityType selectedRarity = DetermineRarityByRound(currentRound);

        // 3. 무기 또는 모듈 50% 확률 결정
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
        float rand = Random.value * 100f; // 0 ~ 100

        // 라운드가 높아질수록 레전더리/엘리트 확률 상승 연산
        float legendaryChance = Mathf.Clamp((round - 10) * 1.5f, 0f, 20f); // 10R 이후부터 등장 (최대 20%)
        float eliteChance = Mathf.Clamp(round * 1.5f, 5f, 35f);            // 최대 35%
        float rareChance = Mathf.Clamp(30f + round, 30f, 40f);

        if (rand < legendaryChance) return RarityType.Legendary;
        if (rand < legendaryChance + eliteChance) return RarityType.Elite;
        if (rand < legendaryChance + eliteChance + rareChance) return RarityType.Rare;
        return RarityType.Common;
    }

    private WeaponDataSO GetRandomWeaponOfRarity(RarityType rarity)
    {
        List<WeaponDataSO> filtered = allWeapons.FindAll(w => w.rarity == rarity);
        if (filtered.Count == 0) filtered = allWeapons; // 없으면 전체에서 선택
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
}