using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 전투 씬 후처리 (자동 생성, 씬 수정 불필요) — GameplayBootstrap이 생성
//   전역 Volume 생성 + 메인 카메라 후처리 켜기
//   프로파일: Resources/PostProcessProfile 에셋이 있으면 사용(에디터에서 조절), 없으면 FillDefaults 값으로 실행 중 생성
//   에셋 만들기: Tools > TD Shoot > Create Post Process Profile
//   렌더 품질 보강 (사용자 채택, 2026-10-29):
//     톤매핑 — 별도 Volume(우선순위 5, 프로파일 에셋보다 아래)에 Neutral, 밝은 부분이 하얗게 타는 것 방지 (에셋 수정 없음)
//     안티에일리어싱 — 메인 카메라 SMAA High (계단 현상)
//     그림자 — 직교 탑다운에 맞게 그림자 거리 = 카메라-플레이어 거리 + shadowMargin, 단계 shadowCascades
//       (URP 에셋 값이라 에디터에서는 끝날 때 원래 값으로 복구), SSAO는 Tools > TD Shoot > Apply Render Quality Settings
public class GameplayPostProcess : MonoBehaviour
{
    public const string ResourcePath = "PostProcessProfile";

    public static GameplayPostProcess Instance { get; private set; }

    public TonemappingMode tonemapping = TonemappingMode.Neutral;
    public AntialiasingMode antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    public float shadowMargin = 20f;
    [Range(1, 4)] public int shadowCascades = 2;

    private Volume volume;
    private Volume baseVolume;
    private UniversalRenderPipelineAsset urpAsset;
    private float originalShadowDistance;
    private int originalCascades;
    private bool shadowsChanged;

    public static GameplayPostProcess EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("GameplayPostProcess");
        return go.AddComponent<GameplayPostProcess>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        VolumeProfile asset = Resources.Load<VolumeProfile>(ResourcePath);
        if (asset != null)
        {
            volume.sharedProfile = asset;
        }
        else
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            FillDefaults(profile);
            volume.sharedProfile = profile;
        }

        // 톤매핑 (프로파일 에셋에 없어도 적용되도록 아래 우선순위 Volume에)
        baseVolume = gameObject.AddComponent<Volume>();
        baseVolume.isGlobal = true;
        baseVolume.priority = 5f;
        VolumeProfile baseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        baseProfile.hideFlags = HideFlags.DontSave;
        Tonemapping tone = baseProfile.Add<Tonemapping>(true);
        tone.mode.Override(tonemapping);
        baseVolume.sharedProfile = baseProfile;
    }

    private void Start()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = antialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            StartCoroutine(ApplyShadowDistanceLater(cam));
        }
        else
        {
            Debug.LogWarning("[GameplayPostProcess] 메인 카메라(MainCamera 태그)를 찾지 못해 후처리를 켜지 못했습니다.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (shadowsChanged && urpAsset != null)
        {
            urpAsset.shadowDistance = originalShadowDistance;
            urpAsset.shadowCascadeCount = originalCascades;
        }
        if (baseVolume != null && baseVolume.sharedProfile != null) Destroy(baseVolume.sharedProfile);
    }

    // 그림자 거리는 카메라 위치부터 재므로, 카메라가 플레이어에서 떨어진 만큼 + 화면 범위 여유
    // 시작 직후엔 JU 카메라가 아직 플레이어(맵 생성으로 이동)를 따라가기 전일 수 있어 잠시 뒤 측정
    private System.Collections.IEnumerator ApplyShadowDistanceLater(Camera cam)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        if (cam == null) yield break;

        urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (urpAsset == null || player == null) yield break;

        originalShadowDistance = urpAsset.shadowDistance;
        originalCascades = urpAsset.shadowCascadeCount;
        float distance = Vector3.Distance(cam.transform.position, player.transform.position) + shadowMargin;
        urpAsset.shadowDistance = Mathf.Min(distance, originalShadowDistance * 2f);
        urpAsset.shadowCascadeCount = shadowCascades;
        shadowsChanged = true;
        Debug.Log($"[GameplayPostProcess] 그림자 거리 {originalShadowDistance:0} → {urpAsset.shadowDistance:0}m, 단계 {originalCascades} → {shadowCascades}");
    }

    // 기본값 — 탑다운 시점에서 과하지 않게
    //   블룸: HDR 밝은 부분(발광 재질·총구 화염·폭발)만 은은하게
    //   비네트: 가장자리를 살짝 어둡게 해 시선을 캐릭터 쪽으로
    //   색 조정: 대비·채도 소폭 상향
    public static void FillDefaults(VolumeProfile profile)
    {
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.65f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.45f);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(10f);
        color.saturation.Override(8f);
    }
}
