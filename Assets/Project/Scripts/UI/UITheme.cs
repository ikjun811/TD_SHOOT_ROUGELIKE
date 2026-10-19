using UnityEngine;

// 정비 UI·월드 아이템 공용 외형 값 (색·스프라이트) — 디자인 에셋 적용 시 이 에셋만 바꾸면 됨
// Resources/UITheme 에셋을 자동으로 불러옴 (없으면 기본값으로 동작, 컴포넌트 연결 불필요)
// 에셋 만들기: Tools > TD Shoot > Create UI Theme (Assets/Project/Resources/UITheme.asset)
// 픽셀 에셋 연결: Tools > TD Shoot > Apply PixelUISciFi to Theme. 씬의 기존 Image는 UIThemeSkin을 붙여 역할별 스프라이트 적용
[CreateAssetMenu(fileName = "UITheme", menuName = "GameData/UI Theme")]
public class UITheme : ScriptableObject
{
    public const string ResourcePath = "UITheme";

    [Header("Rarity Colors (모듈·무기 공통)")]
    public Color commonColor = Color.white;
    public Color rareColor = new Color(0.2f, 0.6f, 1f);
    public Color eliteColor = new Color(0.7f, 0.3f, 0.9f);
    public Color legendaryColor = new Color(1f, 0.8f, 0.1f);

    [Header("Module Category Colors")]
    public Color statColor = new Color(0.55f, 0.85f, 0.55f);
    public Color mechanismColor = new Color(1f, 0.6f, 0.25f);
    public Color specialColor = new Color(0.35f, 0.8f, 1f);
    public Color uniqueColor = new Color(1f, 0.35f, 0.45f);

    [Header("Pixel Art (PixelUISciFiFree 등 픽셀 에셋)")]
    [Tooltip("그림 1픽셀을 캔버스 몇 단위로 그릴지 (9-slice 테두리 두께). 1920x1080 캔버스 기준 3")]
    [Min(1f)] public float pixelScale = 3f;

    [Header("Panel Sprites (비우면 단색)")]
    public Sprite panelSprite;        // 일반 패널
    public Sprite insetPanelSprite;   // 안쪽으로 패인 패널 (목록·그리드 바탕)
    public Sprite windowSprite;       // 제목줄 있는 창
    public Sprite tooltipSprite;
    public Sprite bracketSprite;      // 모서리 괄호 강조 테두리

    [Header("Button Sprites (Sprite Swap)")]
    public Sprite buttonNormal;
    public Sprite buttonHover;
    public Sprite buttonPressed;
    public Sprite buttonDisabled;

    [Header("Rarity Slot Frames (전리품 카드·슬롯·툴팁 머리줄)")]
    public Sprite commonSlot;
    public Sprite rareSlot;
    public Sprite eliteSlot;
    public Sprite legendarySlot;
    [Tooltip("빈 장비·금고 슬롯")]
    public Sprite emptySlotSprite;
    public Color emptySlotTextColor = new Color(0.55f, 0.58f, 0.62f);

    [Header("Slot Content (장착·금고 슬롯 안 그림)")]
    [Tooltip("슬롯 테두리 안쪽 여백 (캔버스 단위)")]
    [Min(0f)] public float slotContentPadding = 12f;
    [Tooltip("무기 실루엣을 레어도 색으로 (끄면 흰색)")]
    public bool tintWeaponIconByRarity = true;
    [Tooltip("스탯 아이콘 픽셀 배율 (16px 아이콘 x 이 값)")]
    [Min(1f)] public float statIconScale = 2f;
    [Tooltip("모듈 계열 색 띠 두께")]
    [Min(1f)] public float categoryBarHeight = 6f;
    [Tooltip("아이콘 모양에 반영되지 않는 작동 모듈(3점사 등)이 장착됐으면 장착 슬롯 왼쪽 위에 작은 표시 (계열 색 = 작동 색)")]
    public bool showMechanismPip = true;
    [Min(2f)] public float mechanismPipSize = 9f;

    [Header("Drag (무기 고스트·놓을 자리)")]
    [Tooltip("무기를 들고 슬롯 위에 올렸을 때 씌우는 표시 (모서리 괄호)")]
    public Sprite dropHighlightSprite;
    [Tooltip("무기 고스트 크기 (전리품 버튼 260x85 대비)")]
    [Range(0.3f, 1f)] public float weaponGhostScale = 0.7f;
    [Range(0.2f, 1f)] public float weaponGhostAlpha = 0.8f;

    [Header("Stat Icons (스탯 모듈 종류)")]
    public Sprite attackIcon;
    public Sprite healthIcon;
    public Sprite armorIcon;
    public Sprite moveSpeedIcon;
    public Sprite fireRateIcon;
    public Sprite cooldownIcon;
    public Sprite critIcon;
    public Sprite reloadIcon;

    [Header("Module Grid")]
    [Tooltip("빈 칸 스프라이트 (칸마다 테두리가 보여 격자가 드러남)")]
    public Sprite gridEmptySprite;
    [Tooltip("빈 칸 스프라이트의 픽셀 배율 (칸이 작아 테두리가 두꺼우면 2로)")]
    [Min(1f)] public float gridPixelScale = 3f;
    [Tooltip("빈 칸 색 (스프라이트에 곱함). 어둡게 해 격자가 뒤로 물러나게")]
    public Color gridEmptyColor = new Color(90f / 255f, 95f / 255f, 105f / 255f);
    [Tooltip("칸 사이 간격. 0이면 모듈 안쪽 틈이 없어 하나의 블록으로 보임 (틈이 경계선과 헷갈리던 문제)")]
    [Min(0f)] public float gridSpacing = 0f;
    [Tooltip("모듈이 놓인 칸의 채움 밝기 (레어도 색 x 이 값, 경계선이 잘 보이도록 약간 어둡게)")]
    [Range(0.3f, 1f)] public float gridFillBrightness = 0.9f;
    [Tooltip("모듈 경계선 색 (다른 모듈·빈 칸과 맞닿은 변에만 그림). 밝은 채움과 대비되는 어두운 색")]
    public Color gridModuleBorderColor = new Color(5f / 255f, 11f / 255f, 18f / 255f);
    [Min(1f)] public float gridModuleBorderWidth = 3f;

    [Header("Active Effects Panel (적용 효과 목록)")]
    [Tooltip("효과 줄에 마우스를 올렸을 때 해당 모듈 경계선 색")]
    public Color gridHighlightBorderColor = new Color(64f / 255f, 208f / 255f, 236f / 255f);
    [Tooltip("강조 중일 때 나머지 모듈 밝기")]
    [Range(0.1f, 1f)] public float gridDimBrightness = 0.35f;
    [Min(8f)] public float effectsFontSize = 18f;
    [Min(10f)] public float effectsRowHeight = 28f;
    public Color effectsHeaderColor = new Color(0.55f, 0.85f, 0.95f);
    public Color effectsTextColor = new Color(0.9f, 0.92f, 0.95f);
    public Color effectsInactiveColor = new Color(0.45f, 0.47f, 0.5f);

    [Header("Tooltip (설명창, 높이는 내용에 맞춰 자동 — 값은 정비 화면 시작 시 적용)")]
    [Min(150f)] public float tooltipWidth = 340f;
    [Tooltip("테두리 안쪽 여백 (캔버스 단위)")]
    [Min(0f)] public float tooltipPadding = 14f;
    [Tooltip("이름·레어도·수치·설명 사이 간격")]
    [Min(0f)] public float tooltipSpacing = 6f;
    [Tooltip("글자 크기 (0이면 씬에 설정된 값 유지)")]
    [Min(0f)] public float tooltipTitleFontSize = 24f;
    [Min(0f)] public float tooltipBodyFontSize = 18f;
    [Tooltip("레어도 줄과 수치 사이 구분선 (알파 0이면 숨김)")]
    public Color tooltipDividerColor = new Color(64f / 255f, 208f / 255f, 236f / 255f, 0.5f);
    public Color tooltipSubTextColor = new Color(0.65f, 0.7f, 0.75f);
    [Tooltip("마우스에서 떨어진 거리")]
    public Vector2 tooltipCursorOffset = new Vector2(18f, 18f);

    private static UITheme current;
    private static bool loadedFromAsset;

    // Resources/UITheme 에셋, 없으면 기본값 인스턴스
    public static UITheme Current
    {
        get
        {
#if UNITY_EDITOR
            // 에디터에서 에셋을 나중에 만든 경우 다시 찾음
            if (current != null && !loadedFromAsset)
            {
                UITheme asset = Resources.Load<UITheme>(ResourcePath);
                if (asset != null)
                {
                    current = asset;
                    loadedFromAsset = true;
                }
            }
#endif
            if (current == null)
            {
                current = Resources.Load<UITheme>(ResourcePath);
                loadedFromAsset = current != null;
                if (current == null)
                {
                    current = CreateInstance<UITheme>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }

    public Sprite GetRaritySlotSprite(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => commonSlot,
            RarityType.Rare => rareSlot,
            RarityType.Elite => eliteSlot,
            RarityType.Legendary => legendarySlot,
            _ => commonSlot
        };
    }

    // 픽셀 스프라이트를 9-slice로 적용 (테두리 두께 = 그림 픽셀 x pixelScale). sprite가 없으면 아무것도 바꾸지 않음
    public bool ApplySprite(UnityEngine.UI.Image image, Sprite sprite)
    {
        return ApplySprite(image, sprite, pixelScale);
    }

    public Sprite GetStatIcon(ModuleStatType statType)
    {
        return statType switch
        {
            ModuleStatType.AttackDamagePercent => attackIcon,
            ModuleStatType.MaxHealthBonus => healthIcon,
            ModuleStatType.ArmorBonus => armorIcon,
            ModuleStatType.MoveSpeedPercent => moveSpeedIcon,
            ModuleStatType.FireRatePercent => fireRateIcon,
            ModuleStatType.SkillCooldownReduce => cooldownIcon,
            ModuleStatType.CriticalChance => critIcon,
            ModuleStatType.ReloadSpeedPercent => reloadIcon,
            _ => null
        };
    }

    // 아이템이 든 슬롯: 레어도 테두리 스프라이트 (없으면 false → 호출 쪽에서 기존 색 칠하기)
    public bool ApplyRaritySlot(UnityEngine.UI.Image image, RarityType rarity)
    {
        if (!ApplySprite(image, GetRaritySlotSprite(rarity))) return false;
        image.color = Color.white;
        return true;
    }

    // 빈 슬롯
    public bool ApplyEmptySlot(UnityEngine.UI.Image image)
    {
        if (!ApplySprite(image, emptySlotSprite)) return false;
        image.color = Color.white;
        return true;
    }

    public bool ApplySprite(UnityEngine.UI.Image image, Sprite sprite, float scale)
    {
        if (image == null || sprite == null) return false;

        image.sprite = sprite;
        image.type = sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(1f, scale);
        return true;
    }

    public Color GetRarityColor(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => commonColor,
            RarityType.Rare => rareColor,
            RarityType.Elite => eliteColor,
            RarityType.Legendary => legendaryColor,
            _ => commonColor
        };
    }

    public Color GetCategoryColor(ModuleCategory category)
    {
        return category switch
        {
            ModuleCategory.Stat => statColor,
            ModuleCategory.Mechanism => mechanismColor,
            ModuleCategory.Special => specialColor,
            ModuleCategory.Unique => uniqueColor,
            _ => statColor
        };
    }
}
