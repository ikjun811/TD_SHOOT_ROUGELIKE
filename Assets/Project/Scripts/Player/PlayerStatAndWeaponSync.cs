using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;

// 정비 UI 결과를 3D 플레이어 개체의 스탯 및 무기에 실시간 동기화하는 클래스
public class PlayerStatAndWeaponSync : MonoBehaviour
{
    public static PlayerStatAndWeaponSync Instance { get; private set; }

    private JUCharacterController controller;
    private JUHealth juHealth;
    private JUInventory juInventory;

    // 기본 스탯 보존용 변수
    private float defaultMaxHealth = 100f;
    private float defaultProtection = 0f;
    private float defaultWalkSpeed = 3f;
    private float defaultRunSpeed = 5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        controller = GetComponent<JUCharacterController>();
        juHealth = GetComponent<JUHealth>();
        juInventory = GetComponent<JUInventory>();

        // 기본 스탯 값 저장
        if (juHealth != null)
        {
            defaultMaxHealth = juHealth.MaxHealth;
            defaultProtection = juHealth.Protection;
        }
        if (controller != null)
        {
            defaultWalkSpeed = controller.WalkSpeed;
            defaultRunSpeed = controller.RunSpeed;
        }
    }

    private void Start()
    {
        StartCoroutine(InitialWeaponSync());
    }

    // JU의 시작 장착(첫 프레임 끝)과 시작 무기 지급이 끝난 뒤 최초 무기 동기화
    private IEnumerator InitialWeaponSync()
    {
        yield return null;
        SyncEquippedWeaponsToPlayer();
    }

    // 정비 단계 종료 후 다음 라운드 시작 시 호출되는 동기화 함수
    public void ApplyMaintenanceResultsToPlayer()
    {
        ApplyTetrisStatsToPlayer();
        SyncEquippedWeaponsToPlayer();
    }

    // 1. 테트리스 모듈 최종 스탯을 3D 캐릭터 수치에 적용
    private void ApplyTetrisStatsToPlayer()
    {
        if (GridInventorySystem.Instance == null) return;

        float atkPercent = 0f;
        float healthBonus = 0f;
        float armorBonus = 0f;
        float speedPercent = 0f;

        // 그리드 내 안착된 모든 모듈 스탯 가산
        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            float val = pm.moduleData.GetFinalStatValue();
            switch (pm.moduleData.statType)
            {
                case ModuleStatType.AttackDamagePercent: atkPercent += val; break;
                case ModuleStatType.MaxHealthBonus: healthBonus += val; break;
                case ModuleStatType.ArmorBonus: armorBonus += val; break;
                case ModuleStatType.MoveSpeedPercent: speedPercent += val; break;
            }
        }

        // 체력 및 방어력 적용
        if (juHealth != null)
        {
            float newMaxHp = defaultMaxHealth + healthBonus;
            juHealth.SetMaxHealth(newMaxHp);
            juHealth.SetHealth(newMaxHp); // 체력 복구
            juHealth.Protection = defaultProtection + armorBonus;
        }

        // 이동속도 적용 (JUCharacterController의 WalkSpeed 및 RunSpeed 보정) ⭐
        if (controller != null)
        {
            controller.WalkSpeed = defaultWalkSpeed * (1f + speedPercent);
            controller.RunSpeed = defaultRunSpeed * (1f + speedPercent);
        }

        Debug.Log($"PlayerStatAndWeaponSync: Applied Stats -> Health: {defaultMaxHealth + healthBonus}, Armor: {defaultProtection + armorBonus}, Speed: {defaultRunSpeed * (1f + speedPercent)}");
    }

    // 2. 정비 창에서 선택한 무기 2종을 3D 캐릭터 손에 실제로 장착
    // 장착 무기만 Unlocked로 두어 E키 교체가 장착 2종 사이에서만 순환하도록 함
    private void SyncEquippedWeaponsToPlayer()
    {
        if (PlayerInventory.Instance == null || juInventory == null || controller == null) return;
        if (juInventory.HoldableItensRightHand == null) return;

        WeaponDataSO[] equipped = PlayerInventory.Instance.equippedWeapons;
        List<JUHoldableItem> itemsToUnlock = new List<JUHoldableItem>();
        JUHoldableItem itemToHold = null;

        for (int i = 0; i < equipped.Length; i++)
        {
            if (equipped[i] == null) continue;

            JUHoldableItem item = FindRightHandItem(equipped[i].juItemName);
            if (item == null)
            {
                Debug.LogWarning($"PlayerStatAndWeaponSync: JU item '{equipped[i].juItemName}' not found for weapon {equipped[i].weaponName}.");
                continue;
            }

            itemsToUnlock.Add(item);
            if (item.DualItemToWielding != null) itemsToUnlock.Add(item.DualItemToWielding); // 쌍권총 왼손

            if (itemToHold == null) itemToHold = item; // 1번 슬롯 우선, 비어 있으면 2번
        }

        // 장착 무기만 해금, 나머지(근접 무기 포함)는 잠금
        SetUnlockedItems(juInventory.HoldableItensRightHand, itemsToUnlock);
        SetUnlockedItems(juInventory.HoldableItensLeftHand, itemsToUnlock);

        if (itemToHold != null)
        {
            controller.SwitchToItem(itemToHold.ItemSwitchID, true);
            Debug.Log($"PlayerStatAndWeaponSync: Holding 3D weapon -> {itemToHold.ItemName}");
        }
    }

    private JUHoldableItem FindRightHandItem(string juItemName)
    {
        foreach (JUHoldableItem item in juInventory.HoldableItensRightHand)
        {
            if (item != null && !item.IsLeftHandItem && item.ItemName == juItemName) return item;
        }
        return null;
    }

    private void SetUnlockedItems(JUHoldableItem[] items, List<JUHoldableItem> unlockedItems)
    {
        if (items == null) return;

        foreach (JUHoldableItem item in items)
        {
            if (item != null) item.Unlocked = unlockedItems.Contains(item);
        }
    }
}