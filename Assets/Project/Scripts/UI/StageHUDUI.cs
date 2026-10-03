using UnityEngine;
using TMPro;

// 전투 화면 내 드랍률, 남은 목숨, 탈출 타이머를 실시간 표기하는 HUD UI 클래스
public class StageHUDUI : MonoBehaviour
{
    public static StageHUDUI Instance { get; private set; }

    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI dropChanceText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI extractionTimerText;
    [SerializeField] private TextMeshProUGUI zoneStatusText;

    [Header("Extraction Zone Reference")]
    [SerializeField] private ExtractionZone extractionZone;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (extractionZone == null)
        {
            extractionZone = FindObjectOfType<ExtractionZone>();
        }

        if (StageDifficultyManager.Instance == null)
        {
            Debug.LogWarning("StageHUDUI: StageDifficultyManager instance is missing in scene.");
        }
    }

    private void Update()
    {
        UpdateDropChanceUI();
        UpdateLivesUI();
        UpdateExtractionUI();
    }

    // 실시간 드랍률 % 갱신
    private void UpdateDropChanceUI()
    {
        if (dropChanceText == null) return;

        if (StageDifficultyManager.Instance != null)
        {
            float chancePercent = StageDifficultyManager.Instance.currentDropChance * 100f;
            dropChanceText.text = $"드랍률: {chancePercent:F1}%";
        }
        else
        {
            dropChanceText.text = "드랍률: 35.0%"; // 매니저 부재 시 기본 표기
        }
    }

    // 남은 목숨 수치 갱신
    private void UpdateLivesUI()
    {
        if (livesText == null) return;

        if (StageDifficultyManager.Instance != null)
        {
            int currentLives = StageDifficultyManager.Instance.currentLives;
            int maxLives = StageDifficultyManager.Instance.maxLives;
            livesText.text = $"목숨: {currentLives} / {maxLives}";
        }
        else
        {
            livesText.text = "목숨: 3 / 3"; // 매니저 부재 시 기본 표기
        }
    }

    // 탈출 구역 체류 상태 및 60초 타이머 실시간 갱신
    private void UpdateExtractionUI()
    {
        if (extractionZone != null)
        {
            if (extractionTimerText != null)
            {
                float remTime = extractionZone.RemainingTime;
                extractionTimerText.text = $"탈출 진행: {remTime:F1} 초";
            }

            if (zoneStatusText != null)
            {
                if (extractionZone.IsPlayerInside)
                {
                    zoneStatusText.text = "[ 탈출 구역 내부 : 타이머 작동 중 ]";
                    zoneStatusText.color = Color.green;
                }
                else
                {
                    zoneStatusText.text = "[ 탈출 구역 외부 : 타이머 일시정지 ]";
                    zoneStatusText.color = Color.yellow;
                }
            }
        }
    }
}