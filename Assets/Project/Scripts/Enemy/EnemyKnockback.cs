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

    // duration: 밀리는 시간 (0 이하 = 기본 0.15초). 속도 상한이 있어 먼 거리는 시간을 늘려야 함 (위기 반격 2m = 0.25초)
    public static void Apply(EnemyBase enemy, Vector3 direction, float distance, float duration = 0f)
    {
        if (enemy == null || distance <= 0f) return;
        distance *= 1f - enemy.KnockbackResistance; // 종류별 넉백 저항 (브루트 등)
        if (distance <= 0f) return;
        if (duration <= 0f) duration = Duration;

        EnemyKnockback knockback = enemy.GetComponent<EnemyKnockback>();
        if (knockback == null) knockback = enemy.gameObject.AddComponent<EnemyKnockback>();
        knockback.Add(direction * (distance / duration), duration);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<JUHealth>();
    }

    private void Add(Vector3 push, float duration)
    {
        velocity = Vector3.ClampMagnitude(velocity + push, MaxSpeed);
        timer = Mathf.Max(timer, duration);
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
