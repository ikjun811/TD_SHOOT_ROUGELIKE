using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 결과 씬 생성 (Tools > TD Shoot > Create Result Scene) — Assets/Scenes/ResultScene.unity
//   제목·부제 / 왼쪽: 전투 기록 큰 카드 4개(총 피해량·최고 한 방·사망·획득 모듈) + 라운드·시간·처치 + NEW 표시 + 최고 기록
//   오른쪽: 최종 빌드(무기 슬롯 2개 + 그리드 미리보기 + 요약) / 아래: 메인 화면 버튼. ResultSceneUI 연결, 빌드 설정에 추가
//   이미 있으면 덮어쓰지 않음 (만든 뒤 에디터에서 자유롭게 수정). 공용 생성 함수는 MainMenuSceneBuilder 사용
public static class ResultSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ResultScene.unity";
    private static readonly Color LabelColor = new Color(0.65f, 0.7f, 0.75f);
    private static readonly Color NewColor = new Color(1f, 0.82f, 0.25f);

    [MenuItem("Tools/TD Shoot/Create Result Scene")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorUtility.DisplayDialog("Result Scene", $"{ScenePath}가 이미 있습니다. 다시 만들려면 먼저 삭제하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObj.tag = "MainCamera";
        Camera cam = camObj.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasObj = new GameObject("ResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f;
        RectTransform canvas = canvasObj.GetComponent<RectTransform>();

        Vector2 center = new Vector2(0.5f, 0.5f);

        // 제목
        TextMeshProUGUI title = MainMenuSceneBuilder.CreateText("Title", canvas, "GAME CLEAR", 110f, FontStyles.Bold, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 130f), new Vector2(0.5f, 1f));
        TextMeshProUGUI subtitle = MainMenuSceneBuilder.CreateText("Subtitle", canvas, "15라운드 탈출 성공", 36f, FontStyles.Normal, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1200f, 50f), new Vector2(0.5f, 1f));

        // 왼쪽: 전투 기록
        Header(canvas, "RecordsHeader", "전투 기록", new Vector2(-480f, 290f));
        TextMeshProUGUI damage = Card(canvas, "TotalDamageCard", "총 피해량", new Vector2(-680f, 160f));
        TextMeshProUGUI maxHit = Card(canvas, "MaxHitCard", "최고 한 방", new Vector2(-280f, 160f));
        TextMeshProUGUI deaths = Card(canvas, "DeathsCard", "사망 횟수", new Vector2(-680f, -30f));
        TextMeshProUGUI modules = Card(canvas, "ModulesCard", "획득 모듈", new Vector2(-280f, -30f));

        TextMeshProUGUI round = SummaryRow(canvas, "Round", "도달 라운드", -175f, out GameObject newRound);
        TextMeshProUGUI time = SummaryRow(canvas, "Time", "전투 시간", -225f, out GameObject newTime);
        TextMeshProUGUI kills = SummaryRow(canvas, "Kills", "처치 수", -275f, out GameObject unusedBadge);
        Object.DestroyImmediate(unusedBadge);

        TextMeshProUGUI best = MainMenuSceneBuilder.CreateText("BestRecord", canvas, "최고 기록", 26f, FontStyles.Normal, TextAlignmentOptions.Left);
        best.color = LabelColor;
        MainMenuSceneBuilder.SetRect(best.rectTransform, center, center, new Vector2(-880f, -335f), new Vector2(800f, 40f), new Vector2(0f, 0.5f));

        // 오른쪽: 최종 빌드
        Header(canvas, "BuildHeader", "최종 빌드", new Vector2(480f, 290f));
        Image weapon1 = WeaponSlot(canvas, "WeaponSlot1", new Vector2(330f, 190f));
        Image weapon2 = WeaponSlot(canvas, "WeaponSlot2", new Vector2(630f, 190f));

        GameObject gridObj = new GameObject("GridPreview", typeof(RectTransform));
        RectTransform grid = gridObj.GetComponent<RectTransform>();
        grid.SetParent(canvas, false);
        MainMenuSceneBuilder.SetRect(grid, center, center, new Vector2(480f, -80f), new Vector2(380f, 380f), center);

        TextMeshProUGUI summary = MainMenuSceneBuilder.CreateText("BuildSummary", canvas, "모듈 0개", 26f, FontStyles.Normal, TextAlignmentOptions.Center);
        summary.color = LabelColor;
        MainMenuSceneBuilder.SetRect(summary.rectTransform, center, center, new Vector2(480f, -300f), new Vector2(800f, 40f), center);

        // 메인 화면 버튼
        GameObject buttonParent = new GameObject("Buttons", typeof(RectTransform));
        RectTransform buttons = buttonParent.GetComponent<RectTransform>();
        buttons.SetParent(canvas, false);
        MainMenuSceneBuilder.SetRect(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(360f, 80f), new Vector2(0.5f, 0f));
        Button mainMenu = MainMenuSceneBuilder.CreateButton("MainMenuButton", buttons, "메인 화면");
        MainMenuSceneBuilder.Stretch(mainMenu.GetComponent<RectTransform>(), 0f);

        // ResultSceneUI 연결
        ResultSceneUI ui = canvasObj.AddComponent<ResultSceneUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("totalDamageText").objectReferenceValue = damage;
        so.FindProperty("maxHitText").objectReferenceValue = maxHit;
        so.FindProperty("deathsText").objectReferenceValue = deaths;
        so.FindProperty("modulesText").objectReferenceValue = modules;
        so.FindProperty("roundText").objectReferenceValue = round;
        so.FindProperty("timeText").objectReferenceValue = time;
        so.FindProperty("killsText").objectReferenceValue = kills;
        so.FindProperty("bestRecordText").objectReferenceValue = best;
        so.FindProperty("newRoundBadge").objectReferenceValue = newRound;
        so.FindProperty("newTimeBadge").objectReferenceValue = newTime;
        SerializedProperty slots = so.FindProperty("weaponSlots");
        slots.arraySize = 2;
        slots.GetArrayElementAtIndex(0).objectReferenceValue = weapon1;
        slots.GetArrayElementAtIndex(1).objectReferenceValue = weapon2;
        so.FindProperty("gridContainer").objectReferenceValue = grid;
        so.FindProperty("buildSummaryText").objectReferenceValue = summary;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log($"[ResultScene] {ScenePath} 생성, 빌드 설정에 추가");
    }

    private static void Header(RectTransform canvas, string name, string text, Vector2 pos)
    {
        TextMeshProUGUI header = MainMenuSceneBuilder.CreateText(name, canvas, text, 36f, FontStyles.Bold, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.SetRect(header.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(800f, 50f), new Vector2(0.5f, 0.5f));
    }

    // 큰 기록 카드: 위 작은 이름 + 가운데 큰 숫자 (값 텍스트 반환)
    private static TextMeshProUGUI Card(RectTransform canvas, string name, string label, Vector2 pos)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIThemeSkin));
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        MainMenuSceneBuilder.SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(380f, 170f), new Vector2(0.5f, 0.5f));
        UIThemeSkin skin = card.GetComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.InsetPanel;
        skin.Apply();

        TextMeshProUGUI labelText = MainMenuSceneBuilder.CreateText("Label", rect, label, 26f, FontStyles.Normal, TextAlignmentOptions.Center);
        labelText.color = LabelColor;
        MainMenuSceneBuilder.SetRect(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, 40f), new Vector2(0.5f, 1f));

        TextMeshProUGUI value = MainMenuSceneBuilder.CreateText("Value", rect, "0", 64f, FontStyles.Bold, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.SetRect(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 20f), new Vector2(0f, 90f), new Vector2(0.5f, 0f));
        return value;
    }

    // 요약 한 줄: 이름(왼쪽) + 값 + NEW 표시(처음엔 숨김)
    private static TextMeshProUGUI SummaryRow(RectTransform canvas, string name, string label, float y, out GameObject newBadge)
    {
        Vector2 c = new Vector2(0.5f, 0.5f), left = new Vector2(0f, 0.5f);
        TextMeshProUGUI labelText = MainMenuSceneBuilder.CreateText(name + "Label", canvas, label, 30f, FontStyles.Normal, TextAlignmentOptions.Left);
        labelText.color = LabelColor;
        MainMenuSceneBuilder.SetRect(labelText.rectTransform, c, c, new Vector2(-880f, y), new Vector2(280f, 44f), left);

        TextMeshProUGUI value = MainMenuSceneBuilder.CreateText(name + "Value", canvas, "-", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
        MainMenuSceneBuilder.SetRect(value.rectTransform, c, c, new Vector2(-580f, y), new Vector2(360f, 44f), left);

        TextMeshProUGUI badge = MainMenuSceneBuilder.CreateText(name + "New", canvas, "NEW", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
        badge.color = NewColor;
        MainMenuSceneBuilder.SetRect(badge.rectTransform, c, c, new Vector2(-210f, y), new Vector2(120f, 44f), left);
        badge.gameObject.SetActive(false);
        newBadge = badge.gameObject;
        return value;
    }

    private static Image WeaponSlot(RectTransform canvas, string name, Vector2 pos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        MainMenuSceneBuilder.SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(260f, 110f), new Vector2(0.5f, 0.5f));
        Image image = obj.GetComponent<Image>();
        UITheme.Current.ApplyEmptySlot(image);
        image.raycastTarget = false;
        return image;
    }

    private static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
