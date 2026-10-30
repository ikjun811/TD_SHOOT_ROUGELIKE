using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메인 화면 (MainMenu 씬): 에디터에서 배치한 버튼·패널을 연결만 함 (GDD 8-6 #10 UI 원칙)
//   게임 시작 / 튜토리얼 / 게임 정보 / 조작 정보 / 환경설정 / 게임 종료
//   정보·조작·설정은 패널 하나씩 열고, 닫기 버튼 또는 Esc로 닫음
//   씬은 Tools > TD Shoot > Create Main Menu Scene으로 만들 수 있음 (만든 뒤 자유롭게 수정)
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Panels")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("패널 안의 닫기 버튼들")]
    [SerializeField] private Button[] closeButtons;

    [Header("Tutorial")]
    [Tooltip("끄면 버튼 비활성 + 문구 (튜토리얼 구현 완료 2026-10-30 — 씬에 저장된 옛 '준비 중' 값이 덮지 않도록 필드 이름 변경)")]
    [SerializeField] private bool tutorialEnabled = true;
    [SerializeField] private string tutorialUnavailableLabel = "튜토리얼 (준비 중)";
    [Tooltip("훈련을 한 번 마쳤으면 버튼 글자 뒤에 붙임")]
    [SerializeField] private string tutorialClearedSuffix = "   <size=24><alpha=#99>CLEAR</size>";

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true; // 게임에서 넘어올 때 커서가 숨겨지거나 잠겨 있을 수 있음
        Cursor.lockState = CursorLockMode.None;

        Bind(startButton, () => GameSession.StartGame(GameMode.Normal));
        Bind(tutorialButton, () => GameSession.StartGame(GameMode.Tutorial));
        Bind(infoButton, () => OpenPanel(infoPanel));
        Bind(controlsButton, () => OpenPanel(controlsPanel));
        Bind(settingsButton, () => OpenPanel(settingsPanel));
        Bind(quitButton, GameSession.QuitGame);
        if (closeButtons != null)
        {
            foreach (Button close in closeButtons) Bind(close, CloseAllPanels);
        }

        if (tutorialButton != null)
        {
            TextMeshProUGUI label = tutorialButton.GetComponentInChildren<TextMeshProUGUI>();
            if (!tutorialEnabled)
            {
                tutorialButton.interactable = false;
                if (label != null && !string.IsNullOrEmpty(tutorialUnavailableLabel)) label.text = tutorialUnavailableLabel;
            }
            else if (label != null)
            {
                // 씬 글자가 옛 "SOON" 문구면 정리 후, 완료했으면 CLEAR 표시
                label.text = "튜토리얼" + (TutorialDirector.IsCompleted ? tutorialClearedSuffix : "");
            }
        }

        CloseAllPanels();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) CloseAllPanels();
    }

    public void OpenPanel(GameObject panel)
    {
        CloseAllPanels();
        if (panel != null) panel.SetActive(true);
    }

    public void CloseAllPanels()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
