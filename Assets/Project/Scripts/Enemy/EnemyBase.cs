using UnityEngine;
using UnityEngine.AI;
using JU;

// 적 개체 기본 AI, 피격, 플레이어 공격 및 사망 처리 클래스
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(JUHealth))]
public class EnemyBase : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private float baseHealth = 50f;
    [SerializeField] private float baseMoveSpeed = 3.5f;
    [SerializeField] private float baseAttackDamage = 10f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f; // 공격 쿨타임 (1.2초)

    [Header("Pathfinding")]
    [SerializeField] private float repathInterval = 0.2f;      // 경로 재계산 간격 (초)
    [SerializeField] private float repathMoveThreshold = 1.5f; // 플레이어가 이만큼 움직이면 간격과 무관하게 즉시 재계산 (m)

    [Header("Elite (WaveManager가 MarkAsElite로 지정, GDD 8-4 #15 임시값)")]
    [SerializeField] private float eliteHealthMultiplier = 5f;
    [SerializeField] private float eliteDamageMultiplier = 1.5f;
    [SerializeField] private float eliteSpeedMultiplier = 0.9f;
    [SerializeField] private float eliteScaleMultiplier = 1.3f;
    [SerializeField] private Color eliteColor = new Color(0.6f, 0.25f, 0.9f);

    [Header("Effects")]
    [SerializeField] private GameObject deathVFX;

    public bool IsElite { get; private set; }

    // 사망한 프레임 (탄환 환수: 같은 프레임 충돌에서 JU 피해가 먼저 처리돼도 이번 적중의 처치로 판정, BulletHitEffects)
    public int DeathFrame { get; private set; } = -1;

    // 적 사망 알림 (처치 시 쿨감 모듈 등, GrenadeSkill)
    public static event System.Action<EnemyBase> OnAnyEnemyDeath;

    // 생성 직후(Start 전) 호출하면 Start에서 엘리트 배율·외형 적용
    public void MarkAsElite()
    {
        IsElite = true;
    }

    private NavMeshAgent agent;
    private Transform playerTransform;
    private JUHealth juHealth;
    private JUHealth playerHealth;
    private float lastAttackTime = -10f;
    private bool isDead = false;

    private float nextRepathTime;
    private Vector3 lastPathTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        juHealth = GetComponent<JUHealth>();
    }

    private void Start()
    {
        float currentHp = baseHealth;
        float currentDmg = baseAttackDamage;
        float currentSpd = baseMoveSpeed;

        if (IsElite)
        {
            currentHp *= eliteHealthMultiplier;
            currentDmg *= eliteDamageMultiplier;
            currentSpd *= eliteSpeedMultiplier;
            ApplyEliteVisuals();
        }

        if (StageDifficultyManager.Instance != null)
        {
            int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;

            // 스테이지 시간 배율 x 라운드 배율
            currentHp *= StageDifficultyManager.Instance.GetEnemyHpMultiplier() * StageDifficultyManager.Instance.GetRoundHpMultiplier(round);
            currentDmg *= StageDifficultyManager.Instance.GetEnemyDamageMultiplier() * StageDifficultyManager.Instance.GetRoundDamageMultiplier(round);
            currentSpd *= StageDifficultyManager.Instance.GetEnemySpeedMultiplier();
        }

        if (juHealth != null)
        {
            juHealth.SetMaxHealth(currentHp);
            juHealth.SetHealth(currentHp);
        }

        baseAttackDamage = currentDmg;
        baseMoveSpeed = currentSpd;

        if (agent != null)
        {
            agent.speed = baseMoveSpeed;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<JUHealth>();
        }

        if (juHealth != null)
        {
            juHealth.OnDeath += OnEnemyDeath;
            juHealth.OnDamaged += OnEnemyDamaged;
        }

        // 머리 위 체력바 자동 부착 (일반: 피격 후에만 빨강 / 엘리트: 항상 보라, 프리팹에 이미 있으면 그 설정 사용)
        if (GetComponent<WorldHealthBar>() == null)
        {
            WorldHealthBar bar = gameObject.AddComponent<WorldHealthBar>();
            bar.showOnlyWhenDamaged = !IsElite;
            bar.fillColor = IsElite ? eliteColor : new Color(0.9f, 0.2f, 0.2f);
        }

        // 벽 뒤 실루엣 (일반: 빨강 / 엘리트: 보라). 엘리트 크기·색 적용 뒤라 복제 렌더러에 섞이지 않음
        Color silhouetteColor = IsElite ? eliteColor : new Color(1f, 0.25f, 0.2f);
        silhouetteColor.a = 0.6f;
        SilhouetteRenderer.Attach(gameObject, silhouetteColor);
    }

    // 엘리트 외형: 크기 확대 + 보라색 (MaterialPropertyBlock이라 머티리얼 복사본이 생기지 않음)
    private void ApplyEliteVisuals()
    {
        transform.localScale *= eliteScaleMultiplier;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", eliteColor); // URP Lit
            block.SetColor("_Color", eliteColor);     // 기본 셰이더 호환
            rend.SetPropertyBlock(block);
        }
    }

    private void OnDestroy()
    {
        if (juHealth != null)
        {
            juHealth.OnDeath -= OnEnemyDeath;
            juHealth.OnDamaged -= OnEnemyDamaged;
        }
    }

    private void Update()
    {
        if (isDead || playerTransform == null) return;

        // 경로 재계산은 일정 간격마다, 또는 플레이어가 크게 움직였을 때만 (매 프레임 재계산 비용 절감)
        Vector3 playerPos = playerTransform.position;
        if (Time.time >= nextRepathTime || (playerPos - lastPathTarget).sqrMagnitude > repathMoveThreshold * repathMoveThreshold)
        {
            agent.SetDestination(playerPos);
            lastPathTarget = playerPos;
            // 적마다 갱신 시점이 겹치지 않도록 약간의 무작위 간격
            nextRepathTime = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
        }

        // 수평 거리로 판정 (기준점 높이가 다른 플레이어·크기가 커진 엘리트에서도 정확하게)
        Vector3 toPlayer = playerPos - transform.position;
        toPlayer.y = 0f;
        float distanceToPlayer = toPlayer.magnitude;
        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
    }

    // ⭐ 플레이어 근접 사거리 진입 시 데미지 전달 연산
    private void AttackPlayer()
    {
        if (Time.time < lastAttackTime + attackCooldown) return;

        if (playerHealth == null && playerTransform != null)
        {
            playerHealth = playerTransform.GetComponent<JUHealth>();
        }

        if (playerHealth != null && !playerHealth.IsDead)
        {
            lastAttackTime = Time.time;
            playerHealth.DoDamage(baseAttackDamage);
        }
    }

    private void OnEnemyDamaged(IHealth.DamageResultInfo resultInfo)
    {
        // 피격 반응 훅 (매 피격 로그는 성능 문제로 제거)
    }

    private void OnEnemyDeath()
    {
        if (isDead) return;
        isDead = true;
        DeathFrame = Time.frameCount;
        OnAnyEnemyDeath?.Invoke(this);

        if (ItemDropManager.Instance != null)
        {
            int currentR = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
            ItemDropManager.Instance.TryDropItem(transform.position, currentR, IsElite);
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnEnemyKilled(IsElite);
        }

        if (agent.enabled)
        {
            agent.isStopped = true;
        }

        if (deathVFX != null)
        {
            Instantiate(deathVFX, transform.position, Quaternion.identity);
        }

        Destroy(gameObject, 0.1f);
    }
}