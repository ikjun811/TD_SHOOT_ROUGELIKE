using UnityEngine;

// 보급 상자 열림 연출 (C4) — SupplyCrate.Open이 Spawn (색 = 나온 무기 레어도)
//   바닥에 퍼지는 고리 + 위로 튀는 불꽃 + 짧은 빛 번쩍임, 게임 시간 기준, 끝나면 스스로 제거
//   코드 생성 (이펙트 에셋을 구하면 교체), 재질은 Sprites/Default + 코드 텍스처
public class CrateOpenEffect : MonoBehaviour
{
    private const int RingSegments = 48;
    private const float Duration = 1.2f;

    public float ringTime = 0.45f;
    public float ringStartRadius = 0.3f;
    public float ringEndRadius = 3f;
    public float lightTime = 0.3f;
    public float lightIntensity = 4f;

    private static Material lineMaterial;
    private static Material sparkMaterial;
    private static Texture2D sparkTexture;

    private LineRenderer ring;
    private Light flash;
    private Color color;
    private float elapsed;

    public static void Spawn(Vector3 position, Color color)
    {
        GameObject go = new GameObject("CrateOpenEffect");
        go.transform.position = position;
        CrateOpenEffect effect = go.AddComponent<CrateOpenEffect>();
        effect.color = color;
        effect.Build();
        CameraShake.AddTrauma(0.25f, position);
        AudioManager.PlaySfx(AudioManager.Library.crateOpened, position);
    }

    private void Build()
    {
        EnsureMaterials();

        // 바닥 고리
        GameObject ringObj = new GameObject("Ring");
        ringObj.transform.SetParent(transform, false);
        ring = ringObj.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = RingSegments;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.sharedMaterial = lineMaterial;

        // 빛 번쩍임
        GameObject lightObj = new GameObject("Flash");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1f;
        flash = lightObj.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 6f;
        flash.color = Color.Lerp(color, Color.white, 0.3f);
        flash.intensity = lightIntensity;
        flash.shadows = LightShadows.None;

        BuildSparks();
        UpdateEffect();
    }

    // 불꽃: 위쪽 원뿔로 튀어 떨어짐, 흰색 → 레어도 색
    private void BuildSparks()
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.4f;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
        main.gravityModifier = 1.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.Lerp(color, Color.white, 0.4f));
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 위쪽으로

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch; // 속도 방향으로 늘어난 불꽃
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 1.5f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= Duration)
        {
            Destroy(gameObject);
            return;
        }
        UpdateEffect();
    }

    private void UpdateEffect()
    {
        // 고리: 빠르게 퍼지며 얇아지고 사라짐 (ease-out)
        float t = Mathf.Clamp01(elapsed / ringTime);
        float ease = 1f - (1f - t) * (1f - t);
        float radius = Mathf.Lerp(ringStartRadius, ringEndRadius, ease);
        ring.enabled = t < 1f;
        ring.widthMultiplier = Mathf.Lerp(0.18f, 0.03f, ease);
        Color c = color;
        c.a = 1f - t;
        ring.startColor = ring.endColor = c;
        Vector3 center = transform.position;
        center.y = 0.06f;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }

        // 빛: 바로 최대 → 사라짐
        float lt = Mathf.Clamp01(elapsed / lightTime);
        flash.intensity = lightIntensity * (1f - lt) * (1f - lt);
        flash.enabled = lt < 1f;
    }

    private static void EnsureMaterials()
    {
        if (lineMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader) { name = "CrateOpenLine", hideFlags = HideFlags.DontSave };

        const int size = 32;
        sparkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CrateSpark", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                byte a = (byte)(Mathf.Clamp01((1f - d) * 2f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        sparkTexture.SetPixels32(pixels);
        sparkTexture.Apply();
        sparkMaterial = new Material(shader) { name = "CrateSpark", mainTexture = sparkTexture, hideFlags = HideFlags.DontSave };
    }
}
