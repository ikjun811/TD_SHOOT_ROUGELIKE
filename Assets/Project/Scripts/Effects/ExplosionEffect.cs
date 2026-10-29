using UnityEngine;

// 폭발 코드 연출 (Vefects 빌보드 대체)
//   SpawnBomber: 봄버 자폭·처치 폭발 — 빛나는 불덩이 + 퍼지는 불길 + 튀는 불티 + 피어오르는 연기 + 바닥 충격 고리 + 빛 번쩍임
//   SpawnGroundRing: 바닥 충격 고리만 (공용 폭발에 덧붙이기용)
//   크기는 폭발 반경 기준 (봄버 3m = 1배), 게임 시간 기준, 끝나면 스스로 제거
//   재질 Sprites/Default + 코드 텍스처, 불·불티는 재질 _Color를 HDR 배율로 둬 블룸으로 빛남
public class ExplosionEffect : MonoBehaviour
{
    private const int RingSegments = 48;

    public static float fireIntensity = 2.5f;
    public static float sparkIntensity = 3f;
    public static Color ringColor = new Color(1f, 0.75f, 0.45f, 0.9f);
    public static Color flashColor = new Color(1f, 0.6f, 0.3f);

    private static Material lineMaterial;
    private static Material fireMaterial;
    private static Material smokeMaterial;
    private static Material sparkMaterial;

    private LineRenderer ring;
    private Light flash;
    private float ringRadius;
    private float ringTime;
    private Color ringTint;
    private float lightIntensity;
    private float lightTime;
    private float lifetime;
    private float elapsed;

    // 봄버 폭발 (radius = 실제 피해 반경, 바닥 예고 원과 같은 크기로 고리가 퍼짐)
    public static void SpawnBomber(Vector3 center, float radius)
    {
        EnsureMaterials();
        float s = Mathf.Max(0.3f, radius / 3f);
        ExplosionEffect effect = Create("BomberExplosion", center, 2.4f);
        effect.SetupRing(radius, 0.3f, ringColor);
        effect.SetupLight(radius * 2.5f, 6f, 0.3f);

        Vector3 core = Vector3.up * (0.8f * s);
        // 불덩이: 크게 번쩍였다 금방 꺼짐
        effect.AddBurst("Core", core, fireMaterial, 7, ParticleSystemShapeType.Sphere, 0.3f * s,
            new ParticleSystem.MinMaxCurve(0.2f, 0.35f), new ParticleSystem.MinMaxCurve(0.5f * s, 1.5f * s),
            new ParticleSystem.MinMaxCurve(1.3f * s, 2f * s), 0f, 0f, FireGradient(true), new Vector2(0.6f, 1.5f), false);
        // 불길: 바깥으로 퍼지며 느려지고 붉어짐
        effect.AddBurst("Fire", core, fireMaterial, 16, ParticleSystemShapeType.Sphere, 0.4f * s,
            new ParticleSystem.MinMaxCurve(0.35f, 0.65f), new ParticleSystem.MinMaxCurve(4f * s, 8f * s),
            new ParticleSystem.MinMaxCurve(0.6f * s, 1.1f * s), -0.2f, 0.25f, FireGradient(false), new Vector2(0.7f, 1.8f), false);
        // 불티: 멀리 튀어 떨어짐
        effect.AddBurst("Sparks", core, sparkMaterial, 22, ParticleSystemShapeType.Hemisphere, 0.3f * s,
            new ParticleSystem.MinMaxCurve(0.4f, 0.8f), new ParticleSystem.MinMaxCurve(7f * s, 13f * s),
            new ParticleSystem.MinMaxCurve(0.04f * s, 0.09f * s), 1.2f, 0f, SparkGradient(), Vector2.one, true);
        // 연기: 조금 늦게, 천천히 피어오르며 커짐
        effect.AddBurst("Smoke", core, smokeMaterial, 10, ParticleSystemShapeType.Sphere, 0.6f * s,
            new ParticleSystem.MinMaxCurve(1.1f, 1.7f), new ParticleSystem.MinMaxCurve(0.6f * s, 1.6f * s),
            new ParticleSystem.MinMaxCurve(1f * s, 1.6f * s), -0.12f, 0.15f, SmokeGradient(), new Vector2(0.7f, 2.4f), false, 0.08f);
    }

    // 바닥 충격 고리만 (공용 폭발 등)
    public static void SpawnGroundRing(Vector3 center, float radius, Color color, float time = 0.3f)
    {
        EnsureMaterials();
        ExplosionEffect effect = Create("ExplosionRing", center, time + 0.1f);
        effect.SetupRing(radius, time, color);
    }

    private static ExplosionEffect Create(string name, Vector3 center, float lifetime)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center;
        ExplosionEffect effect = go.AddComponent<ExplosionEffect>();
        effect.lifetime = lifetime;
        return effect;
    }

    private void SetupRing(float radius, float time, Color color)
    {
        GameObject ringObj = new GameObject("Ring");
        ringObj.transform.SetParent(transform, false);
        ring = ringObj.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = RingSegments;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.sharedMaterial = lineMaterial;
        ringRadius = radius;
        ringTime = Mathf.Max(0.05f, time);
        ringTint = color;
        UpdateRing();
    }

    private void SetupLight(float range, float intensity, float time)
    {
        GameObject lightObj = new GameObject("Flash");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1.2f;
        flash = lightObj.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = range;
        flash.color = flashColor;
        flash.intensity = intensity;
        flash.shadows = LightShadows.None;
        lightIntensity = intensity;
        lightTime = time;
    }

    // sizeCurve = (시작 배율, 끝 배율)
    private void AddBurst(string name, Vector3 localPosition, Material material, int count, ParticleSystemShapeType shapeType, float shapeRadius,
                          ParticleSystem.MinMaxCurve life, ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size,
                          float gravity, float dampen, Gradient colorOverLifetime, Vector2 sizeCurve, bool stretch, float delay = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        if (shapeType == ParticleSystemShapeType.Hemisphere) go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 위쪽 반구
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startDelay = delay;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.maxParticles = count + 4;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = shapeType;
        shape.radius = shapeRadius;

        if (dampen > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.5f;
            limit.dampen = dampen;
        }

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(colorOverLifetime);

        if (sizeCurve != Vector2.one)
        {
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, sizeCurve.x), new Keyframe(1f, sizeCurve.y)));
        }

        if (!stretch)
        {
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
        }

        ps.Play();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
        UpdateRing();
        if (flash != null)
        {
            float lt = Mathf.Clamp01(elapsed / lightTime);
            flash.intensity = lightIntensity * (1f - lt) * (1f - lt);
            flash.enabled = lt < 1f;
        }
    }

    // 고리: 빠르게 퍼지며 얇아지고 사라짐 (ease-out)
    private void UpdateRing()
    {
        if (ring == null) return;
        float t = Mathf.Clamp01(elapsed / ringTime);
        ring.enabled = t < 1f;
        if (!ring.enabled) return;
        float ease = 1f - (1f - t) * (1f - t);
        float radius = Mathf.Lerp(ringRadius * 0.15f, ringRadius, ease);
        ring.widthMultiplier = Mathf.Lerp(0.3f, 0.04f, ease) * Mathf.Clamp(ringRadius / 3f, 0.6f, 1.6f);
        Color c = ringTint;
        c.a *= 1f - t;
        ring.startColor = ring.endColor = c;
        Vector3 center = transform.position;
        center.y = 0.06f;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }

    #region 색·재질

    // 흰노랑 → 주황 → 짙은 빨강 → 검붉게 사라짐 (core는 더 짧고 밝게)
    private static Gradient FireGradient(bool core)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), core ? 0.35f : 0.25f),
                new GradientColorKey(new Color(0.7f, 0.12f, 0.03f), 0.65f),
                new GradientColorKey(new Color(0.15f, 0.05f, 0.03f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Gradient SparkGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Gradient SmokeGradient()
    {
        Gradient g = new Gradient();
        Color smoke = new Color(0.08f, 0.075f, 0.07f);
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.25f, 0.12f, 0.06f), 0f), new GradientColorKey(smoke, 0.3f), new GradientColorKey(smoke, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static void EnsureMaterials()
    {
        if (lineMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader) { name = "ExplosionRing", hideFlags = HideFlags.DontSave };
        Texture2D puff = CreatePuffTexture();
        fireMaterial = new Material(shader) { name = "ExplosionFire", mainTexture = puff, hideFlags = HideFlags.DontSave };
        fireMaterial.color = new Color(fireIntensity, fireIntensity, fireIntensity, 1f);
        smokeMaterial = new Material(shader) { name = "ExplosionSmoke", mainTexture = puff, hideFlags = HideFlags.DontSave };
        sparkMaterial = new Material(shader) { name = "ExplosionSpark", mainTexture = CreateDotTexture(), hideFlags = HideFlags.DontSave };
        sparkMaterial.color = new Color(sparkIntensity, sparkIntensity, sparkIntensity, 1f);
    }

    // 뭉게 덩어리: 원형 감쇠 + 각도별 굴곡 + 노이즈
    private static Texture2D CreatePuffTexture()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ExplosionPuff", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float angle = Mathf.Atan2(p.y, p.x);
                float edge = 0.8f + 0.1f * Mathf.Sin(angle * 4f + 1.3f) + 0.07f * Mathf.Sin(angle * 7f + 0.4f);
                float d = p.magnitude / (size * 0.5f) / edge;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                a *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.12f + 3f, y * 0.12f + 7f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateDotTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ExplosionSpark", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - d) * 2f) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
