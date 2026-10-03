using System.Collections;
using UnityEngine;
using JU;

// 플레이어 사망 감지, 통보 연출, 적 디스폰 및 목숨 차감 제어 클래스
public class PlayerDeathHandler : MonoBehaviour
{
    private JUHealth juHealth;
    private bool isProcessingDeath = false;

    private void Awake()
    {
        juHealth = GetComponent<JUHealth>();
    }

    private void Start()
    {
        if (juHealth != null)
        {
            juHealth.OnDeath += OnPlayerDeath;
        }
    }

    private void OnDestroy()
    {
        if (juHealth != null)
        {
            juHealth.OnDeath -= OnPlayerDeath;
        }
    }

    private void OnPlayerDeath()
    {
        if (isProcessingDeath) return;
        StartCoroutine(PlayerDeathSequence());
    }

    // ⭐ 사망 통보 및 대기 연출 시퀀스 코루틴
    private IEnumerator PlayerDeathSequence()
    {
        isProcessingDeath = true;

        // 1. 화면 중앙 사망 통보 메시지 노출
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.ShowNoticeBanner("MISSION FAILED - PLAYER DIED", Color.red);
        }

        // 2. 맵에 남아있는 주변 적들 즉시 디스폰 소멸
        WaveManager.DespawnAllActiveEnemies();

        // 3. 2.5초 대기 연출
        yield return new WaitForSeconds(2.5f);

        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
        }

        // 4. 목숨 차감 및 정비 단계 진입
        bool isGameOver = false;
        if (StageDifficultyManager.Instance != null)
        {
            isGameOver = StageDifficultyManager.Instance.DeductLifeOnPlayerDeath();
        }

        if (isGameOver)
        {
            Debug.Log("PlayerDeathHandler: All lives lost. Game Over.");
            if (StageHUDUI.Instance != null)
            {
                StageHUDUI.Instance.ShowNoticeBanner("GAME OVER - ALL LIVES LOST", Color.red);
            }
            Time.timeScale = 0f;
        }
        else
        {
            if (juHealth != null)
            {
                juHealth.ResetHealth();
            }

            if (MaintenanceManager.Instance != null)
            {
                MaintenanceManager.Instance.EnterMaintenanceStage();
            }
        }

        isProcessingDeath = false;
    }
}