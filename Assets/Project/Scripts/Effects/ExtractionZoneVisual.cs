using UnityEngine;

// 탈출 구역 연출 (코드, 에셋 없이) — ExtractionZone.Awake가 Attach
//   바닥 링(구역 경계, 바닥에 눕힘) + 진행 호(12시부터 시계 방향으로 진행률만큼, 블룸) + 떠오르는 빛 입자 + 성공 순간 빛기둥
//   플레이어가 안에 있으면 밝은 초록·입자 많이, 밖이면 흐린 청록. 판정(트리거 콜라이더)은 그대로, 기존 구체 표시는 끔
//   구역 오브젝트 배율과 무관하게 따로 둔 루트가 매 프레임 구역 위치를 따라감 (라운드마다 재배치 대응)
//   링·입자는 게임 시간, 빛기둥·밝기 전환은 실제 시간 (탈출 성공 슬로모션 중에도 또렷하게)
public class ExtractionZoneVisual : MonoBehaviour
{
    private const int RingSegments = 96;
    private const int ArcSegments = 96;

    public static Color insideColor = new Color(0.35f, 1f, 0.5f, 1f);
    public static Color outsideColor = new Color(0.45f, 0.85f, 0.8f, 0.55f);
    public static float ringWidth = 0.18f;
    public static float arcWidth = 0.45f;
    public static float arcInset = 0.6f;       // 진행 호를 링보다 안쪽으로 (m)
    public static float beamHeight = 30f;
    public static float beamTime = 1.6f;

    private static Material ringMaterial;
    private static Material arcMaterial;
    private static Material moteMaterial;
    private static Material beamMaterial;

    private ExtractionZone zone;
    private SphereCollider trigger;
    private Transform root;
    private LineRenderer ring;
    private LineRenderer track;
    private LineRenderer arc;
    private ParticleSystem motes;
    private LineRenderer beam;
    private LineRenderer beamCore;

    private float radius;
    private float insideBlend;    // 0 밖 → 1 안 (부드럽게)
    private float beamStartTime = -1f;
    private bool wasComplete;

    public static void Attach(ExtractionZone zone)
    {
        if (zone.GetComponent<ExtractionZoneVisual>() != null) return;
        zone.gameObject.AddComponent<ExtractionZoneVisual>();
    }

    private void Awake()
    {
        zone = GetComponent<ExtractionZone>();
        trigger = GetComponent<SphereCollider>();
        EnsureMaterials();

        // 기존 표시(구체 등) 끄기 — 판정 콜라이더는 그대로
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        root = new GameObject("ExtractionZoneVisual").transform;
        ring = CreateFlatLine("Ring", ringMaterial, true, RingSegments);
        track = CreateFlatLine("ProgressTrack", ringMaterial, true, ArcSegments);
        arc = CreateFlatLine("ProgressArc", arcMaterial, false, 2);
        motes = CreateMotes();
        beam = CreateBeamLine("Beam", beamMaterial);
        beamCore = CreateBeamLine("BeamCore", beamMaterial);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void LateUpdate()
    {
        if (zone == null) return;
        Vector3 center = transform.position;
        center.y = 0f;
        root.position = center;

        float worldRadius = trigger != null ? trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 10f;
        if (!Mathf.Approximately(worldRadius, radius))
        {
            radius = worldRadius;
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.radius = radius * 0.95f;
        }

        float target = zone.IsPlayerInside ? 1f : 0f;
        insideBlend = Mathf.MoveTowards(insideBlend, target, Time.unscaledDeltaTime * 4f);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * (zone.IsPlayerInside ? 6f : 2.5f));
        Color color = Color.Lerp(outsideColor, insideColor, insideBlend);

        // 바닥 링
        Color ringColor = color;
        ringColor.a *= pulse;
        ring.startColor = ring.endColor = ringColor;
        ring.widthMultiplier = ringWidth * (1f + 0.4f * insideBlend);
        SetCircle(ring, center, radius, RingSegments);

        // 진행 호: 바탕 고리(흐림) + 진행률만큼 채움
        float arcRadius = Mathf.Max(0.5f, radius - arcInset);
        Color trackColor = color;
        trackColor.a *= 0.18f;
        track.startColor = track.endColor = trackColor;
        track.widthMultiplier = arcWidth;
        SetCircle(track, center, arcRadius, ArcSegments);

        float progress = zone.ProgressPercent;
        arc.enabled = progress > 0.001f;
        if (arc.enabled)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(ArcSegments * progress) + 1);
            arc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                // 12시(+Z)에서 시작해 위에서 보아 시계 방향
                float angle = (90f - 360f * progress * i / (count - 1)) * Mathf.Deg2Rad;
                arc.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * arcRadius, 0.04f, Mathf.Sin(angle) * arcRadius));
            }
            Color arcColor = color;
            arcColor.a = Mathf.Lerp(0.6f, 1f, insideBlend);
            arc.startColor = arc.endColor = arcColor;
            arc.widthMultiplier = arcWidth;
        }

        // 떠오르는 입자: 안에 있으면 많이
        ParticleSystem.EmissionModule emission = motes.emission;
        emission.rateOverTime = Mathf.Lerp(10f, 40f, insideBlend) * (radius / 10f);
        ParticleSystem.MainModule main = motes.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));

        // 성공 순간 빛기둥 (한 번)
        bool complete = progress >= 1f;
        if (complete && !wasComplete) beamStartTime = Time.unscaledTime;
        wasComplete = complete;
        UpdateBeam(center);
    }

    private void UpdateBeam(Vector3 center)
    {
        float t = beamStartTime < 0f ? 1f : (Time.unscaledTime - beamStartTime) / beamTime;
        bool show = t < 1f;
        beam.enabled = beamCore.enabled = show;
        if (!show) return;

        // 빠르게 펼쳐졌다(0.08) 천천히 가늘어짐
        float open = Mathf.Clamp01(t / 0.08f);
        float close = 1f - Mathf.Clamp01((t - 0.3f) / 0.7f);
        float width = radius * 0.45f * open * Mathf.Lerp(0.15f, 1f, close);
        float alpha = Mathf.Clamp01(close * 1.2f);

        Vector3 bottom = center + Vector3.up * 0.05f;
        Vector3 top = center + Vector3.up * beamHeight;
        beam.SetPosition(0, bottom);
        beam.SetPosition(1, top);
        beamCore.SetPosition(0, bottom);
        beamCore.SetPosition(1, top);

        Color outer = insideColor;
        outer.a = 0.7f * alpha;
        beam.startColor = beam.endColor = outer;
        beam.widthMultiplier = width;
        Color core = Color.Lerp(insideColor, Color.white, 0.7f);
        core.a = alpha;
        beamCore.startColor = beamCore.endColor = core;
        beamCore.widthMultiplier = width * 0.3f;
    }

    private static void SetCircle(LineRenderer line, Vector3 center, float r, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r));
        }
    }

    #region 생성

    // 바닥에 눕힌 선 (TransformZ 정렬 + 아래를 보는 회전 → 카메라 각도와 무관하게 납작)
    private LineRenderer CreateFlatLine(string name, Material material, bool loop, int count)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = count;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = loop ? 0 : 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        return line;
    }

    private LineRenderer CreateBeamLine(string name, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        line.enabled = false;
        return line;
    }

    private ParticleSystem CreateMotes()
    {
        GameObject go = new GameObject("Motes");
        go.transform.SetParent(root, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 원판을 바닥에, 방출은 위로
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.maxParticles = 200;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 9.5f;
        shape.radiusThickness = 1f;

        // 원판 방출 방향이 원판 평면이라 속도는 위쪽으로 따로
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSpeed = 0f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.8f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = moteMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    // 재질: Sprites/Default + _Color HDR 배율 → 블룸
    private static void EnsureMaterials()
    {
        if (ringMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        ringMaterial = CreateMaterial(shader, "ExtractionRing", 1.6f, null);
        arcMaterial = CreateMaterial(shader, "ExtractionArc", 2.6f, null);
        moteMaterial = CreateMaterial(shader, "ExtractionMote", 2.5f, CreateDotTexture());
        beamMaterial = CreateMaterial(shader, "ExtractionBeam", 3f, CreateBeamTexture());
    }

    private static Material CreateMaterial(Shader shader, string name, float intensity, Texture2D texture)
    {
        Material mat = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
        if (texture != null) mat.mainTexture = texture;
        mat.color = new Color(intensity, intensity, intensity, 1f);
        return mat;
    }

    private static Texture2D CreateDotTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ExtractionMote", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 빛기둥: u = 길이(아래 → 위로 옅어짐), v = 폭(가장자리 부드럽게)
    private static Texture2D CreateBeamTexture()
    {
        const int w = 64, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "ExtractionBeam", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            float across = Mathf.Clamp01(1f - v);
            across = across * across * (3f - 2f * across);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float along = Mathf.Lerp(1f, 0f, u * u);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(across * along * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
