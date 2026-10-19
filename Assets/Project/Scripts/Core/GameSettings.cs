using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using JUTPS.GameSettings;

// 환경설정 (해상도·음량) 저장·적용 — PlayerPrefs, 게임 시작 시 자동 적용
//   전체 음량: JU의 JUGameSettings.AudioGeneralVolume을 그대로 사용 (JU UI 프리팹의 JUGameSettings가 씬 시작 때 이 값으로 AudioListener를 덮어쓰므로 같은 값을 공유)
//   효과음: JU 효과음 태그(SFX Audio Tag)의 음량 키 — JUApplyAudioVolumeSettings가 붙은 JU 소리에 반영
//   배경음: 우리 키에 저장만 (배경음 추가 시 MusicVolume을 읽어 적용)
//   해상도: JUGameSettings가 씬 시작 때 해상도를 (최대 해상도 x 렌더 배율)로 바꾸므로, 씬이 열릴 때마다 저장한 해상도로 다시 적용
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
        ApplyResolution();
        AudioListener.volume = MasterVolume;
    }

    // 씬 오브젝트의 Awake 이후 호출 → JUGameSettings가 바꾼 해상도를 되돌림
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyResolution();
    }

    #region Resolution

    public static bool HasSavedResolution => PlayerPrefs.HasKey(ResolutionWidthKey) && PlayerPrefs.HasKey(ResolutionHeightKey);

    // 저장된 해상도 (없으면 현재 화면)
    public static Vector2Int Resolution =>
        HasSavedResolution
            ? new Vector2Int(PlayerPrefs.GetInt(ResolutionWidthKey), PlayerPrefs.GetInt(ResolutionHeightKey))
            : new Vector2Int(Screen.width, Screen.height);

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
    private static void ApplyResolution()
    {
        if (!HasSavedResolution) return;
        Vector2Int size = Resolution;
        if (Screen.width == size.x && Screen.height == size.y) return;
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
