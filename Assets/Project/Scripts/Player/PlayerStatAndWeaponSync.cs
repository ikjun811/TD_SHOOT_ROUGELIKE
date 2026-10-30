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

    // 무기별 무기군 (ApplyWeaponData에서 기록, CharacterModuleRuntime이 단일 P226 판정에 사용)
    private readonly Dictionary<Weapon, WeaponType> weaponTypes = new Dictionary<Weapon, WeaponType>();

    public bool TryGetWeaponType(Weapon weapon, out WeaponType type)
    {
        type = default;
        return weapon != null && weaponTypes.TryGetValue(weapon, out type);
    }

    // 작동 모듈이 바꾸는 JU 무기 값의 원본 (모듈을 빼면 되돌림)
    private struct WeaponOriginals
    {
        public Weapon.WeaponFireMode fireMode;
        public int pelletCount;
        public float spread;
        public bool continuousUse; // JU는 이 값이 꺼져 있으면 FireMode와 무관하게 클릭할 때만 사격
    }
    private readonly Dictionary<Weapon, WeaponOriginals> weaponOriginals = new Dictionary<Weapon, WeaponOriginals>();

    // 모듈 보정값 (ApplyTetrisStatsToPlayer에서 갱신). 공격력·연사는 WeaponStatCalculator가 그리드에서 직접 합산
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

        // 캐릭터 효과 런타임 (단일 P226 쿨타임 단축 등, A4c)
        if (GetComponent<CharacterModuleRuntime>() == null) gameObject.AddComponent<CharacterModuleRuntime>();

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

    // 탄환 환수 모듈 (BulletHitEffects가 처치 시 호출): 탄창의 ratio만큼(올림, 최소 1발) 채움
    // 쌍권총은 좌우 번갈아 쏘기 + 양손 0발일 때만 자동 장전이라, 한 손만 채우면 다른 손이 0발로 남아 사격이 멈춤 → 양손에 같은 양
    // 쌍권총 장전 보정은 "탄이 늘면 장전 완료"로 보므로 추적값도 함께 갱신 (안 하면 반대손이 가득 차고 장전이 취소됨)
    public void RefundAmmo(Weapon weapon, float ratio)
    {
        if (weapon == null || ratio <= 0f) return;

        bool isDual = lastDualRight != null && lastDualLeft != null && (weapon == lastDualRight || weapon == lastDualLeft);
        if (isDual)
        {
            AddAmmo(lastDualRight, ratio);
            AddAmmo(lastDualLeft, ratio);
            lastDualRightAmmo = lastDualRight.BulletsAmounts;
            lastDualLeftAmmo = lastDualLeft.BulletsAmounts;
            ReleaseReloadHold(lastDualRight);
            ReleaseReloadHold(lastDualLeft);
            return;
        }

        AddAmmo(weapon, ratio);
        ReleaseReloadHold(weapon);
    }

    // 막탄 장전 보류 (탄환 환수 모듈): JU 자동 장전은 탄창 0발 다음 프레임에 바로 시작돼 막탄 처치 환수가 늦음
    // → 막탄이 결과를 낼 때까지 예비탄(TotalBullets)을 0으로 둬 장전 시작을 막음 (JU 장전 시작 조건: 예비탄 > 0, 무한탄이라 장전 결과엔 영향 없음)
    // 환수되면 즉시 해제, 막탄(산탄은 전부)이 사라지면 그 프레임 LateUpdate에서 해제, 최대 0.3초
    private class ReloadHold
    {
        public int savedTotalBullets;
        public int pendingBullets;
        public float releaseTime;
    }
    private const float ReloadHoldMaxTime = 0.3f;
    private readonly Dictionary<Weapon, ReloadHold> reloadHolds = new Dictionary<Weapon, ReloadHold>();
    private readonly List<Weapon> releaseBuffer = new List<Weapon>();

    // 막탄 생성 시 (BulletHitEffects.Awake, 탄창이 줄기 전)
    public void BeginReloadHold(Weapon weapon)
    {
        if (weapon == null) return;
        if (!reloadHolds.TryGetValue(weapon, out ReloadHold hold))
        {
            hold = new ReloadHold { savedTotalBullets = Mathf.Max(1, weapon.TotalBullets) };
            reloadHolds[weapon] = hold;
            weapon.TotalBullets = 0;
        }
        hold.pendingBullets++;
        hold.releaseTime = Time.time + ReloadHoldMaxTime;
    }

    // 막탄이 사라질 때 (BulletHitEffects.OnDestroy). 해제는 LateUpdate에서 (같은 프레임 처치 환수 판정 이후)
    public void EndReloadHoldBullet(Weapon weapon)
    {
        if (weapon != null && reloadHolds.TryGetValue(weapon, out ReloadHold hold)) hold.pendingBullets--;
    }

    private void ReleaseReloadHold(Weapon weapon)
    {
        if (weapon == null || !reloadHolds.TryGetValue(weapon, out ReloadHold hold)) return;
        weapon.TotalBullets = hold.savedTotalBullets;
        reloadHolds.Remove(weapon);
    }

    private void LateUpdate()
    {
        if (reloadHolds.Count == 0) return;

        releaseBuffer.Clear();
        foreach (var pair in reloadHolds)
        {
            if (pair.Key == null || pair.Value.pendingBullets <= 0 || Time.time >= pair.Value.releaseTime) releaseBuffer.Add(pair.Key);
        }
        foreach (Weapon weapon in releaseBuffer)
        {
            if (weapon == null) reloadHolds.Remove(weapon);
            else ReleaseReloadHold(weapon);
        }
    }

    // 손당 탄창의 ratio만큼 (올림, 최소 1발)
    private static void AddAmmo(Weapon weapon, float ratio)
    {
        int amount = Mathf.Max(1, Mathf.CeilToInt(weapon.BulletsPerMagazine * ratio));
        weapon.BulletsAmounts = Mathf.Min(weapon.BulletsPerMagazine, weapon.BulletsAmounts + amount);
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

        // 장전 시작·완료 감지 → 무기 래퍼 (빈 탄창 급속 장전·재장전 시 폭발·빠른 장전 후 강화)
        if (isReloading != wasReloading)
        {
            WeaponModuleRuntime rightRuntime = GetRuntime(controller.RightHandWeapon);
            WeaponModuleRuntime leftRuntime = GetRuntime(controller.LeftHandWeapon);
            if (isReloading)
            {
                if (rightRuntime != null) rightRuntime.OnReloadStarted(true);
                if (leftRuntime != null) leftRuntime.OnReloadStarted(rightRuntime == null); // 쌍권총 폭발은 한 번만
            }
            else
            {
                if (rightRuntime != null) rightRuntime.OnReloadFinished();
                if (leftRuntime != null) leftRuntime.OnReloadFinished();
            }
            wasReloading = isReloading;
        }

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
                    WeaponModuleRuntime runtime = GetRuntime(reloadingWeapon);
                    float reloadMultiplier = runtime != null ? runtime.CurrentReloadMultiplier : 1f;
                    targetSpeed *= clips[0].clip.length / (reloadTime * reloadMultiplier);
                }
            }
        }

        if (!Mathf.Approximately(anim.speed, targetSpeed)) anim.speed = targetSpeed;
    }

    private bool wasReloading;

    private static WeaponModuleRuntime GetRuntime(Weapon weapon)
    {
        return weapon != null ? weapon.GetComponent<WeaponModuleRuntime>() : null;
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

        // 수류탄 쿨감·모듈 (GrenadeSkill이 플레이어에 붙어 있을 때만). 공격력 % 스탯 공유 (GDD 8-6 #17)
        if (grenadeSkill == null) grenadeSkill = GetComponent<GrenadeSkill>();
        if (grenadeSkill != null)
        {
            ModuleDataSO multi = FindGrenadeModule(ModuleEffect.MultiThrow);
            ModuleDataSO cluster = FindGrenadeModule(ModuleEffect.ClusterGrenade);
            ModuleDataSO extraCharge = FindGrenadeModule(ModuleEffect.ExtraCharge);
            ModuleDataSO killRefund = FindGrenadeModule(ModuleEffect.KillCooldownRefund);

            grenadeSkill.SetCooldownReduction(cooldownReduce);
            grenadeSkill.ApplyModules(
                1f + atkPercent,
                FindGrenadeModule(ModuleEffect.AutoThrow) != null,
                multi != null ? multi.effectCount : 1,
                multi != null ? multi.effectValue : 1f,
                cluster != null ? cluster.effectValue : 0f,
                extraCharge != null ? extraCharge.effectCount : 0,
                killRefund != null ? killRefund.effectValue : 0f);
            grenadeSkill.RefillCharges(); // 다음 라운드 시작 시 충전 가득
        }

        // 공격력·연사는 무기 동기화(ApplyWeaponData) 시 WeaponStatCalculator가 적용
        moduleCritChance = Mathf.Clamp01(critChance);

        // 체력 및 방어력 적용
        if (juHealth != null)
        {
            float newMaxHp = defaultMaxHealth + healthBonus;
            juHealth.SetMaxHealth(newMaxHp);
            juHealth.SetHealth(newMaxHp); // 체력 복구
            juHealth.Protection = defaultProtection + armorBonus;
        }

        // 캐릭터 특수 모듈 (흡혈: effectValue = 처치당 회복 비율, effectCount = 라운드 상한 % / 대시 폭탄: effectValue = 수류탄 피해 비율)
        CharacterModuleRuntime character = GetComponent<CharacterModuleRuntime>();
        if (character != null)
        {
            ModuleDataSO lifesteal = FindCharacterModule(ModuleEffect.Lifesteal);
            ModuleDataSO dashBomb = FindCharacterModule(ModuleEffect.DashBomb);
            ModuleDataSO focus = FindCharacterModule(ModuleEffect.ExtractionFocus); // effectValue = 연사 %, effectCount = 피해 감소 %
            ModuleDataSO lastStand = FindCharacterModule(ModuleEffect.LastStand);   // effectValue = 사격 1회 피해 배율
            character.ApplyModules(
                lifesteal != null ? lifesteal.effectValue : 0f,
                lifesteal != null ? lifesteal.effectCount / 100f : 0f,
                dashBomb != null ? dashBomb.effectValue : 0f,
                focus != null ? focus.effectValue : 0f,
                focus != null ? focus.effectCount / 100f : 0f,
                lastStand != null ? lastStand.effectValue : 0f);
            if (dashBomb != null || lastStand != null) EnsureExplosionVFX();
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

        // 최종 수치: SO 기본값 → 작동 모듈 → 스탯 % (툴팁과 같은 계산, WeaponStatCalculator)
        WeaponStatCalculator.Result stats = WeaponStatCalculator.Calculate(data, ToBaseValues(originals),
            GridInventorySystem.Instance != null ? GridInventorySystem.Instance.placedModules : null);
        int magazine = stats.magazine;
        float reloadTime = stats.reloadTime;
        Weapon.WeaponFireMode fireMode = stats.fireMode;
        bool continuousUse = stats.continuousUse;
        int pelletCount = stats.pelletCountSetting;
        float spread = stats.spread;
        ModuleEffect fireModeEffect = stats.fireModeEffect; // 래퍼가 발사 간격을 조절하는 사격 방식 모듈
        bool quickReload = stats.quickReload;
        bool emptyQuickReload = stats.emptyQuickReload;
        string mechanismNames = "";
        foreach (ModuleDataSO m in stats.mechanisms) mechanismNames += $" [{m.moduleName}]";

        float finalDamage = stats.damage;
        float finalFireRate = stats.FireInterval; // 발사 간격이므로 연사 % 만큼 나눈 값

        weapon.FireMode = fireMode;
        weapon.ContinuousUseItem = continuousUse;
        weapon.NumberOfShotgunBulletsPerShot = pelletCount;
        weapon.LossOfAccuracyPerShot = spread;

        // 연사·탄창: 탄창은 가득 채우고, 예비탄은 무한 (재장전은 필요)
        weapon.Fire_Rate = finalFireRate;
        weaponReloadTimes[weapon] = reloadTime;
        weaponTypes[weapon] = data.weaponType;
        weapon.BulletsPerMagazine = Mathf.Max(1, magazine);
        weapon.BulletsAmounts = weapon.BulletsPerMagazine;
        weapon.InfiniteAmmo = true;

        // 래퍼 모듈 (3점사·연사 예열·거치 사격·빠른 장전·빈 탄창 급속 장전·재장전 시 폭발). 모듈이 없으면 아무것도 바꾸지 않음
        ModuleDataSO reloadExplosion = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.ReloadExplosion);
        if (reloadExplosion != null) mechanismNames += $" [{reloadExplosion.moduleName}]";
        WeaponModuleRuntime runtime = weapon.GetComponent<WeaponModuleRuntime>();
        if (runtime == null) runtime = weapon.gameObject.AddComponent<WeaponModuleRuntime>();
        float perShotDamage = stats.PerShotDamage;
        runtime.Configure(weapon, finalFireRate, stats.fireRateFactor, fireModeEffect, quickReload, emptyQuickReload,
            reloadExplosion != null ? reloadExplosion.effectValue : 0f, perShotDamage, GetComponent<Rigidbody>(), gameObject);
        if (reloadExplosion != null) EnsureExplosionVFX();

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

        // 무기군 기본 치명타(저격총 15%)를 더하고, 배율은 무기군별 (저격총 3.5배)
        float weaponCritChance = Mathf.Clamp01(moduleCritChance + WeaponStatCalculator.BaseCritChance(data.weaponType));
        float weaponCritMultiplier = WeaponStatCalculator.CritMultiplier(data.weaponType, critDamageMultiplier);

        Bullet bullet = template.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.BulletDamage = finalDamage;

            // 탑다운 수평 비행 + 좌우 분산만 (위아래 기울기·분산 제거)
            template.AddComponent<FlatBullet>();

            // 빠른 장전: 장전 직후 3발 피해 증가 (WeaponModuleRuntime의 배율을 탄 생성 시 적용)
            if (quickReload) template.AddComponent<BulletWeaponLink>().sourceWeapon = weapon;

            // 치명타: 템플릿에 판정 컴포넌트를 붙여 두면 JU가 복제한 탄마다 판정 (템플릿은 비활성 부모 밑이라 판정 안 함)
            if (weaponCritChance > 0f)
            {
                CriticalBullet crit = template.AddComponent<CriticalBullet>();
                crit.critChance = weaponCritChance;
                crit.critMultiplier = weaponCritMultiplier;
            }

            // 적중 효과 특수 (넉백·착탄 폭발·처형, 함께 사용 가능). BulletChain보다 먼저 붙여 가로챈 적중에서도 호출되게 함
            ModuleDataSO knockback = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Knockback);
            ModuleDataSO explosion = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.ImpactExplosion);
            ModuleDataSO execute = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Execute);
            ModuleDataSO lastRound = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.LastRoundBonus);
            ModuleDataSO refund = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.AmmoRefund);
            bool autoThrow = FindGrenadeModule(ModuleEffect.AutoThrow) != null;
            if (knockback != null || explosion != null || execute != null || lastRound != null || refund != null || autoThrow)
            {
                BulletHitEffects effects = template.AddComponent<BulletHitEffects>();
                effects.sourceWeapon = weapon;
                effects.autoThrowGrenade = autoThrow;
                if (lastRound != null) { effects.lastRoundBonus = lastRound.effectValue; mechanismNames += $" [{lastRound.moduleName}]"; }
                if (refund != null) { effects.refundRatio = refund.effectValue; mechanismNames += $" [{refund.moduleName}]"; }
                if (knockback != null) { effects.knockbackDistance = knockback.effectValue; mechanismNames += $" [{knockback.moduleName}]"; }
                if (explosion != null) { effects.explosionChance = explosion.effectValue; mechanismNames += $" [{explosion.moduleName}]"; }
                if (execute != null) { effects.executeThreshold = execute.effectValue; mechanismNames += $" [{execute.moduleName}]"; }
                EnsureExplosionVFX();
            }

            // 탄 궤적 특수 (관통·도탄·산탄총 도탄은 충돌 칸이 같아 하나만 배치됨). 공용 도탄은 산탄총에 효과 없음 (유니크가 담당)
            ModuleDataSO pathModule = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Pierce);
            if (pathModule == null && data.weaponType != WeaponType.Shotgun)
            {
                pathModule = FindActiveWeaponSpecial(data.weaponType, ModuleEffect.Ricochet);
            }
            if (pathModule == null && data.weaponType == WeaponType.Shotgun)
            {
                pathModule = FindActiveWeaponUnique(data.weaponType, ModuleEffect.ShotgunRicochet);
            }
            if (pathModule != null)
            {
                BulletChain chain = template.AddComponent<BulletChain>();
                chain.mode = pathModule.effect == ModuleEffect.Pierce ? BulletChain.ChainMode.Pierce : BulletChain.ChainMode.Ricochet;
                chain.remaining = Mathf.Max(1, pathModule.effectCount);
                if (pathModule.effectValue > 0f) chain.damageFactor = pathModule.effectValue;
                if (pathModule.effect == ModuleEffect.ShotgunRicochet) chain.targetPoolSize = ShotgunRicochetTargetPool;
                mechanismNames += $" [{pathModule.moduleName}]";
            }
            else if (data.weaponType == WeaponType.Sniper)
            {
                // 저격총 기본 관통 (탄 궤적 모듈이 있으면 그 모듈로 대체 — 도탄을 끼우면 도탄 저격총)
                BulletChain chain = template.AddComponent<BulletChain>();
                chain.mode = BulletChain.ChainMode.Pierce;
                chain.remaining = WeaponStatCalculator.SniperBasePierce;
                chain.damageFactor = 1f;
                mechanismNames += $" [기본 관통 {WeaponStatCalculator.SniperBasePierce}]";
            }
        }

        bulletTemplates[weapon] = template;
        weapon.BulletPrefab = template;

        Debug.Log($"PlayerStatAndWeaponSync: Applied {data.weaponName} -> Damage {finalDamage:0.#}, FireRate {finalFireRate:0.###}, Magazine {weapon.BulletsPerMagazine}, Reload {reloadTime}s, Crit {weaponCritChance * 100f:0.#}% x{weaponCritMultiplier}{mechanismNames}");
    }

    private static WeaponStatCalculator.BaseValues ToBaseValues(WeaponOriginals originals)
    {
        return new WeaponStatCalculator.BaseValues
        {
            fireMode = originals.fireMode,
            continuousUse = originals.continuousUse,
            pelletCount = originals.pelletCount,
            spread = originals.spread
        };
    }

    // 툴팁용: 지금 그리드 기준으로 이 무기를 장착하면 적용될 최종 수치 (JU 무기 원래 값은 플레이어 인벤토리에서 찾음)
    public bool TryPreviewWeaponStats(WeaponDataSO data, out WeaponStatCalculator.Result result)
    {
        result = default;
        if (data == null || juInventory == null || juInventory.HoldableItensRightHand == null) return false;

        JUHoldableItem item = FindRightHandItem(data.juItemName);
        Weapon weapon = item != null ? item.GetComponent<Weapon>() : null;
        if (weapon == null) return false;

        WeaponOriginals originals = weaponOriginals.TryGetValue(weapon, out WeaponOriginals saved) ? saved : new WeaponOriginals
        {
            fireMode = weapon.FireMode,
            pelletCount = weapon.NumberOfShotgunBulletsPerShot,
            spread = weapon.LossOfAccuracyPerShot,
            continuousUse = weapon.ContinuousUseItem
        };
        result = WeaponStatCalculator.Calculate(data, ToBaseValues(originals),
            GridInventorySystem.Instance != null ? GridInventorySystem.Instance.placedModules : null);
        return true;
    }

    // 수류탄 대상 모듈 (작동: 자동 투척 / 특수: 중복이면 효과가 적용되는 1개만)
    private ModuleDataSO FindGrenadeModule(ModuleEffect effect)
    {
        if (GridInventorySystem.Instance == null) return null;

        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m == null || m.target != ModuleTarget.Grenade || m.effect != effect) continue;
            if (m.category == ModuleCategory.Mechanism || GridInventorySystem.Instance.IsEffectActive(pm)) return m;
        }
        return null;
    }

    // 유니크 산탄총 도탄: 산탄마다 가까운 적 3명 중 무작위로 튕김 (12발이 한 적에게 몰리지 않게)
    private const int ShotgunRicochetTargetPool = 3;

    // 이 무기 대상 유니크 모듈 (중복이면 효과가 적용되는 1개만)
    private ModuleDataSO FindActiveWeaponUnique(WeaponType weaponType, ModuleEffect effect)
    {
        if (GridInventorySystem.Instance == null) return null;

        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m == null || m.category != ModuleCategory.Unique || m.effect != effect) continue;
            if (m.target != ModuleTarget.All && !m.TargetsWeapon(weaponType)) continue;
            if (GridInventorySystem.Instance.IsEffectActive(pm)) return m;
        }
        return null;
    }

    // 캐릭터 대상 특수 모듈 (중복이면 효과가 적용되는 1개만)
    private ModuleDataSO FindCharacterModule(ModuleEffect effect)
    {
        if (GridInventorySystem.Instance == null) return null;

        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m == null || m.target != ModuleTarget.Character || m.effect != effect) continue;
            if (GridInventorySystem.Instance.IsEffectActive(pm)) return m;
        }
        return null;
    }

    // 착탄 폭발·재장전 시 폭발 이펙트 (수류탄 폭발 이펙트 재사용)
    private void EnsureExplosionVFX()
    {
        if (BulletHitEffects.ExplosionVFX != null) return;
        GrenadeSkill grenade = grenadeSkill != null ? grenadeSkill : GetComponent<GrenadeSkill>();
        if (grenade != null) BulletHitEffects.ExplosionVFX = grenade.ExplosionVFX;
    }

    // 이 무기군에 해당 작동 모듈이 그리드에 배치돼 있는지 (단일 권총 수류탄 엔진: 리볼버 개조 적중 충전)
    public bool HasWeaponMechanism(WeaponType weaponType, ModuleEffect effect)
    {
        if (GridInventorySystem.Instance == null) return false;
        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m != null && m.category == ModuleCategory.Mechanism && m.effect == effect && m.TargetsWeapon(weaponType)) return true;
        }
        return false;
    }

    // 이 무기군에 탄 궤적 특수 모듈(관통·도탄)이 적용 중인지 (툴팁: 저격총 기본 관통 대체 여부)
    public bool HasBulletPathModule(WeaponType weaponType)
    {
        return FindActiveWeaponSpecial(weaponType, ModuleEffect.Pierce) != null
            || FindActiveWeaponSpecial(weaponType, ModuleEffect.Ricochet) != null;
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