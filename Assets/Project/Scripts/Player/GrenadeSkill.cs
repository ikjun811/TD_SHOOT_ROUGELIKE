using UnityEngine;
using JU;

// 수류탄 스킬 (G키, 무기 슬롯과 별개, GDD 3-3) — 플레이어 루트에 부착
// 마우스가 가리키는 바닥 지점으로 포물선 투척, fuse 후 범위 피해 (GrenadeProjectile)
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

    [Header("Current Status (Read Only)")]
    [SerializeField] private int currentCharges;
    [SerializeField] private float currentCooldownTimer;

    private float baseCooldown; // 모듈 보정 전 원래 쿨타임
    private JUHealth health;
    private Collider[] ownColliders;

    public GameObject ExplosionVFX => explosionVFX; // 착탄 폭발 모듈이 이펙트 재사용 (BulletHitEffects)
    public int CurrentCharges => currentCharges;
    public int MaxCharges => maxCharges;
    public float CooldownProgress => currentCooldownTimer / cooldown;

    private void Awake()
    {
        baseCooldown = cooldown;
        health = GetComponent<JUHealth>();
        ownColliders = GetComponentsInChildren<Collider>(true);
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

        // 쿨타임 충전 연산
        if (currentCharges < maxCharges)
        {
            currentCooldownTimer += Time.deltaTime;
            if (currentCooldownTimer >= cooldown)
            {
                currentCooldownTimer = 0f;
                currentCharges++;
                Debug.Log($"💣 [수류탄 충전 완료] 현재 충전량: {currentCharges} / {maxCharges}");
            }
        }

        bool isDead = health != null && health.IsDead;
        if (Input.GetKeyDown(grenadeKey) && currentCharges > 0 && !isDead)
        {
            ThrowGrenade();
        }
    }

    private void ThrowGrenade()
    {
        currentCharges--;

        Vector3 start = transform.position + Vector3.up * throwHeight;
        Vector3 target = GetAimGroundPoint();

        GameObject grenade = CreateGrenadeObject(start);
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        rb.linearVelocity = CalculateLaunchVelocity(start, target, flightTime);

        GrenadeProjectile projectile = grenade.GetComponent<GrenadeProjectile>();
        projectile.fuseTime = fuseTime;
        projectile.radius = explosionRadius;
        projectile.centerDamage = centerDamage;
        projectile.edgeDamage = edgeDamage;
        projectile.explosionVFX = explosionVFX;
        projectile.vfxScale = vfxScale;
        projectile.vfxSpeed = vfxSpeed;
        projectile.vfxDuration = vfxDuration;
        projectile.owner = gameObject;

        Debug.Log($"💣 [수류탄 투척!] 남은 충전량: {currentCharges} / {maxCharges}");
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

        Rigidbody rb = grenade.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
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
