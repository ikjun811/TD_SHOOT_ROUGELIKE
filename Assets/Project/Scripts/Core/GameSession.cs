using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 사이에 넘기는 게임 시작 정보 (GDD 8-6 #10): 메인 화면에서 모드를 기록하고 GameplayScene을 불러옴
// 튜토리얼 모드는 TutorialDirector가 이 값을 보고 진행 (GameplayBootstrap이 생성)
public enum GameMode
{
    Normal,
    Tutorial
}

public static class GameSession
{
    public const string GameplaySceneName = "GameplayScene";
    public const string MainMenuSceneName = "MainMenu";
    public const string ResultSceneName = "ResultScene";

    public static GameMode Mode { get; private set; } = GameMode.Normal;
    public static bool IsTutorial => Mode == GameMode.Tutorial;

    // 0이 아니면 BspArenaGenerator가 씬 설정 대신 이 시드로 생성 (튜토리얼 = 매번 같은 맵)
    // 맵은 씬 시작 Awake에서 생성되므로 씬을 불러오기 전에 정해 둠
    public static int ArenaSeedOverride { get; private set; }

    public static void StartGame(GameMode mode)
    {
        Mode = mode;
        ArenaSeedOverride = mode == GameMode.Tutorial ? TutorialDirector.ArenaSeed : 0;
        LoadScene(GameplaySceneName);
    }

#if UNITY_EDITOR
    // 에디터 확인용: Tools > TD Shoot > Tutorial Test Mode가 켜져 있으면 GameplayScene을 바로 실행해도 튜토리얼로 시작
    public const string EditorTutorialTestKey = "TDShoot.TutorialTestMode";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyEditorTestMode()
    {
        if (!UnityEditor.EditorPrefs.GetBool(EditorTutorialTestKey, false)) return;
        if (SceneManager.GetActiveScene().name != GameplaySceneName) return;
        Mode = GameMode.Tutorial;
        ArenaSeedOverride = TutorialDirector.ArenaSeed;
        Debug.Log("GameSession: Tutorial Test Mode — GameplayScene을 튜토리얼로 시작");
    }
#endif

    public static void LoadMainMenu()
    {
        LoadScene(MainMenuSceneName);
    }

    // RunResultManager가 RunResultData를 채운 뒤 호출
    public static void LoadResultScene()
    {
        LoadScene(ResultSceneName);
    }

    // 정비·결과 화면에서 timeScale 0인 채로 넘어오는 경우가 있어 항상 1로 복구 (GDD 8-6 #10)
    private static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
