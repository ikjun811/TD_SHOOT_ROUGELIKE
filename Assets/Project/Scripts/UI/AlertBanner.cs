using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경고 배너 (D2 라운드 시작 + 엘리트 출현) — 첫 호출 시 자동 생성 (씬 연결 불필요)
//   라운드: 사용자 채택 A안 경고 띠 — 화면 가로 띠(어두운 바탕) + 위·아래 사선 경고 줄무늬(흐름) + 가운데 큰 영문 + 보조줄, 양옆 ⚠
//     미끄러져 들어오는 연출 없이 제자리에서 (사용자): 띠가 가운데에서 위아래로 펼쳐짐 → 큰 글자 몇 번 깜빡임 → 유지 → 위아래로 접힘
//     라운드 노랑 / 최종 라운드 빨강, 새 배너가 오면 바로 교체
//     같은 띠로 통일 (사용자): 탈출 성공 초록 / 임무 완료(최종 탈출) 금색 / 임무 실패·게임 오버 빨강
//   엘리트: 후반에 자주 나와 띠는 방해됨 (사용자) → HUD 아래 작은 한 줄 "── ⚠ ELITE INCOMING ×N ──" 보라
//     표시 중 또 나오면 다시 재생하지 않고 개수만 올리고 유지 시간 연장 (살짝 커졌다 돌아오는 강조만)
//   실제 시간 기준이지만 timeScale 0(정비·결과)인 동안은 숨기고 멈춤 → 정비 중에 호출돼도 전투 재개 후 처음부터
public class AlertBanner : MonoBehaviour
{
    private static AlertBanner instance;

    public float openTime = 0.18f;
    public float closeTime = 0.22f;
    public float blinkTime = 0.6f;                 // 처음 이 시간 동안 큰 글자 깜빡임
    public float blinkInterval = 0.1f;
    public float roundHoldTime = 1.6f;
    public float eliteHoldTime = 1.8f;
    public float positionY = 200f;                 // 화면 중앙 기준 위로 (플레이어 머리 위를 피함)
    public float bandHeight = 124f;
    public float stripeHeight = 14f;
    public float stripeTileSize = 28f;             // 줄무늬 한 칸 크기 (캔버스 단위)
    public float stripeScrollSpeed = 0.6f;         // 초당 칸 수
    public float titleFontSize = 60f;   // 갈무리 12배수 (F4a)
    public float subFontSize = 24f;
    public float iconSize = 54f;
    public Color backgroundColor = new Color(0.02f, 0.02f, 0.03f, 0.72f);
    public Color roundColor = new Color(1f, 0.8f, 0.15f);
    public Color finalRoundColor = new Color(1f, 0.28f, 0.22f);
    public Color eliteColor = new Color(0.78f, 0.42f, 1f);
    public Color successColor = new Color(0.35f, 0.95f, 0.45f);
    public Color clearColor = new Color(1f, 0.85f, 0.3f);
    public Color failColor = new Color(1f, 0.3f, 0.3f);

    [Header("엘리트 한 줄 알림")]
    public float eliteTopOffset = 112f;            // 화면 위에서 아래로 (HUD 라운드 눈금·탈출 막대 아래)
    public float eliteFontSize = 24f;
    public float eliteIconSize = 26f;
    public float eliteLineLength = 70f;
    public float eliteFadeIn = 0.15f;
    public float eliteFadeOut = 0.3f;
    public float elitePopScale = 1.15f;            // 새 엘리트마다 잠깐 커졌다 돌아옴
    public float elitePopTime = 0.18f;

    private Canvas canvas;
    private RectTransform band;
    private RawImage stripeTop;
    private RawImage stripeBottom;
    private TextMeshProUGUI title;
    private TextMeshProUGUI sub;
    private CanvasGroup titleGroup;
    private RectTransform iconLeft;
    private RectTransform iconRight;
    private Image iconLeftImage;
    private Image iconRightImage;
    private Texture2D stripeTexture;
    private Sprite triangleSprite;

    private float elapsed = -1f; // <0 = 표시 안 함
    private float holdTime;
    private SoundEntry pendingSound;

    private RectTransform eliteRoot;
    private CanvasGroup eliteGroup;
    private TextMeshProUGUI eliteText;
    private RectTransform eliteIcon;
    private Image eliteLineLeft;
    private Image eliteLineRight;
    private float eliteElapsed = -1f; // <0 = 표시 안 함
    private float elitePopElapsed;
    private int eliteCount;
    private bool pendingEliteSound;

    // 라운드 = 깊이 표시 (DEPTH ZERO 컨셉, 2026-10-30): 숫자가 줄어드는 이유를 보조줄이 매번 설명
    //   첫 라운드 = 상황 브리핑, 일반 = 지상까지 남은 층 + 위로 갈수록 적이 많아짐(사용자 채택 문구), 최종 = 지상 바로 아래
    public static void ShowRound(int round, int maxRound)
    {
        bool isFinal = maxRound > 0 && round >= maxRound;
        int depth = DepthDisplay.FromRound(round, maxRound);
        AlertBanner banner = GetInstance();
        if (isFinal)
        {
            banner.Begin(DepthDisplay.Label(depth), "SURFACE AHEAD  —  SURVIVE AND EXTRACT", banner.finalRoundColor, banner.roundHoldTime, AudioManager.Library.roundStart);
        }
        else if (round <= 1)
        {
            banner.Begin(DepthDisplay.Label(depth), "FACILITY BREACHED  —  CLIMB TO THE SURFACE", banner.roundColor, banner.roundHoldTime, AudioManager.Library.roundStart);
        }
        else
        {
            banner.Begin(DepthDisplay.Label(depth), $"{depth} FLOORS TO SURFACE  —  MORE HOSTILES AHEAD", banner.roundColor, banner.roundHoldTime, AudioManager.Library.roundStart);
        }
    }

    // 탈출 성공 (ExtractionZone, 슬로모션·암전 동안 유지) — 최종 라운드면 지상 도달 (제목 회수)
    public static void ShowExtractionSuccess(int round, bool isGameClear, float hold)
    {
        AlertBanner banner = GetInstance();
        if (isGameClear) banner.Begin(DepthDisplay.Label(0), "SURFACE REACHED", banner.clearColor, hold, null);
        else banner.Begin("FLOOR CLEARED", $"ASCENDING TO {DepthDisplay.Label(DepthDisplay.FromRound(round) - 1)}", banner.successColor, hold, null);
    }

    // 사망 (PlayerDeathHandler) — 남은 목숨이 없으면 게임 오버
    public static void ShowMissionFailed(int livesLeft, float hold)
    {
        AlertBanner banner = GetInstance();
        if (livesLeft <= 0) banner.Begin("GAME OVER", "NO LIVES REMAINING", banner.failColor, hold, null);
        else banner.Begin("MISSION FAILED", $"LIVES REMAINING  {livesLeft}  —  RETRY THIS FLOOR", banner.failColor, hold, null);
    }

    // 직접 문구 지정 (튜토리얼 등) — success면 탈출 성공 색, 아니면 라운드 색. hold 0 이하면 라운드 배너 유지 시간
    public static void ShowCustom(string title, string subtitle, bool success, float hold = 0f)
    {
        AlertBanner banner = GetInstance();
        banner.Begin(title, subtitle, success ? banner.successColor : banner.roundColor,
            hold > 0f ? hold : banner.roundHoldTime, success ? null : AudioManager.Library.roundStart);
    }

    public static void ShowElite()
    {
        GetInstance().BeginElite();
    }

    private static AlertBanner GetInstance()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("AlertBanner");
            instance = go.AddComponent<AlertBanner>();
        }
        return instance;
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        stripeTexture = CreateStripeTexture();
        triangleSprite = CreateTriangleSprite();

        // 띠: 화면 가로 전체
        GameObject bandObj = new GameObject("Band", typeof(RectTransform), typeof(Image));
        band = bandObj.GetComponent<RectTransform>();
        band.SetParent(transform, false);
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.anchoredPosition = new Vector2(0f, positionY);
        band.sizeDelta = new Vector2(0f, bandHeight);
        Image background = bandObj.GetComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        stripeTop = CreateStripe("StripeTop", 1f);
        stripeBottom = CreateStripe("StripeBottom", 0f);

        // 큰 글자 + 양옆 ⚠ (깜빡임은 이 묶음 전체)
        GameObject titleRoot = new GameObject("TitleGroup", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform titleRect = titleRoot.GetComponent<RectTransform>();
        titleRect.SetParent(band, false);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 12f);
        titleRect.sizeDelta = new Vector2(1400f, titleFontSize * 1.3f);
        titleGroup = titleRoot.GetComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;

        title = CreateText("Title", titleRect, titleFontSize, Vector2.zero);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 14f;
        iconLeft = CreateWarningIcon("IconLeft", titleRect, out iconLeftImage);
        iconRight = CreateWarningIcon("IconRight", titleRect, out iconRightImage);

        sub = CreateText("Sub", band, subFontSize, new Vector2(0f, -36f));
        sub.characterSpacing = 10f;

        band.localScale = new Vector3(1f, 0f, 1f);
        BuildEliteLine();
        canvas.enabled = false;
    }

    // 엘리트 한 줄: 선 ─ ⚠ 글자 ─ 선 (배경 없음)
    private void BuildEliteLine()
    {
        GameObject rootObj = new GameObject("EliteLine", typeof(RectTransform), typeof(CanvasGroup));
        eliteRoot = rootObj.GetComponent<RectTransform>();
        eliteRoot.SetParent(transform, false);
        eliteRoot.anchorMin = eliteRoot.anchorMax = new Vector2(0.5f, 1f);
        eliteRoot.pivot = new Vector2(0.5f, 0.5f);
        eliteRoot.anchoredPosition = new Vector2(0f, -eliteTopOffset);
        eliteRoot.sizeDelta = new Vector2(900f, eliteFontSize * 1.6f);
        eliteGroup = rootObj.GetComponent<CanvasGroup>();
        eliteGroup.blocksRaycasts = false;
        eliteGroup.alpha = 0f;

        eliteText = CreateText("Text", eliteRoot, eliteFontSize, Vector2.zero);
        eliteText.fontStyle = FontStyles.Bold;
        eliteText.characterSpacing = 8f;
        eliteText.color = eliteColor;

        eliteIcon = CreateWarningIcon("Icon", eliteRoot, eliteIconSize, out Image icon);
        icon.color = eliteColor;

        eliteLineLeft = CreateLine("LineLeft", eliteRoot);
        eliteLineRight = CreateLine("LineRight", eliteRoot);
    }

    private Image CreateLine(string objName, Transform parent)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(eliteLineLength, 2f);
        Image image = obj.GetComponent<Image>();
        image.color = new Color(eliteColor.r, eliteColor.g, eliteColor.b, 0.7f);
        image.raycastTarget = false;
        return image;
    }

    private void BeginElite()
    {
        // 표시 중이면 개수만 올리고 유지 시간 처음부터 (펼침 연출 반복 없음)
        if (eliteElapsed >= 0f)
        {
            eliteCount++;
            eliteElapsed = Mathf.Min(eliteElapsed, eliteFadeIn);
        }
        else
        {
            eliteCount = 1;
            eliteElapsed = 0f;
        }
        elitePopElapsed = 0f;
        pendingEliteSound = true;

        eliteText.text = eliteCount > 1 ? $"ELITE INCOMING  ×{eliteCount}" : "ELITE INCOMING";
        eliteText.ForceMeshUpdate();

        // 아이콘 + 글자 묶음을 가운데로, 양옆에 선
        const float iconGap = 14f;
        const float lineGap = 16f;
        float half = eliteText.preferredWidth * 0.5f;
        float shift = (eliteIconSize + iconGap) * 0.5f;
        eliteText.rectTransform.anchoredPosition = new Vector2(shift, 0f);
        eliteIcon.anchoredPosition = new Vector2(shift - half - iconGap - eliteIconSize * 0.5f, 0f);
        RectTransform left = eliteLineLeft.rectTransform;
        left.pivot = new Vector2(1f, 0.5f);
        left.anchoredPosition = new Vector2(shift - half - iconGap - eliteIconSize - lineGap, 0f);
        RectTransform right = eliteLineRight.rectTransform;
        right.pivot = new Vector2(0f, 0.5f);
        right.anchoredPosition = new Vector2(shift + half + lineGap, 0f);
        ApplyElite();
    }

    private void ApplyElite()
    {
        float inT = eliteFadeIn > 0f ? Mathf.Clamp01(eliteElapsed / eliteFadeIn) : 1f;
        float outStart = eliteFadeIn + eliteHoldTime;
        float outT = eliteFadeOut > 0f ? Mathf.Clamp01((eliteElapsed - outStart) / eliteFadeOut) : 0f;
        eliteGroup.alpha = inT * (1f - outT);

        float popT = elitePopTime > 0f ? Mathf.Clamp01(elitePopElapsed / elitePopTime) : 1f;
        float scale = Mathf.Lerp(elitePopScale, 1f, 1f - (1f - popT) * (1f - popT));
        eliteRoot.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (stripeTexture != null) Destroy(stripeTexture);
        if (triangleSprite != null)
        {
            Destroy(triangleSprite.texture);
            Destroy(triangleSprite);
        }
    }

    private void Begin(string titleText, string subText, Color color, float hold, SoundEntry sound)
    {
        title.text = titleText;
        title.color = color;
        sub.text = subText;
        sub.color = new Color(color.r, color.g, color.b, 0.75f);
        stripeTop.color = stripeBottom.color = color;
        iconLeftImage.color = iconRightImage.color = color;

        // 아이콘은 글자 폭 바깥에
        title.ForceMeshUpdate();
        float offset = title.preferredWidth * 0.5f + iconSize * 0.5f + 28f;
        iconLeft.anchoredPosition = new Vector2(-offset, 0f);
        iconRight.anchoredPosition = new Vector2(offset, 0f);

        holdTime = hold;
        pendingSound = sound;
        elapsed = 0f;
        Apply();
    }

    private void Update()
    {
        bool any = elapsed >= 0f || eliteElapsed >= 0f;

        // 정비·결과 화면(timeScale 0)에서는 숨기고 멈춤
        bool running = Time.timeScale > 0f;
        canvas.enabled = running && any;
        if (!running || !any) return;

        if (pendingSound != null)
        {
            AudioManager.PlaySfx(pendingSound);
            pendingSound = null;
        }
        if (pendingEliteSound)
        {
            AudioManager.PlaySfx(AudioManager.Library.eliteIncoming);
            pendingEliteSound = false;
        }

        float dt = Time.unscaledDeltaTime;
        if (elapsed >= 0f)
        {
            elapsed += dt;
            if (elapsed >= openTime + holdTime + closeTime)
            {
                elapsed = -1f;
                band.localScale = new Vector3(1f, 0f, 1f);
            }
            else Apply();
        }

        if (eliteElapsed >= 0f)
        {
            eliteElapsed += dt;
            elitePopElapsed += dt;
            if (eliteElapsed >= eliteFadeIn + eliteHoldTime + eliteFadeOut)
            {
                eliteElapsed = -1f;
                eliteGroup.alpha = 0f;
            }
            else ApplyElite();
        }
    }

    private void Apply()
    {
        // 펼침·접힘 (세로 배율, 가운데 기준)
        float openT = openTime > 0f ? Mathf.Clamp01(elapsed / openTime) : 1f;
        float closeStart = openTime + holdTime;
        float closeT = closeTime > 0f ? Mathf.Clamp01((elapsed - closeStart) / closeTime) : 0f;
        float scaleY = (1f - (1f - openT) * (1f - openT)) * (1f - closeT * closeT);
        band.localScale = new Vector3(1f, scaleY, 1f);

        // 큰 글자 깜빡임 (펼친 직후 blinkTime 동안)
        float sinceOpen = elapsed - openTime;
        bool visible = sinceOpen >= blinkTime || sinceOpen < 0f || Mathf.FloorToInt(sinceOpen / Mathf.Max(0.01f, blinkInterval)) % 2 == 0;
        titleGroup.alpha = visible ? 1f : 0.15f;

        // 줄무늬 흐름 (위·아래 반대 방향)
        float width = Mathf.Max(1f, band.rect.width);
        float tiles = width / Mathf.Max(1f, stripeTileSize);
        float scroll = Time.unscaledTime * stripeScrollSpeed;
        float v = stripeHeight / Mathf.Max(1f, stripeTileSize);
        stripeTop.uvRect = new Rect(scroll, 0f, tiles, v);
        stripeBottom.uvRect = new Rect(-scroll, 0f, tiles, v);
    }

    private RawImage CreateStripe(string objName, float anchorY)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(RawImage));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(band, false);
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, anchorY);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, stripeHeight);
        RawImage image = obj.GetComponent<RawImage>();
        image.texture = stripeTexture;
        image.raycastTarget = false;
        return image;
    }

    // 삼각형 + 가운데 "!" (글자는 검정)
    private RectTransform CreateWarningIcon(string objName, Transform parent, out Image image)
    {
        return CreateWarningIcon(objName, parent, iconSize, out image);
    }

    private RectTransform CreateWarningIcon(string objName, Transform parent, float iconSize, out Image image)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        image = obj.GetComponent<Image>();
        image.sprite = triangleSprite;
        image.raycastTarget = false;

        TextMeshProUGUI mark = CreateText("Mark", rect, iconSize * 0.6f, new Vector2(0f, -iconSize * 0.08f));
        mark.text = "!";
        mark.fontStyle = FontStyles.Bold;
        mark.color = new Color(0.05f, 0.05f, 0.05f);
        mark.outlineWidth = 0f;
        mark.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        return rect;
    }

    private static TextMeshProUGUI CreateText(string objName, Transform parent, float size, Vector2 position)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1400f, size * 1.4f);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, UIFontRole.Heading); // 외곽선(재질) 설정 전에 폰트부터
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        return text;
    }

    // 사선 경고 줄무늬 한 칸 (흰 줄 = 색이 입혀짐, 나머지 거의 검정), 반복
    private static Texture2D CreateStripeTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AlertStripe",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool stripe = ((x + y) % size) < size / 2;
                pixels[y * size + x] = stripe ? new Color32(255, 255, 255, 255) : new Color32(14, 14, 16, 255);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 위를 가리키는 꽉 찬 삼각형, 가장자리 부드럽게
    private static Sprite CreateTriangleSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AlertTriangle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                const float bottom = 0.08f;
                const float top = 0.94f;
                float halfWidth = 0.46f * (top - v) / (top - bottom);
                bool inside = v >= bottom && v <= top && Mathf.Abs(u - 0.5f) <= halfWidth;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
