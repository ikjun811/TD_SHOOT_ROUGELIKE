using UnityEngine;
using UnityEngine.SceneManagement;

// 배경음 자동 선택 (AudioManager가 같은 오브젝트에 부착, 씬 연결 불필요)
//   메인 화면 → mainMenu / 결과 씬 → resultClear·resultFail
//   전투 씬: 튜토리얼 → tutorial(비면 combat), 최종층 → finalRound(비면 combat), 그 외 combat
//     정비 중: maintenance 곡이 있으면 그 곡, 없으면 전투 곡을 이어 틀며 먹먹하게
//     정비·탈출 연출 중엔 곡을 바꾸지 않음 (15층 진입 곡 전환은 다음 전투 시작 때)
//     결과 화면을 씬 없이 코드로 띄우면(RunResultManager) 결과 곡
public class MusicDirector : MonoBehaviour
{
    private ExtractionZone extractionZone;
    private float nextZoneSearch;

    private void Update()
    {
        SoundLibrary lib = AudioManager.Library;
        string scene = SceneManager.GetActiveScene().name;
        bool muffled = false;

        if (scene == GameSession.MainMenuSceneName)
        {
            AudioManager.PlayMusic(lib.mainMenu);
        }
        else if (scene == GameSession.ResultSceneName)
        {
            AudioManager.PlayMusic(ResultMusic(lib));
        }
        else if (scene == GameSession.GameplaySceneName)
        {
            RunResultManager result = RunResultManager.Instance;
            MaintenanceManager maintenance = MaintenanceManager.Instance;
            if (result != null && result.IsShowing)
            {
                AudioManager.PlayMusic(ResultMusic(lib));
            }
            else if (maintenance != null && maintenance.isInMaintenance)
            {
                if (lib.maintenance.clip != null) AudioManager.PlayMusic(lib.maintenance);
                else
                {
                    muffled = true;
                    if (!AudioManager.IsMusicPlaying) AudioManager.PlayMusic(CombatMusic(lib));
                }
            }
            else if (!IsExtractionSequenceActive() || !AudioManager.IsMusicPlaying)
            {
                AudioManager.PlayMusic(CombatMusic(lib));
            }
        }

        AudioManager.SetMusicMuffled(muffled);
    }

    private static MusicEntry ResultMusic(SoundLibrary lib)
    {
        return RunResultData.Cleared ? lib.resultClear : lib.resultFail;
    }

    private static MusicEntry CombatMusic(SoundLibrary lib)
    {
        if (GameSession.IsTutorial && lib.tutorial.clip != null) return lib.tutorial;
        WaveManager wave = WaveManager.Instance;
        bool finalRound = !GameSession.IsTutorial && wave != null && wave.CurrentRound >= wave.MaxRound;
        if (finalRound && lib.finalRound.clip != null) return lib.finalRound;
        return lib.combat;
    }

    private bool IsExtractionSequenceActive()
    {
        if (extractionZone == null && Time.unscaledTime >= nextZoneSearch)
        {
            extractionZone = FindAnyObjectByType<ExtractionZone>();
            nextZoneSearch = Time.unscaledTime + 1f;
        }
        return extractionZone != null && extractionZone.IsSequenceActive;
    }
}
