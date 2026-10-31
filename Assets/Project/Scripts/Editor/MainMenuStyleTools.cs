using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메인 화면 미니멀 스타일 (사용자 채택 2026-10-30 A안 "글자 목록" — 기본·정석 느낌 대신 깔끔·심플, 전투 HUD처럼 선과 글자만)
//   Tools > TD Shoot > Restyle Main Menu (Minimal): 열린 MainMenu 씬을 바꿈 (Undo 가능, 씬 저장은 직접)
//   - 제목: 84·자간, 아래 청록 선 + 부제 "ESCAPE TO THE SURFACE", 조작 정보의 목표 문단을 DEPTH ZERO 컨셉 글로 교체
//   - 메뉴 버튼 6개: 테두리 스킨 제거 → 왼쪽 정렬 글자만 + MenuItemHighlight(마우스 올리면 청록·왼쪽 막대·살짝 밀림), 튜토리얼 "SOON"
//   - 패널 3개: 테두리 창 → 테두리 없는 반투명 남색 + 왼쪽 청록 세로선 + 제목 아래 흐린 선, 닫기도 글자 버튼
//   - 패널은 제목과 겹치지 않게 오른쪽 아래(300,-70) 1100x640, 버전·졸업작품 표시는 넣지 않음(사용자, 있으면 제거)
//   여러 번 실행해도 같은 결과 (이미 만든 선·막대는 이름으로 찾아 재사용)
public static class MainMenuStyleTools
{
    private const string Subtitle = "ESCAPE TO THE SURFACE"; // 깊이 표시 D-4 (이전 TOP-DOWN EXTRACTION ROGUELIKE)
    internal static readonly Color PanelColor = new Color(0.02f, 0.035f, 0.05f, 0.92f);
    internal static readonly Color DimText = new Color(0.55f, 0.6f, 0.66f);

    [MenuItem("Tools/TD Shoot/Restyle Main Menu (Minimal)")]
    public static void Restyle()
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menuUI == null)
        {
            EditorUtility.DisplayDialog("Main Menu", "열린 씬에서 MainMenuUI를 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }

        UITheme theme = UIThemeTools.GetOrCreateTheme();
        Color accent = theme.gridHighlightBorderColor;
        RectTransform canvas = menuUI.GetComponent<RectTransform>();
        SerializedObject so = new SerializedObject(menuUI);
        Undo.RegisterFullObjectHierarchyUndo(menuUI.gameObject, "Restyle Main Menu");

        // 1. 제목 + 선 + 부제
        Transform titleTransform = canvas.Find("Title");
        if (titleTransform != null && titleTransform.TryGetComponent(out TextMeshProUGUI title))
        {
            theme.ApplyFont(title, UIFontRole.Heading);
            title.text = MainMenuSceneBuilder.GameTitle;
            title.fontSize = 84f;
            title.characterSpacing = 6f;
            title.color = Color.white;
            MainMenuSceneBuilder.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -150f), new Vector2(1400f, 110f), new Vector2(0f, 1f));

            Image line = GetOrCreateImage(canvas, "TitleLine");
            line.color = accent;
            MainMenuSceneBuilder.SetRect(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -272f), new Vector2(120f, 3f), new Vector2(0f, 1f));

            TextMeshProUGUI sub = GetOrCreateText(canvas, "Subtitle");
            theme.ApplyFont(sub, UIFontRole.Heading);
            sub.text = Subtitle;
            sub.fontSize = 24f;
            sub.characterSpacing = 12f;
            sub.color = DimText;
            sub.alignment = TextAlignmentOptions.Left;
            MainMenuSceneBuilder.SetRect(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -292f), new Vector2(1200f, 36f), new Vector2(0f, 1f));
        }

        // 2. 메뉴: 글자 목록
        string[] buttonFields = { "startButton", "tutorialButton", "infoButton", "controlsButton", "settingsButton", "quitButton" };
        Button first = so.FindProperty(buttonFields[0]).objectReferenceValue as Button;
        if (first != null && first.transform.parent is RectTransform menu)
        {
            MainMenuSceneBuilder.SetRect(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, -90f), new Vector2(520f, 6 * 64f + 5 * 8f), new Vector2(0f, 0.5f));
            if (menu.TryGetComponent(out VerticalLayoutGroup layout)) layout.spacing = 8f;
        }
        foreach (string field in buttonFields)
        {
            Button button = so.FindProperty(field).objectReferenceValue as Button;
            if (button != null) StyleTextButton(button, theme, 36f, TextAlignmentOptions.Left, 28f);
        }
        so.FindProperty("tutorialUnavailableLabel").stringValue = "튜토리얼   <size=24><alpha=#99>SOON</size>";

        // 3. 패널: 테두리 없는 반투명 + 왼쪽 세로선 + 제목 아래 선
        foreach (string field in new[] { "infoPanel", "controlsPanel", "settingsPanel" })
        {
            GameObject panel = so.FindProperty(field).objectReferenceValue as GameObject;
            if (panel != null) StylePanel(panel, theme, accent);
        }

        // 조작 정보의 '목표' 문단만 DEPTH ZERO 컨셉 글로 교체 (그 앞의 조작 설명은 사용자가 고친 그대로 유지)
        GameObject controlsPanel = so.FindProperty("controlsPanel").objectReferenceValue as GameObject;
        Transform controlsBody = controlsPanel != null ? controlsPanel.transform.Find("Body") : null;
        if (controlsBody != null && controlsBody.TryGetComponent(out TextMeshProUGUI controlsText))
        {
            int goalIndex = controlsText.text.IndexOf("<b>목표</b>", System.StringComparison.Ordinal);
            controlsText.text = goalIndex >= 0
                ? controlsText.text.Substring(0, goalIndex) + MainMenuSceneBuilder.GoalText
                : controlsText.text.TrimEnd() + "\n\n" + MainMenuSceneBuilder.GoalText;

            // 조작 변경 반영: 수류탄 G → 휠 클릭(2026-10-30) → 마우스 오른쪽 클릭(2026-10-31), 정비 휠 클릭 즉시 해제 추가
            controlsText.text = controlsText.text.Replace("수류탄  G\n", "수류탄  마우스 오른쪽 클릭\n")
                                                 .Replace("수류탄  마우스 휠 클릭\n", "수류탄  마우스 오른쪽 클릭\n");
            const string revertLine = "들고 있는 아이템 되돌리기  마우스 오른쪽\n";
            const string quickLine = "장착 즉시 해제(가방으로)  마우스 휠 클릭\n";
            if (!controlsText.text.Contains(quickLine) && controlsText.text.Contains(revertLine))
                controlsText.text = controlsText.text.Replace(revertLine, revertLine + quickLine);
        }
        SerializedProperty closes = so.FindProperty("closeButtons");
        for (int i = 0; i < closes.arraySize; i++)
        {
            Button close = closes.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            if (close == null) continue;
            StyleTextButton(close, theme, 24f, TextAlignmentOptions.Right, 0f);
            if (close.GetComponentInChildren<TextMeshProUGUI>(true) is TextMeshProUGUI label) label.text = "닫기  <alpha=#88>ESC";
        }
        so.ApplyModifiedProperties();

        // 4. 오른쪽 아래 버전·졸업작품 표시는 넣지 않음 (사용자 2026-10-30 — 필요하면 추후 기입). 이전 실행으로 생긴 것은 제거
        Transform footer = canvas.Find("Footer");
        if (footer != null) Undo.DestroyObjectImmediate(footer.gameObject);

        EditorSceneManager.MarkSceneDirty(menuUI.gameObject.scene);
        Debug.Log("[MainMenu] 미니멀 스타일 적용 (제목·글자 메뉴·패널). 씬 저장은 직접, Undo로 되돌리기 가능");
    }

    // 게임 제목 적용 (사용자 채택 2026-10-30 "DEPTH ZERO"): Player Settings > Product Name(빌드 실행 파일·창 제목) + 열린 MainMenu 씬 제목
    //   주의: Product Name이 바뀌면 PlayerPrefs 저장 위치도 바뀌어 최고 기록·환경설정이 처음 값으로 돌아감
    [MenuItem("Tools/TD Shoot/Apply Game Title")]
    public static void ApplyGameTitle()
    {
        string title = MainMenuSceneBuilder.GameTitle;
        if (PlayerSettings.productName != title)
        {
            if (!EditorUtility.DisplayDialog("게임 제목",
                    $"Product Name을 '{PlayerSettings.productName}' → '{title}'(으)로 바꿉니다.\n\n" +
                    "PlayerPrefs 저장 위치가 바뀌어 최고 기록·환경설정이 처음 값으로 돌아갑니다.", "바꾸기", "취소"))
                return;
            PlayerSettings.productName = title;
            AssetDatabase.SaveAssets();
        }

        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        Transform titleTransform = menuUI != null ? menuUI.transform.Find("Title") : null;
        if (titleTransform != null && titleTransform.TryGetComponent(out TextMeshProUGUI text) && text.text != title)
        {
            Undo.RecordObject(text, "Apply Game Title");
            text.text = title;
            EditorSceneManager.MarkSceneDirty(menuUI.gameObject.scene);
        }

        Debug.Log($"[MainMenu] 게임 제목 '{title}' 적용 — Product Name" + (titleTransform != null ? " + 메인 화면 제목 (씬 저장은 직접)" : " (메인 화면 제목은 MainMenu 씬을 열고 다시 실행)"));
    }

    // 테두리 스킨을 떼고 글자만 남김 (클릭 영역은 투명 Image 유지) + MenuItemHighlight
    internal static void StyleTextButton(Button button, UITheme theme, float fontSize, TextAlignmentOptions alignment, float textInset)
    {
        if (button.TryGetComponent(out UIThemeSkin skin)) Undo.DestroyObjectImmediate(skin);
        if (button.TryGetComponent(out Image image))
        {
            image.sprite = null;
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            theme.ApplyFont(label, UIFontRole.Heading);
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.alignment = alignment;
            label.characterSpacing = 2f;
            label.richText = true;
            MainMenuSceneBuilder.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(textInset, 0f);
        }

        Image bar = null;
        if (textInset > 0f)
        {
            bar = GetOrCreateImage(button.GetComponent<RectTransform>(), "HoverBar");
            MainMenuSceneBuilder.SetRect(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(4f, fontSize), new Vector2(0f, 0.5f));
            bar.enabled = false;
        }

        MenuItemHighlight highlight = button.GetComponent<MenuItemHighlight>();
        if (highlight == null) highlight = button.gameObject.AddComponent<MenuItemHighlight>();
        highlight.label = label;
        highlight.bar = bar;
        highlight.hoverColor = theme.gridHighlightBorderColor;
        highlight.hoverShift = textInset > 0f ? 14f : 0f;
        if (label != null) label.color = highlight.normalColor;
    }

    private static void StylePanel(GameObject panel, UITheme theme, Color accent)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        // 제목·부제와 겹치지 않게 오른쪽 아래로 (이전 1100x760 가운데는 제목 뒤를 덮음)
        MainMenuSceneBuilder.SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, -70f), new Vector2(1100f, 640f), new Vector2(0.5f, 0.5f));
        if (panel.TryGetComponent(out UIThemeSkin skin)) Undo.DestroyObjectImmediate(skin);
        if (panel.TryGetComponent(out Image image))
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = PanelColor;
        }

        Image edge = GetOrCreateImage(rect, "AccentEdge");
        edge.color = accent;
        edge.rectTransform.anchorMin = new Vector2(0f, 0f);
        edge.rectTransform.anchorMax = new Vector2(0f, 1f);
        edge.rectTransform.pivot = new Vector2(0f, 0.5f);
        edge.rectTransform.anchoredPosition = Vector2.zero;
        edge.rectTransform.sizeDelta = new Vector2(3f, 0f);
        edge.transform.SetAsFirstSibling();

        Transform titleTransform = rect.Find("Title");
        if (titleTransform != null && titleTransform.TryGetComponent(out TextMeshProUGUI title))
        {
            theme.ApplyFont(title, UIFontRole.Heading);
            title.fontSize = 48f;
            title.characterSpacing = 4f;
            title.color = Color.white;
        }

        Image rule = GetOrCreateImage(rect, "TitleRule");
        rule.color = new Color(accent.r, accent.g, accent.b, 0.35f);
        rule.rectTransform.anchorMin = new Vector2(0f, 1f);
        rule.rectTransform.anchorMax = new Vector2(1f, 1f);
        rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -118f);
        rule.rectTransform.sizeDelta = new Vector2(-120f, 1f);

        Transform body = rect.Find("Body");
        if (body != null && body.TryGetComponent(out TextMeshProUGUI bodyText))
        {
            theme.ApplyFont(bodyText, UIFontRole.Body);
            bodyText.color = new Color(0.85f, 0.88f, 0.92f);
            bodyText.lineSpacing = 12f;
        }
    }

    // 게임 정보 패널 글 전체를 MainMenuSceneBuilder.InfoText로 교체 (열린 MainMenu 씬, Undo 가능, 씬 저장은 직접)
    [MenuItem("Tools/TD Shoot/Apply Game Info To Main Menu")]
    public static void ApplyGameInfo()
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        GameObject infoPanel = menuUI != null ? new SerializedObject(menuUI).FindProperty("infoPanel").objectReferenceValue as GameObject : null;
        Transform body = infoPanel != null ? infoPanel.transform.Find("Body") : null;
        if (body == null || !body.TryGetComponent(out TextMeshProUGUI bodyText))
        {
            EditorUtility.DisplayDialog("Main Menu", "열린 씬에서 게임 정보 패널(InfoPanel > Body)을 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }
        Undo.RecordObject(bodyText, "Apply Game Info");
        bodyText.text = MainMenuSceneBuilder.InfoText;
        EditorUtility.SetDirty(bodyText);
        EditorSceneManager.MarkSceneDirty(bodyText.gameObject.scene);
        Debug.Log("[MainMenu] 게임 정보 글 교체 — 씬 저장은 직접");
    }

    internal static Image GetOrCreateImage(RectTransform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null && found.TryGetComponent(out Image existing)) return existing;
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(obj, "Restyle Main Menu");
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    internal static TextMeshProUGUI GetOrCreateText(RectTransform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null && found.TryGetComponent(out TextMeshProUGUI existing)) return existing;
        TextMeshProUGUI text = MainMenuSceneBuilder.CreateText(name, parent, "", 24f, FontStyles.Normal, TextAlignmentOptions.Left);
        Undo.RegisterCreatedObjectUndo(text.gameObject, "Restyle Main Menu");
        return text;
    }
}
