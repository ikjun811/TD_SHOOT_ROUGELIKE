using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 히트 스톱 (GameplayBootstrap이 생성) — 엘리트 처치 순간만, 크게 보여 줌 (치명타·처형은 제외, 사용자 결정)
//   시간: 거의 정지(timeScale 0.05)로 hold 유지 → recover 동안 서서히 1로 복귀 (ease-out)
//   화면: 전용 Volume(우선순위 20)을 같은 곡선으로 켰다 끔 — 채도 크게 낮춤·대비 상승·색수차·비네트 강화
//   timeScale은 0이 아닌 값만 사용: 다른 시스템이 timeScale <= 0을 정비·일시정지로 판단하므로
//   복구는 실제 시간 기준, 진행 중 정비·사망·결과 화면이 timeScale을 바꿨으면 손 뗌 (덮어쓰지 않음)
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Header("엘리트 처치")]
    public float eliteTimeScale = 0.05f;
    public float eliteHold = 0.45f;
    public float eliteRecover = 0.7f;

    [Header("화면 연출 (가중치 1일 때 값)")]
    public float impactSaturation = -75f;
    public float impactContrast = 25f;
    public float impactChromatic = 1f;
    public float impactVignette = 0.5f;

    private bool isActive;
    private float startScale;
    private float holdEndTime;
    private float recoverEndTime;
    private float recoverDuration;
    private float appliedScale; // 마지막으로 이 컴포넌트가 넣은 값 (다른 곳에서 바꿨는지 확인용)

    private Volume impactVolume;

    public static HitStop EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("HitStop");
        return go.AddComponent<HitStop>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
        CreateImpactVolume();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
        End();
        if (impactVolume != null && impactVolume.sharedProfile != null) Destroy(impactVolume.sharedProfile);
        Instance = null;
    }

    private void CreateImpactVolume()
    {
        impactVolume = gameObject.AddComponent<Volume>();
        impactVolume.isGlobal = true;
        impactVolume.priority = 20f; // 기본 후처리(GameplayPostProcess 10)보다 위
        impactVolume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(impactSaturation);
        color.contrast.Override(impactContrast);

        ChromaticAberration chromatic = profile.Add<ChromaticAberration>(true);
        chromatic.intensity.Override(impactChromatic);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(impactVignette);
        vignette.smoothness.Override(0.5f);

        impactVolume.sharedProfile = profile;
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        if (enemy != null && enemy.IsElite) Request(eliteTimeScale, eliteHold, eliteRecover);
    }

    // 진행 중이면 더 강한(낮은) 배율·더 늦게 끝나는 쪽으로 합침
    public void Request(float scale, float hold, float recover)
    {
        if (hold <= 0f && recover <= 0f) return;
        scale = Mathf.Max(0.01f, scale);
        float now = Time.unscaledTime;

        if (!isActive)
        {
            // 정비·사망 연출 등으로 이미 시간이 바뀌어 있으면 끼어들지 않음
            if (!Mathf.Approximately(Time.timeScale, 1f)) return;
            isActive = true;
            startScale = scale;
            holdEndTime = now + hold;
            recoverDuration = recover;
        }
        else
        {
            startScale = Mathf.Min(startScale, scale);
            if (now + hold >= holdEndTime)
            {
                holdEndTime = now + hold;
                recoverDuration = Mathf.Max(recoverDuration, recover);
            }
        }
        recoverEndTime = holdEndTime + recoverDuration;
        Set(startScale, 1f);
    }

    private void Update()
    {
        if (!isActive) return;

        // 다른 곳에서 timeScale을 바꿨으면(정비 0 등) 손 떼기 (화면 연출도 끔)
        if (!Mathf.Approximately(Time.timeScale, appliedScale))
        {
            isActive = false;
            impactVolume.weight = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (now < holdEndTime)
        {
            Set(startScale, 1f);
        }
        else if (now < recoverEndTime && recoverDuration > 0f)
        {
            // 처음엔 빠르게, 끝에서 부드럽게 1로 (ease-out)
            float t = Mathf.Clamp01((now - holdEndTime) / recoverDuration);
            t = 1f - (1f - t) * (1f - t);
            Set(Mathf.Lerp(startScale, 1f, t), 1f - t);
        }
        else
        {
            End();
        }
    }

    private void Set(float scale, float weight)
    {
        Time.timeScale = scale;
        appliedScale = Time.timeScale;
        impactVolume.weight = weight;
    }

    private void End()
    {
        if (impactVolume != null) impactVolume.weight = 0f;
        if (!isActive) return;
        isActive = false;
        if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = 1f;
    }
}
