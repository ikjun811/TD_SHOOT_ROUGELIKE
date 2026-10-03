using System.Collections;
using UnityEngine;
using TMPro;

// 전투 화면 내 드랍률, 남은 목숨, 탈출 타이머 및 중앙 통보 안내를 표기하는 HUD UI 클래스
public class StageHUDUI : MonoBehaviour
{
    public static StageHUDUI Instance { get; private set; }

    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI dropChanceText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI extractionTimerText;
    [SerializeField] private TextMeshProUGUI zoneStatusText;
    [SerializeField] private TextMeshProUGUI noticeBannerText; // ⭐ 화면 중앙 통보 메시지 텍스트

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

        if (noticeBannerText != null)
        {
            noticeBannerText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateDropChanceUI();
        UpdateLivesUI();
        UpdateExtractionUI();
    }

    // ⭐ 화면 중앙 통보 메시지 띄우기 함수
    public void ShowNoticeBanner(string message, Color textColor)
    {
        if (noticeBannerText == null) return;

        noticeBannerText.gameObject.SetActive(true);
        noticeBannerText.text = message;
        noticeBannerText.color = textColor;
    }

    public void HideNoticeBanner()
    {
        if (noticeBannerText != null)
        {
            noticeBannerText.gameObject.SetActive(false);
        }
    }

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
            dropChanceText.text = "드랍률: 35.0%";
        }
    }

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
            livesText.text = "목숨: 3 / 3";
        }
    }

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