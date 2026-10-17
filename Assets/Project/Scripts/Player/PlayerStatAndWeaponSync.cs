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
    private GrenadeSkill grenadeSkill;

    // 기본 스탯 보존용 변수
    private float defaultMaxHealth = 100f;
    private float defaultProtection = 0f;
    private float defaultWalkSpeed = 3f;
    private float defaultRunSpeed = 5f;

    // 무기별 런타임 탄 템플릿 (JU 탄 프리팹 원본은 수정하지 않음)
    private GameObject bulletTemplateHolder;
    private readonly Dictionary<Weapon, GameObject> originalBulletPrefabs = new Dictionary<Weapon, GameObject>();
    private readonly Dictionary<Weapon, GameObject> bulletTemplates = new Dictionary<Weapon, GameObject>();

    // 모듈 무기 보정값 (ApplyTetrisStatsToPlayer에서 갱신, ApplyWeaponData에서 사용)
    private float moduleAttackPercent = 0f;
    private float moduleFireRatePercent = 0f;
    private float moduleCritChance = 0f;
    private float moduleReloadSpeedPercent = 0f;

    [Header("Critical (임시값, GDD 8-4)")]
    [SerializeField] private float critDamageMultiplier = 2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        controller = GetComponent<JUCharacterController>();
        juHealth = GetComponent<JUHealth>();
        juInventory = GetComponent<JUInventory>();
        grenadeSkill = GetComponent<GrenadeSkill>();

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

        ApplyGunClippingLayer();
    }

    // 총 끼임 방지가 벽 전용 레이어에만 반응하도록 설정 (적은 Default 레이어라 근접 시 사격이 막히던 문제)
    // JU PreventGunClipping.Start()는 WallsLayer가 0일 때만 기본값을 넣으므로, 그보다 먼저 실행되는 Awake에서 지정
    [SerializeField] private string gunClippingLayerName = "Walls";

    private void ApplyGunClippingLayer()
    {
        int mask = LayerMask.GetMask(gunClippingLayerName);
        if (mask == 0)
        {
            Debug.LogWarning($"[PlayerStatAndWeaponSync] '{gunClippingLayerName}' 레이어가 없습니다. 총 끼임 방지는 JU 기본 레이어를 사용합니다.");
            return;
        }

        foreach (PreventGunClipping clipping in GetComponentsInChildren<PreventGunClipping>(true))
        {
            clipping.WallsLayer = mask;
        }
    }

    private void Start()
    {
        StartCoroutine(InitialWeaponSync());
    }

    private void Update()
    {
        ApplyReloadAnimationSpeed();
    }

    // 장전 속도 모듈: JU 장전은 애니메이션 이벤트(reloadRightHandWeapon)로 끝나므로, 장전 중에만 애니메이터 속도를 올려 장전을 빠르게 함
    // JU는 RootMotion을 쓰지 않아 이동 속도에는 영향 없음 (장전 중 하체 애니메이션만 같이 빨라짐)
    private void ApplyReloadAnimationSpeed()
    {
        if (controller == null || controller.anim == null) return;

        Animator anim = controller.anim;
        int armsLayer = controller.AnimatorParameters._BothArmsLayerIndex;
        AnimatorStateInfo armsState = anim.GetCurrentAnimatorStateInfo(armsLayer);

        // JU의 장전 판정과 같은 조건 (IsReloading 또는 장전 애니메이션 재생 중)
        bool isReloading = controller.IsReloading
                           || armsState.IsName("Reload Right Hand Weapon")
                           || armsState.IsName("Reload Left Hand Weapon");

        float targetSpeed = isReloading ? 1f + moduleReloadSpeedPercent : 1f;
        if (!Mathf.Approximately(anim.speed, targetSpeed)) anim.speed = targetSpeed;
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
        float fireRatePercent = 0f;
        float cooldownReduce = 0f;
        float critChance = 0f;
        float reloadSpeedPercent = 0f;

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
                case ModuleStatType.FireRatePercent: fireRatePercent += val; break;
                case ModuleStatType.SkillCooldownReduce: cooldownReduce += val; break;
                case ModuleStatType.CriticalChance: critChance += val; break;
                case ModuleStatType.ReloadSpeedPercent: reloadSpeedPercent += val; break;
            }
        }

        // 장전 속도는 장전 중에만 Update에서 애니메이터 속도로 적용
        moduleReloadSpeedPercent = reloadSpeedPercent;

        // 수류탄 쿨감 (GrenadeSkill이 플레이어에 붙어 있을 때만)
        if (grenadeSkill != null)
        {
            grenadeSkill.SetCooldownReduction(cooldownReduce);
            grenadeSkill.RefillCharges(); // 다음 라운드 시작 시 충전 가득
        }

        // 공격력·연사는 무기 동기화(ApplyWeaponData) 시 적용
        moduleAttackPercent = atkPercent;
        moduleFireRatePercent = fireRatePercent;
        moduleCritChance = Mathf.Clamp01(critChance);

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

        Debug.Log($"PlayerStatAndWeaponSync: Applied Stats -> Health: {defaultMaxHealth + healthBonus}, Armor: {defaultProtection + armorBonus}, Speed: {defaultRunSpeed * (1f + speedPercent)}, Attack: +{atkPercent * 100f:0.#}%, FireRate: +{fireRatePercent * 100f:0.#}%, Reload: +{reloadSpeedPercent * 100f:0.#}%");
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

            if (itemsToUnlock.Contains(item)) continue; // 두 슬롯이 같은 JU 무기면 1번 슬롯 데이터 사용

            itemsToUnlock.Add(item);
            ApplyWeaponData(item.GetComponent<Weapon>(), equipped[i]);

            if (item.DualItemToWielding != null) // 쌍권총 왼손
            {
                itemsToUnlock.Add(item.DualItemToWielding);
                ApplyWeaponData(item.DualItemToWielding.GetComponent<Weapon>(), equipped[i]);
            }

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

    // 3. WeaponDataSO 수치를 JU 무기에 적용 (모듈 공격력·연사 보정 포함)
    private void ApplyWeaponData(Weapon weapon, WeaponDataSO data)
    {
        if (weapon == null || data == null) return;

        float finalDamage = data.GetFinalDamage() * (1f + moduleAttackPercent);
        float finalFireRate = data.fireRate / (1f + moduleFireRatePercent); // 발사 간격이므로 연사 % 만큼 나눔

        // 연사·탄창: 탄창은 가득 채우고, 예비탄은 무한 (재장전은 필요)
        weapon.Fire_Rate = finalFireRate;
        weapon.BulletsPerMagazine = Mathf.Max(1, data.maxAmmo);
        weapon.BulletsAmounts = weapon.BulletsPerMagazine;
        weapon.InfiniteAmmo = true;

        // JU는 재장전 시작 조건으로 예비탄 > 0을 검사하므로 0이면 채워 둠 (무한탄이라 이후 줄지 않음)
        if (weapon.TotalBullets <= 0) weapon.TotalBullets = weapon.BulletsPerMagazine;

        // 피해: 원본 탄 프리팹을 복제한 무기 전용 템플릿에 피해값 설정
        if (!originalBulletPrefabs.ContainsKey(weapon))
        {
            originalBulletPrefabs[weapon] = weapon.BulletPrefab;
        }

        GameObject originalBullet = originalBulletPrefabs[weapon];
        if (originalBullet == null) return;

        if (bulletTemplateHolder == null)
        {
            // 비활성 부모 밑에 두어 템플릿 자체는 동작하지 않음 (JU가 복제한 탄은 부모가 없어 정상 동작)
            bulletTemplateHolder = new GameObject("RuntimeBulletTemplates");
            bulletTemplateHolder.SetActive(false);
        }

        if (bulletTemplates.TryGetValue(weapon, out GameObject oldTemplate) && oldTemplate != null)
        {
            Destroy(oldTemplate);
        }

        GameObject template = Instantiate(originalBullet, bulletTemplateHolder.transform);
        template.name = $"{originalBullet.name} ({data.weaponName})";

        Bullet bullet = template.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.BulletDamage = finalDamage;

            // 치명타: 템플릿에 판정 컴포넌트를 붙여 두면 JU가 복제한 탄마다 판정 (템플릿은 비활성 부모 밑이라 판정 안 함)
            if (moduleCritChance > 0f)
            {
                CriticalBullet crit = template.AddComponent<CriticalBullet>();
                crit.critChance = moduleCritChance;
                crit.critMultiplier = critDamageMultiplier;
            }
        }

        bulletTemplates[weapon] = template;
        weapon.BulletPrefab = template;

        Debug.Log($"PlayerStatAndWeaponSync: Applied {data.weaponName} -> Damage {finalDamage:0.#}, FireRate {finalFireRate:0.###}, Magazine {weapon.BulletsPerMagazine}, Crit {moduleCritChance * 100f:0.#}% x{critDamageMultiplier}");
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