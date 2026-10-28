using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 전투 씬 후처리 (자동 생성, 씬 수정 불필요) — GameplayBootstrap이 생성
//   전역 Volume 생성 + 메인 카메라 후처리 켜기
//   프로파일: Resources/PostProcessProfile 에셋이 있으면 사용(에디터에서 조절), 없으면 FillDefaults 값으로 실행 중 생성
//   에셋 만들기: Tools > TD Shoot > Create Post Process Profile
public class GameplayPostProcess : MonoBehaviour
{
    public const string ResourcePath = "PostProcessProfile";

    public static GameplayPostProcess Instance { get; private set; }

    private Volume volume;

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
    }

    private void Start()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
        else
        {
            Debug.LogWarning("[GameplayPostProcess] 메인 카메라(MainCamera 태그)를 찾지 못해 후처리를 켜지 못했습니다.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
