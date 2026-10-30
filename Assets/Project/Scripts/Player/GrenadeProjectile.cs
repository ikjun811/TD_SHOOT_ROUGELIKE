using System.Collections.Generic;
using UnityEngine;
using JU;
using JUTPS.PhysicsScripts;

// 수류탄 투사체 (GrenadeSkill이 런타임에 생성)
// 던진 뒤 fuseTime 후 폭발, 반경 내 적에게 거리 비례 피해 (플레이어 제외)
// 폭발 이펙트 프리팹에 JU Explosion이 있으면 꺼서 이펙트·사운드만 사용 (JU 피해·물리 힘 제외)
[RequireComponent(typeof(Rigidbody))]
public class GrenadeProjectile : MonoBehaviour
{
    [HideInInspector] public float fuseTime = 1.2f;
    [HideInInspector] public float radius = 4f;
    [HideInInspector] public float centerDamage = 80f;
    [HideInInspector] public float edgeDamage = 40f;
    [HideInInspector] public GameObject explosionVFX;
    [HideInInspector] public float vfxScale = 0.5f;     // 이펙트 크기 배율
    [HideInInspector] public float vfxSpeed = 1.5f;     // 이펙트 재생 속도 배율 (클수록 빨리 끝남)
    [HideInInspector] public float vfxDuration = 2f;    // 이 시간 뒤 이펙트 제거 (초)
    [HideInInspector] public GameObject owner;
    [HideInInspector] public float clusterDamageRatio;  // 집속탄 모듈: 소형 폭탄 피해 비율 (0 = 없음, 소형 폭탄은 다시 집속 안 함)
    [HideInInspector] public bool playSound = true;     // 집속탄 소형 폭탄은 첫 개만 소리 (4개가 연달아 울려 소리가 과했음)

    private const int ClusterCount = 4;
    private const float ClusterSpread = 3f;      // 소형 폭탄 흩어지는 반경 (m)
    private const float ClusterFuse = 0.4f;
    private const float ClusterRadius = 2f;
    private const float ClusterVfxScale = 0.5f;  // 원래 이펙트 크기 대비

    private float timer;
    private bool exploded;

    private void Awake()
    {
        RoundCleanup.Register(gameObject); // 다음 라운드까지 남지 않게 (탈출 직전 던진 수류탄·소형 폭탄)
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (!exploded && timer >= fuseTime) Explode();
    }

    private void Explode()
    {
        exploded = true;
        Vector3 center = transform.position;
        CameraShake.AddExplosion(center, vfxScale);
        ExplosionEffect.SpawnGroundRing(center, radius, ExplosionEffect.ringColor); // 피해 반경 바닥 고리

        // VfxLibrary 폭발 칸이 있으면 그것 (수류탄 vfxScale 0.5 = 칸 배율 그대로), 없으면 JU 폭발
        bool libraryVfx = VfxSpawner.Play(VfxLibrary.Current.explosion, center, vfxScale / 0.5f);
        if (libraryVfx && playSound) AudioManager.PlaySfx(AudioManager.Library.grenadeExplosion, center); // JU 폭발 프리팹은 자체 소리
        if (!libraryVfx && explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, center, Quaternion.identity);
            RoundCleanup.Register(vfx);

            // JU Explosion은 Start에서 피해·물리 힘을 주므로 Start 전에 꺼 둠 (이펙트·사운드·흔들림은 유지)
            foreach (Explosion juExplosion in vfx.GetComponentsInChildren<Explosion>(true))
            {
                juExplosion.enabled = false;
            }

            // 크기·재생 속도 조절 (파티클이 부모 스케일을 따르도록 Hierarchy 모드로)
            vfx.transform.localScale *= vfxScale;
            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpeed *= vfxSpeed;
            }
            foreach (Light light in vfx.GetComponentsInChildren<Light>(true))
            {
                light.range *= vfxScale;
            }
            Destroy(vfx, vfxDuration);
        }

        // 같은 대상의 여러 콜라이더에 중복 피해가 들어가지 않도록 체력 컴포넌트 단위로 1회만
        ExplosionDamageScope.Begin(); // 탄 적중과 구분 (단일 권총 수류탄 엔진)
        HashSet<JUHealth> damaged = new HashSet<JUHealth>();
        foreach (Collider col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            JUHealth health = col.GetComponentInParent<JUHealth>();
            if (health == null || health.IsDead || damaged.Contains(health)) continue;
            if (health.CompareTag("Player")) continue;

            damaged.Add(health);

            float distance = Vector3.Distance(center, health.transform.position);
            float damage = Mathf.Lerp(centerDamage, edgeDamage, Mathf.Clamp01(distance / radius));

            health.DoDamage(new IHealth.DamageInfo
            {
                Damage = damage,
                HitPosition = col.bounds.ClosestPoint(center),
                HitDirection = (health.transform.position - center).normalized,
                HitOriginPosition = center,
                DamageOwner = owner
            });
        }

        ExplosionDamageScope.End();

        if (clusterDamageRatio > 0f) SpawnClusterBomblets(center);

        Destroy(gameObject);
    }

    // 집속탄: 주변에 소형 폭탄을 흩뿌림 (같은 클래스를 작게 재사용, 물리 없이 그 자리에서 짧은 지연 후 폭발)
    private void SpawnClusterBomblets(Vector3 center)
    {
        for (int i = 0; i < ClusterCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * ClusterSpread;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);

            GameObject bomblet = new GameObject("Cluster Bomblet");
            bomblet.transform.position = position;
            GrenadeProjectile projectile = bomblet.AddComponent<GrenadeProjectile>(); // Rigidbody는 RequireComponent로 자동 추가
            bomblet.GetComponent<Rigidbody>().isKinematic = true;

            projectile.fuseTime = ClusterFuse + Random.Range(0f, 0.15f); // 동시에 터지지 않게 약간씩 엇갈림
            projectile.radius = ClusterRadius;
            projectile.centerDamage = centerDamage * clusterDamageRatio;
            projectile.edgeDamage = edgeDamage * clusterDamageRatio;
            projectile.explosionVFX = explosionVFX;
            projectile.vfxScale = vfxScale * ClusterVfxScale;
            projectile.vfxSpeed = vfxSpeed;
            projectile.vfxDuration = vfxDuration;
            projectile.owner = owner;
            projectile.clusterDamageRatio = 0f;
            projectile.playSound = i == 0;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
