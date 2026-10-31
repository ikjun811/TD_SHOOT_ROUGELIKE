using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메인 화면 씬 생성 (Tools > TD Shoot > Create Main Menu Scene)
//   Assets/Scenes/MainMenu.unity: 카메라(단색 배경, 추후 3D 배경용 조명 포함)·EventSystem·Canvas(1920x1080)
//   제목 + 왼쪽 메뉴 버튼 6개 + 패널 3개(게임 정보·조작 정보·환경설정, 닫기 버튼) + MainMenuUI 연결, PixelUISciFi 테마(UIThemeSkin)
//   빌드 설정 맨 앞에 등록. 이미 있으면 덮어쓰지 않음 (만든 뒤에는 에디터에서 자유롭게 수정)
public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    internal const string GameTitle = "DEPTH ZERO"; // 사용자 채택 2026-10-30 (지하 시설에서 지상(깊이 0)까지 한 층씩 탈출)

    // 게임 정보 (사용자 제공 2026-10-31). 기존 씬 글은 Tools > TD Shoot > Apply Game Info To Main Menu로 교체
    internal const string InfoText =
        "<b>졸업작품 정보</b>\n" +
        "홍익대학교 세종캠퍼스 게임소프트웨어학과\n" +
        "제작  이익준 (C093196)\n" +
        "지도교수  강신진 교수님\n" +
        "제작 기간  약 4개월\n" +
        "GitHub  github.com/ikjun811/TD_SHOOT_ROUGELIKE\n\n" +
        "<b>개발 환경</b>\n" +
        "Unity 6 (URP)\n\n" +
        "<b>사용 에셋</b>\n" +
        "캐릭터·적  Horrors(pack), SciFi Space Soldier\n" +
        "맵  SciFi TopDown SpaceStation\n" +
        "이동·사격 기반  Julhiecio TPS Controller\n" +
        "UI  PixelUISciFiFree\n" +
        "이펙트  Vefects Flipbook VFX Bundle Lite\n" +
        "폰트  갈무리11, Pretendard (SIL Open Font License)\n" +
        "배경음  Pixabay — eisenvater, brvhrtz, fnx_sound, KK Sample / Freesound — timbre";

    private const string ControlsText =
        "<b>전투</b>\n" +
        "이동  WASD\n" +
        "조준  마우스\n" +
        "사격  마우스 왼쪽\n" +
        "대시  Space\n" +
        "재장전  R\n" +
        "무기 교체  E\n" +
        "수류탄  마우스 오른쪽 클릭\n\n" +
        "<b>정비</b>\n" +
        "아이템 집기·놓기  마우스 왼쪽 클릭\n" +
        "모듈 회전  R\n" +
        "들고 있는 아이템 되돌리기  마우스 오른쪽\n" +
        "장착 즉시 해제(가방으로)  마우스 휠 클릭\n" +
        "상세 정보  아이템 위에 마우스 올리기\n\n" +
        GoalText;

    // 조작 정보의 '목표' 문단 (깊이 표시 D-4, DEPTH ZERO 컨셉 — 마지막 문장은 사용자 채택 문구). MainMenuStyleTools가 기존 씬 글의 목표 문단도 이것으로 교체
    internal const string GoalText =
        "<b>목표</b>\n" +
        "괴물이 침입한 지하 시설(DEPTH 15)에서 한 층씩 올라가 지상(DEPTH 0)에 도달하세요.\n" +
        "각 층의 탈출 구역에서 버티면 다음 층으로 올라갑니다. 정비 시간에는 그리드에 모듈을 배치해 무기를 강화하세요.\n" +
        "지상에 가까워질수록 더 많은 적을 상대해야 할 것입니다.";

    [MenuItem("Tools/TD Shoot/Create Main Menu Scene")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorUtility.DisplayDialog("Main Menu", $"{ScenePath}가 이미 있습니다. 다시 만들려면 먼저 삭제하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라·조명 (배경은 추후 3D 모델 배치 예정)
        GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0f, 1.5f, -6f);
        Camera cam = camObj.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
        GameObject lightObj = new GameObject("Directional Light", typeof(Light));
        lightObj.GetComponent<Light>().type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas
        GameObject canvasObj = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f; // 정비 화면 캔버스와 같은 기준 (UITheme pixelScale 3 전제)
        RectTransform canvas = canvasObj.GetComponent<RectTransform>();

        // 제목
        TextMeshProUGUI title = CreateText("Title", canvas, GameTitle, 96f, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -140f), new Vector2(1400f, 130f), new Vector2(0f, 1f));

        // 메뉴 버튼 (왼쪽 세로 목록)
        GameObject menuObj = new GameObject("Menu", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform menu = menuObj.GetComponent<RectTransform>();
        menu.SetParent(canvas, false);
        SetRect(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -60f), new Vector2(440f, 6 * 72f + 5 * 16f), new Vector2(0f, 0.5f));
        VerticalLayoutGroup layout = menuObj.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Button start = CreateButton("StartButton", menu, "게임 시작");
        Button tutorial = CreateButton("TutorialButton", menu, "튜토리얼");
        Button info = CreateButton("InfoButton", menu, "게임 정보");
        Button controls = CreateButton("ControlsButton", menu, "조작 정보");
        Button settings = CreateButton("SettingsButton", menu, "환경설정");
        Button quit = CreateButton("QuitButton", menu, "게임 종료");

        // 패널
        List<Button> closeButtons = new List<Button>();
        GameObject infoPanel = CreatePanel("InfoPanel", canvas, "게임 정보", InfoText, closeButtons);
        GameObject controlsPanel = CreatePanel("ControlsPanel", canvas, "조작 정보", ControlsText, closeButtons);
        GameObject settingsPanel = CreatePanel("SettingsPanel", canvas, "환경설정", "", closeButtons);
        BuildSettingsControls(settingsPanel);

        // MainMenuUI 연결
        MainMenuUI menuUI = canvasObj.AddComponent<MainMenuUI>();
        SerializedObject so = new SerializedObject(menuUI);
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("tutorialButton").objectReferenceValue = tutorial;
        so.FindProperty("infoButton").objectReferenceValue = info;
        so.FindProperty("controlsButton").objectReferenceValue = controls;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("infoPanel").objectReferenceValue = infoPanel;
        so.FindProperty("controlsPanel").objectReferenceValue = controlsPanel;
        so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        SerializedProperty closes = so.FindProperty("closeButtons");
        closes.arraySize = closeButtons.Count;
        for (int i = 0; i < closeButtons.Count; i++) closes.GetArrayElementAtIndex(i).objectReferenceValue = closeButtons[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettingsFirst(ScenePath);
        Debug.Log($"[MainMenu] {ScenePath} 생성, 빌드 설정 맨 앞에 등록");
    }

    // 이미 만든 MainMenu 씬의 환경설정 패널에 해상도·음량 컨트롤 추가 (씬 저장은 직접)
    [MenuItem("Tools/TD Shoot/Add Settings Controls To Main Menu")]
    public static void AddSettingsControlsToOpenScene()
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        GameObject panel = menuUI != null ? new SerializedObject(menuUI).FindProperty("settingsPanel").objectReferenceValue as GameObject : null;
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Settings Controls", "열린 씬에서 MainMenuUI의 Settings Panel을 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }
        if (panel.GetComponent<SettingsPanelUI>() != null)
        {
            EditorUtility.DisplayDialog("Settings Controls", "이미 SettingsPanelUI가 있습니다. 다시 만들려면 먼저 제거하세요.", "확인");
            return;
        }

        BuildSettingsControls(panel);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        Debug.Log("[MainMenu] 환경설정 컨트롤 추가 (해상도·음량 3개). 씬 저장은 직접.");
    }

    // 패널 본문 자리에 해상도 드롭다운 + 음량 슬라이더 3줄, SettingsPanelUI 연결
    private static void BuildSettingsControls(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        Transform body = rect.Find("Body");
        if (body != null) body.gameObject.SetActive(false); // 안내 글 자리 → 컨트롤로 대체

        TMP_DefaultControls.Resources tmpResources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
        DefaultControls.Resources uiResources = new DefaultControls.Resources
        {
            standard = tmpResources.standard,
            background = tmpResources.background,
            knob = tmpResources.knob,
            checkmark = tmpResources.checkmark,
            dropdown = tmpResources.dropdown,
            mask = tmpResources.mask
        };

        const float rowTop = -170f, rowHeight = 110f;
        Color accent = UITheme.Current.gridHighlightBorderColor;

        // 해상도
        CreateRowLabel(rect, "ResolutionLabel", "해상도", rowTop);
        GameObject dropdownObj = TMP_DefaultControls.CreateDropdown(tmpResources);
        dropdownObj.name = "ResolutionDropdown";
        RectTransform dropdownRect = dropdownObj.GetComponent<RectTransform>();
        dropdownRect.SetParent(rect, false);
        SetRect(dropdownRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f, rowTop), new Vector2(520f, 60f), new Vector2(0f, 0.5f));
        TMP_Dropdown dropdown = dropdownObj.GetComponent<TMP_Dropdown>();
        StyleDropdown(dropdown);

        // 음량
        string[] names = { "Master", "Sfx", "Music" };
        string[] labels = { "전체 음량", "효과음", "배경음" };
        Slider[] sliders = new Slider[3];
        TextMeshProUGUI[] values = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            float y = rowTop - rowHeight * (i + 1);
            CreateRowLabel(rect, names[i] + "Label", labels[i], y);

            GameObject sliderObj = DefaultControls.CreateSlider(uiResources);
            sliderObj.name = names[i] + "Slider";
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.SetParent(rect, false);
            SetRect(sliderRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f, y), new Vector2(520f, 28f), new Vector2(0f, 0.5f));
            Slider slider = sliderObj.GetComponent<Slider>();
            if (slider.fillRect != null) slider.fillRect.GetComponent<Image>().color = accent;
            if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(36f, 0f);
            sliders[i] = slider;

            TextMeshProUGUI value = CreateText(names[i] + "Value", rect, "100%", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetRect(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(930f, y), new Vector2(140f, 60f), new Vector2(0f, 0.5f));
            values[i] = value;
        }

        SettingsPanelUI ui = panel.AddComponent<SettingsPanelUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("resolutionDropdown").objectReferenceValue = dropdown;
        so.FindProperty("masterSlider").objectReferenceValue = sliders[0];
        so.FindProperty("sfxSlider").objectReferenceValue = sliders[1];
        so.FindProperty("musicSlider").objectReferenceValue = sliders[2];
        so.FindProperty("masterValueText").objectReferenceValue = values[0];
        so.FindProperty("sfxValueText").objectReferenceValue = values[1];
        so.FindProperty("musicValueText").objectReferenceValue = values[2];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateRowLabel(RectTransform panel, string name, string label, float y)
    {
        TextMeshProUGUI text = CreateText(name, panel, label, 32f, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(300f, 60f), new Vector2(0f, 0.5f));
    }

    // 기본 드롭다운(160x30)을 메인 화면 크기에 맞게: 글자 28, 펼친 목록 높이 400·항목 48
    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown.captionText != null) dropdown.captionText.fontSize = 28f;
        if (dropdown.itemText != null) dropdown.itemText.fontSize = 26f;

        RectTransform template = dropdown.template;
        if (template == null) return;
        template.sizeDelta = new Vector2(template.sizeDelta.x, 400f);

        Transform item = template.Find("Viewport/Content/Item");
        if (item is RectTransform itemRect) itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 48f);
        Transform content = template.Find("Viewport/Content");
        if (content is RectTransform contentRect) contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 48f);
    }

    internal static Button CreateButton(string name, RectTransform parent, string label)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIThemeSkin));
        obj.transform.SetParent(parent, false);
        UIThemeSkin skin = obj.GetComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.Button;
        skin.Apply();

        TextMeshProUGUI text = CreateText("Label", obj.GetComponent<RectTransform>(), label, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f);
        return obj.GetComponent<Button>();
    }

    // 가운데 창 패널: 제목 + 본문(위 왼쪽 정렬) + 오른쪽 아래 닫기
    private static GameObject CreatePanel(string name, RectTransform parent, string titleText, string body, List<Button> closeButtons)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIThemeSkin));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 0f), new Vector2(1100f, 760f), new Vector2(0.5f, 0.5f));
        UIThemeSkin skin = panel.GetComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.Window;
        skin.Apply();

        TextMeshProUGUI title = CreateText("Title", rect, titleText, 48f, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-120f, 70f), new Vector2(0.5f, 1f));

        TextMeshProUGUI text = CreateText("Body", rect, body, 28f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        Stretch(text.rectTransform, 0f);
        text.rectTransform.offsetMin = new Vector2(60f, 120f);
        text.rectTransform.offsetMax = new Vector2(-60f, -130f);
        text.gameObject.AddComponent<ScrollableText>(); // 글이 길면 스크롤

        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIThemeSkin));
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.SetParent(rect, false);
        SetRect(closeRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(200f, 64f), new Vector2(1f, 0f));
        UIThemeSkin closeSkin = closeObj.GetComponent<UIThemeSkin>();
        closeSkin.role = UIThemeSkin.Role.Button;
        closeSkin.Apply();
        TextMeshProUGUI closeLabel = CreateText("Label", closeRect, "닫기", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform, 0f);
        closeButtons.Add(closeObj.GetComponent<Button>());

        panel.SetActive(false);
        return panel;
    }

    internal static TextMeshProUGUI CreateText(string name, RectTransform parent, string content, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    internal static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    internal static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void AddToBuildSettingsFirst(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
