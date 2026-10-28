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

        // 1. 사망 통보 경고 띠 (이번 사망으로 목숨이 다 떨어지면 GAME OVER, 차감은 대기 후 4단계에서)
        int livesLeft = StageDifficultyManager.Instance != null ? StageDifficultyManager.Instance.currentLives - 1 : 1;
        AlertBanner.ShowMissionFailed(livesLeft, 2.3f);

        // 2. 맵에 남아있는 주변 적들 즉시 디스폰 소멸
        WaveManager.EndRoundAndDespawn();

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
            if (RunResultManager.Instance != null)
            {
                RunResultManager.Instance.ShowResult(false);
            }
            else
            {
                if (StageHUDUI.Instance != null)
                {
                    StageHUDUI.Instance.ShowNoticeBanner("게임 오버", new Color(1f, 0.3f, 0.3f));
                }
                Time.timeScale = 0f;
            }
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