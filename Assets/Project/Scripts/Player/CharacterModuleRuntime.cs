using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS;
using JUTPS.WeaponSystem;

// 캐릭터 효과 런타임 (GDD 8-6 #17 A4c) — 플레이어 루트에 부착 (PlayerStatAndWeaponSync가 자동 추가)
// A4c-1 단일 P226: 들고 있는 동안 대시 쿨타임·수류탄 충전 시간 25% 단축 (쌍권총 제외)
// A4c-2 흡혈: 처치당 최대 체력 % 회복, 라운드당 상한
// A4c-3 대시 폭탄: 대시 시작 자리에 폭탄 (IsDashing 감지)
// A4c-4 탈출 집중: 탈출 구역 안에서 연사 증가·받는 피해 감소
// A4c-5 위기 반격: 체력 30% 이하 피격 시 주변 충격파 + 넉백
// JU 코드는 수정하지 않고 DashCooldown 값만 바꿈
public class CharacterModuleRuntime : MonoBehaviour
{
    [Header("Single P226 (GDD 8-6 #17)")]
    [SerializeField] private float pistolCooldownFactor = 0.75f; // 대시 쿨타임·수류탄 충전 시간 배율

    private JUCharacterController controller;
    private JUHealth health;
    private GrenadeSkill grenadeSkill;
    private PlayerStatAndWeaponSync sync;
    private float baseDashCooldown;
    private bool holdingSinglePistol;

    // 흡혈 (A4c-2): 처치당 최대 체력의 lifestealRatio 회복, 라운드당 회복 총량은 최대 체력의 lifestealCap까지
    private float lifestealRatio;
    private float lifestealCap;
    private float lifestealHealedThisRound; // 이번 라운드 실제 회복량 (넘친 양 제외)

    // 대시 폭탄 (A4c-3): 대시 시작 자리에 폭탄, fuse 뒤 반경 폭발, 피해 = 수류탄 중심 피해(공격력 % 반영) x dashBombRatio, 내부 쿨
    [Header("Dash Bomb (GDD 8-6 #17)")]
    [SerializeField] private float dashBombFuse = 0.5f;
    [SerializeField] private float dashBombRadius = 3f;
    [SerializeField] private float dashBombCooldown = 4f;
    [SerializeField] private float dashBombVfxScale = 0.4f;
    private float dashBombRatio;
    private float nextDashBombTime;
    private bool wasDashing;

    // 탈출 집중 (A4c-4): 탈출 구역 안에서 연사 +focusFireRate, 받는 피해 −focusDamageReduction
    // 피해 감소는 JUHealth 피해 적용 직전 콜백(방어력 적용 후)에서 줄임 → 치명타격도 감소 (GDD 원안의 되돌림 방식 대신, 2026-10-19 결정)
    private float focusFireRate;
    private float focusDamageReduction;
    private ExtractionZone extractionZone;
    private bool zoneSearched;
    private bool insideFocusZone;

    // 위기 반격 (A4c-5): 피격 후 체력 lastStandThreshold 이하면 주변 충격파 (현재 무기 사격 1회 피해 x lastStandRatio + 넉백), 내부 쿨
    [Header("Last Stand (GDD 8-6 #17)")]
    [SerializeField] private float lastStandThreshold = 0.3f;
    [SerializeField] private float lastStandRadius = 4f;
    [SerializeField] private float lastStandKnockback = 2f;
    [SerializeField] private float lastStandKnockbackTime = 0.25f; // EnemyKnockback 속도 상한 8m/s → 2m에 0.25초
    [SerializeField] private float eliteKnockbackFactor = 0.3f;
    [SerializeField] private float lastStandCooldown = 12f;
    [SerializeField] private float lastStandVfxScale = 0.6f;
    private float lastStandRatio;
    private float nextLastStandTime;
    private static readonly Collider[] overlapBuffer = new Collider[64];

    public bool HoldingSinglePistol => holdingSinglePistol;

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
        health = GetComponent<JUHealth>();
        grenadeSkill = GetComponent<GrenadeSkill>();
        sync = GetComponent<PlayerStatAndWeaponSync>();
        if (controller != null) baseDashCooldown = controller.DashCooldown;
    }

    private void OnEnable()
    {
        EnemyBase.OnAnyEnemyDeath += OnEnemyKilled;
        if (health != null)
        {
            health.SubscribeOnBeforeDamaged(ReduceIncomingDamage);
            health.OnDamaged += OnPlayerDamaged;
        }
    }

    private void OnDisable()
    {
        EnemyBase.OnAnyEnemyDeath -= OnEnemyKilled;
        if (health != null)
        {
            health.UnsubscribeOnBeforeDamaged(ReduceIncomingDamage);
            health.OnDamaged -= OnPlayerDamaged;
        }
        WeaponModuleRuntime.ExternalFireRateMultiplier = 1f; // 정적 값이라 씬 재시작 시 남지 않게
    }

    // 캐릭터 특수 모듈 적용 (PlayerStatAndWeaponSync가 라운드 시작 시 호출, 0 = 모듈 없음). 라운드 회복량도 초기화
    public void ApplyModules(float lifestealRatio, float lifestealCap, float dashBombRatio, float focusFireRate, float focusDamageReduction,
        float lastStandRatio)
    {
        this.lastStandRatio = lastStandRatio;
        nextLastStandTime = 0f;
        if (lastStandRatio > 0f) Debug.Log($"🛡 [위기 반격] 체력 {lastStandThreshold * 100f:0}% 이하 피격 시 사격 피해의 {lastStandRatio * 100f:0}%, 내부 쿨 {lastStandCooldown}s");

        this.focusFireRate = focusFireRate;
        this.focusDamageReduction = Mathf.Clamp01(focusDamageReduction);
        if (focusFireRate > 0f || focusDamageReduction > 0f) Debug.Log($"🎯 [탈출 집중] 구역 안 연사 +{focusFireRate * 100f:0}%, 받는 피해 −{this.focusDamageReduction * 100f:0}%");

        this.lifestealRatio = lifestealRatio;
        this.lifestealCap = lifestealCap;
        lifestealHealedThisRound = 0f;
        this.dashBombRatio = dashBombRatio;
        nextDashBombTime = 0f;
        if (lifestealRatio > 0f) Debug.Log($"🩸 [흡혈] 처치당 {lifestealRatio * 100f:0.##}%, 라운드 상한 {lifestealCap * 100f:0.#}%");
        if (dashBombRatio > 0f) Debug.Log($"💥 [대시 폭탄] 수류탄 피해의 {dashBombRatio * 100f:0}%, 내부 쿨 {dashBombCooldown}s");
    }

    // 탈출 집중: 구역 안이면 모든 무기 연사 배율 (WeaponModuleRuntime이 적용)
    private void UpdateExtractionFocus()
    {
        bool inside = false;
        if (focusFireRate > 0f || focusDamageReduction > 0f)
        {
            if (extractionZone == null && !zoneSearched)
            {
                extractionZone = FindFirstObjectByType<ExtractionZone>();
                zoneSearched = extractionZone != null; // 없으면 다음 프레임에 다시 찾음
            }
            inside = extractionZone != null && extractionZone.IsPlayerInside;
        }

        insideFocusZone = inside;
        WeaponModuleRuntime.ExternalFireRateMultiplier = inside ? 1f + focusFireRate : 1f;
    }

    // JUHealth 피해 적용 직전 (방어력 적용 후 피해)
    private IHealth.DamageInfo ReduceIncomingDamage(IHealth.DamageInfo info)
    {
        if (insideFocusZone && focusDamageReduction > 0f) info.Damage *= 1f - focusDamageReduction;
        return info;
    }

    // 위기 반격: JU OnDamaged는 피해 반영 후 호출 → 실제 남은 체력으로 판정 (사망한 피격은 제외)
    private void OnPlayerDamaged(IHealth.DamageResultInfo result)
    {
        if (lastStandRatio <= 0f || health == null || health.IsDead || result.RealDamage <= 0f) return;
        if (Time.time < nextLastStandTime || health.Health > health.MaxHealth * lastStandThreshold) return;

        nextLastStandTime = Time.time + lastStandCooldown;

        // 오른손 무기 기준 (쌍권총은 재장전 시 폭발과 같이 한 손)
        WeaponModuleRuntime runtime = controller != null && controller.RightHandWeapon != null
            ? controller.RightHandWeapon.GetComponent<WeaponModuleRuntime>() : null;
        float damage = runtime != null ? runtime.PerShotDamage * lastStandRatio : 0f;

        Vector3 center = transform.position + Vector3.up;
        BulletHitEffects.ExplodeAt(center, lastStandRadius, damage, null, gameObject, lastStandVfxScale);

        // 넉백: 반경 안 살아 있는 적을 플레이어 반대 방향으로
        int count = Physics.OverlapSphereNonAlloc(center, lastStandRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        HashSet<EnemyBase> pushed = new HashSet<EnemyBase>();
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = overlapBuffer[i].GetComponentInParent<EnemyBase>();
            if (enemy == null || !pushed.Add(enemy)) continue;

            Vector3 away = enemy.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) continue;

            float distance = lastStandKnockback * (enemy.IsElite ? eliteKnockbackFactor : 1f);
            EnemyKnockback.Apply(enemy, away.normalized, distance, lastStandKnockbackTime);
        }
    }

    // 대시 시작 순간 (IsDashing false → true) 그 자리에 폭탄
    private void CheckDashBomb()
    {
        bool dashing = controller != null && controller.IsDashing;
        bool started = dashing && !wasDashing;
        wasDashing = dashing;

        if (!started || dashBombRatio <= 0f || Time.time < nextDashBombTime) return;
        if (health != null && health.IsDead) return;

        nextDashBombTime = Time.time + dashBombCooldown;
        StartCoroutine(DashBombRoutine(transform.position));
    }

    private IEnumerator DashBombRoutine(Vector3 position)
    {
        // 폭발 전 표시: 콜라이더 없는 작은 빨간 구체
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "DashBomb";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.position = position + Vector3.up * 0.15f;
        marker.transform.localScale = Vector3.one * 0.3f;
        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null) markerRenderer.material.color = new Color(1f, 0.2f, 0.1f);

        yield return new WaitForSeconds(dashBombFuse); // 정비 중(timeScale 0)엔 함께 정지

        Destroy(marker);
        float baseDamage = grenadeSkill != null ? grenadeSkill.ScaledCenterDamage : 80f;
        BulletHitEffects.ExplodeAt(position + Vector3.up * 0.5f, dashBombRadius, baseDamage * dashBombRatio, null, gameObject, dashBombVfxScale);
    }

    // 흡혈: 처치 원인(탄·폭발·수류탄)과 무관하게 모든 적 처치에 적용
    private void OnEnemyKilled(EnemyBase enemy)
    {
        if (lifestealRatio <= 0f || health == null || health.IsDead) return;

        float maxHealth = health.MaxHealth;
        float remainingCap = maxHealth * lifestealCap - lifestealHealedThisRound;
        float heal = Mathf.Min(maxHealth * lifestealRatio, remainingCap, maxHealth - health.Health);
        if (heal <= 0f) return;

        health.SetHealth(health.Health + heal);
        lifestealHealedThisRound += heal;
    }

    private void Update()
    {
        CheckDashBomb();
        UpdateExtractionFocus();

        holdingSinglePistol = IsHoldingSinglePistol();
        float factor = holdingSinglePistol ? pistolCooldownFactor : 1f;

        if (controller != null) controller.DashCooldown = baseDashCooldown * factor;
        if (grenadeSkill == null) grenadeSkill = GetComponent<GrenadeSkill>(); // 늦게 붙는 경우 대비
        if (grenadeSkill != null) grenadeSkill.ChargeTimeMultiplier = factor;
    }

    // 오른손 무기가 P226(무기군 Pistol)이고 왼손 무기가 없을 때 (쌍권총은 무기군 DualPistols + 왼손 무기 있음)
    private bool IsHoldingSinglePistol()
    {
        if (controller == null || sync == null) return false;

        Weapon right = controller.RightHandWeapon;
        if (right == null || controller.LeftHandWeapon != null) return false;
        return sync.TryGetWeaponType(right, out WeaponType type) && type == WeaponType.Pistol;
    }
}
