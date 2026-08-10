using UnityEngine;
using UnityEngine.AI;
using JU; 

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(JUHealth))] 
public class EnemyBase : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Effects")]
    [SerializeField] private GameObject deathVFX;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private JUHealth juHealth;
    private bool isDead = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        juHealth = GetComponent<JUHealth>();
    }

    private void Start()
    {
        agent.speed = moveSpeed;

        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // JUHealth 이벤트 코드 직접 연결
        if (juHealth != null)
        {
            juHealth.OnDeath += OnEnemyDeath;
            juHealth.OnDamaged += OnEnemyDamaged;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 메모리 해제
        if (juHealth != null)
        {
            juHealth.OnDeath -= OnEnemyDeath;
            juHealth.OnDamaged -= OnEnemyDamaged;
        }
    }

    private void Update()
    {
        if (isDead || playerTransform == null) return;

        // 플레이어 추적
        agent.SetDestination(playerTransform.position);

        // 공격 거리 확인
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
    }

    // JUHealth에서 피격될 때 자동 실행
    private void OnEnemyDamaged(IHealth.DamageResultInfo resultInfo)
    {
        if (isDead) return;
        Debug.Log($"[적 피격!] {gameObject.name} 남은 체력: {juHealth.Health} / {juHealth.MaxHealth}");
    }

    private void AttackPlayer()
    {
        // TODO: 플레이어 공격 로직 
    }

    // JUHealth에서 체력이 0이 될 때 자동 실행
    private void OnEnemyDeath()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[적 사망!] {gameObject.name} 처치됨!");

        // ⭐ 추가된 한 줄: 웨이브 매니저에 처치 알림 전달
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
