using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 공포 분위기 조명 (렌더링 ②, 사용자 채택 2026-10-29) — GameplayBootstrap이 생성, 실행 중에만 바꿈 (씬 수정 없음)
//   달빛: 씬 방향광을 차갑고 어둡게, 주변광: 어두운 남색 단색
//   플레이어 빛: 머리 위 따뜻한 포인트 라이트 (플레이어 주변은 항상 보이게)
//   창고 조명: 맵 구역마다 중앙 위 포인트 라이트, 일부는 고장 난 듯 깜빡임 — BspArenaGenerator.OnGenerated마다 재배치
//   추가 조명은 그림자 없음 (Forward+라 개수 부담 적음)
//   어두운 곳에 혼자 남은 느낌 (사용자 2026-10-29): 천장등 = 아래를 비추는 스포트(빛 웅덩이), 구역 일부만 켜짐(나머지는 깜깜),
//     켜진 등마다 빛줄기(TDShoot/LightShaft 원뿔, 깜빡임 동기) + 빛 속 먼지, 맵 전체 바닥 안개(조명 받는 파티클 → 빛 아래에서만 보임)
//   목표물 조명 (렌더링 ③): 탈출 구역 초록(맥박, 탈출 중 더 밝게) — 여기서 따라다님 /
//     보급 상자 노랑(SupplyCrate.Start → AttachCrateLight) / 엘리트 보라(EnemyBase.Start → AttachEliteLight, 사망 시 끔)
public class AtmosphereLighting : MonoBehaviour
{
    public static AtmosphereLighting Instance { get; private set; }

    [Header("달빛·주변광")]
    public Color sunColor = new Color(0.55f, 0.65f, 0.95f);
    // 참고 분위기(사용자 제시 이미지): 빛 밖은 거의 검정, 따뜻한 호박색 조명, 누런 저채도·고대비
    public float sunIntensity = 0.24f;                              // 이전 0.35 → 0.2 → 0.12 → 0.04 → 0.09 → 0.18 → 0.24 (재질 변환 후 너무 어두워 올림)
    public Color ambientColor = new Color(0.14f, 0.14f, 0.16f);     // 이전 (0.12, 0.13, 0.18) → (0.07, 0.08, 0.11) → (0.018, 0.02, 0.025) → (0.045, 0.047, 0.055) → (0.1, 0.1, 0.115)
    [Range(0f, 1f)] public float reflectionIntensity = 0.2f;        // 하늘 반사 (씬 기본 1)

    [Header("플레이어 빛")]
    public Color playerLightColor = new Color(1f, 0.8f, 0.55f);
    public float playerLightRange = 14f;
    public float playerLightIntensity = 4.5f;
    public float playerLightHeight = 2.5f;

    [Header("창고 조명 (구역마다)")]
    public Color lampColor = new Color(1f, 0.78f, 0.45f);  // 호박색 (이전 1, 0.88, 0.7)
    public float lampRange = 10f;
    public float lampIntensity = 12f;
    public float bulbGlow = 4f;                           // 천장등 전구 HDR 밝기 (블룸으로 빛나는 점)
    public float lampHeight = 5f;
    public float lampSpotAngle = 80f;                  // 아래로 비추는 각도 → 바닥 빛 웅덩이 반경 = 높이 x tan(각/2)
    [Range(0f, 1f)] public float lampLitChance = 0.7f; // 켜진 구역 비율 (나머지는 깜깜), 1라운드 기준 — 라운드가 오르면 lateLampLitChance 쪽으로
    [Range(0f, 1f)] public float flickerChance = 0.3f;

    [Header("빛줄기·먼지 (켜진 천장등마다)")]
    public float shaftAlpha = 0.17f;
    public float shaftWidthRatio = 0.75f;              // 바닥 빛 웅덩이 대비 빛줄기 아래 폭
    public int dustPerLamp = 14;

    [Header("바닥 안개")]
    public bool groundFog = false; // 끔 (사용자 2026-10-29: 빌드에서 존재감이 약한데 화면 크기 반투명 판 ~70장 겹침 비용) — 다시 쓰려면 true
    public Color fogColor = new Color(0.62f, 0.58f, 0.48f, 0.13f); // 누런 회색 (이전 푸른 회색 알파 0.09)

    [Header("분위기 색감 (전용 Volume, 기본 후처리 10 위·위기 화면 15 아래)")]
    public bool moodGrading = true;
    public float moodContrast = 15f;      // 이전 22 (어두운 부분이 뭉개져 낮춤)
    public float moodSaturation = -25f;
    public float moodTemperature = 18f;   // 따뜻하게
    public float moodTint = 8f;           // 살짝 초록
    public float moodVignette = 0.3f;     // 이전 0.4

    [Header("라운드 진행 분위기 (1라운드 → 최종 직전으로 갈수록, 맵 생성마다 적용)")]
    public Color lateLampColor = new Color(1f, 0.55f, 0.32f);      // 붉은 주황
    [Range(0f, 1f)] public float lateLampLitChance = 0.5f;          // 점점 어두워짐 (시작 = lampLitChance)
    [Range(0f, 1f)] public float lateFlickerChance = 0.55f;         // 고장 난 조명 증가 (시작 = flickerChance)
    public float lateContrast = 30f;
    public float lateSaturation = -35f;
    public Color lateColorFilter = new Color(1f, 0.88f, 0.82f);    // 붉은 기

    [Header("최종 라운드 비상등")]
    public Color emergencyColor = new Color(1f, 0.12f, 0.08f);
    public float emergencyIntensity = 9f;
    public float emergencyPulseSpeed = 1.1f;                        // 경보 주기 (회/초)
    [Range(0f, 1f)] public float emergencyLitChance = 0.8f;
    public Color emergencyColorFilter = new Color(1f, 0.74f, 0.7f);
    public float fogHeight = 0.35f;
    public int fogCount = 70;

    [Header("목표물 조명")]
    public Color extractionColor = new Color(0.3f, 1f, 0.45f);
    public float extractionRange = 13f;
    public float extractionIntensity = 2.5f;
    public float extractionActiveBoost = 1.6f;   // 플레이어가 구역 안에서 탈출 중일 때 배율
    public float extractionHeight = 3f;
    public Color crateColor = new Color(1f, 0.88f, 0.6f);  // 노란 상자 위에서 디테일이 묻히지 않게 흰빛 쪽으로 (이전 1, 0.8, 0.25)
    public float crateRange = 6f;
    public float crateIntensity = 4f;
    public float crateLightHeight = 2.6f;                    // 위에서 비춰 윗면·소총 디테일을 살림 (이전 공용 1.5)
    public Color eliteColor = new Color(0.7f, 0.35f, 1f);
    public float eliteRange = 5f;
    public float eliteIntensity = 3f;
    public float objectLightHeight = 1.5f;
    public float pulseSpeed = 1.6f;              // 맥박 / 초 (상자·탈출 구역)

    private class Lamp
    {
        public Light light;
        public Renderer shaft;
        public Renderer bulb;
        public bool flicker;
        public float nextChangeTime;
        public float target = 1f;
        public float current = 1f;
    }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private MaterialPropertyBlock shaftBlock;
    private ParticleSystem fog;
    private static Material shaftMaterial;
    private static Material bulbMaterial;
    private VolumeProfile moodProfile;
    private ColorAdjustments moodColor;
    private Color currentLampColor;     // 이번 라운드 조명 색 (라운드 진행·비상등)
    private bool emergency;             // 최종 라운드 비상등
    private static Material dustMaterial;
    private static Material fogMaterial;
    private static Mesh shaftMesh;
    private static float shaftMeshKey;
    private readonly List<Light> crateLights = new List<Light>();
    private Light playerLight;
    private Light extractionLight;
    private ExtractionZone extractionZone;

    // 보급 상자 위 노란 빛 (상자와 함께 사라짐), 분위기 조명이 없으면 붙이지 않음
    // color 지정 시 그 색 (의료 상자 = 붉은 흰빛), 없으면 보급 상자 노랑
    public static void AttachCrateLight(Transform crate, Color? color = null)
    {
        if (Instance == null || crate == null) return;
        Light light = Instance.CreateObjectLight("CrateLight", crate, color ?? Instance.crateColor, Instance.crateRange, Instance.crateIntensity, Instance.crateLightHeight);
        Instance.crateLights.Add(light);
    }

    // 엘리트 위 보라 빛 (EnemyBase가 사망 시 끔)
    public static void AttachEliteLight(Transform elite)
    {
        if (Instance == null || elite == null) return;
        Instance.CreateObjectLight("EliteLight", elite, Instance.eliteColor, Instance.eliteRange, Instance.eliteIntensity);
    }

    // 대상 자식으로 붙이되 위치는 월드 기준 (크기가 바뀐 대상에서도 높이 유지)
    private Light CreateObjectLight(string objName, Transform target, Color color, float range, float intensity, float height = -1f)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(target, true);
        go.transform.position = target.position + Vector3.up * (height > 0f ? height : objectLightHeight);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    public static AtmosphereLighting EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("AtmosphereLighting");
        return go.AddComponent<AtmosphereLighting>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BspArenaGenerator.OnGenerated += BuildLamps;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        BspArenaGenerator.OnGenerated -= BuildLamps;
        if (moodProfile != null) Destroy(moodProfile);
        Instance = null;
    }

    private void Start()
    {
        ApplySunAndAmbient();
        CreateMoodVolume();
        CreatePlayerLight();
        CreateExtractionLight();

        // 첫 맵은 이 컴포넌트보다 먼저 생성됨 (BspArenaGenerator.Awake)
        BspArenaGenerator generator = FindAnyObjectByType<BspArenaGenerator>();
        if (generator != null && generator.Leaves.Count > 0) BuildLamps(generator);
    }

    private void ApplySunAndAmbient()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }

        // 씬은 하늘(Skybox) 주변광 0.7 — 모드만 바꾸면 URP가 쓰는 주변광 프로브(SH)는 예전 하늘 밝기 그대로라 직접 채움
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        SphericalHarmonicsL2 probe = new SphericalHarmonicsL2();
        probe.AddAmbientLight(ambientColor);
        RenderSettings.ambientProbe = probe;

        // 금속 재질에 비치는 하늘 반사도 줄임 (씬 기본 1)
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }

    private void CreatePlayerLight()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        GameObject go = new GameObject("PlayerLight");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = Vector3.up * playerLightHeight;
        playerLight = go.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.color = playerLightColor;
        playerLight.range = playerLightRange;
        playerLight.intensity = playerLightIntensity;
        playerLight.shadows = LightShadows.None;
    }

    // 탈출 구역은 크기가 큰 트리거라 자식으로 두지 않고 매 프레임 위치를 따라감 (라운드마다 재배치)
    private void CreateExtractionLight()
    {
        extractionZone = FindAnyObjectByType<ExtractionZone>();
        if (extractionZone == null) return;
        GameObject go = new GameObject("ExtractionLight");
        go.transform.SetParent(transform, false);
        extractionLight = go.AddComponent<Light>();
        extractionLight.type = LightType.Point;
        extractionLight.color = extractionColor;
        extractionLight.range = extractionRange;
        extractionLight.shadows = LightShadows.None;
    }

    private void BuildLamps(BspArenaGenerator generator)
    {
        foreach (Lamp lamp in lamps)
        {
            if (lamp.light != null) Destroy(lamp.light.gameObject);
        }
        lamps.Clear();
        if (generator == null) return;

        // 라운드 진행 분위기: 1라운드(t=0) → 최종 직전(t=1), 최종 라운드는 비상등
        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        int maxRound = WaveManager.Instance != null ? WaveManager.Instance.MaxRound : 15;
        float t = Mathf.Clamp01((round - 1f) / Mathf.Max(1f, maxRound - 2f));
        emergency = round >= maxRound;
        currentLampColor = emergency ? emergencyColor : Color.Lerp(lampColor, lateLampColor, t);
        float litChance = emergency ? emergencyLitChance : Mathf.Lerp(lampLitChance, lateLampLitChance, t);
        float flicker = emergency ? 0f : Mathf.Lerp(flickerChance, lateFlickerChance, t);
        ApplyRoundMood(t);

        List<Vector3> centers = generator.GetZoneCenters();
        int lit = 0;
        for (int i = 0; i < centers.Count; i++)
        {
            // 일부 구역만 켬 (최소 1개는 켜짐)
            bool last = i == centers.Count - 1;
            if (Random.value > litChance && !(last && lit == 0)) continue;
            lit++;

            Vector3 center = centers[i];
            GameObject go = new GameObject("ZoneLamp");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, lampHeight, center.z), Quaternion.Euler(90f, 0f, 0f)); // 아래를 비춤
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = lampSpotAngle;
            light.innerSpotAngle = lampSpotAngle * 0.55f;
            light.color = currentLampColor;
            light.range = lampRange;
            light.intensity = lampIntensity;
            light.shadows = LightShadows.None;
            Renderer shaft = CreateShaft(go.transform);
            Renderer bulb = CreateBulb(go.transform);
            CreateDust(go.transform);
            lamps.Add(new Lamp { light = light, shaft = shaft, bulb = bulb, flicker = Random.value < flicker });
        }

        BuildFog(generator);
    }

    private float PoolRadius => lampHeight * Mathf.Tan(lampSpotAngle * 0.5f * Mathf.Deg2Rad);

    // 빛줄기: 조명에서 바닥까지 넓어지는 원뿔 (뚜껑 없음), 바닥에서 조금 띄움
    private Renderer CreateShaft(Transform lamp)
    {
        Shader shader = Shader.Find("TDShoot/LightShaft");
        if (shader == null) return null;
        if (shaftMaterial == null) shaftMaterial = new Material(shader) { name = "LightShaft", hideFlags = HideFlags.DontSave };

        float bottomRadius = PoolRadius * shaftWidthRatio;
        float key = lampHeight * 1000f + bottomRadius;
        if (shaftMesh == null || !Mathf.Approximately(shaftMeshKey, key))
        {
            shaftMesh = BuildConeMesh(lampHeight - 0.1f, 0.25f, bottomRadius, 32);
            shaftMeshKey = key;
        }

        GameObject go = new GameObject("LightShaft");
        go.transform.SetParent(lamp, false);
        go.transform.SetPositionAndRotation(new Vector3(lamp.position.x, 0.1f, lamp.position.z), Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = shaftMesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = shaftMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // 전구: 천장등 위치의 작은 납작 구, 비조명 HDR 색 → 블룸으로 빛나는 점 (깜빡임 동기)
    private Renderer CreateBulb(Transform lamp)
    {
        if (bulbMaterial == null) bulbMaterial = new Material(Shader.Find("Sprites/Default")) { name = "LampBulb", hideFlags = HideFlags.DontSave };
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "LampBulb";
        go.transform.SetParent(lamp, false);
        go.transform.SetPositionAndRotation(lamp.position, Quaternion.identity);
        go.transform.localScale = new Vector3(0.55f, 0.18f, 0.55f);
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = bulbMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // 분위기 색감: 대비↑·채도↓·따뜻하고 살짝 초록 톤·비네트 (참고 이미지)
    private void CreateMoodVolume()
    {
        if (!moodGrading) return;
        GameObject go = new GameObject("MoodVolume");
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 12f;
        moodProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        moodProfile.hideFlags = HideFlags.DontSave;

        ColorAdjustments color = moodProfile.Add<ColorAdjustments>(true);
        color.contrast.Override(moodContrast);
        color.saturation.Override(moodSaturation);
        color.colorFilter.Override(Color.white);
        moodColor = color;
        WhiteBalance balance = moodProfile.Add<WhiteBalance>(true);
        balance.temperature.Override(moodTemperature);
        balance.tint.Override(moodTint);
        Vignette vignette = moodProfile.Add<Vignette>(true);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(moodVignette);
        vignette.smoothness.Override(0.5f);
        volume.sharedProfile = moodProfile;
    }

    // 라운드 진행에 따라 색감 Volume 조정 (최종 라운드는 붉은 필터)
    private void ApplyRoundMood(float t)
    {
        if (moodColor == null) return;
        moodColor.contrast.Override(Mathf.Lerp(moodContrast, lateContrast, emergency ? 1f : t));
        moodColor.saturation.Override(Mathf.Lerp(moodSaturation, lateSaturation, emergency ? 1f : t));
        moodColor.colorFilter.Override(emergency ? emergencyColorFilter : Color.Lerp(Color.white, lateColorFilter, t));
    }

    // 원뿔 옆면 (아래 원 y=0 → 위 원 y=height), uv.y = 0 아래 → 1 위
    private static Mesh BuildConeMesh(float height, float topRadius, float bottomRadius, int segments)
    {
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        float slope = (bottomRadius - topRadius) / Mathf.Max(0.01f, height);
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 normal = (dir + Vector3.up * slope).normalized;
            vertices[i * 2] = dir * bottomRadius;
            vertices[i * 2 + 1] = dir * topRadius + Vector3.up * height;
            normals[i * 2] = normals[i * 2 + 1] = normal;
            uvs[i * 2] = new Vector2((float)i / segments, 0f);
            uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
        }
        for (int i = 0; i < segments; i++)
        {
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
        }
        Mesh mesh = new Mesh { name = "LightShaftCone", hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // 빛 속 먼지: 빛줄기 안에서 천천히 떠다니는 작은 점
    private void CreateDust(Transform lamp)
    {
        if (dustPerLamp <= 0) return;
        if (dustMaterial == null)
        {
            dustMaterial = new Material(Shader.Find("Sprites/Default")) { name = "LampDust", mainTexture = CreateSoftTexture("LampDust", 16, false), hideFlags = HideFlags.DontSave };
        }

        float radius = PoolRadius * shaftWidthRatio * 0.7f;
        GameObject go = new GameObject("LampDust");
        go.transform.SetParent(lamp, false);
        go.transform.SetPositionAndRotation(new Vector3(lamp.position.x, lampHeight * 0.45f, lamp.position.z), Quaternion.identity);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        Color c = currentLampColor;
        c.a = 0.55f;
        main.startColor = c;
        main.maxParticles = dustPerLamp * 2;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = dustPerLamp / 7f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 1.4f, lampHeight * 0.8f, radius * 1.4f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeInOut(0.3f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
    }

    // 바닥 안개: 맵 전체에 넓고 옅은 덩어리가 바닥에 깔려 천천히 흐름
    //   조명을 받는 URP 파티클 재질 → 빛 웅덩이·플레이어 주변에서만 드러나고 어두운 곳에서는 거의 안 보임 (없으면 비조명 재질)
    private void BuildFog(BspArenaGenerator generator)
    {
        if (fog != null) Destroy(fog.gameObject);
        if (!groundFog) return;

        float size = generator.ArenaSize;
        GameObject go = new GameObject("GroundFog");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(new Vector3(generator.transform.position.x, fogHeight, generator.transform.position.z), Quaternion.identity);
        fog = go.AddComponent<ParticleSystem>();
        fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = fog.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = fogColor;
        main.maxParticles = fogCount * 2;

        ParticleSystem.EmissionModule emission = fog.emission;
        emission.rateOverTime = fogCount / 24f;

        ParticleSystem.ShapeModule shape = fog.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size, 0.3f, size);

        // 한쪽으로 아주 천천히 흐르며 살짝 회전
        ParticleSystem.VelocityOverLifetimeModule velocity = fog.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);

        ParticleSystem.RotationOverLifetimeModule rotation = fog.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        ParticleSystem.ColorOverLifetimeModule col = fog.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeInOut(0.25f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 바닥에 눕힘
        renderer.sharedMaterial = GetFogMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 50f; // 다른 반투명(빛줄기·이펙트)보다 뒤로
        fog.Play();
    }

    private static Material GetFogMaterial()
    {
        if (fogMaterial != null) return fogMaterial;
        Texture2D tex = CreateSoftTexture("GroundFog", 128, true);

        // 빌드 대응: URP Particles/Lit 반투명이 빌드에서 풀려 안개 판이 사각형으로 보임 (기준 재질 복사로도 해결 안 됨)
        // → 키워드 변형이 없는 전용 셰이더 TDShoot/GroundFog (Resources/Shaders, 조명 반영) 우선
        Shader fogShader = Shader.Find("TDShoot/GroundFog");
        if (fogShader != null)
        {
            fogMaterial = new Material(fogShader) { name = "GroundFog", hideFlags = HideFlags.DontSave };
            fogMaterial.SetTexture("_BaseMap", tex);
            fogMaterial.SetColor("_BaseColor", Color.white);
            return fogMaterial;
        }

        Material reference = Resources.Load<Material>("BuildShaderRefs/Ref_ParticlesLit_Transparent");
        if (reference != null)
        {
            Material mat = new Material(reference) { name = "GroundFog", hideFlags = HideFlags.DontSave };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            fogMaterial = mat;
            return fogMaterial;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (lit != null)
        {
            Material mat = new Material(lit) { name = "GroundFog", hideFlags = HideFlags.DontSave };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // 반투명 알파 혼합 (Particles/Lit 기본은 불투명)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            fogMaterial = mat;
        }
        else
        {
            fogMaterial = new Material(Shader.Find("Sprites/Default")) { name = "GroundFog", mainTexture = tex, hideFlags = HideFlags.DontSave };
        }
        return fogMaterial;
    }

    // 원형 감쇠 텍스처, cloud면 노이즈 2겹을 섞어 구름 얼룩처럼
    private static Texture2D CreateSoftTexture(string texName, int size, bool cloud)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, cloud) { name = texName, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                if (cloud)
                {
                    a = a * a * (3f - 2f * a);
                    float n = Mathf.PerlinNoise(x * 0.04f + 11f, y * 0.04f + 3f) * 0.65f + Mathf.PerlinNoise(x * 0.11f + 5f, y * 0.11f + 17f) * 0.35f;
                    a = Mathf.Clamp01(a * (0.35f + n));
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(cloud);
        return tex;
    }

    private static Gradient FadeInOut(float edge)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, edge), new GradientAlphaKey(1f, 1f - edge), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private void Update()
    {
        if (playerLight != null)
        {
            playerLight.color = playerLightColor;
            playerLight.range = playerLightRange;
            playerLight.intensity = playerLightIntensity;
        }

        // 목표물: 천천히 맥박 (0.75 ~ 1)
        float pulse = 0.75f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f));
        if (extractionLight != null && extractionZone != null)
        {
            extractionLight.transform.position = extractionZone.transform.position + Vector3.up * extractionHeight;
            float boost = extractionZone.IsPlayerInside ? extractionActiveBoost : 1f;
            extractionLight.intensity = extractionZone.IsHidden ? 0f : extractionIntensity * pulse * boost; // 튜토리얼에서 숨긴 동안 끔
            extractionLight.color = extractionColor;
            extractionLight.range = extractionRange;
        }
        for (int i = crateLights.Count - 1; i >= 0; i--)
        {
            if (crateLights[i] == null) { crateLights.RemoveAt(i); continue; }
            crateLights[i].intensity = crateIntensity * pulse;
        }

        // 고장 난 조명: 대부분 켜져 있다가 가끔 짧게 꺼지거나 흐려짐 (게임 시간 기준)
        float now = Time.time;
        // 최종 라운드 비상등: 경보처럼 밝아졌다 어두워짐 (어두울 때도 0.15는 남김)
        float alarm = 1f;
        if (emergency)
        {
            float s = Mathf.Sin(now * emergencyPulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            alarm = 0.15f + 0.85f * s * s;
        }
        float baseIntensity = emergency ? emergencyIntensity : lampIntensity;
        foreach (Lamp lamp in lamps)
        {
            if (lamp.light == null) continue;
            if (lamp.flicker && now >= lamp.nextChangeTime)
            {
                bool dip = lamp.target >= 0.99f && Random.value < 0.5f;
                lamp.target = dip ? Random.Range(0f, 0.35f) : 1f;
                lamp.nextChangeTime = now + (dip ? Random.Range(0.04f, 0.15f) : Random.Range(0.1f, 1.8f));
            }
            lamp.current = lamp.flicker ? Mathf.MoveTowards(lamp.current, lamp.target, Time.deltaTime * 25f) : 1f;
            float level = lamp.current * alarm;
            lamp.light.intensity = baseIntensity * level;
            Color lampTint = currentLampColor;
            if (lamp.shaft != null)
            {
                if (shaftBlock == null) shaftBlock = new MaterialPropertyBlock();
                Color c = lampTint;
                c.a = shaftAlpha * level;
                shaftBlock.SetColor("_Color", c);
                lamp.shaft.SetPropertyBlock(shaftBlock);
            }
            if (lamp.bulb != null)
            {
                if (shaftBlock == null) shaftBlock = new MaterialPropertyBlock();
                float glow = Mathf.Lerp(0.15f, bulbGlow, level);
                shaftBlock.SetColor("_Color", new Color(lampTint.r * glow, lampTint.g * glow, lampTint.b * glow, 1f));
                lamp.bulb.SetPropertyBlock(shaftBlock);
            }
        }
    }
}
