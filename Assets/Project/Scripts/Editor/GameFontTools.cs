using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// 게임 폰트 (사용자 채택 2026-10-30: 제목 갈무리11 + 본문 Pretendard, 둘 다 OFL)
//   Create Game Font Assets: Assets/Project/UI/Fonts의 ttf·otf로 TMP 동적 SDF 에셋 5개 생성 (이미 있으면 재사용, 설정만 갱신)
//     - 굵게(Bold) = 가짜 굵게 대신 실제 굵은 서체 (갈무리 → Galmuri11-Bold, Pretendard → SemiBold(600)·Bold(700))
//     - 대체 순서: 갈무리 → Pretendard / Pretendard → (TMP 전역 대체) 갈무리 — MALGUN은 라이선스 문제로 제거 (2026-10-31)
//     - TMP 기본 폰트 = Pretendard Regular (폰트를 따로 정하지 않은 코드 UI가 전부 따라감)
//     - UITheme.headingFont = 갈무리11, bodyFont = Pretendard
//     - 게임에서 쓰는 기호가 각 폰트에 있는지 검사해 로그
//   Apply Game Fonts To Open Scenes / To Project Prefabs: 배치된 텍스트의 MALGUN·LiberationSans를 테마 폰트로 (F3)
//   Snap Heading Font Sizes In Open Scenes: 씬의 갈무리 글자 크기를 12배수로 (F4b)
//   Remove MALGUN Fallback: 게임에서 쓰는 글자(씬·프리팹·에셋·코드 문자열)가 갈무리·Pretendard에 다 있는지 검사 → 대체 목록에서 MALGUN 제거 → 파일 휴지통
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

        // 굵게 = 실제 굵은 서체 (TMP는 Bold 스타일에 무게 표 7번(700)을 사용)
        SetWeight(galmuri, 7, galmuriBold);
        SetWeight(pretendard, 6, pretendardSemi);
        SetWeight(pretendard, 7, pretendardBold);

        // 대체 폰트: 갈무리에 없는 글자는 같은 굵기의 Pretendard로, Pretendard에 없는 글자는 전역 대체(갈무리)로
        SetFallbacks(galmuri, pretendard);
        SetFallbacks(galmuriBold, pretendardBold);
        SetFallbacks(pretendard);
        SetFallbacks(pretendardSemi);
        SetFallbacks(pretendardBold);

        // TMP 기본 폰트
        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null)
        {
            Undo.RecordObject(settings, "Set Default Game Font");
            TMP_Settings.defaultFontAsset = pretendard;
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { galmuri };
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

        Debug.Log("[GameFontTools] 게임 폰트 적용 — 기본 폰트 Pretendard, 테마 제목 갈무리11 / 본문 Pretendard, 전역 대체 갈무리11");
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

    // ── MALGUN 대체 폰트 제거 (라이선스 정리 2026-10-31) ─────────────────
    //   1. 게임에서 쓰는 글자 수집: 씬·Assets/Project의 프리팹·에셋(폰트 폴더 제외)의 문자열 + 런타임 코드의 문자열 리터럴(주석·Debug 로그 제외)
    //   2. 갈무리11·Pretendard 어느 쪽에도 없는 글자 목록 (있으면 화면에서 빈 칸) → 확인 창
    //   3. 폰트 에셋의 대체 목록·TMP 전역 대체에서 MALGUN 제거 (Pretendard → 전역 대체 갈무리)
    //   4. MALGUN을 쓰는 곳이 더 없으면 Assets/Project/Font 폴더를 휴지통으로 (확인 창)
    [MenuItem("Tools/TD Shoot/Remove MALGUN Fallback")]
    public static void RemoveMalgunFallback()
    {
        TMP_FontAsset legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFontPath);
        TMP_FontAsset galmuri = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetFolder + "/Galmuri11 SDF.asset");
        TMP_FontAsset pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetFolder + "/Pretendard-Regular SDF.asset");
        if (galmuri == null || pretendard == null)
        {
            EditorUtility.DisplayDialog("MALGUN 제거", "갈무리11·Pretendard 폰트 에셋을 찾지 못했습니다. Create Game Font Assets를 먼저 실행하세요.", "확인");
            return;
        }

        // 1·2. 사용 글자 검사 (ASCII는 두 폰트 모두 있음 → 그 밖의 글자만)
        string used = CollectUsedCharacters(out int fileCount);
        galmuri.HasCharacters(used, out uint[] notInGalmuri, false, true);
        pretendard.HasCharacters(used, out uint[] notInPretendard, false, true);
        var missing = new List<uint>();
        if (notInGalmuri != null && notInPretendard != null)
        {
            var galmuriMissing = new HashSet<uint>(notInGalmuri);
            foreach (uint code in notInPretendard)
                if (galmuriMissing.Contains(code)) missing.Add(code);
        }
        string missingText = ToText(missing.ToArray());
        Debug.Log($"[GameFontTools] 사용 글자 검사 — 파일 {fileCount}개, ASCII 외 글자 {used.Length}개. " +
                  $"Pretendard에 없음(갈무리로 표시) [{ToText(notInPretendard)}] / 두 폰트 모두 없음 [{missingText}]");

        string message = missing.Count == 0
            ? $"게임에서 쓰는 글자(ASCII 외 {used.Length}개)가 모두 갈무리11·Pretendard에 있습니다.\n\nMALGUN을 대체 폰트에서 제거할까요?"
            : $"두 폰트 모두 없는 글자가 있습니다 (MALGUN을 빼면 빈 칸으로 보일 수 있음):\n{missingText}\n\n콘솔에 전체 목록. 그래도 제거할까요?";
        if (!EditorUtility.DisplayDialog("MALGUN 제거", message, "제거", "취소")) return;

        // 3. 대체 목록에서 제거
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { AssetFolder }))
        {
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || asset.fallbackFontAssetTable == null) continue;
            if (asset.fallbackFontAssetTable.RemoveAll(f => f == null || f == legacy) > 0)
            {
                EditorUtility.SetDirty(asset);
                Debug.Log($"[GameFontTools] {asset.name}: 대체 목록에서 MALGUN 제거");
            }
        }
        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null)
        {
            Undo.RecordObject(settings, "Remove MALGUN Fallback");
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { galmuri };
            EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssets();

        // 4. 남은 참조 확인 후 파일 삭제
        if (legacy == null) return;
        string legacyGuid = AssetDatabase.AssetPathToGUID(LegacyFontPath);
        string legacyFolder = System.IO.Path.GetDirectoryName(LegacyFontPath).Replace('\\', '/');
        var users = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || path.StartsWith(legacyFolder + "/")) continue;
            if (!(path.EndsWith(".unity") || path.EndsWith(".prefab") || path.EndsWith(".asset") || path.EndsWith(".mat"))) continue;
            if (System.IO.File.ReadAllText(path).Contains(legacyGuid)) users.Add(path);
        }
        if (users.Count > 0)
        {
            Debug.LogWarning("[GameFontTools] MALGUN SDF를 아직 쓰는 파일 (폰트 파일은 남겨 둠 — 씬이면 Apply Game Fonts To Open Scenes 후 저장):\n" + string.Join("\n", users));
            EditorUtility.DisplayDialog("MALGUN 제거", $"대체 목록에서는 제거했습니다.\nMALGUN을 아직 쓰는 파일이 {users.Count}개 있어 폰트 파일은 남겨 두었습니다 (콘솔 목록).", "확인");
            return;
        }
        if (EditorUtility.DisplayDialog("MALGUN 제거", $"MALGUN을 쓰는 곳이 없습니다.\n{legacyFolder} 폴더(맑은 고딕 TTF 3개 + SDF)를 휴지통으로 옮길까요?", "휴지통으로", "남겨 두기"))
        {
            AssetDatabase.MoveAssetToTrash(legacyFolder);
            Debug.Log($"[GameFontTools] {legacyFolder} 휴지통으로 이동");
        }
    }

    // 게임 화면에 나올 수 있는 ASCII 외 글자 (중복 없이)
    private static string CollectUsedCharacters(out int fileCount)
    {
        var chars = new SortedSet<int>();
        fileCount = 0;
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            bool data = (path.StartsWith("Assets/Scenes/") && path.EndsWith(".unity"))
                        || (path.StartsWith("Assets/Project/") && (path.EndsWith(".prefab") || path.EndsWith(".asset") || path.EndsWith(".unity"))
                            && !path.Contains("/Fonts/") && !path.StartsWith("Assets/Project/Font/"));
            bool code = path.StartsWith("Assets/Project/Scripts/") && path.EndsWith(".cs") && !path.Contains("/Editor/");
            if (!data && !code) continue;
            fileCount++;
            string text = System.IO.File.ReadAllText(path);
            if (data) AddYamlCharacters(text, chars);
            else AddCodeStringCharacters(text, chars);
        }
        var sb = new StringBuilder();
        foreach (int c in chars) sb.Append(char.ConvertFromUtf32(c));
        return sb.ToString();
    }

    // YAML: 비ASCII는 \uXXXX(·\UXXXXXXXX·\xXX)로 저장되거나 그대로 들어 있음
    private static void AddYamlCharacters(string text, SortedSet<int> chars)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\\(?:u([0-9A-Fa-f]{4})|U([0-9A-Fa-f]{8})|x([0-9A-Fa-f]{2}))"))
        {
            string hex = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            AddCode(System.Convert.ToInt32(hex, 16), chars);
        }
        AddRaw(text, chars);
    }

    // 코드: 주석·Debug 로그 줄을 뺀 문자열 리터럴 안의 글자만 (주석의 기호는 화면에 안 나옴)
    private static void AddCodeStringCharacters(string text, SortedSet<int> chars)
    {
        var literal = new StringBuilder();
        foreach (string line in text.Split('\n'))
        {
            if (line.Contains("Debug.Log")) continue;
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inString)
                {
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                    if (c == '"') inString = true;
                    else if (c == '\'' && i + 2 < line.Length && line[i + 2] == '\'') { literal.Append(line[i + 1]); i += 2; }
                    continue;
                }
                if (c == '\\') { i++; continue; }
                if (c == '"') { inString = false; continue; }
                literal.Append(c);
            }
        }
        AddRaw(literal.ToString(), chars);
    }

    private static void AddRaw(string text, SortedSet<int> chars)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] < 0x80) continue;
            int code = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? char.ConvertToUtf32(text[i], text[++i]) : text[i];
            AddCode(code, chars);
        }
    }

    // 공백·제어 문자·이모지 변형 선택자 등 글리프가 필요 없는 것 제외
    private static void AddCode(int code, SortedSet<int> chars)
    {
        if (code < 0xA1 || code == 0xFEFF || (code >= 0x200B && code <= 0x200F) || (code >= 0xFE00 && code <= 0xFE0F)) return;
        if (code >= 0xD800 && code <= 0xDFFF) return;
        chars.Add(code);
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
