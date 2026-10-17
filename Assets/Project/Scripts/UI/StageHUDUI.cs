using UnityEngine;
using TMPro;

// 전투 화면 내 드랍률, 남은 목숨, 탈출 타이머를 실시간 표기하는 HUD UI 클래스
public class StageHUDUI : MonoBehaviour
{
    public static StageHUDUI Instance { get; private set; }

    [Header("UI Canvas Root")]
    [SerializeField] private GameObject hudCanvasObject;

    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI dropChanceText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI extractionTimerText;
    [SerializeField] private TextMeshProUGUI zoneStatusText;
    [SerializeField] private TextMeshProUGUI noticeBannerText;

    [Header("Extraction Zone Reference")]
    [SerializeField] private ExtractionZone extractionZone;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (hudCanvasObject == null)
        {
            hudCanvasObject = gameObject;
        }
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

    // 전투 HUD On/Off 제어 함수
    public void SetHUDActive(bool isActive)
    {
        if (hudCanvasObject != null)
        {
            hudCanvasObject.SetActive(isActive);
        }
    }

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

    // 일정 시간 뒤 자동으로 사라지는 알림 (그 사이 다른 배너로 바뀌었으면 숨기지 않음)
    public void ShowTimedNotice(string message, Color textColor, float duration)
    {
        if (noticeBannerText == null) return;

        // 탈출 성공·사망 배너가 떠 있으면 덮어쓰지 않음
        if (noticeBannerText.gameObject.activeSelf && noticeBannerText.text != message) return;

        ShowNoticeBanner(message, textColor);
        StartCoroutine(HideNoticeAfter(message, duration));
    }

    private System.Collections.IEnumerator HideNoticeAfter(string message, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (noticeBannerText != null && noticeBannerText.text == message) HideNoticeBanner();
    }

    private void UpdateDropChanceUI()
    {
        if (dropChanceText == null) return;

        if (StageDifficultyManager.Instance != null)
        {
            float chancePercent = StageDifficultyManager.Instance.currentDropChance * 100f;
            dropChanceText.text = $"드랍률: {chancePercent:F1}%";
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