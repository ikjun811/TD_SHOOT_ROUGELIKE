using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 공포 분위기 조명 (렌더링 ②, 사용자 채택 2026-10-29) — GameplayBootstrap이 생성, 실행 중에만 바꿈 (씬 수정 없음)
//   달빛: 씬 방향광을 차갑고 어둡게, 주변광: 어두운 남색 단색
//   플레이어 빛: 머리 위 따뜻한 포인트 라이트 (플레이어 주변은 항상 보이게)
//   창고 조명: 맵 구역마다 중앙 위 포인트 라이트, 일부는 고장 난 듯 깜빡임 — BspArenaGenerator.OnGenerated마다 재배치
//   추가 조명은 그림자 없음 (Forward+라 개수 부담 적음)
//   목표물 조명 (렌더링 ③): 탈출 구역 초록(맥박, 탈출 중 더 밝게) — 여기서 따라다님 /
//     보급 상자 노랑(SupplyCrate.Start → AttachCrateLight) / 엘리트 보라(EnemyBase.Start → AttachEliteLight, 사망 시 끔)
public class AtmosphereLighting : MonoBehaviour
{
    public static AtmosphereLighting Instance { get; private set; }

    [Header("달빛·주변광")]
    public Color sunColor = new Color(0.55f, 0.65f, 0.95f);
    public float sunIntensity = 0.35f;
    public Color ambientColor = new Color(0.12f, 0.13f, 0.18f);

    [Header("플레이어 빛")]
    public Color playerLightColor = new Color(1f, 0.82f, 0.6f);
    public float playerLightRange = 10f;
    public float playerLightIntensity = 2.2f;
    public float playerLightHeight = 2.5f;

    [Header("창고 조명 (구역마다)")]
    public Color lampColor = new Color(1f, 0.88f, 0.7f);
    public float lampRange = 11f;
    public float lampIntensity = 3.5f;
    public float lampHeight = 4f;
    [Range(0f, 1f)] public float flickerChance = 0.3f;

    [Header("목표물 조명")]
    public Color extractionColor = new Color(0.3f, 1f, 0.45f);
    public float extractionRange = 13f;
    public float extractionIntensity = 2.5f;
    public float extractionActiveBoost = 1.6f;   // 플레이어가 구역 안에서 탈출 중일 때 배율
    public float extractionHeight = 3f;
    public Color crateColor = new Color(1f, 0.8f, 0.25f);
    public float crateRange = 5f;
    public float crateIntensity = 2.5f;
    public Color eliteColor = new Color(0.7f, 0.35f, 1f);
    public float eliteRange = 5f;
    public float eliteIntensity = 3f;
    public float objectLightHeight = 1.5f;
    public float pulseSpeed = 1.6f;              // 맥박 / 초 (상자·탈출 구역)

    private class Lamp
    {
        public Light light;
        public bool flicker;
        public float nextChangeTime;
        public float target = 1f;
        public float current = 1f;
    }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private readonly List<Light> crateLights = new List<Light>();
    private Light playerLight;
    private Light extractionLight;
    private ExtractionZone extractionZone;

    // 보급 상자 위 노란 빛 (상자와 함께 사라짐), 분위기 조명이 없으면 붙이지 않음
    public static void AttachCrateLight(Transform crate)
    {
        if (Instance == null || crate == null) return;
        Light light = Instance.CreateObjectLight("CrateLight", crate, Instance.crateColor, Instance.crateRange, Instance.crateIntensity);
        Instance.crateLights.Add(light);
    }

    // 엘리트 위 보라 빛 (EnemyBase가 사망 시 끔)
    public static void AttachEliteLight(Transform elite)
    {
        if (Instance == null || elite == null) return;
        Instance.CreateObjectLight("EliteLight", elite, Instance.eliteColor, Instance.eliteRange, Instance.eliteIntensity);
    }

    // 대상 자식으로 붙이되 위치는 월드 기준 (크기가 바뀐 대상에서도 높이 유지)
    private Light CreateObjectLight(string objName, Transform target, Color color, float range, float intensity)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(target, true);
        go.transform.position = target.position + Vector3.up * objectLightHeight;
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
        Instance = null;
    }

    private void Start()
    {
        ApplySunAndAmbient();
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

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
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

        foreach (Vector3 center in generator.GetZoneCenters())
        {
            GameObject go = new GameObject("ZoneLamp");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, lampHeight, center.z);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lampColor;
            light.range = lampRange;
            light.intensity = lampIntensity;
            light.shadows = LightShadows.None;
            lamps.Add(new Lamp { light = light, flicker = Random.value < flickerChance });
        }
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
            extractionLight.intensity = extractionIntensity * pulse * boost;
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
            lamp.light.intensity = lampIntensity * lamp.current;
        }
    }
}
