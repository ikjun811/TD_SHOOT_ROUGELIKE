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

    // 무기별 장전 시간 (WeaponDataSO.reloadTime, 장전 애니메이션 속도 계산에 사용)
    private readonly Dictionary<Weapon, float> weaponReloadTimes = new Dictionary<Weapon, float>();

    // 작동 모듈이 바꾸는 JU 무기 값의 원본 (모듈을 빼면 되돌림)
    private struct WeaponOriginals
    {
        public Weapon.WeaponFireMode fireMode;
        public int pelletCount;
        public float spread;
        public bool continuousUse; // JU는 이 값이 꺼져 있으면 FireMode와 무관하게 클릭할 때만 사격
    }
    private readonly Dictionary<Weapon, WeaponOriginals> weaponOriginals = new Dictionary<Weapon, WeaponOriginals>();

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

    // 사망 후 부활 시 JU가 애니메이터를 재시작해 무기 자세가 풀리므로, 다음 동기화 때 같은 무기도 다시 장착
    private bool needsReequip = false;

    private void Start()
    {
        StartCoroutine(InitialWeaponSync());

        if (juHealth != null) juHealth.OnDeath += MarkNeedsReequip;
    }

    private void OnDestroy()
    {
        if (juHealth != null) juHealth.OnDeath -= MarkNeedsReequip;
    }

    private void MarkNeedsReequip()
    {
        needsReequip = true;
    }

    private void Update()
    {
        ApplyReloadAnimationSpeed();
        SyncDualWeaponReload();
    }

    // 쌍권총: JU는 좌우를 번갈아 쏘며(왼손이 쏴야 오른손 차례가 돌아옴), 장전도 손마다 따로 끝남
    // 한 손 장전 직후 무기를 바꾸면 다른 손이 0발로 남아 번갈아 쏘기가 멈추고 사격 불가가 됨
    // → 한 손 장전이 끝나는 순간(탄 증가) 다른 손도 함께 채움 (장전은 무기 단위 한 동작, 예비탄 무한이라 손익 없음)
    private Weapon lastDualRight, lastDualLeft;
    private int lastDualRightAmmo, lastDualLeftAmmo;

    private void SyncDualWeaponReload()
    {
        if (controller == null) return;

        Weapon right = controller.RightHandWeapon;
        Weapon left = controller.LeftHandWeapon;
        if (right == null || left == null)
        {
            lastDualRight = lastDualLeft = null;
            return;
        }

        // 쌍권총을 새로 들었으면 기준값만 기록
        if (right != lastDualRight || left != lastDualLeft)
        {
            lastDualRight = right;
            lastDualLeft = left;
            lastDualRightAmmo = right.BulletsAmounts;
            lastDualLeftAmmo = left.BulletsAmounts;
            return;
        }

        // 반대손 장전 애니메이션 트리거도 취소해 장전을 한 번의 모션으로 끝냄 (장전 UI 완료 시점 = 사격 가능 시점)
        if (right.BulletsAmounts > lastDualRightAmmo)
        {
            left.BulletsAmounts = left.BulletsPerMagazine;
            if (controller.anim != null) controller.anim.ResetTrigger(controller.AnimatorParameters.ReloadLeftWeapon);
        }
        else if (left.BulletsAmounts > lastDualLeftAmmo)
        {
            right.BulletsAmounts = right.BulletsPerMagazine;
            if (controller.anim != null) controller.anim.ResetTrigger(controller.AnimatorParameters.ReloadRightWeapon);
        }

        lastDualRightAmmo = right.BulletsAmounts;
        lastDualLeftAmmo = left.BulletsAmounts;
    }

    // 장전 속도: JU 장전은 애니메이션 이벤트(reloadRightHandWeapon)로 끝나므로, 장전 중에만 애니메이터 속도를 조절
    // 속도 = (장전 애니메이션 원래 길이 / 무기 reloadTime) x (1 + 장전 속도 모듈%) → 장전 모션 전체 길이 = reloadTime
    // JU는 RootMotion을 쓰지 않아 이동 속도에는 영향 없음 (장전 중 하체 애니메이션만 같이 빨라짐)
    private void ApplyReloadAnimationSpeed()
    {
        if (controller == null || controller.anim == null) return;

        Animator anim = controller.anim;
        int armsLayer = controller.AnimatorParameters._BothArmsLayerIndex;
        AnimatorStateInfo armsState = anim.GetCurrentAnimatorStateInfo(armsLayer);

        bool reloadingRight = armsState.IsName("Reload Right Hand Weapon");
        bool reloadingLeft = armsState.IsName("Reload Left Hand Weapon");

        // JU의 장전 판정과 같은 조건 (IsReloading 또는 장전 애니메이션 재생 중)
        bool isReloading = controller.IsReloading || reloadingRight || reloadingLeft;

        float targetSpeed = 1f;
        if (isReloading)
        {
            targetSpeed = 1f + moduleReloadSpeedPercent;

            // 장전 애니메이션 재생 중이면 무기별 장전 시간에 맞춤
            Weapon reloadingWeapon = reloadingRight ? controller.RightHandWeapon : (reloadingLeft ? controller.LeftHandWeapon : null);
            if (reloadingWeapon != null
                && weaponReloadTimes.TryGetValue(reloadingWeapon, out float reloadTime) && reloadTime > 0f)
            {
                AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(armsLayer);
                if (clips.Length > 0 && clips[0].clip != null)
                {
                    targetSpeed *= clips[0].clip.length / reloadTime;
                }
            }
        }

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
        SetPlayerControlEnabled(true);
    }

    // 정비 중 플레이어 입력 차단 (timeScale 0이어도 JU Update가 클릭을 사격으로 처리해 격발음이 나던 문제)
    // 재개 시에는 NEXT ROUND 클릭이 사격으로 이어지지 않도록 마우스 버튼을 뗀 뒤에 켬
    private Coroutine enableControlRoutine;

    public void SetPlayerControlEnabled(bool enabled)
    {
        if (controller == null) return;

        if (enableControlRoutine != null)
        {
            StopCoroutine(enableControlRoutine);
            enableControlRoutine = null;
        }

        if (!enabled)
        {
            controller.enabled = false;
            return;
        }

        enableControlRoutine = StartCoroutine(EnableControlAfterMouseRelease());
    }

    private IEnumerator EnableControlAfterMouseRelease()
    {
        while (Input.GetMouseButton(0) || Input.GetMouseButton(1)) yield return null;
        controller.enabled = true;
        enableControlRoutine = null;
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
            if (pm.moduleData.category != ModuleCategory.Stat) continue; // 작동·특수·유니크는 스탯 없음
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
            // JU SwitchToItem은 같은 무기면 건너뛰므로, 부활 후에는 한 번 해제했다가 다시 장착 (무기 교체와 같은 효과)
            if (needsReequip)
            {
                controller.IsReloading = false; // 장전 중 사망 시 남은 장전 상태 정리 (장전 중이면 교체가 막힘)
                controller.SwitchToItem(-1, true);
                needsReequip = false;
            }

            controller.SwitchToItem(itemToHold.ItemSwitchID, true);
            Debug.Log($"PlayerStatAndWeaponSync: Holding 3D weapon -> {itemToHold.ItemName}");
        }
    }

    // 3. WeaponDataSO 수치를 JU 무기에 적용 (작동 모듈 → 모듈 공격력·연사 보정 순)
    private void ApplyWeaponData(Weapon weapon, WeaponDataSO data)
    {
        if (weapon == null || data == null) return;

        if (!weaponOriginals.ContainsKey(weapon))
        {
            weaponOriginals[weapon] = new WeaponOriginals
            {
                fireMode = weapon.FireMode,
                pelletCount = weapon.NumberOfShotgunBulletsPerShot,
                spread = weapon.LossOfAccuracyPerShot,
                continuousUse = weapon.ContinuousUseItem
            };
        }
        WeaponOriginals originals = weaponOriginals[weapon];

        // SO 기본값에 이 무기 대상 작동 모듈의 값을 덮어씀 (같은 칸은 충돌 규칙으로 1개만 배치됨)
        int magazine = data.maxAmmo;
        float reloadTime = data.reloadTime;
        float fireInterval = data.fireRate;
        float damageMultiplier = 1f;
        Weapon.WeaponFireMode fireMode = originals.fireMode;
        bool continuousUse = originals.continuousUse;
        int pelletCount = originals.pelletCount;
        float spread = originals.spread;
        string mechanismNames = "";

        if (GridInventorySystem.Instance != null)
        {
            foreach (var pm in GridInventorySystem.Instance.placedModules)
            {
                ModuleDataSO m = pm.moduleData;
                if (m == null || m.category != ModuleCategory.Mechanism || !m.TargetsWeapon(data.weaponType)) continue;

                if (m.magazineOverride > 0) magazine = m.magazineOverride;
                if (m.reloadTimeOverride > 0f) reloadTime = m.reloadTimeOverride;
                if (m.fireIntervalOverride > 0f) fireInterval = m.fireIntervalOverride;
                if (m.damageMultiplier > 0f) damageMultiplier *= m.damageMultiplier;
                if (m.pelletCountOverride > 0) pelletCount = m.pelletCountOverride;
                if (m.spreadOverride > 0f) spread = m.spreadOverride;
                if (m.fireModeOverride == FireModeOverride.Auto)
                {
                    fireMode = Weapon.WeaponFireMode.Auto;
                    continuousUse = true;
                }
                else if (m.fireModeOverride == FireModeOverride.SemiAuto)
                {
                    fireMode = Weapon.WeaponFireMode.SemiAuto;
                    continuousUse = false;
                }
                mechanismNames += $" [{m.moduleName}]";
            }
        }

        float finalDamage = data.GetFinalDamage() * damageMultiplier * (1f + moduleAttackPercent);
        float finalFireRate = fireInterval / (1f + moduleFireRatePercent); // 발사 간격이므로 연사 % 만큼 나눔

        weapon.FireMode = fireMode;
        weapon.ContinuousUseItem = continuousUse;
        weapon.NumberOfShotgunBulletsPerShot = pelletCount;
        weapon.LossOfAccuracyPerShot = spread;

        // 연사·탄창: 탄창은 가득 채우고, 예비탄은 무한 (재장전은 필요)
        weapon.Fire_Rate = finalFireRate;
        weaponReloadTimes[weapon] = reloadTime;
        weapon.BulletsPerMagazine = Mathf.Max(1, magazine);
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

            // 탑다운 수평 비행 + 좌우 분산만 (위아래 기울기·분산 제거)
            template.AddComponent<FlatBullet>();

            // 치명타: 템플릿에 판정 컴포넌트를 붙여 두면 JU가 복제한 탄마다 판정 (템플릿은 비활성 부모 밑이라 판정 안 함)
            if (moduleCritChance > 0f)
            {
                CriticalBullet crit = template.AddComponent<CriticalBullet>();
                crit.critChance = moduleCritChance;
                crit.critMultiplier = critDamageMultiplier;
            }

            // 탄 궤적 특수 (관통·도탄은 충돌 칸이 같아 둘 중 하나만 배치됨). 공용 도탄은 산탄총에 효과 없음 (유니크가 담당)
            ModuleDataSO pathModule = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Pierce);
            if (pathModule == null && data.weaponType != WeaponType.Shotgun)
            {
                pathModule = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Ricochet);
            }
            if (pathModule != null)
            {
                BulletChain chain = template.AddComponent<BulletChain>();
                chain.mode = pathModule.effect == ModuleEffect.Pierce ? BulletChain.ChainMode.Pierce : BulletChain.ChainMode.Ricochet;
                chain.remaining = Mathf.Max(1, pathModule.effectCount);
                if (pathModule.effectValue > 0f) chain.damageFactor = pathModule.effectValue;
                mechanismNames += $" [{pathModule.moduleName}]";
            }
        }

        bulletTemplates[weapon] = template;
        weapon.BulletPrefab = template;

        Debug.Log($"PlayerStatAndWeaponSync: Applied {data.weaponName} -> Damage {finalDamage:0.#}, FireRate {finalFireRate:0.###}, Magazine {weapon.BulletsPerMagazine}, Reload {reloadTime}s, Crit {moduleCritChance * 100f:0.#}% x{critDamageMultiplier}{mechanismNames}");
    }

    // 이 무기에 적용되는 특수 모듈 (공용 또는 이 무기 전용, 중복이면 효과가 적용되는 1개만)
    private ModuleDataSO FindActiveWeaponSpecial(WeaponType weaponType, ModuleEffect effect)
    {
        if (GridInventorySystem.Instance == null) return null;

        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m == null || m.category != ModuleCategory.Special || m.effect != effect) continue;
            if (m.target != ModuleTarget.All && !m.TargetsWeapon(weaponType)) continue;
            if (GridInventorySystem.Instance.IsEffectActive(pm)) return m;
        }
        return null;
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