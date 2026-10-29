using JU;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 히트 스톱 (GameplayBootstrap이 생성) — 두 종류, 느낌이 확실히 다르게 (치명타·처형은 제외, 사용자 결정)
//   엘리트 처치 = 길고 묵직한 "해냈다": 거의 정지(0.05) 0.45초 → 0.7초 복귀 + 화면 흑백·대비 + 카메라 살짝 확대
//   플레이어 피격 = 짧고 날카로운 "아프다": 0.1배 0.06초(큰 피해면 0.1초) → 0.12초 복귀, 화면 연출 없음
//     (붉은 테두리·색수차는 PlayerVitalsEffect), 쿨 0.6초로 몰려 맞아도 끊김 없음. 엘리트 진행 중이면 무시 (엘리트 우선)
//   시간: hold 유지 → recover 동안 서서히 1로 복귀 (ease-out)
//   화면: 전용 Volume(우선순위 20)을 같은 곡선으로 켰다 끔 — 채도 크게 낮춤·대비 상승·색수차·비네트 강화
//   확대: 메인 카메라를 그리기 직전에만 직교 크기(원근이면 FOV)를 줄였다 그린 뒤 복구 → JU 카메라·조준 무관
//   timeScale은 0이 아닌 값만 사용: 다른 시스템이 timeScale <= 0을 정비·일시정지로 판단하므로
//   복구는 실제 시간 기준, 진행 중 정비·사망·결과 화면이 timeScale을 바꿨으면 손 뗌 (덮어쓰지 않음)
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Header("엘리트 처치")]
    public float eliteTimeScale = 0.05f;
    public float eliteHold = 0.45f;
    public float eliteRecover = 0.7f;

    public float eliteZoom = 0.06f;       // 카메라 확대 비율 (직교 크기 -6%)
    public float eliteZoomInTime = 0.08f; // 확대되는 시간 (실제 시간)

    [Header("플레이어 피격")]
    public float hurtTimeScale = 0.1f;
    public float hurtHold = 0.06f;
    public float hurtHoldBig = 0.1f;          // 최대 체력 bigHitRatio 이상 피해
    public float bigHitRatio = 0.15f;
    public float hurtRecover = 0.12f;
    public float hurtCooldown = 0.6f;

    [Header("화면 연출 (엘리트, 가중치 1일 때 값)")]
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
    private bool isElite;       // 진행 중인 것이 엘리트 처치인지 (화면 연출·확대·우선순위)
    private float eliteStartTime;
    private float nextHurtTime;
    private float zoomFactor = 1f; // 현재 프레임 카메라 크기 배율 (1 = 그대로)

    private Camera zoomedCamera;
    private float savedOrthoSize;
    private float savedFov;

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
        IHealth.OnAnyDamaged += HandleAnyDamaged;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        CreateImpactVolume();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
        IHealth.OnAnyDamaged -= HandleAnyDamaged;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreCamera();
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
        if (enemy != null && enemy.IsElite) Request(eliteTimeScale, eliteHold, eliteRecover, true);
    }

    private void HandleAnyDamaged(IHealth.DamageResultInfo result)
    {
        Component target = result.DamagedObject as Component;
        if (target == null || result.RealDamage <= 0f || !target.CompareTag("Player")) return;
        if (isActive && isElite) return; // 엘리트 처치 연출 우선
        float now = Time.unscaledTime;
        if (now < nextHurtTime) return;
        nextHurtTime = now + hurtCooldown;

        float maxHealth = result.DamagedObject.MaxHealth;
        bool big = maxHealth > 0f && result.RealDamage >= maxHealth * bigHitRatio;
        Request(hurtTimeScale, big ? hurtHoldBig : hurtHold, hurtRecover, false);
    }

    // 진행 중이면 더 강한(낮은) 배율·더 늦게 끝나는 쪽으로 합침 (엘리트가 섞이면 엘리트 연출로)
    public void Request(float scale, float hold, float recover, bool elite = false)
    {
        if (hold <= 0f && recover <= 0f) return;
        scale = Mathf.Max(0.01f, scale);
        float now = Time.unscaledTime;

        if (!isActive)
        {
            // 정비·사망 연출 등으로 이미 시간이 바뀌어 있으면 끼어들지 않음
            if (!Mathf.Approximately(Time.timeScale, 1f)) return;
            isActive = true;
            isElite = false;
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
        if (elite && !isElite)
        {
            isElite = true;
            eliteStartTime = now;
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
            isElite = false;
            impactVolume.weight = 0f;
            zoomFactor = 1f;
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

    // weight = 엘리트 연출 세기 (피격은 화면 연출·확대 없음)
    private void Set(float scale, float weight)
    {
        Time.timeScale = scale;
        appliedScale = Time.timeScale;
        float w = isElite ? weight : 0f;
        impactVolume.weight = w;
        float zoomIn = eliteZoomInTime > 0f ? Mathf.Clamp01((Time.unscaledTime - eliteStartTime) / eliteZoomInTime) : 1f;
        zoomIn = 1f - (1f - zoomIn) * (1f - zoomIn);
        zoomFactor = 1f - eliteZoom * w * zoomIn;
    }

    private void End()
    {
        if (impactVolume != null) impactVolume.weight = 0f;
        zoomFactor = 1f;
        if (!isActive) return;
        isActive = false;
        isElite = false;
        if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = 1f;
    }

    // 메인 카메라를 그리기 직전에만 확대, 그린 뒤 원래 값으로 (CameraShake와 같은 방식)
    private void BeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (zoomFactor >= 0.9999f || cam != Camera.main) return;
        zoomedCamera = cam;
        if (cam.orthographic)
        {
            savedOrthoSize = cam.orthographicSize;
            cam.orthographicSize = savedOrthoSize * zoomFactor;
        }
        else
        {
            savedFov = cam.fieldOfView;
            cam.fieldOfView = savedFov * zoomFactor;
        }
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == zoomedCamera) RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (zoomedCamera == null) return;
        if (zoomedCamera.orthographic) zoomedCamera.orthographicSize = savedOrthoSize;
        else zoomedCamera.fieldOfView = savedFov;
        zoomedCamera = null;
    }
}
