using UnityEngine;
using JU;

// 수류탄 스킬 (G키, 무기 슬롯과 별개, GDD 3-3) — 플레이어 루트에 부착
// 마우스가 가리키는 바닥 지점으로 포물선 투척, fuse 후 범위 피해 (GrenadeProjectile)
// 모듈(A4b): 공격력 % 공유, 자동 투척(G키 불가·적중 시 확률), 다중 투척, 집속탄, 충전 +1, 처치 시 쿨감
public class GrenadeSkill : MonoBehaviour
{
    [Header("Grenade Skill Settings")]
    [SerializeField] private KeyCode grenadeKey = KeyCode.G;
    [SerializeField] private float cooldown = 10f;       // 쿨타임 (초)
    [SerializeField] private int maxCharges = 2;         // 최대 충전 횟수
    [SerializeField] private float maxCooldownReduction = 0.7f; // 쿨감 상한 (임시값, GDD 8-4)

    [Header("Throw")]
    [SerializeField] private float maxThrowRange = 8f;   // 최대 사거리 (m), 마우스가 더 멀어도 이 거리에 떨어짐
    [SerializeField] private float flightTime = 0.7f;    // 목표 지점까지 비행 시간 (초)
    [SerializeField] private float throwHeight = 1.5f;   // 발밑 기준 투척 높이 (m)

    [Header("Explosion (임시값)")]
    [SerializeField] private float fuseTime = 1.2f;      // 투척 후 폭발까지 (초)
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float centerDamage = 80f;
    [SerializeField] private float edgeDamage = 40f;

    [Header("Visuals")]
    [Tooltip("수류탄 외형 모델 (예: JU Granade.fbx). 비우면 작은 구체 사용. 콜라이더·스크립트는 제거됨")]
    [SerializeField] private GameObject grenadeModel;
    [Tooltip("폭발 이펙트 (예: JU Explosion Particle). JU Explosion 피해·물리 힘은 자동으로 꺼짐")]
    [SerializeField] private GameObject explosionVFX;
    [SerializeField] private float vfxScale = 0.5f;     // 폭발 이펙트 크기 배율
    [SerializeField] private float vfxSpeed = 1.5f;     // 폭발 이펙트 재생 속도 배율 (클수록 빨리 끝남)
    [SerializeField] private float vfxDuration = 2f;    // 이 시간 뒤 이펙트 제거 (초)

    [Header("Modules (GDD 8-6 #17, A4b)")]
    [SerializeField] private float autoThrowChance = 0.08f;     // 자동 투척: 탄 적중당 확률
    [SerializeField] private float autoThrowCooldown = 1f;      // 자동 투척 내부 쿨 (초)
    [SerializeField] private float autoThrowDamageRatio = 0.6f;
    [SerializeField] private float autoThrowFlightTime = 0.5f;  // 움직이는 적을 맞히도록 짧게
    [SerializeField] private float autoThrowFuseAfterLanding = 0.3f;
    [SerializeField] private float multiThrowSpacing = 2f;      // 다중 투척 좌우 간격 (m)

    [Header("Current Status (Read Only)")]
    [SerializeField] private int currentCharges;
    [SerializeField] private float currentCooldownTimer;

    private float baseCooldown; // 모듈 보정 전 원래 쿨타임
    private int baseMaxCharges;
    private JUHealth health;
    private Collider[] ownColliders;

    // 모듈 설정 (PlayerStatAndWeaponSync가 라운드 시작 시 ApplyModules로 지정)
    private float damageMultiplier = 1f;   // 공격력 % 스탯 공유
    private bool autoThrow;                // 자동 투척: G키 사용 불가, 충전과 무관하게 적중 시 확률 투척
    private int multiThrowCount = 1;       // 다중 투척: 한 번에 던지는 개수
    private float multiThrowDamageRatio = 1f;
    private float clusterDamageRatio;      // 집속탄: 소형 폭탄 피해 비율 (0 = 없음)
    private float killRefundSeconds;       // 처치 시 쿨감 (자동 투척이면 효과 없음)
    private float nextAutoThrowTime;

    public GameObject ExplosionVFX => explosionVFX; // 착탄 폭발 모듈이 이펙트 재사용 (BulletHitEffects)
    public bool UsesCharges => !autoThrow; // 자동 투척이면 충전을 쓰지 않음 (WorldAmmoRing이 충전 막대 숨김)
    public int CurrentCharges => currentCharges;
    public int MaxCharges => maxCharges;
    public float CooldownProgress => currentCooldownTimer / cooldown;
    public float ScaledCenterDamage => centerDamage * damageMultiplier; // 중심 피해 x 공격력 % (대시 폭탄 기준값)

    // 충전 시간 배율 (CharacterModuleRuntime: 단일 P226을 드는 동안 0.75). 진행도 표시가 그대로 맞도록 타이머 증가 속도로 적용
    public float ChargeTimeMultiplier { get; set; } = 1f;

    private void Awake()
    {
        baseCooldown = cooldown;
        baseMaxCharges = maxCharges;
        health = GetComponent<JUHealth>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        EnemyBase.OnAnyEnemyDeath += OnEnemyKilled;
    }

    private void OnDisable()
    {
        EnemyBase.OnAnyEnemyDeath -= OnEnemyKilled;
    }

    // 수류탄 모듈 적용 (RefillCharges 전에 호출). 자동 투척이면 충전 +1·처치 시 쿨감은 효과 없음 (의도된 상극)
    public void ApplyModules(float damageMultiplier, bool autoThrow, int multiThrowCount, float multiThrowDamageRatio,
        float clusterDamageRatio, int bonusCharges, float killRefundSeconds)
    {
        this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
        this.autoThrow = autoThrow;
        this.multiThrowCount = Mathf.Max(1, multiThrowCount);
        this.multiThrowDamageRatio = multiThrowCount > 1 ? multiThrowDamageRatio : 1f;
        this.clusterDamageRatio = clusterDamageRatio;
        this.killRefundSeconds = autoThrow ? 0f : killRefundSeconds;
        maxCharges = baseMaxCharges + (autoThrow ? 0 : bonusCharges);
        Debug.Log($"💣 [수류탄 모듈] 피해 x{this.damageMultiplier:0.##}, 자동 투척 {autoThrow}, 다중 {this.multiThrowCount}개 x{this.multiThrowDamageRatio:0.##}, 집속 {clusterDamageRatio:0.##}, 최대 충전 {maxCharges}, 처치 쿨감 {this.killRefundSeconds}s");
    }

    // 처치 시 쿨감: 충전 대기 시간을 앞당김 (충전 완료 처리는 Update)
    private void OnEnemyKilled(EnemyBase enemy)
    {
        if (killRefundSeconds <= 0f || currentCharges >= maxCharges) return;
        currentCooldownTimer += killRefundSeconds;
    }

    // 자동 투척 (BulletHitEffects가 내 탄의 적 적중마다 호출)
    public void TryAutoThrow(EnemyBase target)
    {
        if (!autoThrow || target == null || Time.timeScale <= 0f) return;
        if (health != null && health.IsDead) return;
        if (Time.time < nextAutoThrowTime || Random.value >= autoThrowChance) return;

        nextAutoThrowTime = Time.time + autoThrowCooldown;
        Vector3 point = target.transform.position;
        point.y = transform.position.y; // 바닥 높이
        ThrowAt(point, autoThrowFlightTime, autoThrowFlightTime + autoThrowFuseAfterLanding, autoThrowDamageRatio);
    }

    private void Start()
    {
        currentCharges = maxCharges;
    }

    // 테트리스 모듈 쿨감 적용 (0.1 = 10% 감소)
    public void SetCooldownReduction(float reduction)
    {
        reduction = Mathf.Clamp(reduction, 0f, maxCooldownReduction);
        cooldown = baseCooldown * (1f - reduction);
        Debug.Log($"💣 [수류탄 쿨타임] {baseCooldown}s -> {cooldown:0.##}s (-{reduction * 100f:0.#}%)");
    }

    // 다음 라운드 시작 시 충전 가득 채움
    public void RefillCharges()
    {
        currentCharges = maxCharges;
        currentCooldownTimer = 0f;
    }

    private void Update()
    {
        // 정비 단계(timeScale 0)에서는 충전·투척 모두 정지
        if (Time.timeScale <= 0f) return;

        // 쿨타임 충전 연산 (처치 시 쿨감으로 한 번에 여러 충전이 찰 수 있음)
        if (currentCharges < maxCharges)
        {
            currentCooldownTimer += Time.deltaTime / Mathf.Max(0.01f, ChargeTimeMultiplier);
            while (currentCooldownTimer >= cooldown && currentCharges < maxCharges)
            {
                currentCooldownTimer -= cooldown;
                currentCharges++;
                Debug.Log($"💣 [수류탄 충전 완료] 현재 충전량: {currentCharges} / {maxCharges}");
            }
            if (currentCharges >= maxCharges) currentCooldownTimer = 0f;
        }

        // 자동 투척 모듈이면 G키 투척 불가 (대가)
        bool isDead = health != null && health.IsDead;
        if (!autoThrow && Input.GetKeyDown(grenadeKey) && currentCharges > 0 && !isDead)
        {
            currentCharges--;
            ThrowAt(GetAimGroundPoint(), flightTime, fuseTime, 1f);
            Debug.Log($"💣 [수류탄 투척!] 남은 충전량: {currentCharges} / {maxCharges}");
        }
    }

    // 목표 지점으로 투척 (다중 투척이면 던지는 방향의 좌우로 간격을 두고 여러 개)
    private void ThrowAt(Vector3 target, float flight, float fuse, float damageRatio)
    {
        Vector3 start = transform.position + Vector3.up * throwHeight;
        Vector3 forward = target - transform.position;
        forward.y = 0f;
        Vector3 side = forward.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, forward.normalized) : transform.right;

        float ratio = damageRatio * multiThrowDamageRatio * damageMultiplier;
        Collider[] thrown = new Collider[multiThrowCount];
        for (int i = 0; i < multiThrowCount; i++)
        {
            float offset = (i - (multiThrowCount - 1) * 0.5f) * multiThrowSpacing;
            // 출발 위치도 좌우로 조금 벌림 (같은 위치에서 겹쳐 생성되면 서로 밀어내 방향이 흐트러짐)
            thrown[i] = SpawnGrenade(start + side * offset * MultiThrowStartSpread, target + side * offset, flight, fuse, ratio);
        }

        // 같은 투척의 수류탄끼리는 충돌하지 않음
        for (int i = 0; i < thrown.Length; i++)
        {
            for (int j = i + 1; j < thrown.Length; j++)
            {
                if (thrown[i] != null && thrown[j] != null) Physics.IgnoreCollision(thrown[i], thrown[j]);
            }
        }
    }

    private const float MultiThrowStartSpread = 0.15f; // 출발 위치 좌우 간격 = 착지 간격 x 이 값

    private Collider SpawnGrenade(Vector3 start, Vector3 target, float flight, float fuse, float damageRatio)
    {
        GameObject grenade = CreateGrenadeObject(start);
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        rb.linearVelocity = CalculateLaunchVelocity(start, target, flight);

        GrenadeProjectile projectile = grenade.GetComponent<GrenadeProjectile>();
        projectile.fuseTime = fuse;
        projectile.radius = explosionRadius;
        projectile.centerDamage = centerDamage * damageRatio;
        projectile.edgeDamage = edgeDamage * damageRatio;
        projectile.explosionVFX = explosionVFX;
        projectile.vfxScale = vfxScale;
        projectile.vfxSpeed = vfxSpeed;
        projectile.vfxDuration = vfxDuration;
        projectile.owner = gameObject;
        projectile.clusterDamageRatio = clusterDamageRatio;
        return grenade.GetComponent<Collider>();
    }

    // 튕기지 않고 착지한 자리 근처에 멈추는 물리 재질 (조준 지점 근처에서 폭발)
    private static PhysicsMaterial grenadeMaterial;

    private static PhysicsMaterial GetGrenadeMaterial()
    {
        if (grenadeMaterial == null)
        {
            grenadeMaterial = new PhysicsMaterial("Grenade")
            {
                bounciness = 0f,
                dynamicFriction = 1f,
                staticFriction = 1f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
        }
        return grenadeMaterial;
    }

    // 마우스가 가리키는 바닥 지점 (플레이어 발 높이 평면 기준, 최대 사거리로 제한)
    private Vector3 GetAimGroundPoint()
    {
        Vector3 origin = transform.position;
        Vector3 fallback = origin + transform.forward * maxThrowRange * 0.5f;

        Camera cam = Camera.main;
        if (cam == null) return fallback;

        Plane ground = new Plane(Vector3.up, origin);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!ground.Raycast(ray, out float enter)) return fallback;

        Vector3 offset = ray.GetPoint(enter) - origin;
        offset.y = 0f;
        return origin + Vector3.ClampMagnitude(offset, maxThrowRange);
    }

    // 정해진 비행 시간에 목표 지점에 닿는 초기 속도 (포물선)
    private static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time)
    {
        Vector3 displacement = target - start;
        return displacement / time - 0.5f * Physics.gravity * time;
    }

    private GameObject CreateGrenadeObject(Vector3 position)
    {
        GameObject grenade = new GameObject("Grenade");
        grenade.transform.position = position;

        SphereCollider sphere = grenade.AddComponent<SphereCollider>();
        sphere.radius = 0.12f;
        sphere.sharedMaterial = GetGrenadeMaterial();

        Rigidbody rb = grenade.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.angularDamping = 10f; // 착지 후 구르지 않게 (공중 궤적에는 영향 없음)
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        grenade.AddComponent<GrenadeProjectile>();

        // 던진 직후 플레이어 몸에 부딪히지 않도록
        foreach (Collider own in ownColliders)
        {
            if (own != null) Physics.IgnoreCollision(sphere, own);
        }

        // 외형: 지정 모델 또는 기본 구체 (외형의 콜라이더·스크립트는 제거해 물리는 위 구체만 사용)
        GameObject visual = grenadeModel != null
            ? Instantiate(grenadeModel, grenade.transform)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (grenadeModel == null)
        {
            visual.transform.SetParent(grenade.transform, false);
            visual.transform.localScale = Vector3.one * 0.25f;
        }
        visual.transform.localPosition = Vector3.zero;

        // 콜라이더·리지드바디는 첫 물리 프레임에 섞이지 않도록 즉시 제거
        foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody r in visual.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
        foreach (MonoBehaviour m in visual.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);

        return grenade;
    }
}
