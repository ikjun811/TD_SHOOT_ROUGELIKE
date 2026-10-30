using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// 게임 폰트 (사용자 채택 2026-10-30: 제목 갈무리11 + 본문 Pretendard, 둘 다 OFL)
//   Create Game Font Assets: Assets/Project/UI/Fonts의 ttf·otf로 TMP 동적 SDF 에셋 5개 생성 (이미 있으면 재사용, 설정만 갱신)
//     - 굵게(Bold) = 가짜 굵게 대신 실제 굵은 서체 (갈무리 → Galmuri11-Bold, Pretendard → SemiBold(600)·Bold(700))
//     - 대체 순서: 갈무리 → Pretendard → MALGUN SDF / Pretendard → MALGUN SDF (MALGUN은 기호 확인 후 제거 예정 — 라이선스)
//     - TMP 기본 폰트 = Pretendard Regular (폰트를 따로 정하지 않은 코드 UI가 전부 따라감)
//     - UITheme.headingFont = 갈무리11, bodyFont = Pretendard
//     - 게임에서 쓰는 기호가 각 폰트에 있는지 검사해 로그
//   Apply Game Fonts To Open Scenes / To Project Prefabs: 배치된 텍스트의 MALGUN·LiberationSans를 테마 폰트로 (F3)
//   Snap Heading Font Sizes In Open Scenes: 씬의 갈무리 글자 크기를 12배수로 (F4b)
public static class GameFontTools
{
    private const string FontFolder = "Assets/Project/UI/Fonts";
    private const string AssetFolder = FontFolder + "/FontAssets";
    private const string LegacyFontPath = "Assets/Project/Font/MALGUN SDF.asset";

    // 갈무리11은 12px 격자 픽셀 폰트 → 샘플링 48(4배)로 픽셀 모서리 유지. HUD 외곽선 때문에 SDF
    private const int GalmuriSampling = 48;
    private const int GalmuriPadding = 6;
    private const int PretendardSampling = 64;
    private const int PretendardPadding = 7;
    private const int AtlasSize = 2048;

    // 코드 UI 문자열에 쓰이는 기호 + 영문·숫자·자주 쓰는 한글
    private const string GlyphTest =
        "0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz %+-/()[]<>:;!?.,'\"#_=" +
        "·×–—→─▼★•" +
        "체력탈출엘리트레전더리희귀일반유니크라운드정비모듈무기";

    [MenuItem("Tools/TD Shoot/Create Game Font Assets")]
    public static void CreateGameFontAssets()
    {
        TMP_FontAsset galmuri = GetOrCreate(FontFolder + "/Galmuri/Galmuri11.ttf", GalmuriSampling, GalmuriPadding);
        TMP_FontAsset galmuriBold = GetOrCreate(FontFolder + "/Galmuri/Galmuri11-Bold.ttf", GalmuriSampling, GalmuriPadding);
        TMP_FontAsset pretendard = GetOrCreate(FontFolder + "/Pretendard/Pretendard-Regular.otf", PretendardSampling, PretendardPadding);
        TMP_FontAsset pretendardSemi = GetOrCreate(FontFolder + "/Pretendard/Pretendard-SemiBold.otf", PretendardSampling, PretendardPadding);
        TMP_FontAsset pretendardBold = GetOrCreate(FontFolder + "/Pretendard/Pretendard-Bold.otf", PretendardSampling, PretendardPadding);

        if (galmuri == null || galmuriBold == null || pretendard == null || pretendardSemi == null || pretendardBold == null)
        {
            EditorUtility.DisplayDialog("게임 폰트", "폰트 에셋을 만들지 못했습니다. 콘솔 경고를 확인하세요.\n(폰트 파일 위치: " + FontFolder + ")", "확인");
            return;
        }

        TMP_FontAsset legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFontPath);

        // 굵게 = 실제 굵은 서체 (TMP는 Bold 스타일에 무게 표 7번(700)을 사용)
        SetWeight(galmuri, 7, galmuriBold);
        SetWeight(pretendard, 6, pretendardSemi);
        SetWeight(pretendard, 7, pretendardBold);

        // 대체 폰트: 갈무리에 없는 글자는 같은 굵기의 Pretendard로, 그다음 MALGUN
        SetFallbacks(galmuri, pretendard, legacy);
        SetFallbacks(galmuriBold, pretendardBold, legacy);
        SetFallbacks(pretendard, legacy);
        SetFallbacks(pretendardSemi, legacy);
        SetFallbacks(pretendardBold, legacy);

        // TMP 기본 폰트
        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null)
        {
            Undo.RecordObject(settings, "Set Default Game Font");
            TMP_Settings.defaultFontAsset = pretendard;
            var globalFallbacks = new List<TMP_FontAsset>();
            if (legacy != null) globalFallbacks.Add(legacy);
            TMP_Settings.fallbackFontAssets = globalFallbacks;
            EditorUtility.SetDirty(settings);
        }
        else
        {
            Debug.LogWarning("[GameFontTools] TMP Settings를 찾지 못해 기본 폰트는 바꾸지 않았습니다.");
        }

        // UI 테마
        UITheme theme = UIThemeTools.GetOrCreateTheme();
        Undo.RecordObject(theme, "Assign Game Fonts");
        theme.headingFont = galmuri;
        theme.bodyFont = pretendard;
        EditorUtility.SetDirty(theme);

        AssetDatabase.SaveAssets();

        Debug.Log("[GameFontTools] 게임 폰트 적용 — 기본 폰트 Pretendard, 테마 제목 갈무리11 / 본문 Pretendard" +
                  (legacy != null ? ", 마지막 대체 MALGUN SDF" : ", MALGUN SDF 없음(대체 없음)"));
        ReportMissing(galmuri, "갈무리11");
        ReportMissing(pretendard, "Pretendard");
        AssetDatabase.SaveAssets(); // 검사 중 아틀라스에 추가된 글자 저장

        Selection.activeObject = theme;
    }

    // ── F3: 씬·프리팹에 배치된 텍스트 교체 ─────────────────────────────
    //   MALGUN SDF·LiberationSans SDF(또는 폰트 없음)를 쓰는 텍스트만 테마 폰트로. 다른 폰트는 그대로
    //   제목(갈무리) = 버튼 안 글자(드롭다운 제외) / 크기 40 이상 / 이름에 "Title", 나머지는 본문(Pretendard)
    private const string LiberationFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string PrefabFolder = "Assets/Project/Prefabs";
    private const float HeadingMinSize = 40f;

    [MenuItem("Tools/TD Shoot/Apply Game Fonts To Open Scenes")]
    public static void ApplyGameFontsToOpenScenes()
    {
        UITheme theme = GetThemeWithFonts();
        if (theme == null) return;
        HashSet<TMP_FontAsset> replaceable = GetReplaceableFonts();

        int heading = 0, body = 0;
        var log = new StringBuilder();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            int before = heading + body;
            foreach (GameObject root in scene.GetRootGameObjects())
                ApplyToHierarchy(root, theme, replaceable, true, ref heading, ref body, log);
            if (heading + body > before) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[GameFontTools] 열린 씬 텍스트 폰트 교체 — 제목(갈무리) {heading}개, 본문(Pretendard) {body}개. 씬 저장은 직접 하세요 (Undo로 되돌리기 가능)\n{log}");
    }

    [MenuItem("Tools/TD Shoot/Apply Game Fonts To Project Prefabs")]
    public static void ApplyGameFontsToProjectPrefabs()
    {
        UITheme theme = GetThemeWithFonts();
        if (theme == null) return;
        HashSet<TMP_FontAsset> replaceable = GetReplaceableFonts();

        int heading = 0, body = 0, prefabs = 0;
        var log = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int before = heading + body;
            ApplyToHierarchy(root, theme, replaceable, false, ref heading, ref body, log);
            if (heading + body > before)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabs++;
                log.AppendLine($"  ↳ 저장: {path}");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[GameFontTools] 프리팹 텍스트 폰트 교체 — 프리팹 {prefabs}개, 제목(갈무리) {heading}개, 본문(Pretendard) {body}개\n{log}");
    }

    // ── F4b: 씬의 갈무리 글자 크기를 가장 가까운 12의 배수로 (갈무리11은 12px 격자 → 1920x1080 기준 12배수가 가장 선명)
    //   UI 텍스트(TextMeshProUGUI)만, 자동 크기(Auto Size) 켠 텍스트는 건너뜀
    private const float HeadingSizeStep = 12f;

    [MenuItem("Tools/TD Shoot/Snap Heading Font Sizes In Open Scenes")]
    public static void SnapHeadingFontSizesInOpenScenes()
    {
        UITheme theme = GetThemeWithFonts();
        if (theme == null) return;

        var headingFonts = new HashSet<TMP_FontAsset> { theme.headingFont };
        TMP_FontWeightPair[] weights = theme.headingFont.fontWeightTable;
        if (weights != null)
            foreach (TMP_FontWeightPair pair in weights)
                if (pair.regularTypeface != null) headingFonts.Add(pair.regularTypeface);

        int changed = 0, skippedAuto = 0;
        var log = new StringBuilder();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            int before = changed;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text.font == null || !headingFonts.Contains(text.font)) continue;
                    if (text.enableAutoSizing)
                    {
                        skippedAuto++;
                        log.AppendLine($"  건너뜀(자동 크기)  {GetPath(text.transform)}");
                        continue;
                    }

                    float snapped = Mathf.Max(HeadingSizeStep, Mathf.Round(text.fontSize / HeadingSizeStep) * HeadingSizeStep);
                    if (Mathf.Approximately(snapped, text.fontSize)) continue;

                    Undo.RecordObject(text, "Snap Heading Font Sizes");
                    log.AppendLine($"  {text.fontSize:0} → {snapped:0}  {GetPath(text.transform)}");
                    text.fontSize = snapped;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    EditorUtility.SetDirty(text);
                    changed++;
                }
            }
            if (changed > before) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[GameFontTools] 갈무리 글자 크기 12배수 정리 — 변경 {changed}개, 자동 크기라 건너뜀 {skippedAuto}개. 씬 저장은 직접 하세요 (Undo로 되돌리기 가능)\n{log}");
    }

    private static void ApplyToHierarchy(GameObject root, UITheme theme, HashSet<TMP_FontAsset> replaceable, bool recordUndo,
                                         ref int heading, ref int body, StringBuilder log)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null && !replaceable.Contains(text.font)) continue;

            UIFontRole role = ChooseRole(text);
            TMP_FontAsset font = theme.GetFont(role);
            if (recordUndo) Undo.RecordObject(text, "Apply Game Fonts");
            text.font = font;
            text.fontSharedMaterial = font.material; // 기존 재질은 모두 기본 재질 (꾸밈 없음 확인)
            if (recordUndo) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            EditorUtility.SetDirty(text);

            if (role == UIFontRole.Heading) heading++;
            else body++;
            log.AppendLine($"  {(role == UIFontRole.Heading ? "제목" : "본문")}  {GetPath(text.transform)}  (크기 {text.fontSize:0})");
        }
    }

    private static UIFontRole ChooseRole(TMP_Text text)
    {
        if (text.GetComponentInParent<TMP_Dropdown>(true) != null) return UIFontRole.Body;
        if (text.GetComponentInParent<UnityEngine.UI.Button>(true) != null) return UIFontRole.Heading;
        if (text.fontSize >= HeadingMinSize) return UIFontRole.Heading;
        if (text.name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0) return UIFontRole.Heading;
        return UIFontRole.Body;
    }

    private static HashSet<TMP_FontAsset> GetReplaceableFonts()
    {
        var set = new HashSet<TMP_FontAsset>();
        TMP_FontAsset legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFontPath);
        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontPath);
        if (legacy != null) set.Add(legacy);
        if (liberation != null) set.Add(liberation);
        return set;
    }

    private static UITheme GetThemeWithFonts()
    {
        UITheme theme = UIThemeTools.GetOrCreateTheme();
        if (theme.headingFont == null || theme.bodyFont == null)
        {
            EditorUtility.DisplayDialog("게임 폰트", "UI 테마에 폰트가 없습니다. 먼저 Tools > TD Shoot > Create Game Font Assets를 실행하세요.", "확인");
            return null;
        }
        return theme;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static TMP_FontAsset GetOrCreate(string fontPath, int sampling, int padding)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogWarning($"[GameFontTools] 폰트 파일 없음: {fontPath}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder(FontFolder, "FontAssets");
        string assetPath = $"{AssetFolder}/{System.IO.Path.GetFileNameWithoutExtension(fontPath)} SDF.asset";

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, sampling, padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return null;

        asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);

        // 아틀라스 텍스처·재질을 에셋 안에 저장 (TMP 생성 메뉴와 같은 방식)
        Texture2D atlas = asset.atlasTextures[0];
        atlas.name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(atlas, asset);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameFontTools] 폰트 에셋 생성 -> {assetPath} (샘플링 {sampling}, 여백 {padding}, 아틀라스 {AtlasSize}, 동적·멀티 아틀라스)");
        return asset;
    }

    private static void SetWeight(TMP_FontAsset asset, int index, TMP_FontAsset typeface)
    {
        TMP_FontWeightPair[] table = asset.fontWeightTable;
        if (table == null || table.Length <= index) return;
        table[index].regularTypeface = typeface;
        EditorUtility.SetDirty(asset);
    }

    private static void SetFallbacks(TMP_FontAsset asset, params TMP_FontAsset[] fallbacks)
    {
        var list = new List<TMP_FontAsset>();
        foreach (TMP_FontAsset fallback in fallbacks)
            if (fallback != null && fallback != asset) list.Add(fallback);
        asset.fallbackFontAssetTable = list;
        EditorUtility.SetDirty(asset);
    }

    // 폰트 자체에 없는 글자 / 대체 폰트까지 찾아도 없는 글자
    private static void ReportMissing(TMP_FontAsset asset, string label)
    {
        asset.HasCharacters(GlyphTest, out uint[] own, false, true);
        asset.HasCharacters(GlyphTest, out uint[] withFallback, true, true);
        string ownText = ToText(own);
        string fallbackText = ToText(withFallback);

        if (ownText.Length == 0)
            Debug.Log($"[GameFontTools] {label}: 검사 글자 모두 있음");
        else
            Debug.Log($"[GameFontTools] {label}: 자체에 없는 글자 [{ownText}] → 대체 폰트로 표시" +
                      (fallbackText.Length > 0 ? $", 대체에도 없음 [{fallbackText}]" : " (대체에서 모두 찾음)"));
    }

    private static string ToText(uint[] codes)
    {
        if (codes == null) return "";
        var sb = new StringBuilder();
        foreach (uint code in codes)
            if (code != ' ') sb.Append(char.ConvertFromUtf32((int)code)).Append(' ');
        return sb.ToString().Trim();
    }
}
