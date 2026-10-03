using UnityEngine;
using JU;

// 플레이어 사망 감지, 목숨 차감, 정비 단계 또는 게임오버 제어 클래스
public class PlayerDeathHandler : MonoBehaviour
{
    private JUHealth juHealth;

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

    // 플레이어 체력 0 도달 사망 시
    private void OnPlayerDeath()
    {
        Debug.Log("PlayerDeathHandler: Player died.");

        bool isGameOver = false;

        // 목숨 1 차감
        if (StageDifficultyManager.Instance != null)
        {
            isGameOver = StageDifficultyManager.Instance.DeductLifeOnPlayerDeath();
        }

        if (isGameOver)
        {
            // 목숨 0 소진 시 최종 게임오버
            Debug.Log("PlayerDeathHandler: Game Over. Returning to Main Menu / Show GameOver Panel.");
            Time.timeScale = 0f;
            // TODO: 게임오버 UI 패널 오픈
        }
        else
        {
            // 목숨 남아있음 ➔ 체력 복구 후 정비 단계 진입
            if (juHealth != null)
            {
                juHealth.ResetHealth();
            }

            if (MaintenanceManager.Instance != null)
            {
                MaintenanceManager.Instance.EnterMaintenanceStage();
            }
        }
    }
}