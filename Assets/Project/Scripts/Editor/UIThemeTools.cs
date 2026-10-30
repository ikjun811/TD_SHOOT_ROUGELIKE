using UnityEditor;
using UnityEngine;

// UI 테마 에셋 생성·픽셀 에셋 연결
//   Create UI Theme: Assets/Project/Resources/UITheme.asset 생성 (이미 있으면 선택만)
//   Apply PixelUISciFi to Theme: PixelUISciFiFree(Cyan) 스프라이트를 테마 칸에 채움 (레어도: 일반 common / 희귀 rare / 엘리트 epic / 레전더리 legendary)
public static class UIThemeTools
{
    private const string Folder = "Assets/Project/Resources";
    private const string AssetPath = Folder + "/" + UITheme.ResourcePath + ".asset";
    private const string PixelSpriteFolder = "Assets/PixelUISciFiFree/Sprites/themes/cyan";

    [MenuItem("Tools/TD Shoot/Create UI Theme")]
    public static void CreateTheme()
    {
        UITheme theme = GetOrCreateTheme();
        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);
    }

    [MenuItem("Tools/TD Shoot/Apply PixelUISciFi to Theme")]
    public static void ApplyPixelUISciFi()
    {
        if (!AssetDatabase.IsValidFolder(PixelSpriteFolder))
        {
            EditorUtility.DisplayDialog("UI 테마", $"{PixelSpriteFolder} 폴더가 없습니다. PixelUISciFiFree 에셋을 확인하세요.", "확인");
            return;
        }

        UITheme theme = GetOrCreateTheme();
        Undo.RecordObject(theme, "Apply PixelUISciFi to Theme");

        int missing = 0;
        theme.panelSprite = Load("panel", ref missing);
        theme.insetPanelSprite = Load("panel_inset", ref missing);
        theme.windowSprite = Load("window", ref missing);
        theme.tooltipSprite = Load("tooltip", ref missing);
        theme.bracketSprite = Load("bracket", ref missing);
        theme.buttonNormal = Load("button_normal", ref missing);
        theme.buttonHover = Load("button_hover", ref missing);
        theme.buttonPressed = Load("button_pressed", ref missing);
        theme.buttonDisabled = Load("button_disabled", ref missing);
        theme.commonSlot = Load("slot_common", ref missing);
        theme.rareSlot = Load("slot_rare", ref missing);
        theme.eliteSlot = Load("slot_epic", ref missing);
        theme.legendarySlot = Load("slot_legendary", ref missing);
        theme.emptySlotSprite = Load("panel_inset", ref missing);
        theme.dropHighlightSprite = Load("focus", ref missing);

        // 전투 HUD (사용자 채택 A안: 배치 그대로 부품만 픽셀 스프라이트)
        theme.hudTrackSprite = Load("progress_bg", ref missing);
        theme.hudFillSprite = Load("progress_fill", ref missing);
        theme.hudActiveFillSprite = LoadBar("bar_green", ref missing);
        theme.hudFinalFillSprite = LoadBar("bar_red", ref missing);
        theme.hudMarkerSprite = Load("arrow", ref missing);
        theme.hudLifeIcon = LoadIcon("heart", ref missing);
        theme.hudBannerFrameSprite = Load("focus", ref missing);

        // 스탯 아이콘 (사용자 채택 매핑)
        theme.attackIcon = LoadIcon("crosshair", ref missing);
        theme.healthIcon = LoadIcon("heart", ref missing);
        theme.armorIcon = LoadIcon("shield", ref missing);
        theme.moveSpeedIcon = LoadIcon("rocket", ref missing);
        theme.fireRateIcon = LoadIcon("energy", ref missing);
        theme.cooldownIcon = LoadIcon("gear", ref missing);
        theme.critIcon = LoadIcon("star", ref missing);
        theme.reloadIcon = LoadIcon("fuel", ref missing);
        theme.gridEmptySprite = GenerateGridCellSprite(); // panel_inset 테두리를 흰색으로 바꾼 사본 (대칭 모서리·밑줄 없음, 밝기는 Grid Empty Color로)
        if (theme.gridEmptySprite == null) missing++;

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Selection.activeObject = theme;
        Debug.Log($"[UIThemeTools] PixelUISciFi 스프라이트를 UI 테마에 연결 (없는 스프라이트 {missing}개)");
    }

    // 그리드 빈 칸 스프라이트: panel_inset을 읽어 테두리 픽셀만 흰색으로 바꿔 새 PNG로 저장 (원본 에셋은 그대로)
    // 가져오기 설정(스프라이트·Point 필터·무압축·9-slice 테두리)은 TextureImporter로 지정 (.meta는 Unity가 생성)
    private const string GridCellSourceName = "panel_inset";
    private const string GridCellFolder = "Assets/Project/UI/Sprites";
    private const string GridCellPath = GridCellFolder + "/grid_cell.png";

    [MenuItem("Tools/TD Shoot/Generate Grid Cell Sprite")]
    public static void GenerateGridCellSpriteMenu()
    {
        Sprite sprite = GenerateGridCellSprite();
        if (sprite == null) return;

        UITheme theme = GetOrCreateTheme();
        Undo.RecordObject(theme, "Generate Grid Cell Sprite");
        theme.gridEmptySprite = sprite;
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Selection.activeObject = sprite;
    }

    private static Sprite GenerateGridCellSprite()
    {
        string sourcePath = $"{PixelSpriteFolder}/{GridCellSourceName}.png";
        if (!System.IO.File.Exists(sourcePath))
        {
            Debug.LogWarning($"[UIThemeTools] 원본 스프라이트 없음: {sourcePath}");
            return null;
        }

        // 원본 텍스처의 읽기 설정과 무관하게 PNG 파일을 직접 읽음
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(System.IO.File.ReadAllBytes(sourcePath));
        Color32[] pixels = source.GetPixels32();

        // 안쪽 배경색 = 가운데 픽셀. 그 외 보이는 픽셀(테두리)은 흰색
        Color32 inner = pixels[(source.height / 2) * source.width + source.width / 2];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.a == 0) continue;
            bool isInner = p.r == inner.r && p.g == inner.g && p.b == inner.b;
            if (!isInner) pixels[i] = new Color32(255, 255, 255, p.a);
        }

        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.SetPixels32(pixels);
        result.Apply();

        if (!AssetDatabase.IsValidFolder("Assets/Project/UI")) AssetDatabase.CreateFolder("Assets/Project", "UI");
        if (!AssetDatabase.IsValidFolder(GridCellFolder)) AssetDatabase.CreateFolder("Assets/Project/UI", "Sprites");
        System.IO.File.WriteAllBytes(GridCellPath, result.EncodeToPNG());
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(result);
        AssetDatabase.ImportAsset(GridCellPath);

        // 원본과 같은 9-slice 테두리·PPU
        TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        TextureImporter importer = AssetImporter.GetAtPath(GridCellPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = sourceImporter != null ? sourceImporter.spritePixelsPerUnit : 100f;
            importer.spriteBorder = sourceImporter != null ? sourceImporter.spriteBorder : new Vector4(3, 3, 3, 3);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GridCellPath);
        Debug.Log($"[UIThemeTools] 그리드 칸 스프라이트 생성 -> {GridCellPath} (원본 {GridCellSourceName}, 테두리 흰색)");
        return sprite;
    }

    private const string PixelIconFolder = "Assets/PixelUISciFiFree/Sprites/icons";

    private static Sprite LoadIcon(string fileName, ref int missing)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelIconFolder}/{fileName}.png");
        if (sprite == null)
        {
            missing++;
            Debug.LogWarning($"[UIThemeTools] 아이콘 없음: {fileName}");
        }
        return sprite;
    }

    private const string PixelBarFolder = "Assets/PixelUISciFiFree/Sprites/bars";

    private static Sprite LoadBar(string fileName, ref int missing)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelBarFolder}/{fileName}.png");
        if (sprite == null)
        {
            missing++;
            Debug.LogWarning($"[UIThemeTools] 막대 스프라이트 없음: {fileName}");
        }
        return sprite;
    }

    private static Sprite Load(string fileName, ref int missing)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelSpriteFolder}/{fileName}.png");
        if (sprite == null)
        {
            missing++;
            Debug.LogWarning($"[UIThemeTools] 스프라이트 없음: {fileName}");
        }
        return sprite;
    }

    internal static UITheme GetOrCreateTheme()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(AssetPath);
        if (theme == null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIThemeTools] UI 테마 생성 -> {AssetPath}");
        }
        return theme;
    }
}
