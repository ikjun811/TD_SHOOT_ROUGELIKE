using UnityEngine;
using UnityEngine.AI;
using JU;

// 봄버 (GDD 8-6 #18) — 빠르게 접근 → 사거리 안이면 멈춰 붉게 깜빡이며 부풂(바닥에 폭발 범위 원) → 자폭
//   자폭: 범위 안 플레이어에게 공격력만큼 피해, 주변 적에게도 피해 후 처치가 아닌 제거(드랍·처치 수 없음)
//   처치당해도 폭발: 같은 범위·피해 → 무리 안에서 잡으면 연쇄, 플레이어가 가까우면 플레이어도 피해 (가까이 오기 전에 잡아야 함)
//   적에게 주는 피해 = 봄버 최대 체력 x enemyDamageMultiplier (라운드 배율 자동 반영, 러너는 거의 한 방)
//   근접 공격은 EnemyBase.useMeleeAttack = false (생성기가 설정)
[RequireComponent(typeof(EnemyBase))]
public class EnemyBomber : MonoBehaviour
{
    private static readonly Collider[] overlapBuffer = new Collider[64];

    public float triggerRange = 2.2f;          // 자폭 시작 거리 (수평)
    public float fuseTime = 0.6f;              // 예고 시간
    public float explosionRadius = 3f;
    public float enemyDamageMultiplier = 1.5f; // 적에게 주는 피해 = 최대 체력 x 배율
    public float fuseSwell = 1.3f;             // 예고 중 커지는 배율
    public float blinkInterval = 0.1f;
    public Color ringColor = new Color(1f, 0.15f, 0.1f, 0.9f);

    private EnemyBase enemy;
    private NavMeshAgent agent;
    private JUHealth health;
    private EnemyHitFlash hitFlash;
    private Transform player;
    private JUHealth playerHealth;

    private bool fuseStarted;
    private bool exploded;
    private float fuseTimer;
    private float nextBlinkTime;
    private Vector3 baseScale;
    private LineRenderer ring;

    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<JUHealth>();
    }

    private void Start()
    {
        hitFlash = GetComponent<EnemyHitFlash>(); // EnemyBase.Start가 부착
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<JUHealth>();
        }
        if (health != null) health.OnDeath += OnKilled;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnKilled;
        if (ring != null) Destroy(ring.gameObject);
    }

    private void Update()
    {
        if (exploded || enemy.IsDead || player == null) return;

        if (!fuseStarted)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude <= triggerRange * triggerRange && playerHealth != null && !playerHealth.IsDead) StartFuse();
            return;
        }

        fuseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(fuseTimer / fuseTime);

        // 점점 빨라지는 깜빡임 + 부풂
        if (Time.time >= nextBlinkTime && hitFlash != null)
        {
            float interval = Mathf.Lerp(blinkInterval, blinkInterval * 0.4f, t);
            hitFlash.FlashAlert(interval * 0.5f);
            nextBlinkTime = Time.time + interval;
        }
        transform.localScale = baseScale * Mathf.Lerp(1f, fuseSwell, t);
        UpdateRing(t);

        if (fuseTimer >= fuseTime) SelfDestruct();
    }

    private void StartFuse()
    {
        fuseStarted = true;
        fuseTimer = 0f;
        nextBlinkTime = 0f;
        baseScale = transform.localScale;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        CreateRing();
    }

    private void SelfDestruct()
    {
        Explode();
        enemy.RemoveWithoutReward();
    }

    // 처치당함 (EnemyBase.OnEnemyDeath와 별개로 JUHealth.OnDeath 구독)
    private void OnKilled()
    {
        Explode();
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        if (ring != null) ring.enabled = false;

        Vector3 center = transform.position;
        BulletHitEffects.SpawnExplosionVFX(center, explosionRadius / 4f);

        // 플레이어 (수평 거리)
        if (playerHealth != null && !playerHealth.IsDead)
        {
            Vector3 toPlayer = player.position - center;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude <= explosionRadius * explosionRadius) playerHealth.DoDamage(enemy.AttackDamage);
        }

        // 주변 적 (자기 제외) — 피해로 죽은 봄버가 다시 폭발 → 연쇄 (exploded 플래그로 한 번만)
        float enemyDamage = enemy.MaxHealth * enemyDamageMultiplier;
        int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        // 연쇄 폭발이 같은 버퍼를 덮어쓰므로 대상 먼저 복사
        JUHealth[] targets = new JUHealth[count];
        int targetCount = 0;
        for (int i = 0; i < count; i++)
        {
            EnemyBase other = overlapBuffer[i].GetComponentInParent<EnemyBase>();
            if (other == null || other == enemy || other.IsDead) continue;
            JUHealth otherHealth = other.GetComponent<JUHealth>();
            if (otherHealth == null || otherHealth.IsDead || System.Array.IndexOf(targets, otherHealth, 0, targetCount) >= 0) continue;
            targets[targetCount++] = otherHealth;
        }

        for (int i = 0; i < targetCount; i++)
        {
            JUHealth target = targets[i];
            if (target == null || target.IsDead) continue;
            Vector3 direction = target.transform.position - center;
            direction.y = 0f;
            target.DoDamage(new IHealth.DamageInfo
            {
                Damage = enemyDamage,
                HitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                HitPosition = target.transform.position,
                HitOriginPosition = center,
                DamageOwner = gameObject
            });
        }
    }

    #region 폭발 범위 원

    private const int RingSegments = 48;
    private static Material ringMaterial; // 모든 봄버 공유

    private void CreateRing()
    {
        if (ring != null) return;
        GameObject go = new GameObject("BomberRing");
        ring = go.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = RingSegments;
        ring.widthMultiplier = 0.1f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        if (ringMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) ringMaterial = new Material(shader) { name = "BomberRing", hideFlags = HideFlags.DontSave };
        }
        if (ringMaterial != null) ring.sharedMaterial = ringMaterial;
        ring.startColor = ring.endColor = ringColor;
    }

    // 실제 폭발 범위 그대로 바닥(y 0.05)에 표시, 진행할수록 빠르게 깜빡임
    private void UpdateRing(float t)
    {
        if (ring == null) return;
        Vector3 center = transform.position;
        center.y = 0.05f;
        float radius = explosionRadius;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
        Color c = ringColor;
        c.a = ringColor.a * Mathf.Lerp(0.5f, 1f, Mathf.PingPong(t * 6f, 1f));
        ring.startColor = ring.endColor = c;
    }

    #endregion
}
