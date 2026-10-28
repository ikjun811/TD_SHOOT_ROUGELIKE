using JU;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 플레이어 위기 화면 (A2) — GameplayBootstrap이 생성
//   체력 낮음: 체력 비율이 lowHealthThreshold 아래면 채도↓ + 붉은 비네트가 심장 박동처럼 맥박 (낮을수록 진하고 빠름)
//   피격: 맞는 순간 색수차 + 붉은 테두리가 확 들어왔다 빠짐 (실제 시간 기준)
//   전용 Volume 2개 (우선순위 15·16: 기본 후처리 10 위, 엘리트 히트 스톱 20 아래), 가중치로만 켜고 끔
//   사망·정비·결과 화면(timeScale 0)에서는 끔
public class PlayerVitalsEffect : MonoBehaviour
{
    public static PlayerVitalsEffect Instance { get; private set; }

    [Header("체력 낮음")]
    [Range(0.05f, 0.9f)] public float lowHealthThreshold = 0.35f; // 이 비율 아래부터 시작
    [Range(0.01f, 0.5f)] public float criticalHealth = 0.1f;      // 이 비율에서 최대
    public float lowSaturation = -45f;
    public Color lowVignetteColor = new Color(0.55f, 0f, 0f);
    public float lowVignetteBase = 0.32f;
    public float lowVignettePulse = 0.14f;         // 박동 때 더해지는 세기
    public float pulseRateMin = 1.1f;              // 박동 / 초 (임계 직후)
    public float pulseRateMax = 2.2f;              // 박동 / 초 (최저 체력)
    public float fadeSpeed = 3f;                   // 켜고 끌 때 가중치 변화 속도

    [Header("피격")]
    public float hitChromatic = 0.8f;
    public Color hitVignetteColor = new Color(0.8f, 0f, 0f);
    public float hitVignette = 0.38f;
    public float hitDuration = 0.35f;

    private Volume lowVolume;
    private Volume hitVolume;
    private Vignette lowVignette;
    private JUHealth playerHealth;
    private float lowWeight;
    private float pulsePhase;
    private float hitElapsed = float.MaxValue;

    public static PlayerVitalsEffect EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("PlayerVitalsEffect");
        return go.AddComponent<PlayerVitalsEffect>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        lowVolume = CreateVolume(15f, out VolumeProfile lowProfile);
        ColorAdjustments color = lowProfile.Add<ColorAdjustments>(true);
        color.saturation.Override(lowSaturation);
        lowVignette = lowProfile.Add<Vignette>(true);
        lowVignette.color.Override(lowVignetteColor);
        lowVignette.intensity.Override(lowVignetteBase);
        lowVignette.smoothness.Override(0.6f);

        hitVolume = CreateVolume(16f, out VolumeProfile hitProfile);
        ChromaticAberration chromatic = hitProfile.Add<ChromaticAberration>(true);
        chromatic.intensity.Override(hitChromatic);
        Vignette hitVig = hitProfile.Add<Vignette>(true);
        hitVig.color.Override(hitVignetteColor);
        hitVig.intensity.Override(hitVignette);
        hitVig.smoothness.Override(0.5f);

        IHealth.OnAnyDamaged += HandleAnyDamaged;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        IHealth.OnAnyDamaged -= HandleAnyDamaged;
        if (lowVolume != null) Destroy(lowVolume.sharedProfile);
        if (hitVolume != null) Destroy(hitVolume.sharedProfile);
        Instance = null;
    }

    private Volume CreateVolume(float priority, out VolumeProfile profile)
    {
        GameObject go = new GameObject("Volume_" + priority);
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        volume.sharedProfile = profile;
        return volume;
    }

    private void HandleAnyDamaged(IHealth.DamageResultInfo result)
    {
        Component target = result.DamagedObject as Component;
        if (target == null || result.RealDamage <= 0f) return;
        if (target.CompareTag("Player")) hitElapsed = 0f;
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<JUHealth>();
        }

        bool active = Time.timeScale > 0f && playerHealth != null && !playerHealth.IsDead;
        float dt = Time.unscaledDeltaTime;

        // 체력 낮음: 임계 아래에서 0 → 1 (최저 체력)
        float severity = 0f;
        float targetWeight = 0f;
        if (active && playerHealth.MaxHealth > 0f)
        {
            float ratio = playerHealth.Health / playerHealth.MaxHealth;
            if (ratio < lowHealthThreshold)
            {
                severity = Mathf.InverseLerp(lowHealthThreshold, criticalHealth, ratio);
                targetWeight = Mathf.Lerp(0.45f, 1f, severity); // 임계를 넘자마자 눈에 띄게 시작
            }
        }
        lowWeight = Mathf.MoveTowards(lowWeight, targetWeight, fadeSpeed * dt);
        lowVolume.weight = lowWeight;

        if (lowWeight > 0f)
        {
            // 심장 박동: 한 주기 앞쪽에 짧게 두 번 뛰는 모양
            pulsePhase = (pulsePhase + dt * Mathf.Lerp(pulseRateMin, pulseRateMax, severity)) % 1f;
            float beat = Beat(pulsePhase, 0f) + 0.6f * Beat(pulsePhase, 0.18f);
            lowVignette.intensity.value = lowVignetteBase + lowVignettePulse * Mathf.Clamp01(beat);
        }

        // 피격: 즉시 1 → hitDuration 동안 0 (ease-out)
        if (hitElapsed < hitDuration && active)
        {
            hitElapsed += dt;
            float t = Mathf.Clamp01(hitElapsed / hitDuration);
            hitVolume.weight = (1f - t) * (1f - t);
        }
        else
        {
            hitElapsed = float.MaxValue;
            hitVolume.weight = 0f;
        }
    }

    // start 위치에서 짧게 솟았다 빠지는 박동 (주기 0~1 기준 폭 0.12)
    private static float Beat(float phase, float start)
    {
        float x = (phase - start) / 0.12f;
        if (x < 0f || x > 1f) return 0f;
        return Mathf.Sin(x * Mathf.PI);
    }
}
