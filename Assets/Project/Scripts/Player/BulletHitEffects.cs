using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS.ArmorSystem;
using JUTPS.PhysicsScripts;
using JUTPS.WeaponSystem;

// 적중 효과 특수 모듈: 넉백·착탄 폭발·처형 (PlayerStatAndWeaponSync가 무기별 런타임 탄 템플릿에 붙임, 값이 0이면 그 효과는 꺼짐)
// 적중 경로 2가지를 모두 받음: JU 탄의 일반 충돌(OnCollisionEnter) / BulletChain이 가로챈 관통·도탄 적중(OnEnemyHit 직접 호출)
// 가로챈 적중은 물리 충돌이 없으므로 중복 처리되지 않음
//   넉백: 탄 방향과 무관하게 항상 플레이어 반대 방향 (도탄으로 옆·플레이어 쪽으로 밀리는 것 방지), 엘리트는 30%
//   착탄 폭발: 확률로 맞은 적 주변 적에게 탄 피해 x 비율 (맞은 적·플레이어 제외, 폭발 피해는 다시 폭발·처형을 일으키지 않음)
//   처형: 이번 피해 후 남은 체력이 최대 체력 x 기준 이하면 즉사 (엘리트는 기준 절반)
//         JU 피해와 이 컴포넌트의 충돌 처리 순서가 정해져 있지 않아, 같은 프레임 LateUpdate에서 실제 체력으로 판정
[RequireComponent(typeof(Bullet))]
public class BulletHitEffects : MonoBehaviour
{
    public float knockbackDistance;          // 적중당 밀어내는 거리 (m)
    public float eliteKnockbackFactor = 0.3f;
    public float explosionChance;            // 0.2 = 20%
    public float explosionDamageRatio = 0.5f;
    public float explosionRadius = 2.5f;
    public float executeThreshold;           // 0.15 = 남은 체력 15% 이하 즉사
    public float eliteExecuteFactor = 0.5f;

    // 착탄 폭발 이펙트 (수류탄 폭발 이펙트 재사용, PlayerStatAndWeaponSync가 지정)
    public static GameObject ExplosionVFX;
    private const float VfxScale = 0.3f;
    private const float VfxDuration = 2f;

    private static readonly Collider[] overlapBuffer = new Collider[64];

    private Bullet bullet;
    private bool collisionHandled;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();
    }

    // JU 탄의 일반 적중 (관통·도탄 횟수를 다 쓴 마지막 적중 포함)
    private void OnCollisionEnter(Collision col)
    {
        if (collisionHandled || bullet == null) return;

        // JU와 같은 예외: 부위 판정이 있는 캐릭터의 몸통 캡슐(9번)은 무시하고 탄이 계속 날아감
        if (col.gameObject.layer == 9 && col.gameObject.GetComponentInChildren<DamageableBodyPart>() != null) return;

        EnemyBase enemy = col.gameObject.GetComponentInParent<EnemyBase>();
        if (enemy == null) return;

        collisionHandled = true;
        Vector3 point = col.contactCount > 0 ? col.GetContact(0).point : transform.position;
        OnEnemyHit(enemy, point, bullet.BulletDamage);
    }

    public void OnEnemyHit(EnemyBase enemy, Vector3 hitPoint, float damage)
    {
        if (enemy == null) return;
        JUHealth health = enemy.GetComponent<JUHealth>();
        if (health != null && health.IsDead) return;

        GameObject owner = bullet != null ? bullet.GetOwner() : null;

        if (knockbackDistance > 0f && owner != null)
        {
            Vector3 away = enemy.transform.position - owner.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
            {
                float distance = knockbackDistance * (enemy.IsElite ? eliteKnockbackFactor : 1f);
                EnemyKnockback.Apply(enemy, away.normalized, distance);
            }
        }

        if (explosionChance > 0f && Random.value < explosionChance)
        {
            Explode(hitPoint, damage * explosionDamageRatio, enemy, owner);
        }

        if (executeThreshold > 0f && health != null)
        {
            float threshold = executeThreshold * (enemy.IsElite ? eliteExecuteFactor : 1f);
            ExecuteChecker.Queue(health, threshold, hitPoint, owner);
        }
    }

    private void Explode(Vector3 center, float damage, EnemyBase hitEnemy, GameObject owner)
    {
        SpawnExplosionVFX(center);

        HashSet<JUHealth> damaged = new HashSet<JUHealth>();
        int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = overlapBuffer[i].GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy == hitEnemy) continue;

            JUHealth health = enemy.GetComponent<JUHealth>();
            if (health == null || health.IsDead || !damaged.Add(health)) continue;

            Vector3 direction = enemy.transform.position - center;
            direction.y = 0f;
            health.DoDamage(new IHealth.DamageInfo
            {
                Damage = damage,
                HitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                HitPosition = enemy.transform.position,
                HitOriginPosition = center,
                DamageOwner = owner
            });
        }
    }

    // GrenadeProjectile 폭발 이펙트와 같은 처리 (JU Explosion 피해·물리 힘은 끄고 이펙트만, 크기 축소)
    private static void SpawnExplosionVFX(Vector3 center)
    {
        if (ExplosionVFX == null) return;

        GameObject vfx = Instantiate(ExplosionVFX, center, Quaternion.identity);
        foreach (Explosion juExplosion in vfx.GetComponentsInChildren<Explosion>(true))
        {
            juExplosion.enabled = false;
        }

        vfx.transform.localScale *= VfxScale;
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (Light light in vfx.GetComponentsInChildren<Light>(true))
        {
            light.range *= VfxScale;
        }
        Destroy(vfx, VfxDuration);
    }

    // 처형 판정 대기열: 같은 프레임의 모든 충돌·피해 처리가 끝난 뒤(LateUpdate) 실제 체력으로 판정
    private class ExecuteChecker : MonoBehaviour
    {
        private struct Pending
        {
            public JUHealth health;
            public float threshold;
            public Vector3 point;
            public GameObject owner;
        }

        private static ExecuteChecker instance;
        private readonly List<Pending> pending = new List<Pending>();

        public static void Queue(JUHealth health, float threshold, Vector3 point, GameObject owner)
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ExecuteChecker");
                instance = obj.AddComponent<ExecuteChecker>();
            }
            instance.pending.Add(new Pending { health = health, threshold = threshold, point = point, owner = owner });
        }

        private void LateUpdate()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (p.health == null || p.health.IsDead) continue;
                if (p.health.Health > p.health.MaxHealth * p.threshold) continue;

                // 방어력만큼 깎이므로 더해서 확실히 0으로
                p.health.DoDamage(new IHealth.DamageInfo
                {
                    Damage = p.health.Health + p.health.Protection + 1f,
                    HitDirection = Vector3.forward,
                    HitPosition = p.point,
                    HitOriginPosition = p.point,
                    DamageOwner = p.owner
                });
            }
            pending.Clear();
        }
    }
}
