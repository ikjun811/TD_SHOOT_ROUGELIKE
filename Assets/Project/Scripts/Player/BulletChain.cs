using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS.WeaponSystem;

// 관통·도탄 (특수 모듈, 탄 궤적 칸 — 둘은 동시 배치 불가라 한 컴포넌트가 모드로 구분)
// PlayerStatAndWeaponSync가 무기별 런타임 탄 템플릿에 붙임
// JU 탄은 물리 충돌 순간 삭제되므로, 매 물리 프레임 이번 이동 경로를 미리 검사(SphereCast)해 적을 먼저 처리:
//   피해를 JU 탄과 같은 방식(IHealth.DoDamage)으로 직접 주고, 그 적과의 물리 충돌을 꺼서 같은 탄이 계속 날아감 (탄 재생성 없음)
//   관통: 방향 그대로 직진 (뒤에 적이 없어도 항상 관통) / 도탄: 맞은 지점에서 다음 대상으로 방향 전환 + 대상 유도
// 맞은 적은 목록에 넣고 충돌을 끄므로 다시 맞거나 끼지 않음. 횟수를 다 쓰면 가로채지 않고 마지막 적중은 JU가 처리
// 이 탄은 JU 고정밀 충돌 검사(Linecast)를 끔: 지나온 적을 다시 감지해 탄을 되돌리기 때문 (벽은 리지드바디 Continuous가 처리)
// 매 프레임 검사는 미리 만든 배열 사용(NonAlloc)으로 GC 없음
[RequireComponent(typeof(Bullet))]
public class BulletChain : MonoBehaviour
{
    public enum ChainMode { Pierce, Ricochet }

    public ChainMode mode = ChainMode.Ricochet;
    public int remaining = 1;             // 남은 관통·도탄 횟수
    public float damageFactor = 0.7f;     // 이어갈 때마다 피해 배율
    public float searchRadius = 12f;      // 도탄 대상 탐색 반경
    public float castRadius = 0.15f;      // 경로 검사 반지름 (탄 반지름 0.05보다 약간 크게)
    public float homingTimeout = 0.5f;    // 이 시간 안에 대상을 못 맞히면 유도 중지
    public float pierceTracerLength = 15f; // 관통 궤적 선 길이 (벽에 막히면 그 앞까지)
    public string wallLayerName = "Walls";

    private static readonly Color RicochetColor = new Color(1f, 0.6f, 0.15f, 1f);
    private static readonly Color PierceColor = new Color(0.35f, 0.85f, 1f, 1f);
    private static readonly RaycastHit[] castBuffer = new RaycastHit[16];
    private static readonly Collider[] overlapBuffer = new Collider[64];

    private Bullet bullet;
    private Rigidbody rb;
    private Collider selfCollider;
    private BulletHitEffects hitEffects;
    private GameObject owner;
    private Vector3 startPosition;
    private int wallMask;
    private readonly HashSet<EnemyBase> hitEnemies = new HashSet<EnemyBase>();

    private EnemyBase homingTarget;
    private float homingStartTime;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();
        rb = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
        hitEffects = GetComponent<BulletHitEffects>();
    }

    private void Start()
    {
        bullet.HightPrecisionCollisionDetection = false;
        owner = bullet.GetOwner();
        startPosition = transform.position;
        wallMask = LayerMask.GetMask(wallLayerName);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        UpdateHoming();
        if (remaining > 0) CheckPath();
    }

    // 도탄 유도: 매 물리 프레임 대상 중심을 바로 향함 (회전 제한 없음 → 맴돌지 않음)
    private void UpdateHoming()
    {
        if (homingTarget == null) return;

        if (!IsAlive(homingTarget) || Time.time - homingStartTime > homingTimeout)
        {
            homingTarget = null;
            return;
        }

        Vector3 toTarget = GetTargetPoint(homingTarget, rb.position.y) - rb.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;
        SetDirection(toTarget.normalized);
    }

    // 이번 물리 프레임 이동 경로(약 3m)의 미적중 적을 가까운 순으로 처리 (벽·장애물보다 먼 적은 제외)
    // 관통은 한 프레임 안에 붙어 있는 적 여러 마리도 차례로 처리 (안 하면 두 번째 적과 실제 충돌해 탄이 삭제됨)
    private static readonly List<(float distance, EnemyBase enemy, Vector3 point)> pathEnemies = new List<(float, EnemyBase, Vector3)>();

    private void CheckPath()
    {
        Vector3 direction = transform.forward;
        float distance = bullet.BulletVelocity * Time.fixedDeltaTime + castRadius;
        int count = Physics.SphereCastNonAlloc(rb.position, castRadius, direction, castBuffer, distance, ~0, QueryTriggerInteraction.Ignore);

        pathEnemies.Clear();
        float blockDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            Collider col = hit.collider;
            if (col == null || col == selfCollider) continue;
            if (owner != null && col.transform.IsChildOf(owner.transform)) continue;
            if (col.GetComponent<Bullet>() != null) continue;

            EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null)
            {
                blockDistance = Mathf.Min(blockDistance, hit.distance);
                continue;
            }
            if (hitEnemies.Contains(enemy) || !IsAlive(enemy)) continue;

            // 겹친 상태에서 시작한 검사는 point가 0 → 탄 위치 사용
            Vector3 point = hit.distance > 0f ? hit.point : rb.position;
            point.y = rb.position.y;
            pathEnemies.Add((hit.distance, enemy, point));
        }

        if (pathEnemies.Count == 0) return;
        pathEnemies.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach (var entry in pathEnemies)
        {
            if (remaining <= 0 || entry.distance > blockDistance) break;
            if (hitEnemies.Contains(entry.enemy)) continue; // 같은 적의 다른 콜라이더

            int remainingBefore = remaining;
            bool continueStraight = Intercept(entry.enemy, entry.point);
            if (continueStraight) continue;

            // 도탄으로 방향이 바뀌었으면 새 방향의 이번 프레임 경로도 검사 (사이에 낀 적과 실제 충돌해 탄이 삭제되는 것 방지)
            // 재귀 깊이는 남은 횟수 이하. 공유 목록이 바뀌므로 바로 종료
            if (remaining < remainingBefore && remaining > 0) CheckPath();
            break;
        }
    }

    // 반환: 같은 방향으로 계속 진행하면 true (관통), 방향 전환·가로채지 않음이면 false
    private bool Intercept(EnemyBase enemy, Vector3 hitPoint)
    {
        EnemyBase next = null;
        if (mode == ChainMode.Ricochet)
        {
            next = FindRicochetTarget(hitPoint, enemy);
            if (next == null)
            {
                return false; // 가로채지 않음 → JU가 일반 적중 처리
            }
        }

        float damage = bullet.BulletDamage;
        ApplyDamage(enemy, hitPoint);
        if (hitEffects != null) hitEffects.OnEnemyHit(enemy, hitPoint, damage); // 넉백·착탄 폭발·처형
        hitEnemies.Add(enemy);
        IgnoreAll(enemy.gameObject);
        remaining--;
        bullet.BulletDamage *= damageFactor;

        if (mode == ChainMode.Pierce)
        {
            ChainTracer.Draw(hitPoint, GetPierceTracerEnd(hitPoint), PierceColor);
            return true;
        }

        // 도탄: 맞은 지점으로 옮겨 다음 대상 방향으로 전환, 유도 시작
        rb.position = hitPoint;
        transform.position = hitPoint;
        Vector3 targetPoint = GetTargetPoint(next, hitPoint.y);
        Vector3 toTarget = targetPoint - hitPoint;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.0001f) SetDirection(toTarget.normalized);
        homingTarget = next;
        homingStartTime = Time.time;

        ChainTracer.Draw(hitPoint, targetPoint, RicochetColor);
        return false;
    }

    // JU Bullet.OnCollisionEnter와 같은 방식의 피해 (피해 숫자는 IHealth 피해 이벤트로 자동 표시)
    private void ApplyDamage(EnemyBase enemy, Vector3 hitPoint)
    {
        IHealth health = enemy.GetComponent<IHealth>();
        if (health == null) return;

        CriticalBullet crit = GetComponent<CriticalBullet>();
        if (crit != null && crit.IsCritical)
        {
            DamageNumberManager.RegisterCriticalHit(enemy.GetComponent<JUHealth>(), bullet.BulletDamage);
        }

        health.DoDamage(new IHealth.DamageInfo
        {
            Damage = bullet.BulletDamage,
            HitDirection = -transform.forward,
            HitPosition = hitPoint,
            HitOriginPosition = startPosition,
            DamageOwner = owner
        });
    }

    // 도탄: 반경 안, 살아 있고, 아직 안 맞았고, 벽에 가리지 않은 가장 가까운 적 (방금 맞은 적 제외)
    private EnemyBase FindRicochetTarget(Vector3 from, EnemyBase exclude)
    {
        int count = Physics.OverlapSphereNonAlloc(from, searchRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        EnemyBase best = null;
        float bestSqr = searchRadius * searchRadius;

        for (int i = 0; i < count; i++)
        {
            EnemyBase candidate = overlapBuffer[i].GetComponentInParent<EnemyBase>();
            if (candidate == null || candidate == exclude || hitEnemies.Contains(candidate) || !IsAlive(candidate)) continue;

            Vector3 point = GetTargetPoint(candidate, from.y);
            Vector3 offset = point - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr >= bestSqr) continue;
            if (wallMask != 0 && Physics.Linecast(from, point, wallMask, QueryTriggerInteraction.Ignore)) continue;

            bestSqr = sqr;
            best = candidate;
        }
        return best;
    }

    private Vector3 GetPierceTracerEnd(Vector3 from)
    {
        Vector3 direction = transform.forward;
        float length = pierceTracerLength;
        if (wallMask != 0 && Physics.Raycast(from, direction, out RaycastHit hit, length, wallMask, QueryTriggerInteraction.Ignore))
        {
            length = hit.distance;
        }
        return from + direction * length;
    }

    private void SetDirection(Vector3 direction)
    {
        transform.rotation = Quaternion.LookRotation(direction);
        rb.rotation = transform.rotation;
        rb.linearVelocity = direction * bullet.BulletVelocity;
    }

    private void IgnoreAll(GameObject target)
    {
        if (selfCollider == null) return;
        foreach (Collider col in target.GetComponentsInChildren<Collider>())
        {
            if (col != null) Physics.IgnoreCollision(selfCollider, col);
        }
    }

    private static bool IsAlive(EnemyBase enemy)
    {
        if (enemy == null) return false;
        JUHealth health = enemy.GetComponent<JUHealth>();
        return health == null || !health.IsDead;
    }

    // 대상 콜라이더 중심 (높이는 탄 높이로 — 적 캡슐은 바닥~2m라 수평 탄이 맞음)
    private static Vector3 GetTargetPoint(EnemyBase enemy, float height)
    {
        Collider col = enemy.GetComponent<Collider>();
        Vector3 center = col != null ? col.bounds.center : enemy.transform.position;
        center.y = height;
        return center;
    }
}
