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

    [Header("Effects")]
    [SerializeField] private GameObject deathVFX;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private JUHealth juHealth;
    private JUHealth playerHealth;
    private float lastAttackTime = -10f;
    private bool isDead = false;

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
            currentHp *= StageDifficultyManager.Instance.GetEnemyHpMultiplier();
            currentDmg *= StageDifficultyManager.Instance.GetEnemyDamageMultiplier();
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

        agent.SetDestination(playerTransform.position);

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
            Debug.Log($"EnemyBase: Attacked player for {baseAttackDamage} damage. Remaining Player HP: {playerHealth.Health}");
        }
    }

    private void OnEnemyDamaged(IHealth.DamageResultInfo resultInfo)
    {
        if (isDead) return;
        Debug.Log($"EnemyBase: Damaged {gameObject.name}. Remaining Health: {juHealth.Health} / {juHealth.MaxHealth}");
    }

    private void OnEnemyDeath()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"EnemyBase: Enemy {gameObject.name} killed.");

        if (ItemDropManager.Instance != null)
        {
            int currentR = 1;
            if (WaveManager.Instance != null)
            {
                currentR = (int)(WaveManager.Instance.GetType().GetField("currentRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(WaveManager.Instance) ?? 1);
            }

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