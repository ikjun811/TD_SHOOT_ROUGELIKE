using System.Collections.Generic;
using UnityEngine;
using JU;

// 적 탄 (슈터) — 물리 없이 직접 이동, 느리고 눈에 잘 보이게 (대시·이동으로 피하는 탄)
//   높이 고정 수평 비행, 매 프레임 이동 구간을 Walls 레이어로 스피어캐스트(벽·장애물에 막힘), 플레이어는 수평 거리로 판정
//   콜라이더가 없어 플레이어 탄·폭발·JU 판정과 무관
//   외형: 발광 구체(HitFlash 셰이더 HDR 색 → 블룸) + 꼬리(TrailRenderer)
//   라운드 종료 시 WaveManager.DespawnAllActiveEnemies가 ClearAll로 함께 제거
public class EnemyProjectile : MonoBehaviour
{
    private const string ShaderName = "TDShoot/HitFlash";
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly List<EnemyProjectile> active = new List<EnemyProjectile>();
    private static Material orbMaterial;
    private static Material trailMaterial;
    private static int wallMask = -1;

    public static readonly Color OrbColor = new Color(3f, 0.9f, 0.25f, 1f);
    public static readonly Color TrailColor = new Color(1f, 0.55f, 0.15f, 0.8f);

    private Vector3 direction;
    private float speed;
    private float damage;
    private float hitRadius;
    private float lifeEnd;
    private Transform player;
    private JUHealth playerHealth;

    public static EnemyProjectile Spawn(Vector3 position, Vector3 direction, float speed, float damage, float lifetime,
                                        float hitRadius, float size, Transform player, JUHealth playerHealth)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyProjectile";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * size;

        Renderer rend = go.GetComponent<Renderer>();
        Material orb = GetOrbMaterial();
        if (orb != null) rend.sharedMaterial = orb;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = size * 0.8f;
        trail.endWidth = 0f;
        trail.startColor = TrailColor;
        trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material trailMat = GetTrailMaterial();
        if (trailMat != null) trail.sharedMaterial = trailMat;

        EnemyProjectile projectile = go.AddComponent<EnemyProjectile>();
        direction.y = 0f;
        projectile.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        projectile.speed = speed;
        projectile.damage = damage;
        projectile.hitRadius = hitRadius;
        projectile.lifeEnd = Time.time + lifetime;
        projectile.player = player;
        projectile.playerHealth = playerHealth;
        return projectile;
    }

    public static void ClearAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] != null) Destroy(active[i].gameObject);
        }
        active.Clear();
    }

    private static Material GetOrbMaterial()
    {
        if (orbMaterial != null) return orbMaterial;
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return null;
        orbMaterial = new Material(shader) { name = "EnemyProjectile", hideFlags = HideFlags.DontSave };
        orbMaterial.SetColor(FlashColorId, OrbColor);
        return orbMaterial;
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null) return trailMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        trailMaterial = new Material(shader) { name = "EnemyProjectileTrail", hideFlags = HideFlags.DontSave };
        return trailMaterial;
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void Update()
    {
        if (Time.time >= lifeEnd)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 end = start + direction * step;

        // 플레이어 (수평 거리: 이동 구간과 플레이어 중심 사이 최단 거리)
        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            Vector3 p = player.position;
            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);
            Vector2 c = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
            if ((a + ab * t - c).sqrMagnitude <= hitRadius * hitRadius)
            {
                playerHealth.DoDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        // 벽·장애물
        if (wallMask == -1) wallMask = LayerMask.GetMask("Walls");
        if (wallMask != 0 && Physics.SphereCast(start, transform.localScale.x * 0.5f, direction, out _, step, wallMask, QueryTriggerInteraction.Ignore))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = end;
    }
}
