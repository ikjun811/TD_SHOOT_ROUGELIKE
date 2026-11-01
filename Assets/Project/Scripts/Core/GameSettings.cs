using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using JUTPS.GameSettings;

// 환경설정 (해상도·음량) 저장·적용 — PlayerPrefs, 게임 시작 시 자동 적용
//   전체 음량: JU의 JUGameSettings.AudioGeneralVolume을 그대로 사용 (JU UI 프리팹의 JUGameSettings가 씬 시작 때 이 값으로 AudioListener를 덮어쓰므로 같은 값을 공유)
//   효과음: JU 효과음 태그(SFX Audio Tag)의 음량 키 — JUApplyAudioVolumeSettings가 붙은 JU 소리에 반영
//   배경음: 우리 키에 저장만 (배경음 추가 시 MusicVolume을 읽어 적용)
//   해상도: JUGameSettings가 씬 시작 때 해상도를 (최대 해상도 x 렌더 배율)로 바꾸므로, 씬이 열릴 때마다 저장한 해상도(없으면 실행 시 창 크기)로 다시 적용
public static class GameSettings
{
    private const string ResolutionWidthKey = "TDS_RESOLUTION_WIDTH";
    private const string ResolutionHeightKey = "TDS_RESOLUTION_HEIGHT";
    private const string MusicVolumeKey = "TDS_AUDIO_MUSIC_VOLUME";
    private const string SfxVolumeKey = "SETTINGS_AUDIO_VOLUME_SFX Audio Tag"; // JUGameSettings.GetAudioVolumeKey(SFX Audio Tag)와 같은 키

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        launchResolution = new Vector2Int(Screen.width, Screen.height); // 첫 씬 전(JUGameSettings가 바꾸기 전) 실행 창 크기
        ApplyResolution();
        ApplyFrameRate();
        AudioListener.volume = MasterVolume;
    }

    // 씬 오브젝트의 Awake 이후 호출 → JUGameSettings가 바꾼 해상도를 되돌림
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyResolution();
        ApplyFrameRate();
    }

    // 프레임 제한 (사용자 요청 2026-10-29: 빌드에서 약 600 FPS로 GPU를 과하게 씀 → 60)
    //   수직 동기화를 끄고 targetFrameRate로 고정 (모니터 주사율과 무관하게 60, 수직 동기화가 켜져 있으면 targetFrameRate가 무시됨)
    public const int TargetFrameRate = 60;

    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    #region Resolution

    public static bool HasSavedResolution => PlayerPrefs.HasKey(ResolutionWidthKey) && PlayerPrefs.HasKey(ResolutionHeightKey);

    // 게임을 켰을 때의 창 크기 (환경설정에서 해상도를 저장한 적이 없으면 이 크기를 유지)
    private static Vector2Int launchResolution;

    // 저장된 해상도 (없으면 실행 시 창 크기 — JUGameSettings가 바꾼 현재 화면이 아니라)
    public static Vector2Int Resolution =>
        HasSavedResolution
            ? new Vector2Int(PlayerPrefs.GetInt(ResolutionWidthKey), PlayerPrefs.GetInt(ResolutionHeightKey))
            : (launchResolution.x > 0 && launchResolution.y > 0 ? launchResolution : new Vector2Int(Screen.width, Screen.height));

    // 모니터가 지원하는 해상도 (가로x세로 중복 제거, 작은 것부터)
    public static List<Vector2Int> GetAvailableResolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!list.Contains(size)) list.Add(size);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    public static void SetResolution(Vector2Int size)
    {
        PlayerPrefs.SetInt(ResolutionWidthKey, size.x);
        PlayerPrefs.SetInt(ResolutionHeightKey, size.y);
        PlayerPrefs.Save();
        ApplyResolution();
    }

    // 화면 모드(전체 화면·창)는 현재 상태 유지
    //   수정 (2026-11-01, 빌드에서 튜토리얼 시작 시 해상도가 바뀌던 문제): 저장값이 없어도 실행 시 창 크기로 되돌리고,
    //   크기 비교 없이 항상 요청 — SetResolution은 프레임 끝에 적용돼 JU가 요청한 직후에도 Screen.width가 예전 값이라
    //   "이미 같음"으로 건너뛰던 문제. 같은 프레임에선 나중 요청이 적용되므로 JU 요청을 덮음
    private static void ApplyResolution()
    {
        Vector2Int size = Resolution;
        if (size.x <= 0 || size.y <= 0) return;
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
    }

    #endregion

    #region Volume (0~1)

    public static float MasterVolume
    {
        get => JUGameSettings.AudioGeneralVolume;
        set
        {
            JUGameSettings.AudioGeneralVolume = Mathf.Clamp01(value); // 저장 + AudioListener 적용 + JU 설정 변경 알림
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.Save();
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            // 실행 중인 JU 소리에 바로 반영 (태그 에셋이 로드돼 있을 때 — 메인 화면에서는 다음 씬부터 적용)
            JU.JUTag tag = FindSfxTag();
            if (tag != null) JUGameSettings.SetAudioVolume(tag, Mathf.Clamp01(value));
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    private static JU.JUTag FindSfxTag()
    {
        foreach (JU.JUTag tag in Resources.FindObjectsOfTypeAll<JU.JUTag>())
        {
            if (tag != null && tag.name == "SFX Audio Tag") return tag;
        }
        return null;
    }

    #endregion
}
