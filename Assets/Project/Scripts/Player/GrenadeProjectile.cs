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

    private float timer;
    private bool exploded;

    private void Update()
    {
        timer += Time.deltaTime;
        if (!exploded && timer >= fuseTime) Explode();
    }

    private void Explode()
    {
        exploded = true;
        Vector3 center = transform.position;

        if (explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, center, Quaternion.identity);

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

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
