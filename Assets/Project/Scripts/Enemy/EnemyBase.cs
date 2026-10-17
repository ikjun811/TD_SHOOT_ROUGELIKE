using UnityEngine;
using UnityEngine.AI;
using JU;

// 적 개체 기본 AI, 피격, 플레이어 공격 및 사망 처리 클래스
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(JUHealth))]
public class EnemyBase : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private float baseMoveSpeed = 3.5f;
    [SerializeField] private float baseAttackDamage = 10f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f; // 공격 쿨타임 (1.2초)

    [Header("Pathfinding")]
    [SerializeField] private float repathInterval = 0.2f;      // 경로 재계산 간격 (초)
    [SerializeField] private float repathMoveThreshold = 1.5f; // 플레이어가 이만큼 움직이면 간격과 무관하게 즉시 재계산 (m)

    [Header("Effects")]
    [SerializeField] private GameObject deathVFX;

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
        float currentHp = 50f;
        float currentDmg = baseAttackDamage;
        float currentSpd = baseMoveSpeed;

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

        // 머리 위 체력바 자동 부착 (피격 후에만 표시, 프리팹에 이미 있으면 그 설정 사용)
        if (GetComponent<WorldHealthBar>() == null)
        {
            WorldHealthBar bar = gameObject.AddComponent<WorldHealthBar>();
            bar.showOnlyWhenDamaged = true;
            bar.fillColor = new Color(0.9f, 0.2f, 0.2f);
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

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
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

        if (ItemDropManager.Instance != null)
        {
            int currentR = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
            ItemDropManager.Instance.TryDropItem(transform.position, currentR);
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnEnemyKilled();
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