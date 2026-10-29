using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JU;

// 인게임 일시정지 메뉴 (사용자 요청 2026-10-29: ESC 일시정지만 되고 게임 종료 등이 불가) — GameplayBootstrap이 생성
//   ESC: 열기 / (확인 창·설정이면) 뒤로 / 닫기. 메뉴 = 계속하기·환경설정·메인 화면으로·게임 종료 (뒤 둘은 확인 창)
//   JU 일시정지(씬 JU TopDown User Interface 프리팹의 JUPauseGame — timeScale 토글만)는 실행 중 ControlsEnabled 끔 (JU 비수정)
//   열 수 없는 때: 정비·결과(timeScale 0), 탈출 성공 연출, 플레이어 사망 연출. 히트 스톱 중(timeScale 0~1)에 누르면 끝난 뒤 열림
//   열면: 직전 timeScale 기억 후 0, 소리 일시정지, 커서 보이기, 플레이어 조작 끔 / 닫으면 되돌리고 조작은 마우스를 뗀 뒤 켬(닫기 클릭이 사격되지 않게)
//   UI는 코드 생성, 창·버튼은 UITheme 픽셀 스킨(UIThemeSkin), 환경설정은 SettingsPanelUI 재사용
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.paused;

    private const float PendingWindow = 1.5f; // 히트 스톱 중 누른 ESC를 기다려 주는 시간 (실제 시간)

    private bool paused;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private float pendingUntil = -1f;
    private JUTPS.JUPauseGame juPause;
    private JUHealth playerHealth;
    private ExtractionZone extractionZone;

    private GameObject root;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private GameObject confirmPanel;
    private TextMeshProUGUI confirmText;
    private System.Action confirmAction;

    public static PauseMenuUI EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("PauseMenuUI");
        return go.AddComponent<PauseMenuUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Build();
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (paused) AudioListener.pause = false; // 씬 전환 등으로 사라질 때 소리 정지가 남지 않게
        Instance = null;
    }

    private void Start()
    {
        DisableJUPause();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<JUHealth>();
        extractionZone = FindAnyObjectByType<ExtractionZone>();
    }

    private void DisableJUPause()
    {
        if (juPause == null) juPause = FindAnyObjectByType<JUTPS.JUPauseGame>(FindObjectsInactive.Include);
        if (juPause != null) juPause.ControlsEnabled = false;
    }

    private void Update()
    {
        DisableJUPause(); // JU 쪽에서 다시 켜는 경우 대비 (찾은 뒤엔 값만 씀)

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (paused) Back();
            else if (CanPause()) Open();
            else if (CanPauseLater()) pendingUntil = Time.unscaledTime + PendingWindow;
        }

        // 히트 스톱이 끝나면(timeScale 1) 기다리던 일시정지 열기
        if (!paused && pendingUntil > 0f)
        {
            if (Time.unscaledTime > pendingUntil || !CanPauseLater()) pendingUntil = -1f;
            else if (CanPause()) { pendingUntil = -1f; Open(); }
        }
    }

    // 지금 바로 열 수 있는지: 정상 진행(timeScale 1)이고 연출·사망·결과 중이 아님
    private bool CanPause()
    {
        return Mathf.Approximately(Time.timeScale, 1f) && CanPauseLater();
    }

    // 잠깐 뒤(히트 스톱 끝)라면 열 수 있는지
    private bool CanPauseLater()
    {
        if (Time.timeScale <= 0f) return false; // 정비·결과 화면
        if (extractionZone != null && extractionZone.IsSequenceActive) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (RunResultManager.Instance != null && RunResultManager.Instance.IsShowing) return false;
        return true;
    }

    #region 열기·닫기

    private void Open()
    {
        paused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(false);
        EnsureEventSystem();

        root.SetActive(true);
        ShowPanel(mainPanel);
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        root.SetActive(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        AudioListener.pause = false;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLock;
        if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(true); // 마우스를 뗀 뒤 켜짐
    }

    // ESC: 확인 창·설정이면 메인 메뉴로, 메인 메뉴면 닫기
    private void Back()
    {
        if (mainPanel.activeSelf) Resume();
        else ShowPanel(mainPanel);
    }

    private void ShowPanel(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        confirmPanel.SetActive(panel == confirmPanel);
    }

    private void AskConfirm(string message, System.Action action)
    {
        confirmText.text = message;
        confirmAction = action;
        ShowPanel(confirmPanel);
    }

    private void GoToMainMenu()
    {
        LeavePause();
        GameSession.LoadMainMenu();
    }

    private void QuitGame()
    {
        LeavePause();
        GameSession.QuitGame();
    }

    // 씬을 떠나기 전 일시정지 상태 정리 (timeScale은 GameSession이 1로)
    private void LeavePause()
    {
        paused = false;
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Debug.Log("[PauseMenuUI] EventSystem이 없어 생성했습니다.", es);
    }

    #endregion

    #region 생성

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // HUD·알림 위, 화면 암전(100) 아래
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        Stretch(rootRect);

        // 어두운 배경 (뒤 화면 클릭 막음)
        Image dim = CreateImage("Dim", rootRect, new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        BuildMainPanel(rootRect);
        BuildSettingsPanel(rootRect);
        BuildConfirmPanel(rootRect);
    }

    private void BuildMainPanel(RectTransform parent)
    {
        RectTransform panel = CreateWindow("MainPanel", parent, new Vector2(520f, 560f), "일시정지");
        mainPanel = panel.gameObject;

        string[] labels = { "계속하기", "환경설정", "메인 화면으로", "게임 종료" };
        System.Action[] actions =
        {
            Resume,
            () => ShowPanel(settingsPanel),
            () => AskConfirm("메인 화면으로 나갈까요?\n<size=24><color=#BBBBBB>이번 게임의 진행 상황은 사라집니다.</color></size>", GoToMainMenu),
            () => AskConfirm("게임을 종료할까요?\n<size=24><color=#BBBBBB>이번 게임의 진행 상황은 사라집니다.</color></size>", QuitGame)
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Button button = CreateButton(labels[i], panel, labels[i], 32f);
            Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -150f - i * 92f), new Vector2(380f, 72f));
            System.Action action = actions[i];
            button.onClick.AddListener(() => action());
        }
    }

    private void BuildSettingsPanel(RectTransform parent)
    {
        RectTransform panel = CreateWindow("SettingsPanel", parent, new Vector2(1000f, 620f), "환경설정");
        settingsPanel = panel.gameObject;
        settingsPanel.SetActive(false); // SettingsPanelUI.OnEnable이 연결 전에 불리지 않게 꺼 둔 채로 구성

        UITheme theme = UITheme.Current;
        Color accent = theme.gridHighlightBorderColor;
        const float rowTop = -150f, rowHeight = 100f;

        CreateLabel(panel, "해상도", rowTop);
        TMP_DefaultControls.Resources tmpResources = new TMP_DefaultControls.Resources { standard = theme.insetPanelSprite, background = theme.insetPanelSprite };
        GameObject dropdownObj = TMP_DefaultControls.CreateDropdown(tmpResources);
        dropdownObj.name = "ResolutionDropdown";
        RectTransform dropdownRect = dropdownObj.GetComponent<RectTransform>();
        dropdownRect.SetParent(panel, false);
        PlaceLeft(dropdownRect, new Vector2(340f, rowTop), new Vector2(480f, 56f));
        TMP_Dropdown dropdown = dropdownObj.GetComponent<TMP_Dropdown>();
        StyleDropdown(dropdown);

        string[] labels = { "전체 음량", "효과음", "배경음" };
        Slider[] sliders = new Slider[3];
        TextMeshProUGUI[] values = new TextMeshProUGUI[3];
        DefaultControls.Resources uiResources = new DefaultControls.Resources { standard = null, background = theme.insetPanelSprite };
        for (int i = 0; i < 3; i++)
        {
            float y = rowTop - rowHeight * (i + 1);
            CreateLabel(panel, labels[i], y);

            GameObject sliderObj = DefaultControls.CreateSlider(uiResources);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.SetParent(panel, false);
            PlaceLeft(sliderRect, new Vector2(340f, y), new Vector2(480f, 26f));
            Slider slider = sliderObj.GetComponent<Slider>();
            if (slider.fillRect != null) slider.fillRect.GetComponent<Image>().color = accent;
            if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(30f, 0f);
            sliders[i] = slider;

            TextMeshProUGUI value = CreateText("Value", panel, 28f, TextAlignmentOptions.Left);
            value.fontStyle = FontStyles.Bold;
            PlaceLeft(value.rectTransform, new Vector2(850f, y), new Vector2(120f, 50f));
            values[i] = value;
        }

        SettingsPanelUI settings = settingsPanel.AddComponent<SettingsPanelUI>();
        settings.Configure(dropdown, sliders[0], sliders[1], sliders[2], values[0], values[1], values[2]);

        Button back = CreateButton("Back", panel, "뒤로", 28f);
        Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(220f, 64f));
        back.onClick.AddListener(() => ShowPanel(mainPanel));
    }

    private void BuildConfirmPanel(RectTransform parent)
    {
        RectTransform panel = CreateWindow("ConfirmPanel", parent, new Vector2(640f, 360f), "확인");
        confirmPanel = panel.gameObject;

        confirmText = CreateText("Message", panel, 32f, TextAlignmentOptions.Center);
        confirmText.textWrappingMode = TextWrappingModes.Normal;
        Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(560f, 110f));

        Button yes = CreateButton("Yes", panel, "확인", 28f);
        Place(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-130f, 80f), new Vector2(220f, 64f));
        yes.onClick.AddListener(() => confirmAction?.Invoke());

        Button no = CreateButton("No", panel, "취소", 28f);
        Place(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(130f, 80f), new Vector2(220f, 64f));
        no.onClick.AddListener(() => ShowPanel(mainPanel));
    }

    // 가운데 창 (테마 Window 스프라이트) + 위쪽 제목
    private RectTransform CreateWindow(string name, RectTransform parent, Vector2 size, string title)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.08f, 0.1f, 0.13f, 0.97f);
        UIThemeSkin skin = obj.AddComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.Window;
        skin.Apply();

        TextMeshProUGUI titleText = CreateText("Title", rect, 42f, TextAlignmentOptions.Center);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(size.x - 80f, 60f));
        return rect;
    }

    private static Button CreateButton(string name, RectTransform parent, string label, float fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
        UIThemeSkin skin = obj.AddComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.Button;
        skin.Apply();

        TextMeshProUGUI text = CreateText("Label", obj.GetComponent<RectTransform>(), fontSize, TextAlignmentOptions.Center);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return obj.GetComponent<Button>();
    }

    private static void CreateLabel(RectTransform panel, string label, float y)
    {
        TextMeshProUGUI text = CreateText("Label", panel, 30f, TextAlignmentOptions.Left);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        PlaceLeft(text.rectTransform, new Vector2(70f, y), new Vector2(260f, 56f));
    }

    // 기본 드롭다운(160x30)을 창 크기에 맞게
    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown.captionText != null) dropdown.captionText.fontSize = 26f;
        if (dropdown.itemText != null) dropdown.itemText.fontSize = 24f;
        RectTransform template = dropdown.template;
        if (template == null) return;
        template.sizeDelta = new Vector2(template.sizeDelta.x, 360f);
        if (template.Find("Viewport/Content/Item") is RectTransform item) item.sizeDelta = new Vector2(item.sizeDelta.x, 44f);
        if (template.Find("Viewport/Content") is RectTransform content) content.sizeDelta = new Vector2(content.sizeDelta.x, 44f);
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 부모 위쪽 가운데 기준
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 부모 왼쪽 위 기준, 세로 가운데 정렬
    private static void PlaceLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    #endregion
}
