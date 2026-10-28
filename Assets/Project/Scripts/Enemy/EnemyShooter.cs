using UnityEngine;
using UnityEngine.AI;
using JU;

// 슈터 (GDD 8-6 #18) — 거리를 유지하며 느리고 눈에 보이는 탄을 쏨 (대시로 피할 수 있음)
//   이동(EnemyBase 기본 추적·근접 공격은 생성기가 끔):
//     멀거나(> preferredMaxRange) 벽에 가려 안 보이면 → 플레이어 쪽으로 접근
//     너무 가까우면(< retreatRange) → 플레이어 반대쪽으로 물러남 (NavMesh 위 지점을 찾지 못하면 제자리)
//     그 사이 → 멈춰 서서 사격
//   사격: 보이고 사거리 안이면 windup 동안 붉게 깜빡이며 조준선 표시(플레이어를 따라감) → 그 순간 플레이어 위치로 발사 → 쿨타임
//   피해 = EnemyBase.AttackDamage (라운드·엘리트 배율 반영)
[RequireComponent(typeof(EnemyBase))]
public class EnemyShooter : MonoBehaviour
{
    public float preferredMaxRange = 13f; // 사거리 (사격은 +2m까지) — 사용자 요청으로 10 → 13 (1.3배)
    public float retreatRange = 5f;
    public float retreatStep = 4f;
    public float repathInterval = 0.25f;

    public float fireCooldown = 2.2f;
    public float windupTime = 0.45f;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 3f;
    public float projectileSize = 0.35f;
    public float projectileHitRadius = 0.55f;
    public float muzzleHeight = 1f;

    public Color aimLineColor = new Color(1f, 0.3f, 0.15f, 0.6f);
    public float aimLineLength = 16f;

    private EnemyBase enemy;
    private NavMeshAgent agent;
    private EnemyHitFlash hitFlash;
    private Transform player;
    private JUHealth playerHealth;
    private int wallMask;

    private float nextFireTime;
    private float windupEndTime = -1f;
    private float nextRepathTime;
    private float nextBlinkTime;
    private LineRenderer aimLine;
    private static Material aimLineMaterial;

    private bool IsWindingUp => windupEndTime >= 0f;

    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        agent = GetComponent<NavMeshAgent>();
        wallMask = LayerMask.GetMask("Walls");
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
        // 여러 마리가 같은 박자로 쏘지 않도록 첫 사격 시점을 흩뜨림
        nextFireTime = Time.time + Random.Range(0.8f, 1.8f);
        CreateAimLine();
    }

    private void Update()
    {
        if (enemy.IsDead || player == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            HideAimLine();
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        bool playerAlive = playerHealth != null && !playerHealth.IsDead;
        bool canSee = HasLineOfSight();

        if (IsWindingUp)
        {
            agent.isStopped = true;
            FacePlayer(toPlayer);
            UpdateWindup(toPlayer);
            if (Time.time >= windupEndTime)
            {
                windupEndTime = -1f;
                HideAimLine();
                if (playerAlive) Fire(toPlayer);
                nextFireTime = Time.time + fireCooldown;
            }
            return;
        }

        UpdateMovement(toPlayer, distance, canSee);

        if (playerAlive && canSee && distance <= preferredMaxRange + 2f && Time.time >= nextFireTime)
        {
            windupEndTime = Time.time + windupTime;
            nextBlinkTime = 0f;
        }
    }

    private void UpdateMovement(Vector3 toPlayer, float distance, bool canSee)
    {
        if (distance > preferredMaxRange || !canSee)
        {
            // 접근 (진행 방향을 봄)
            agent.updateRotation = true;
            agent.isStopped = false;
            if (Time.time >= nextRepathTime)
            {
                agent.SetDestination(player.position);
                nextRepathTime = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
            }
        }
        else if (distance < retreatRange)
        {
            // 후퇴 — 플레이어를 바라본 채 뒷걸음 (모델은 방향별 걷기 애니메이션)
            agent.updateRotation = false;
            FacePlayer(toPlayer);
            if (Time.time >= nextRepathTime)
            {
                Vector3 away = distance > 0.01f ? -toPlayer / distance : -transform.forward;
                Vector3 target = transform.position + away * retreatStep;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }
                else
                {
                    agent.isStopped = true;
                }
                nextRepathTime = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
            }
        }
        else
        {
            // 거리 유지 — 멈춰 서서 플레이어를 바라봄
            agent.isStopped = true;
            FacePlayer(toPlayer);
        }
    }

    // 총구 높이에서 플레이어 몸통까지 벽·장애물(Walls)에 막히지 않았는지
    private bool HasLineOfSight()
    {
        if (wallMask == 0) return true;
        Vector3 from = transform.position;
        from.y = muzzleHeight;
        Vector3 to = player.position;
        to.y = muzzleHeight;
        return !Physics.Linecast(from, to, wallMask, QueryTriggerInteraction.Ignore);
    }

    private void FacePlayer(Vector3 toPlayer)
    {
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
    }

    private void UpdateWindup(Vector3 toPlayer)
    {
        if (hitFlash != null && Time.time >= nextBlinkTime)
        {
            hitFlash.FlashAlert(0.06f);
            nextBlinkTime = Time.time + 0.12f;
        }

        if (aimLine == null) return;
        Vector3 from = MuzzlePosition();
        Vector3 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
        float length = aimLineLength;
        if (wallMask != 0 && Physics.Raycast(from, dir, out RaycastHit hit, aimLineLength, wallMask, QueryTriggerInteraction.Ignore))
        {
            length = hit.distance;
        }
        aimLine.enabled = true;
        aimLine.SetPosition(0, from);
        aimLine.SetPosition(1, from + dir * length);
    }

    private void Fire(Vector3 toPlayer)
    {
        Vector3 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
        EnemyProjectile.Spawn(MuzzlePosition() + dir * 0.6f, dir, projectileSpeed, enemy.AttackDamage, projectileLifetime,
                              projectileHitRadius, projectileSize, player, playerHealth);
        if (enemy.ModelAnimator != null) enemy.ModelAnimator.PlayAction("Shoot"); // 모델 사격 동작 (없으면 무시)
    }

    private Vector3 MuzzlePosition()
    {
        Vector3 pos = transform.position;
        pos.y = muzzleHeight;
        return pos;
    }

    private void CreateAimLine()
    {
        GameObject go = new GameObject("AimLine");
        go.transform.SetParent(transform, false);
        aimLine = go.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.widthMultiplier = 0.05f;
        aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimLine.receiveShadows = false;
        if (aimLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) aimLineMaterial = new Material(shader) { name = "ShooterAimLine", hideFlags = HideFlags.DontSave };
        }
        if (aimLineMaterial != null) aimLine.sharedMaterial = aimLineMaterial;
        aimLine.startColor = aimLineColor;
        aimLine.endColor = new Color(aimLineColor.r, aimLineColor.g, aimLineColor.b, 0f);
        aimLine.enabled = false;
    }

    private void HideAimLine()
    {
        if (aimLine != null) aimLine.enabled = false;
    }
}
