using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 런 기록 수집 + 결과 화면 (클리어 / 게임 오버) — 전투 씬 로드 시 GameplayBootstrap이 자동 생성
// 결과 화면은 코드로 생성 (에디터 작업 불필요). 표시 중에는 시간 정지·플레이어 입력 차단·전투 HUD 숨김
public class RunResultManager : MonoBehaviour
{
    public static RunResultManager Instance { get; private set; }

    // 메인 화면 씬 이름 (빌드 설정에 있을 때만 "메인 화면" 버튼 표시)
    public const string MainMenuSceneName = "MainMenu";

    [Header("Run Stats (Read Only)")]
    [SerializeField] private float combatTime;   // 정비·결과 화면(시간 정지) 제외
    [SerializeField] private int normalKills;
    [SerializeField] private int eliteKills;

    private bool isShowing;

    public static void EnsureCreated()
    {
        if (Instance == null) new GameObject("RunResultManager").AddComponent<RunResultManager>();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (!isShowing && Time.timeScale > 0f) combatTime += Time.deltaTime;
    }

    // WaveManager가 적 처치 시 호출
    public void RecordKill(bool isElite)
    {
        if (isElite) eliteKills++;
        else normalKills++;
    }

    // 최종 라운드 탈출(cleared = true) 또는 목숨 소진(false) 시 호출
    public void ShowResult(bool cleared)
    {
        if (isShowing) return;
        isShowing = true;

        Time.timeScale = 0f;
        if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(false);
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
            StageHUDUI.Instance.SetHUDActive(false);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        BuildResultScreen(cleared);
        Debug.Log($"RunResultManager: {(cleared ? "GAME CLEAR" : "GAME OVER")} — time {FormatTime(combatTime)}, kills {normalKills}/{eliteKills}");
    }

    #region UI

    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    private static readonly Color ClearColor = new Color(1f, 0.82f, 0.2f);
    private static readonly Color GameOverColor = new Color(1f, 0.3f, 0.3f);

    private void BuildResultScreen(bool cleared)
    {
        EnsureEventSystem();

        GameObject canvasObj = new GameObject("ResultScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObj.GetComponent<RectTransform>();

        // 화면 전체 어둡게
        RectTransform dim = CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim);

        // 가운데 패널
        RectTransform panel = CreateImage("Panel", root, PanelColor);
        panel.sizeDelta = new Vector2(760f, 640f);

        CreateText("Title", panel, cleared ? "GAME CLEAR" : "GAME OVER", 64f, cleared ? ClearColor : GameOverColor,
                   new Vector2(0f, 250f), new Vector2(700f, 90f), FontStyles.Bold);

        CreateText("Stats", panel, BuildStatsText(), 30f, Color.white,
                   new Vector2(0f, 20f), new Vector2(660f, 340f), FontStyles.Normal, TextAlignmentOptions.TopLeft);

        bool hasMainMenu = Application.CanStreamedLevelBeLoaded(MainMenuSceneName);
        float buttonX = hasMainMenu ? -150f : 0f;

        CreateButton("RestartButton", panel, "다시 시작", new Vector2(buttonX, -250f), RestartRun);
        if (hasMainMenu) CreateButton("MainMenuButton", panel, "메인 화면", new Vector2(150f, -250f), GoToMainMenu);
    }

    private string BuildStatsText()
    {
        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        int maxRound = WaveManager.Instance != null ? WaveManager.Instance.MaxRound : 15;

        string text = $"도달 라운드   {round} / {maxRound}\n" +
                      $"전투 시간     {FormatTime(combatTime)}\n" +
                      $"처치 수       일반 {normalKills}  /  엘리트 {eliteKills}\n\n" +
                      "<b>최종 빌드</b>\n";

        // 장착 무기 (레어도 색)
        if (PlayerInventory.Instance != null)
        {
            for (int i = 0; i < PlayerInventory.Instance.equippedWeapons.Length; i++)
            {
                WeaponDataSO w = PlayerInventory.Instance.equippedWeapons[i];
                string weaponText = w != null
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(w.GetRarityColor())}>{w.weaponName} [{GetRarityName(w.rarity)}]</color>"
                    : "<color=#888888>비어 있음</color>";
                text += $"무기 {i + 1}        {weaponText}\n";
            }
        }

        // 그리드 모듈 수 / 레전더리 수
        int moduleCount = 0;
        int legendaryCount = 0;
        if (GridInventorySystem.Instance != null)
        {
            foreach (var pm in GridInventorySystem.Instance.placedModules)
            {
                if (pm == null || pm.moduleData == null) continue;
                moduleCount++;
                if (pm.moduleData.rarity == RarityType.Legendary) legendaryCount++;
            }
        }
        text += $"모듈          {moduleCount}개 (레전더리 {legendaryCount}개)";

        return text;
    }

    private static string GetRarityName(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => "일반",
            RarityType.Rare => "희귀",
            RarityType.Elite => "엘리트",
            RarityType.Legendary => "레전더리",
            _ => rarity.ToString()
        };
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateImage(string objName, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string objName, RectTransform parent, string content, float size, Color color,
                                              Vector2 position, Vector2 boxSize, FontStyles style,
                                              TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void CreateButton(string objName, RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateImage(objName, parent, new Color(0.25f, 0.25f, 0.3f, 1f));
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 70f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.5f, 1f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateText("Label", rect, label, 32f, Color.white, Vector2.zero, rect.sizeDelta, FontStyles.Bold);
    }

    #endregion

    private void RestartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }
}
