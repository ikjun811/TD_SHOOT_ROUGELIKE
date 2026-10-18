using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS.ArmorSystem;
using JUTPS.WeaponSystem;

// 관통·도탄 (특수 모듈, 탄 궤적 칸 — 둘은 동시 배치 불가라 한 컴포넌트가 모드로 구분)
// PlayerStatAndWeaponSync가 무기별 런타임 탄 템플릿에 붙임
// JU 탄은 무엇에 맞든 즉시 사라지므로, 적에게 맞는 순간 맞은 자리에서 탄을 복제해 이어 쏨
//   관통: 같은 방향으로 직진 / 도탄: 반경 안의 가장 가까운, 아직 맞지 않은 적에게
// 복제 탄은 방금 맞은 적·플레이어와 충돌하지 않고, 한 연쇄에서 같은 적은 다시 맞지 않음
// 복제 탄은 템플릿처럼 다른 컴포넌트(치명타 등)도 그대로 가짐 → 치명타는 복제 탄마다 다시 판정
[RequireComponent(typeof(Bullet))]
public class BulletChain : MonoBehaviour
{
    public enum ChainMode { Pierce, Ricochet }

    public ChainMode mode = ChainMode.Ricochet;
    public int remaining = 1;            // 남은 이어가기 횟수
    public float damageFactor = 0.7f;    // 이어갈 때마다 피해 배율
    public float baseDamage;             // 치명타 적용 전 피해 (복제 시 치명타 중복 적용 방지)
    public float searchRadius = 8f;      // 도탄 대상 탐색 반경
    public float lifeTime = 3f;          // 복제 탄 수명

    private Bullet bullet;
    private HashSet<EnemyBase> hitEnemies; // 연쇄 전체가 공유 (직렬화 안 됨 → 복제 시 직접 넘김)
    private bool chained;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();
    }

    private void OnCollisionEnter(Collision col)
    {
        if (chained || remaining <= 0 || bullet == null) return;

        // JU와 같은 예외: 부위 판정이 있는 캐릭터의 몸통 캡슐(9번)은 무시하고 탄이 계속 날아감
        if (col.gameObject.layer == 9 && col.gameObject.GetComponentInChildren<DamageableBodyPart>() != null) return;

        EnemyBase enemy = col.gameObject.GetComponentInParent<EnemyBase>();
        if (enemy == null) return;

        chained = true;
        if (hitEnemies == null) hitEnemies = new HashSet<EnemyBase>();
        hitEnemies.Add(enemy);

        Vector3 hitPoint = col.contactCount > 0 ? col.GetContact(0).point : transform.position;
        hitPoint.y = transform.position.y; // 수평 유지 (조준이 가슴 높이 수평)

        Vector3 direction;
        if (mode == ChainMode.Pierce)
        {
            direction = transform.forward;
        }
        else
        {
            EnemyBase next = FindNextTarget(hitPoint);
            if (next == null) return;
            direction = next.transform.position - hitPoint;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Spawn(hitPoint, direction.normalized, enemy);
    }

    private EnemyBase FindNextTarget(Vector3 from)
    {
        EnemyBase best = null;
        float bestSqr = searchRadius * searchRadius;

        foreach (EnemyBase candidate in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (candidate == null || hitEnemies.Contains(candidate)) continue;

            JUHealth health = candidate.GetComponent<JUHealth>();
            if (health != null && health.IsDead) continue;

            Vector3 offset = candidate.transform.position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }
        return best;
    }

    private void Spawn(Vector3 position, Vector3 direction, EnemyBase hitEnemy)
    {
        GameObject clone = Instantiate(gameObject, position, Quaternion.LookRotation(direction));
        clone.name = gameObject.name;

        Bullet cloneBullet = clone.GetComponent<Bullet>();
        GameObject owner = bullet.GetOwner();
        cloneBullet.SetOwner(owner);
        cloneBullet.FinalPoint = Vector3.zero;          // 카메라 조준점 대신 물리 이동으로 직진
        cloneBullet.MovementType = Bullet.BulletMovementType.Physics;

        // 피해: 기준 피해 x 배율, 치명타는 복제 탄의 판정 결과로 다시 적용
        float nextBase = baseDamage * damageFactor;
        cloneBullet.BulletDamage = nextBase;
        CriticalBullet crit = clone.GetComponent<CriticalBullet>();
        if (crit != null && crit.IsCritical) cloneBullet.BulletDamage *= crit.critMultiplier;

        BulletChain cloneChain = clone.GetComponent<BulletChain>();
        cloneChain.remaining = remaining - 1;
        cloneChain.baseDamage = nextBase;
        cloneChain.hitEnemies = hitEnemies;

        Collider cloneCollider = clone.GetComponent<Collider>();
        if (cloneCollider != null)
        {
            IgnoreAll(cloneCollider, hitEnemy.gameObject);
            if (owner != null && owner != gameObject) IgnoreAll(cloneCollider, owner);
        }

        Destroy(clone, lifeTime);
    }

    private static void IgnoreAll(Collider self, GameObject target)
    {
        foreach (Collider col in target.GetComponentsInChildren<Collider>())
        {
            if (col != null) Physics.IgnoreCollision(self, col);
        }
    }
}
