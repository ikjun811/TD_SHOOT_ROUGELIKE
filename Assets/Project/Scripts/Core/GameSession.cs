using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 사이에 넘기는 게임 시작 정보 (GDD 8-6 #10): 메인 화면에서 모드를 기록하고 GameplayScene을 불러옴
// 튜토리얼 모드는 TutorialDirector(미구현)가 이 값을 보고 진행
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

    public static void StartGame(GameMode mode)
    {
        Mode = mode;
        LoadScene(GameplaySceneName);
    }

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
