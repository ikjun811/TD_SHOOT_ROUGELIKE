using UnityEngine;
using UnityEngine.SceneManagement;

// 전투 씬 자동 구성: 씬이 로드될 때마다(다시 시작 포함) 코드로 만드는 매니저들을 생성
// WaveManager가 있는 씬(전투 씬)에서만 동작하고, 메인 화면 씬에서는 아무것도 만들지 않음
public static class GameplayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 전환 시 시간 정지가 남아 있지 않도록 복구 (GDD 8-6 #10)
        Time.timeScale = 1f;

        if (Object.FindAnyObjectByType<WaveManager>() == null) return;

        DamageNumberManager.EnsureCreated();
        ExtractionIndicator.EnsureCreated();
        SupplyCrateManager.EnsureCreated();
        RunResultManager.EnsureCreated();
        PlayerPositionGuard.EnsureOnPlayer();
        SilhouetteRenderer.Attach(GameObject.FindGameObjectWithTag("Player"), new Color(0.3f, 0.85f, 1f, 0.6f));
    }
}
