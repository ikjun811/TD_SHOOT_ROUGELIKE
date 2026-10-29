using UnityEngine;

// 차저 벽 충돌 (코드 파티클, Vefects 빌보드 대체) — EnemyCharger가 돌진 중 벽·NavMesh 끝에 막혀 기절할 때 Spawn
//   바닥 충격 고리 + 벽 반대쪽 반원으로 튀는 파편·불꽃 + 먼지(ChargerDustEffect) + 카메라 흔들림
//   게임 시간 기준, 끝나면 스스로 제거. 재질은 Sprites/Default + 코드 텍스처
public class ChargerImpactEffect : MonoBehaviour
{
    private const int RingSegments = 40;
    private const float Duration = 1.3f;

    public static float ringTime = 0.35f;
    public static float ringEndRadius = 2.2f;
    public static float shakeTrauma = 0.3f;
    public static Color ringColor = new Color(0.85f, 0.8f, 0.7f, 0.8f);
    public static Color debrisColor = new Color(0.36f, 0.36f, 0.38f, 1f);
    public static Color sparkColor = new Color(1f, 0.65f, 0.3f, 1f);

    private static Material lineMaterial;
    private static Material debrisMaterial;
    private static Material sparkMaterial;

    private LineRenderer ring;
    private float elapsed;

    // position = 부딪힌 지점, outward = 벽에서 튀어나오는 방향 (벽 법선 또는 돌진 반대쪽)
    public static void Spawn(Vector3 position, Vector3 outward)
    {
        EnsureMaterials();
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f) outward = Vector3.back;
        outward.Normalize();

        Vector3 ground = new Vector3(position.x, 0f, position.z) + outward * 0.3f;
        GameObject go = new GameObject("ChargerImpactEffect");
        go.transform.position = ground;
        ChargerImpactEffect effect = go.AddComponent<ChargerImpactEffect>();
        effect.Build(outward);

        ChargerDustEffect.Spawn(ground, outward, true);
        CameraShake.AddTrauma(shakeTrauma, ground);
        AudioManager.PlaySfx(AudioManager.Library.chargerImpact, ground);
    }

    private void Build(Vector3 outward)
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

        // 파편: 벽 바깥쪽 + 위로, 무거워 금방 떨어짐
        Quaternion spray = Quaternion.LookRotation(outward + Vector3.up * 0.6f, Vector3.up);
        BuildBurst("Debris", spray, debrisMaterial, 10, 60f, new ParticleSystem.MinMaxCurve(3f, 7f),
                   new ParticleSystem.MinMaxCurve(0.07f, 0.16f), new ParticleSystem.MinMaxCurve(0.6f, 1f), 2.5f,
                   debrisColor, Color.Lerp(debrisColor, Color.white, 0.25f), false);
        // 불꽃: 짧고 빠름, 속도 방향으로 늘어짐
        BuildBurst("Sparks", spray, sparkMaterial, 8, 50f, new ParticleSystem.MinMaxCurve(5f, 10f),
                   new ParticleSystem.MinMaxCurve(0.04f, 0.08f), new ParticleSystem.MinMaxCurve(0.15f, 0.3f), 1f,
                   Color.white, sparkColor, true);

        UpdateRing();
    }

    private void BuildBurst(string name, Quaternion rotation, Material material, int count, float angle,
                            ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, ParticleSystem.MinMaxCurve lifetime,
                            float gravity, Color colorA, Color colorB, bool stretch)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.6f;
        go.transform.rotation = rotation;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = count + 4;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = 0.4f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        if (!stretch)
        {
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f); // 굴러가는 파편
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
        if (elapsed >= Duration)
        {
            Destroy(gameObject);
            return;
        }
        UpdateRing();
    }

    // 고리: 빠르게 퍼지며 얇아지고 사라짐 (ease-out)
    private void UpdateRing()
    {
        float t = Mathf.Clamp01(elapsed / ringTime);
        ring.enabled = t < 1f;
        if (!ring.enabled) return;
        float ease = 1f - (1f - t) * (1f - t);
        float radius = Mathf.Lerp(0.3f, ringEndRadius, ease);
        ring.widthMultiplier = Mathf.Lerp(0.22f, 0.03f, ease);
        Color c = ringColor;
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

    private static void EnsureMaterials()
    {
        if (lineMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader) { name = "ChargerImpactLine", hideFlags = HideFlags.DontSave };
        debrisMaterial = new Material(shader) { name = "ChargerDebris", mainTexture = CreateTexture(true), hideFlags = HideFlags.DontSave };
        sparkMaterial = new Material(shader) { name = "ChargerSpark", mainTexture = CreateTexture(false), hideFlags = HideFlags.DontSave };
    }

    // chunk = 모서리 깎인 각진 조각 (파편), 아니면 둥근 빛점 (불꽃)
    private static Texture2D CreateTexture(bool chunk)
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = chunk ? "ChargerDebris" : "ChargerSpark", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                float a;
                if (chunk)
                {
                    // 불규칙 사각형: 한쪽 모서리를 비스듬히 깎음
                    bool inside = Mathf.Abs(px) < 0.75f && Mathf.Abs(py) < 0.6f && px + py < 0.9f && px - py > -1.1f;
                    a = inside ? 1f : 0f;
                }
                else
                {
                    float d = Mathf.Sqrt(px * px + py * py);
                    a = Mathf.Clamp01((1f - d) * 2f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
