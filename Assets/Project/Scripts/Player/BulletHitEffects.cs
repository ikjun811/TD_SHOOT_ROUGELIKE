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
//   마지막 탄 추가 피해: 탄창 마지막 탄의 피해 증가 (생성 순간 판정)
//   탄환 환수: 탄으로 적 처치 시 쏜 무기 탄창에 비율만큼 환수 (처형과 같은 LateUpdate 판정, PlayerStatAndWeaponSync.RefundAmmo)
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
    public float lastRoundBonus;             // 0.5 = 탄창 마지막 탄 피해 +50%
    public float refundRatio;                // 0.1 = 처치 시 탄창의 10% 환수 (올림, 최소 1발)
    public Weapon sourceWeapon;              // 이 템플릿을 쓰는 JU 무기 (마지막 탄 판정·환수 대상, 쌍권총은 손마다 따로)
    public bool autoThrowGrenade;            // 자동 투척 모듈: 적중마다 GrenadeSkill.TryAutoThrow (확률·내부 쿨은 GrenadeSkill)

    // 착탄 폭발 이펙트 (수류탄 폭발 이펙트 재사용, PlayerStatAndWeaponSync가 지정)
    public static GameObject ExplosionVFX;
    private const float VfxScale = 0.25f; // 착탄 폭발 이펙트 크기 (사용자: 0.5는 조명 작업 후 과해 보여 절반으로)
    private const float VfxDuration = 2f;

    private static readonly Collider[] overlapBuffer = new Collider[64];

    private Bullet bullet;
    private bool collisionHandled;
    private bool holdingReload;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();

        // 마지막 탄: JU는 탄을 생성한 뒤 탄창을 줄이므로(Weapon.Shot), 생성 순간(Awake) 남은 탄이 1이면 마지막 탄
        // 산탄총은 마지막 사격의 산탄 전부가 해당
        bool isLastRound = sourceWeapon != null && sourceWeapon.BulletsAmounts == 1;
        if (lastRoundBonus > 0f && isLastRound)
        {
            bullet.BulletDamage *= 1f + lastRoundBonus;
        }

        // 탄환 환수: 막탄이 결과를 낼 때까지 자동 장전 보류 (처치 환수 후 장전 없이 계속 사격)
        if (refundRatio > 0f && isLastRound && PlayerStatAndWeaponSync.Instance != null)
        {
            PlayerStatAndWeaponSync.Instance.BeginReloadHold(sourceWeapon);
            holdingReload = true;
        }
    }

    private void OnDestroy()
    {
        if (holdingReload && PlayerStatAndWeaponSync.Instance != null)
        {
            PlayerStatAndWeaponSync.Instance.EndReloadHoldBullet(sourceWeapon);
        }
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
        GameObject owner = bullet != null ? bullet.GetOwner() : null;

        if (health != null && health.IsDead)
        {
            // 일반 충돌은 JU 피해와 처리 순서가 정해져 있지 않음 → 이 프레임에 막 죽었으면 이번 적중의 처치로 보고 환수만 대기
            if (refundRatio > 0f && enemy.DeathFrame == Time.frameCount)
            {
                PostHitChecker.Queue(health, 0f, hitPoint, owner, sourceWeapon, refundRatio);
            }
            return;
        }

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

        if (autoThrowGrenade && owner != null)
        {
            GrenadeSkill grenade = owner.GetComponent<GrenadeSkill>();
            if (grenade != null) grenade.TryAutoThrow(enemy);
        }

        if (explosionChance > 0f && Random.value < explosionChance)
        {
            ExplodeAt(hitPoint, explosionRadius, damage * explosionDamageRatio, enemy, owner, VfxScale, true); // 착탄 폭발 = 예전 JU 수류탄 이펙트 (사용자)
        }

        if ((executeThreshold > 0f || refundRatio > 0f) && health != null)
        {
            float threshold = executeThreshold * (enemy.IsElite ? eliteExecuteFactor : 1f);
            PostHitChecker.Queue(health, threshold, hitPoint, owner, refundRatio > 0f ? sourceWeapon : null, refundRatio);
        }
    }

    // 반경 안 적에게 피해 + 폭발 이펙트 (착탄 폭발, 재장전 시 폭발(WeaponModuleRuntime)이 사용). exclude는 피해 제외, 플레이어는 대상 아님
    // classicVfx = VfxLibrary 폭발 칸 대신 JU 수류탄 폭발 이펙트 (착탄 폭발)
    public static void ExplodeAt(Vector3 center, float radius, float damage, EnemyBase exclude, GameObject owner, float vfxScale, bool classicVfx = false)
    {
        SpawnExplosionVFX(center, vfxScale, classicVfx);
        ExplosionEffect.SpawnGroundRing(center, radius, ExplosionEffect.ringColor); // 피해 반경 바닥 고리

        ExplosionDamageScope.Begin(); // 탄 적중과 구분 (단일 권총 수류탄 엔진)
        HashSet<JUHealth> damaged = new HashSet<JUHealth>();
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        EnemyBase hitEnemy = exclude;
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
        ExplosionDamageScope.End();
    }

    // GrenadeProjectile 폭발 이펙트와 같은 처리 (JU Explosion 피해·물리 힘은 끄고 이펙트만, 크기 축소)
    // 적 폭발(봄버)도 사용 — 모듈이 없어 아직 연결 전이면 씬의 GrenadeSkill에서 가져옴
    public static void SpawnExplosionVFX(Vector3 center, float scale, bool classicVfx = false)
    {
        CameraShake.AddExplosion(center, scale);
        // VfxLibrary 폭발 칸이 있으면 그것 (배율 0.5 = 수류탄 기준 → 칸 배율 그대로), 없거나 classicVfx면 아래 JU 폭발
        if (!classicVfx && VfxSpawner.Play(VfxLibrary.Current.explosion, center, scale / 0.5f))
        {
            AudioManager.PlaySfx(AudioManager.Library.grenadeExplosion, center); // JU 폭발 프리팹(아래)은 자체 소리
            return;
        }
        if (ExplosionVFX == null)
        {
            GrenadeSkill grenade = FindAnyObjectByType<GrenadeSkill>();
            if (grenade != null) ExplosionVFX = grenade.ExplosionVFX;
        }
        if (ExplosionVFX == null) return;

        GameObject vfx = Instantiate(ExplosionVFX, center, Quaternion.identity);
        foreach (Explosion juExplosion in vfx.GetComponentsInChildren<Explosion>(true))
        {
            juExplosion.enabled = false;
        }

        vfx.transform.localScale *= scale;
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (Light light in vfx.GetComponentsInChildren<Light>(true))
        {
            light.range *= scale;
        }
        Destroy(vfx, VfxDuration);
    }

    // 적중 후 판정 대기열: 같은 프레임의 모든 충돌·피해 처리가 끝난 뒤(LateUpdate) 실제 체력으로 판정
    //   1) 처형: 남은 체력이 기준 이하면 즉사  2) 탄환 환수: 적중 시 살아 있던 적이 죽었으면 쏜 무기에 탄 환수 (같은 적은 1번만)
    private class PostHitChecker : MonoBehaviour
    {
        private struct Pending
        {
            public JUHealth health;
            public float threshold;
            public Vector3 point;
            public GameObject owner;
            public Weapon refundWeapon;
            public float refundRatio;
        }

        private static PostHitChecker instance;
        private readonly List<Pending> pending = new List<Pending>();
        private readonly HashSet<JUHealth> refunded = new HashSet<JUHealth>();

        public static void Queue(JUHealth health, float threshold, Vector3 point, GameObject owner, Weapon refundWeapon, float refundRatio)
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("PostHitChecker");
                instance = obj.AddComponent<PostHitChecker>();
            }
            instance.pending.Add(new Pending
            {
                health = health, threshold = threshold, point = point, owner = owner,
                refundWeapon = refundWeapon, refundRatio = refundRatio
            });
        }

        private void LateUpdate()
        {
            if (pending.Count == 0) return;

            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (p.threshold <= 0f || p.health == null || p.health.IsDead) continue;
                if (p.health.Health > p.health.MaxHealth * p.threshold) continue;

                AudioManager.PlaySfx(AudioManager.Library.execute, p.point);
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

            // 처치 판정 (처형으로 죽은 경우 포함, 폭발 피해만으로 죽은 적은 대기열에 없으므로 제외)
            refunded.Clear();
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (p.refundWeapon == null || p.health == null || !p.health.IsDead) continue;
                if (!refunded.Add(p.health)) continue;

                if (PlayerStatAndWeaponSync.Instance != null)
                {
                    PlayerStatAndWeaponSync.Instance.RefundAmmo(p.refundWeapon, p.refundRatio);
                }
            }
            pending.Clear();
        }
    }
}
