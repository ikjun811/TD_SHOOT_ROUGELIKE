using System.Collections.Generic;
using UnityEngine;

// 탈출 성공 텔레포트 연출 (사용자 요청 2026-10-29: 빛이 나다가 빛기둥과 함께 어딘가로 텔레포트) — ExtractionZone.ExtractionSuccessSequence가 Begin
//   실제 시간 기준 (탈출 성공 슬로모션 중에도 또렷하게, 파티클도 useUnscaledTime)
//   0 ~ chargeTime 충전: 플레이어 몸 빛이 점점 강해짐 + 발밑 링이 좁혀 듦 + 빛 입자가 모여 소용돌이치며 위로
//   chargeTime 텔레포트: 하늘에서 빛기둥이 내리꽂힘 + 섬광 + 입자 폭발 + 카메라 흔들림 + 효과음, 플레이어 모습(렌더러·월드 UI)·손전등 숨김
//   이후 빛기둥이 가늘어지며 사라짐. 플레이어 모습은 RestorePlayer(암전 중 ExtractionZone이 호출)로 복구
public class ExtractionTeleportEffect : MonoBehaviour
{
    public static float chargeTime = 1f;
    public static float beamTime = 0.6f;
    public static float beamHeight = 40f;
    public static float beamWidth = 1.6f;
    public static Color color = new Color(0.4f, 1f, 0.55f, 1f);

    private static ExtractionTeleportEffect current;
    private static Material glowMaterial;
    private static Material beamMaterial;

    private Transform player;
    private float startTime;
    private bool teleported;
    private Light glow;
    private LineRenderer ring;
    private LineRenderer beam;
    private LineRenderer beamCore;
    private ParticleSystem motes;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    public static void Begin(Transform player)
    {
        if (player == null) return;
        if (current != null) Destroy(current.gameObject);
        EnsureMaterials();
        GameObject go = new GameObject("ExtractionTeleportEffect");
        current = go.AddComponent<ExtractionTeleportEffect>();
        current.player = player;
        current.startTime = Time.unscaledTime;
        current.Build();
    }

    // 숨긴 플레이어 모습 복구 (암전 중 호출, 연출 오브젝트도 정리)
    public static void RestorePlayer()
    {
        if (current == null) return;
        current.ShowPlayer();
        Destroy(current.gameObject);
        current = null;
    }

    private void OnDestroy()
    {
        ShowPlayer();
        if (current == this) current = null;
    }

    private void Build()
    {
        transform.position = Ground(player.position);

        GameObject lightObj = new GameObject("Glow");
        lightObj.transform.SetParent(transform, false);
        glow = lightObj.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 5f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;

        ring = CreateLine("Ring", glowMaterial, true, 48);
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ring.alignment = LineAlignment.TransformZ;
        beam = CreateLine("Beam", beamMaterial, false, 2);
        beamCore = CreateLine("BeamCore", beamMaterial, false, 2);
        beam.textureMode = beamCore.textureMode = LineTextureMode.Stretch;
        beam.enabled = beamCore.enabled = false;

        motes = CreateMotes();
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            Destroy(gameObject);
            return;
        }
        float t = Time.unscaledTime - startTime;
        Vector3 ground = Ground(player.position);
        transform.position = ground;

        if (!teleported)
        {
            // 충전: 빛 강해짐, 링 좁혀 듦
            float c = Mathf.Clamp01(t / chargeTime);
            float ease = c * c;
            glow.transform.position = ground + Vector3.up * 1.1f;
            glow.intensity = Mathf.Lerp(0.5f, 7f, ease);
            SetRing(ground, Mathf.Lerp(2.4f, 0.35f, ease), Mathf.Lerp(0.05f, 0.18f, c), Mathf.Lerp(0.4f, 1f, c));
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.rateOverTime = Mathf.Lerp(30f, 110f, c);

            if (t >= chargeTime) Teleport(ground);
            return;
        }

        // 텔레포트 후: 빛기둥이 가늘어지며 사라짐, 섬광 감쇠
        float b = Mathf.Clamp01((t - chargeTime) / beamTime);
        float open = Mathf.Clamp01((t - chargeTime) / 0.06f);
        float width = beamWidth * open * Mathf.Lerp(1f, 0.05f, b * b);
        float alpha = 1f - b;
        Vector3 bottom = ground + Vector3.up * 0.05f;
        Vector3 top = ground + Vector3.up * beamHeight;
        beam.SetPosition(0, bottom);
        beam.SetPosition(1, top);
        beamCore.SetPosition(0, bottom);
        beamCore.SetPosition(1, top);
        Color outer = color;
        outer.a = 0.8f * alpha;
        beam.startColor = beam.endColor = outer;
        beam.widthMultiplier = width;
        Color core = Color.Lerp(color, Color.white, 0.75f);
        core.a = alpha;
        beamCore.startColor = beamCore.endColor = core;
        beamCore.widthMultiplier = width * 0.3f;
        beam.enabled = beamCore.enabled = b < 1f;

        glow.transform.position = ground + Vector3.up * 2f;
        glow.intensity = 16f * (1f - b) * (1f - b);
        SetRing(ground, Mathf.Lerp(0.35f, 3.5f, b), Mathf.Lerp(0.25f, 0.02f, b), alpha);
    }

    private void Teleport(Vector3 ground)
    {
        teleported = true;
        glow.range = 9f;
        HidePlayer();

        // 입자: 모이던 것을 멈추고 위로 폭발
        ParticleSystem.EmissionModule emission = motes.emission;
        emission.rateOverTime = 0f;
        for (int i = 0; i < 70; i++)
        {
            Vector2 r = Random.insideUnitCircle * 0.6f;
            motes.Emit(new ParticleSystem.EmitParams
            {
                position = ground + new Vector3(r.x, Random.Range(0.2f, 1.8f), r.y),
                velocity = new Vector3(r.x * 2f, Random.Range(6f, 16f), r.y * 2f),
                startLifetime = Random.Range(0.5f, 1f),
                startSize = Random.Range(0.06f, 0.16f)
            }, 1);
        }

        CameraShake.AddTrauma(0.35f, ground);
        AudioManager.PlaySfx(AudioManager.Library.extractionSuccess);
    }

    private void HidePlayer()
    {
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hiddenRenderers.Add(r);
        }
        foreach (Canvas c in player.GetComponentsInChildren<Canvas>())
        {
            if (!c.enabled) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
        PlayerFlashlight.Suppressed = true;
    }

    private void ShowPlayer()
    {
        foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
        foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenRenderers.Clear();
        hiddenCanvases.Clear();
        PlayerFlashlight.Suppressed = false;
    }

    #region 생성

    private void SetRing(Vector3 center, float radius, float width, float alpha)
    {
        Color c = color;
        c.a = alpha;
        ring.startColor = ring.endColor = c;
        ring.widthMultiplier = width;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius));
        }
    }

    private LineRenderer CreateLine(string name, Material material, bool loop, int count)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = count;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        return line;
    }

    // 모이는 입자: 플레이어 둘레 원에서 안쪽·위로 소용돌이 (실제 시간)
    private ParticleSystem CreateMotes()
    {
        GameObject go = new GameObject("Motes");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
        main.maxParticles = 300;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.2f;
        shape.radiusThickness = 0.3f;

        // 안쪽으로 빨려 들며 위로, 돌면서
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(-2.6f, -1.6f);
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); // 공전 x·y·z도 같은 모드여야 함 (아니면 매 프레임 경고)
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);  // x·y·z는 같은 모드여야 함
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.5f, 3f); // 로컬 Z = 월드 위

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    private static Vector3 Ground(Vector3 p)
    {
        p.y = 0f;
        return p;
    }

    // 재질: Sprites/Default + _Color HDR 배율 → 블룸
    private static void EnsureMaterials()
    {
        if (glowMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        glowMaterial = new Material(shader) { name = "TeleportGlow", mainTexture = CreateDot(), hideFlags = HideFlags.DontSave };
        glowMaterial.color = new Color(2.5f, 2.5f, 2.5f, 1f);
        beamMaterial = new Material(shader) { name = "TeleportBeam", mainTexture = CreateBeam(), hideFlags = HideFlags.DontSave };
        beamMaterial.color = new Color(3f, 3f, 3f, 1f);
    }

    private static Texture2D CreateDot()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TeleportDot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
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

    // 빛기둥: u = 길이(아래 진함 → 위로 옅어짐), v = 폭(가장자리 부드럽게)
    private static Texture2D CreateBeam()
    {
        const int w = 64, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TeleportBeam", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            float across = Mathf.Clamp01(1f - v);
            across = across * across * (3f - 2f * across);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(across * Mathf.Lerp(1f, 0.15f, u) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
