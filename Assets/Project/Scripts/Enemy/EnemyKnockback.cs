using UnityEngine;
using UnityEngine.AI;
using JU;

// 넉백 (BulletHitEffects가 맞은 적에게 자동 부착)
// 적은 리지드바디 없이 NavMeshAgent로 움직이므로 agent.Move로 짧게 밀어냄 → NavMesh 위로만 움직여 벽·맵 밖으로 밀리지 않음
// 연사·산탄 적중이 겹쳐도 밀리는 속도는 maxSpeed까지만 (과도한 밀어내기 방지)
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyKnockback : MonoBehaviour
{
    private const float Duration = 0.15f;  // 한 번 밀리는 시간 (초)
    private const float MaxSpeed = 8f;     // 밀리는 속도 상한 (m/s)

    private NavMeshAgent agent;
    private JUHealth health;
    private Vector3 velocity;
    private float timer;

    public static void Apply(EnemyBase enemy, Vector3 direction, float distance)
    {
        if (enemy == null || distance <= 0f) return;

        EnemyKnockback knockback = enemy.GetComponent<EnemyKnockback>();
        if (knockback == null) knockback = enemy.gameObject.AddComponent<EnemyKnockback>();
        knockback.Add(direction * (distance / Duration));
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<JUHealth>();
    }

    private void Add(Vector3 push)
    {
        velocity = Vector3.ClampMagnitude(velocity + push, MaxSpeed);
        timer = Duration;
    }

    private void Update()
    {
        if (timer <= 0f) return;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh || (health != null && health.IsDead))
        {
            timer = 0f;
            velocity = Vector3.zero;
            return;
        }

        agent.Move(velocity * Time.deltaTime);
        timer -= Time.deltaTime;
        if (timer <= 0f) velocity = Vector3.zero;
    }
}
