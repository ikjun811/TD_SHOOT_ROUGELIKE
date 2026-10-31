using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 결과 화면 미니멀 스타일 (R1, 사용자 채택 2026-10-31 — 메인 화면 미니멀 스타일과 통일)
//   Tools > TD Shoot > Restyle Result Scene (Minimal): 열린 ResultScene을 바꿈 (Undo 가능, 씬 저장은 직접, 여러 번 실행해도 같은 결과)
//   - 제목: 왼쪽 위(기록 카드 왼쪽 끝에 맞춤) 갈무리 72·자간 + 아래 선(실행 시 제목 색) + 부제 Pretendard 흐리게
//   - 구역 제목(전투 기록·최종 빌드): 왼쪽 정렬 갈무리 24 청록 + 아래 흐린 선
//   - 기록 카드 4개: 테두리 스킨 → 반투명 남색 + 왼쪽 청록 세로선, 이름 흐리게·숫자 갈무리 60 왼쪽 정렬
//   - 요약 줄·최고 기록·빌드 요약 글꼴 정리, NEW 청록
//   - 메인 화면 버튼: 메인 메뉴와 같은 글자 버튼(마우스 올리면 청록 + 왼쪽 막대), 왼쪽 아래
//   연결(ResultSceneUI 필드)·데이터 채우기는 그대로
public static class ResultSceneStyleTools
{
    private const float LeftEdge = -870f;  // 왼쪽 열(기록 카드) 왼쪽 끝 (캔버스 가운데 기준)
    private const float RightEdge = 200f;  // 오른쪽 열(무기 슬롯·그리드) 왼쪽 끝
    private const float HeaderY = 280f; // 제목 묶음과 간격 (스크린샷 확인 후 290 → 280)

    [MenuItem("Tools/TD Shoot/Restyle Result Scene (Minimal)")]
    public static void Restyle()
    {
        ResultSceneUI ui = Object.FindAnyObjectByType<ResultSceneUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Result Scene", "열린 씬에서 ResultSceneUI를 찾지 못했습니다. ResultScene을 열어 주세요.", "확인");
            return;
        }

        UITheme theme = UIThemeTools.GetOrCreateTheme();
        Color accent = theme.gridHighlightBorderColor;
        RectTransform canvas = ui.GetComponent<RectTransform>();
        SerializedObject so = new SerializedObject(ui);
        Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Restyle Result Scene");
        Vector2 center = new Vector2(0.5f, 0.5f), topLeft = new Vector2(0f, 1f), left = new Vector2(0f, 0.5f);
        float leftFromScreen = 960f + LeftEdge;

        // 1. 제목 + 선 + 부제
        if (Field<TextMeshProUGUI>(so, "titleText") is TextMeshProUGUI title)
        {
            theme.ApplyFont(title, UIFontRole.Heading);
            title.fontSize = 72f;
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 6f;
            title.alignment = TextAlignmentOptions.Left;
            MainMenuSceneBuilder.SetRect(title.rectTransform, topLeft, topLeft, new Vector2(leftFromScreen, -48f), new Vector2(1400f, 92f), topLeft);
        }
        Image line = MainMenuStyleTools.GetOrCreateImage(canvas, "TitleLine");
        line.color = accent; // 실행 시 ResultSceneUI가 제목 색으로
        MainMenuSceneBuilder.SetRect(line.rectTransform, topLeft, topLeft, new Vector2(leftFromScreen + 4f, -146f), new Vector2(120f, 3f), topLeft);
        if (Field<TextMeshProUGUI>(so, "subtitleText") is TextMeshProUGUI subtitle)
        {
            theme.ApplyFont(subtitle, UIFontRole.Body);
            subtitle.fontSize = 28f;
            subtitle.fontStyle = FontStyles.Normal;
            subtitle.characterSpacing = 2f;
            subtitle.color = MainMenuStyleTools.DimText;
            subtitle.alignment = TextAlignmentOptions.Left;
            MainMenuSceneBuilder.SetRect(subtitle.rectTransform, topLeft, topLeft, new Vector2(leftFromScreen + 4f, -160f), new Vector2(1200f, 40f), topLeft);
        }

        // 2. 구역 제목 + 선
        StyleHeader(canvas, "RecordsHeader", "전투 기록", LeftEdge, 780f, theme, accent);
        StyleHeader(canvas, "BuildHeader", "최종 빌드", RightEdge, 560f, theme, accent);

        // 3. 기록 카드
        foreach (string card in new[] { "TotalDamageCard", "MaxHitCard", "DeathsCard", "ModulesCard" })
        {
            if (canvas.Find(card) is RectTransform rect) StyleCard(rect, theme, accent);
        }

        // 4. 요약 줄 (이름·값·NEW)
        foreach (string row in new[] { "Round", "Time", "Kills" })
        {
            if (canvas.Find(row + "Label") is Transform l && l.TryGetComponent(out TextMeshProUGUI label))
            {
                theme.ApplyFont(label, UIFontRole.Body);
                label.fontSize = 26f;
                label.fontStyle = FontStyles.Normal;
                label.color = MainMenuStyleTools.DimText;
                MainMenuSceneBuilder.SetRect(label.rectTransform, center, center, new Vector2(LeftEdge, label.rectTransform.anchoredPosition.y), new Vector2(280f, 44f), left);
            }
            if (canvas.Find(row + "Value") is Transform v && v.TryGetComponent(out TextMeshProUGUI value))
            {
                theme.ApplyFont(value, UIFontRole.Body);
                value.fontSize = 28f;
                value.fontStyle = FontStyles.Bold;
                value.color = Color.white;
            }
            if (canvas.Find(row + "New") is Transform n && n.TryGetComponent(out TextMeshProUGUI badge))
            {
                theme.ApplyFont(badge, UIFontRole.Heading);
                badge.fontSize = 24f;
                badge.fontStyle = FontStyles.Normal;
                badge.characterSpacing = 4f;
                badge.color = accent;
            }
        }
        foreach (string field in new[] { "bestRecordText", "buildSummaryText" })
        {
            if (Field<TextMeshProUGUI>(so, field) is TextMeshProUGUI text)
            {
                theme.ApplyFont(text, UIFontRole.Body);
                text.fontSize = 22f;
                text.fontStyle = FontStyles.Normal;
                text.color = MainMenuStyleTools.DimText;
            }
        }
        if (Field<TextMeshProUGUI>(so, "bestRecordText") is TextMeshProUGUI best)
            MainMenuSceneBuilder.SetRect(best.rectTransform, center, center, new Vector2(LeftEdge, best.rectTransform.anchoredPosition.y), new Vector2(800f, 40f), left);

        // 5. 메인 화면 버튼: 글자 버튼, 왼쪽 아래
        if (Field<Button>(so, "mainMenuButton") is Button button)
        {
            if (button.transform.parent is RectTransform buttons && buttons != canvas)
                MainMenuSceneBuilder.SetRect(buttons, Vector2.zero, Vector2.zero, new Vector2(leftFromScreen - 28f, 72f), new Vector2(420f, 64f), Vector2.zero);
            MainMenuStyleTools.StyleTextButton(button, theme, 36f, TextAlignmentOptions.Left, 28f);
        }

        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Debug.Log("[ResultScene] 미니멀 스타일 적용 (제목·구역 제목·카드·요약·버튼). 씬 저장은 직접, Undo로 되돌리기 가능");
    }

    private static T Field<T>(SerializedObject so, string name) where T : Object
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.objectReferenceValue as T : null;
    }

    private static void StyleHeader(RectTransform canvas, string name, string text, float x, float width, UITheme theme, Color accent)
    {
        Vector2 center = new Vector2(0.5f, 0.5f), left = new Vector2(0f, 0.5f);
        if (canvas.Find(name) is Transform t && t.TryGetComponent(out TextMeshProUGUI header))
        {
            theme.ApplyFont(header, UIFontRole.Heading);
            header.text = text;
            header.fontSize = 24f;
            header.fontStyle = FontStyles.Normal;
            header.characterSpacing = 6f;
            header.color = accent;
            header.alignment = TextAlignmentOptions.Left;
            MainMenuSceneBuilder.SetRect(header.rectTransform, center, center, new Vector2(x, HeaderY), new Vector2(width, 40f), left);
        }
        Image rule = MainMenuStyleTools.GetOrCreateImage(canvas, name + "Rule");
        rule.color = new Color(accent.r, accent.g, accent.b, 0.35f);
        MainMenuSceneBuilder.SetRect(rule.rectTransform, center, center, new Vector2(x, HeaderY - 22f), new Vector2(width, 1f), left);
    }

    private static void StyleCard(RectTransform card, UITheme theme, Color accent)
    {
        if (card.TryGetComponent(out UIThemeSkin skin)) Undo.DestroyObjectImmediate(skin);
        if (card.TryGetComponent(out Image image))
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = MainMenuStyleTools.PanelColor;
            image.raycastTarget = false;
        }

        Image edge = MainMenuStyleTools.GetOrCreateImage(card, "AccentEdge");
        edge.color = accent;
        edge.rectTransform.anchorMin = new Vector2(0f, 0f);
        edge.rectTransform.anchorMax = new Vector2(0f, 1f);
        edge.rectTransform.pivot = new Vector2(0f, 0.5f);
        edge.rectTransform.anchoredPosition = Vector2.zero;
        edge.rectTransform.sizeDelta = new Vector2(3f, 0f);
        edge.transform.SetAsFirstSibling();

        if (card.Find("Label") is Transform l && l.TryGetComponent(out TextMeshProUGUI label))
        {
            theme.ApplyFont(label, UIFontRole.Body);
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Normal;
            label.color = MainMenuStyleTools.DimText;
            label.alignment = TextAlignmentOptions.Left;
            RectTransform r = label.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(28f, -56f);
            r.offsetMax = new Vector2(-20f, -20f);
        }
        if (card.Find("Value") is Transform v && v.TryGetComponent(out TextMeshProUGUI value))
        {
            theme.ApplyFont(value, UIFontRole.Heading);
            value.fontSize = 60f;
            value.fontStyle = FontStyles.Normal;
            value.color = Color.white;
            value.alignment = TextAlignmentOptions.Left;
            RectTransform r = value.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(28f, 18f);
            r.offsetMax = new Vector2(-20f, 102f);
        }
    }
}
