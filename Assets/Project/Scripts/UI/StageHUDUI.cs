using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 전투 HUD — 코드로 배치, 패널·설명 문구 없이 선과 숫자만 (사용자 결정: 텍스트 최소화·심플)
//   상단 중앙: 라운드 눈금 15칸(지난 라운드 흰색, 현재 길고 밝게, 남은 라운드 흐리게, 최종 라운드 칸 붉게)
//              그 아래 탈출 진행 선(구역 안 초록, 밖 흐린 흰색) + 오른쪽 남은 초 숫자
//   왼쪽 위: 목숨 마름모(잃은 목숨 흐리게) + 드랍률 숫자
//   알림 배너: 배경 없이 글자 + 양옆 가는 선
//   픽셀 스킨(사용자 채택 A안 — 배치 그대로, 부품만 교체): UITheme의 HUD 스프라이트가 있으면
//     눈금·탈출 막대 = 테두리 칸(progress_bg) + 채움(progress_fill / 최종 bar_red / 구역 안 bar_green), 현재 라운드 위 ▼ 표시,
//     목숨 = 하트 아이콘, 배너 = 모서리 괄호(focus). 스프라이트가 없으면 위 단색 모양
//   상단 보강: 눈금 왼쪽 "ROUND 03 / 15"·현재 칸 맥박, 막대 왼쪽 "EXTRACT"(ship 아이콘은 2026-10-30 제거)·남은 초 "12s"·구역 안 초록 맥박(90%+ 빠르게), 드랍률 앞 L 블록
//   씬 텍스트 5개는 실행 시 숨김, 외부 API(ShowNoticeBanner·HideNoticeBanner·ShowTimedNotice·SetHUDActive)는 그대로
public class StageHUDUI : MonoBehaviour
{
    public static StageHUDUI Instance { get; private set; }

    [Header("UI Canvas Root")]
    [SerializeField] private GameObject hudCanvasObject;

    [Header("기존 씬 텍스트 (실행 시 숨김)")]
    [SerializeField] private TextMeshProUGUI dropChanceText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI extractionTimerText;
    [SerializeField] private TextMeshProUGUI zoneStatusText;
    [SerializeField] private TextMeshProUGUI noticeBannerText;

    [Header("Extraction Zone Reference")]
    [SerializeField] private ExtractionZone extractionZone;

    [Header("색")]
    [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.55f);         // 탈출 진행 중
    [SerializeField] private Color pausedColor = new Color(1f, 1f, 1f, 0.8f);         // 탈출 정지(구역 밖)
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.32f);         // 빈 눈금·잃은 목숨·선 바탕
    [SerializeField] private Color finalRoundColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color lifeColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("크기 (기준 해상도 1920x1080) — 가시성 조정: 굵게·크게 + 검정 테두리")]
    [SerializeField] private Vector2 tickSize = new Vector2(26f, 5f);   // 현재 라운드는 높이 x2.6
    [SerializeField] private float tickGap = 7f;
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private float timeTextSize = 36f;   // 갈무리 12배수 (F4a, 필드 이름 변경으로 씬 옛 값 무시)
    [SerializeField] private float lifeSize = 15f;
    [SerializeField] private float dropTextSize = 24f;
    [SerializeField] private float bannerTextSize = 48f;
    [SerializeField] private Color edgeColor = new Color(0f, 0f, 0f, 0.75f);          // 선·마름모 테두리 (밝은 바닥 대비)

    [Header("픽셀 스킨 (UITheme HUD 스프라이트가 있을 때 — Tools > TD Shoot > Apply PixelUISciFi to Theme)")]
    [SerializeField] private Vector2 pixelTickSize = new Vector2(28f, 16f);
    [SerializeField] private float pixelLineHeight = 16f;
    [Tooltip("지난 라운드 채움 밝기 (현재 라운드는 1)")]
    [Range(0.1f, 1f)] [SerializeField] private float passedTickAlpha = 0.55f;
    [Tooltip("구역 밖일 때 탈출 막대 채움 색 (청록 채움에 곱함)")]
    [SerializeField] private Color pausedFillColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private float lifeIconSize = 32f; // 16px 아이콘 x2
    [SerializeField] private Color heartColor = new Color(1f, 0.35f, 0.4f);
    [SerializeField] private Color lostHeartColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("배너 모서리 괄호와 글자 사이 여백 (가로, 세로)")]
    [SerializeField] private Vector2 bannerFramePadding = new Vector2(48f, 16f);
    [Tooltip("채움 스프라이트 9-slice 경계 (왼, 아래, 오른, 위 픽셀) — 에셋 값이 오른쪽 1픽셀 모자라 바로잡음. 0이면 에셋 값 그대로")]
    [SerializeField] private Vector4 fillSpriteBorder = new Vector4(2f, 2f, 3f, 3f);

    [Header("상단 UI 보강 (사용자 채택 2026-10-29: A 라운드 글자·현재 칸 맥박 / B 탈출 아이콘·EXTRACT·초 s / D 드랍률 아이콘 / E 탈출 중 맥박)")]
    [SerializeField] private float hudLabelTextSize = 24f;
    [SerializeField] private Color hudLabelColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private float hudLabelGap = 16f;            // 눈금·막대 왼쪽 끝과 글자 사이
    [SerializeField] private float currentTickPulseSpeed = 1.2f; // 현재 라운드 칸 맥박 (회/초)
    [SerializeField] private float extractPulseSpeed = 1.6f;     // 탈출 중 초록 맥박
    [SerializeField] private float extractPulseFastSpeed = 4.5f; // 진행 90% 이상
    [Range(0f, 1f)] [SerializeField] private float extractFastThreshold = 0.9f;
    [SerializeField] private float dropIconCell = 6f;            // 드랍률 앞 테트리스 L 블록 한 칸 (px)
    [Tooltip("드랍률 숫자 앞 설명 글자 (비우면 숫자만)")]
    [SerializeField] private string dropLabel = "MODULE DROP";

    private RectTransform root;
    private RectTransform tickRow;
    private Image[] ticks = new Image[0];
    private Image[] tickFills = new Image[0];
    private Image roundMarker;
    private Image extractionFill;
    private TextMeshProUGUI extractionTimeText;
    private RectTransform livesRow;
    private Image[] lifeIcons = new Image[0];
    private TextMeshProUGUI dropText;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI extractLabel;
    private GameObject bannerRoot;
    private TextMeshProUGUI bannerText;
    private Image bannerLineLeft;
    private Image bannerLineRight;
    private Image bannerFrame;

    private UITheme theme;
    private bool pixelSkin;
    private Sprite fillSprite;
    private Sprite activeFillSprite;
    private Sprite finalFillSprite;
    private readonly System.Collections.Generic.List<Sprite> createdSprites = new System.Collections.Generic.List<Sprite>();

    private int shownMaxRound = -1;
    private int shownRound = -1;
    private int shownMaxLives = -1;
    private int shownLives = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (hudCanvasObject == null)
        {
            hudCanvasObject = gameObject;
        }

        theme = UITheme.Current;
        pixelSkin = theme.hudTrackSprite != null && theme.hudFillSprite != null;
        fillSprite = FixFillBorder(theme.hudFillSprite);
        activeFillSprite = FixFillBorder(theme.hudActiveFillSprite);
        finalFillSprite = FixFillBorder(theme.hudFinalFillSprite);

        HideSceneTexts();
        BuildHUD();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Sprite sprite in createdSprites) Destroy(sprite);
    }

    // 에셋 채움 스프라이트(progress_fill·bar_*)는 오른쪽 투명 3픽셀인데 9-slice 경계가 2라 늘어나는 가운데에 투명 줄이 섞임
    // → 100%여도 오른쪽 끝이 비어 보이던 문제. 에셋(.meta)은 그대로 두고 경계를 바로잡은 사본을 실행 중에 만들어 사용
    private Sprite FixFillBorder(Sprite source)
    {
        if (source == null || fillSpriteBorder == Vector4.zero) return source;
        Rect rect = source.rect;
        Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        Sprite fixedSprite = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, fillSpriteBorder);
        fixedSprite.name = source.name + "_HUD";
        createdSprites.Add(fixedSprite);
        return fixedSprite;
    }

    private void Start()
    {
        if (extractionZone == null)
        {
            extractionZone = FindAnyObjectByType<ExtractionZone>();
        }
        HideNoticeBanner();
    }

    private void Update()
    {
        UpdateRound();
        PulseCurrentTick();
        UpdateLives();
        UpdateDropChance();
        UpdateExtraction();
    }

    // 전투 HUD On/Off 제어 함수
    public void SetHUDActive(bool isActive)
    {
        if (hudCanvasObject != null)
        {
            hudCanvasObject.SetActive(isActive);
        }
    }

    public void ShowNoticeBanner(string message, Color textColor)
    {
        if (bannerRoot == null) return;
        bannerRoot.SetActive(true);
        bannerText.text = message;
        bannerText.color = textColor;
        if (bannerFrame != null)
        {
            bannerFrame.color = textColor;
            LayoutBannerFrame();
            return;
        }
        Color line = textColor;
        line.a *= 0.6f;
        bannerLineLeft.color = bannerLineRight.color = line;
        LayoutBannerLines();
    }

    public void HideNoticeBanner()
    {
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    // 일정 시간 뒤 자동으로 사라지는 알림 (그 사이 다른 배너로 바뀌었으면 숨기지 않음)
    public void ShowTimedNotice(string message, Color textColor, float duration)
    {
        if (bannerRoot == null) return;

        // 탈출 성공·사망 배너가 떠 있으면 덮어쓰지 않음
        if (bannerRoot.activeSelf && bannerText.text != message) return;

        ShowNoticeBanner(message, textColor);
        StartCoroutine(HideNoticeAfter(message, duration));
    }

    private System.Collections.IEnumerator HideNoticeAfter(string message, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (bannerText != null && bannerText.text == message) HideNoticeBanner();
    }

    #region 갱신

    private void UpdateRound()
    {
        if (WaveManager.Instance == null) return;
        int max = WaveManager.Instance.MaxRound;
        int round = WaveManager.Instance.CurrentRound;
        if (max != shownMaxRound) RebuildTicks(max);
        if (round == shownRound) return;
        shownRound = round;

        if (roundText != null)
        {
            roundText.text = $"ROUND {round:00} / {max:00}";
            roundText.color = round >= max ? finalRoundColor : hudLabelColor;
        }

        if (pixelSkin)
        {
            UpdatePixelTicks(round);
            return;
        }

        for (int i = 0; i < ticks.Length; i++)
        {
            int tickRound = i + 1;
            bool isFinal = tickRound == ticks.Length;
            Color color;
            float height = tickSize.y;
            if (tickRound < round) color = Color.white;
            else if (tickRound == round)
            {
                color = isFinal ? finalRoundColor : Color.white;
                height = tickSize.y * 2.6f; // 현재 라운드는 길게
            }
            else color = isFinal ? new Color(finalRoundColor.r, finalRoundColor.g, finalRoundColor.b, 0.45f) : emptyColor;

            ticks[i].color = color;
            ticks[i].rectTransform.sizeDelta = new Vector2(tickSize.x, height);
        }
    }

    // 픽셀 눈금: 지난 라운드 채움 흐리게, 현재 채움 밝게 + 위에 ▼, 남은 라운드 빈 칸, 최종 칸은 붉은 채움(남았으면 흐리게)
    private void UpdatePixelTicks(int round)
    {
        for (int i = 0; i < ticks.Length; i++)
        {
            int tickRound = i + 1;
            bool isFinal = tickRound == ticks.Length;
            Image fill = tickFills[i];
            SetSprite(fill, isFinal && finalFillSprite != null ? finalFillSprite : fillSprite);

            if (tickRound < round) fill.color = new Color(1f, 1f, 1f, passedTickAlpha);
            else if (tickRound == round) fill.color = Color.white;
            else fill.color = isFinal ? new Color(1f, 1f, 1f, 0.35f) : Color.clear;

            if (tickRound == round && roundMarker != null)
            {
                RectTransform marker = roundMarker.rectTransform;
                marker.SetParent(ticks[i].rectTransform, false);
                marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 1f);
                marker.pivot = new Vector2(0.5f, 0f);
                marker.anchoredPosition = new Vector2(0f, theme.hudPixelScale * 2f);
                roundMarker.color = isFinal ? finalRoundColor : Color.white;
                roundMarker.enabled = true;
            }
        }
        if (roundMarker != null && (round < 1 || round > ticks.Length)) roundMarker.enabled = false;
    }

    // A: 현재 라운드 칸이 천천히 맥박 (실제 시간 — 히트 스톱·슬로모션과 무관)
    private void PulseCurrentTick()
    {
        int i = shownRound - 1;
        if (i < 0 || i >= ticks.Length) return;
        float pulse = Pulse(currentTickPulseSpeed);
        bool isFinal = i == ticks.Length - 1;

        if (pixelSkin)
        {
            tickFills[i].color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, pulse));
            if (roundMarker != null)
            {
                Color m = isFinal ? finalRoundColor : Color.white;
                m.a = Mathf.Lerp(0.6f, 1f, pulse);
                roundMarker.color = m;
            }
            return;
        }

        Color c = isFinal ? finalRoundColor : Color.white;
        c.a = Mathf.Lerp(0.5f, 1f, pulse);
        ticks[i].color = c;
    }

    // 0~1 부드러운 맥박
    private static float Pulse(float speed)
    {
        return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f);
    }

    private void UpdateLives()
    {
        StageDifficultyManager difficulty = StageDifficultyManager.Instance;
        if (difficulty == null) return;

        if (difficulty.maxLives != shownMaxLives) RebuildLifeIcons(difficulty.maxLives);
        if (difficulty.currentLives == shownLives) return;
        shownLives = difficulty.currentLives;
        bool hearts = theme.hudLifeIcon != null;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            bool alive = i < shownLives;
            if (hearts) lifeIcons[i].color = alive ? heartColor : lostHeartColor;
            else lifeIcons[i].color = alive ? lifeColor : emptyColor;
        }
    }

    private void UpdateDropChance()
    {
        if (dropText == null || StageDifficultyManager.Instance == null) return;
        // 숫자만으로는 무슨 값인지 알기 어려움 (사용자 2026-10-30) → 앞에 작은 설명 글자 (EXTRACT·ROUND와 같은 영문 라벨)
        // 같은 크기(갈무리 12배수 유지)·흐리게
        string label = string.IsNullOrEmpty(dropLabel) ? "" : $"<alpha=#99>{dropLabel}<alpha=#FF>  ";
        dropText.text = $"{label}{StageDifficultyManager.Instance.currentDropChance * 100f:F0}%";
    }

    private void UpdateExtraction()
    {
        if (extractionZone == null || extractionFill == null) return;

        bool inside = extractionZone.IsPlayerInside;
        float progress = extractionZone.ProgressPercent;
        // E: 구역 안에서 진행 중이면 초록 맥박, 90% 이상이면 빠르게 (완료 후엔 멈춤)
        bool pulsing = inside && progress < 1f;
        float pulse = pulsing ? Pulse(progress >= extractFastThreshold ? extractPulseFastSpeed : extractPulseSpeed) : 1f;
        float brightness = Mathf.Lerp(0.6f, 1f, pulse);

        Color color = inside ? activeColor : pausedColor;
        Color pulsedColor = inside ? new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a) : color;
        extractionTimeText.text = Mathf.CeilToInt(extractionZone.RemainingTime) + "s";
        extractionTimeText.color = pulsedColor;
        if (extractLabel != null) extractLabel.color = inside ? pulsedColor : hudLabelColor;

        if (pixelSkin)
        {
            // 9-slice 채움은 Filled 타입이 아니라 폭으로 진행도 표시 (테두리 모양 유지)
            RectTransform fill = extractionFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            extractionFill.enabled = progress > 0.005f;
            bool green = inside && activeFillSprite != null;
            SetSprite(extractionFill, green ? activeFillSprite : fillSprite);
            extractionFill.color = inside ? new Color(brightness, brightness, brightness, 1f) : pausedFillColor;
            return;
        }

        extractionFill.fillAmount = progress;
        extractionFill.color = pulsedColor;
    }

    #endregion

    #region 생성

    private void HideSceneTexts()
    {
        foreach (TextMeshProUGUI text in new[] { dropChanceText, livesText, extractionTimerText, zoneStatusText, noticeBannerText })
        {
            if (text != null) text.gameObject.SetActive(false);
        }
    }

    private void BuildHUD()
    {
        // 이 오브젝트(또는 위)에 캔버스가 있으면 그 아래에, 없으면 자체 캔버스 생성 → SetHUDActive로 함께 켜고 끔
        Canvas parentCanvas = hudCanvasObject.GetComponentInParent<Canvas>(true);
        GameObject rootObj = new GameObject("CombatHUD", typeof(RectTransform));
        root = rootObj.GetComponent<RectTransform>();
        root.SetParent(hudCanvasObject.transform, false);
        if (parentCanvas == null)
        {
            Canvas canvas = rootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            CanvasScaler scaler = rootObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        BuildTop();
        BuildLives();
        BuildBanner();
    }

    private Vector2 TickSize => pixelSkin ? pixelTickSize : tickSize;
    private float TickRowHeight => pixelSkin ? pixelTickSize.y : tickSize.y * 2.6f;

    // 상단 중앙: 라운드 눈금 + 탈출 진행 선 + 남은 초
    private void BuildTop()
    {
        float rowWidth = 15 * TickSize.x + 14 * tickGap;
        float lineY = pixelSkin ? -62f : -58f;
        float lineHeight = pixelSkin ? pixelLineHeight : lineThickness;

        GameObject rowObj = new GameObject("RoundTicks", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tickRow = rowObj.GetComponent<RectTransform>();
        tickRow.SetParent(root, false);
        Place(tickRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(rowWidth, TickRowHeight));
        HorizontalLayoutGroup layout = rowObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = tickGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Image lineBg = CreateImage("ExtractionLine", root, pixelSkin ? Color.white : emptyColor);
        Place(lineBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, lineY), new Vector2(rowWidth, lineHeight));

        extractionFill = CreateImage("ExtractionFill", lineBg.rectTransform, activeColor);
        Stretch(extractionFill.rectTransform);
        if (pixelSkin)
        {
            SetSprite(lineBg, theme.hudTrackSprite);
            SetSprite(extractionFill, fillSprite);
        }
        else
        {
            AddEdge(lineBg);
            extractionFill.sprite = WhiteSprite();
            extractionFill.type = Image.Type.Filled;
            extractionFill.fillMethod = Image.FillMethod.Horizontal;
            extractionFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        extractionTimeText = CreateText("ExtractionTime", root, timeTextSize, TextAlignmentOptions.Left);
        extractionTimeText.fontStyle = FontStyles.Bold;
        Place(extractionTimeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(rowWidth * 0.5f + 14f, lineY), new Vector2(90f, 36f));

        // A·B: 눈금·막대 왼쪽 끝에 오른쪽 정렬 글자 "ROUND 03 / 15", [아이콘] "EXTRACT"
        float labelX = -rowWidth * 0.5f - hudLabelGap;
        roundText = CreateText("RoundLabel", root, hudLabelTextSize, TextAlignmentOptions.Right);
        roundText.fontStyle = FontStyles.Bold;
        roundText.characterSpacing = 4f;
        roundText.color = hudLabelColor;
        Place(roundText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(labelX, -34f), new Vector2(260f, 30f));

        extractLabel = CreateText("ExtractLabel", root, hudLabelTextSize, TextAlignmentOptions.Right);
        extractLabel.fontStyle = FontStyles.Bold;
        extractLabel.characterSpacing = 4f;
        extractLabel.color = hudLabelColor;
        extractLabel.text = "EXTRACT";
        Place(extractLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(labelX, lineY), new Vector2(200f, 30f));
        // EXTRACT 앞 ship 아이콘은 제거 (사용자 2026-10-30)
    }

    private void RebuildTicks(int maxRound)
    {
        shownMaxRound = maxRound;
        shownRound = -1;
        for (int i = tickRow.childCount - 1; i >= 0; i--) Destroy(tickRow.GetChild(i).gameObject);
        roundMarker = null;

        ticks = new Image[Mathf.Max(0, maxRound)];
        tickFills = new Image[ticks.Length];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = CreateImage("Tick_" + (i + 1), tickRow, pixelSkin ? Color.white : emptyColor);
            ticks[i].rectTransform.sizeDelta = TickSize;
            if (pixelSkin)
            {
                SetSprite(ticks[i], theme.hudTrackSprite);
                tickFills[i] = CreateImage("Fill", ticks[i].rectTransform, Color.clear);
                Stretch(tickFills[i].rectTransform);
                SetSprite(tickFills[i], fillSprite);
            }
            else
            {
                AddEdge(ticks[i]);
            }
        }

        // 현재 라운드 ▼ (UpdatePixelTicks가 현재 눈금 아래로 옮김)
        if (pixelSkin && theme.hudMarkerSprite != null && ticks.Length > 0)
        {
            roundMarker = CreateImage("CurrentMarker", ticks[0].rectTransform, Color.white);
            SetSprite(roundMarker, theme.hudMarkerSprite);
            Rect r = theme.hudMarkerSprite.rect;
            roundMarker.rectTransform.sizeDelta = new Vector2(r.width, r.height) * theme.hudPixelScale;
            roundMarker.enabled = false;
        }

        float rowWidth = ticks.Length * TickSize.x + Mathf.Max(0, ticks.Length - 1) * tickGap;
        tickRow.sizeDelta = new Vector2(rowWidth, TickRowHeight);
    }

    private float LifeRowHeight => theme.hudLifeIcon != null ? lifeIconSize : lifeSize * 1.6f;

    // 왼쪽 위: 목숨 마름모(또는 하트) + 드랍률
    private void BuildLives()
    {
        GameObject rowObj = new GameObject("Lives", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        livesRow = rowObj.GetComponent<RectTransform>();
        livesRow.SetParent(root, false);
        Place(livesRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(240f, LifeRowHeight));
        HorizontalLayoutGroup layout = rowObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = theme.hudLifeIcon != null ? lifeIconSize * 0.2f : lifeSize * 0.7f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // D: 드랍률 앞 아이콘 = 테트리스 L 블록 (모듈 드랍), 숫자만 오른쪽으로 밀림
        Color dropColor = new Color(1f, 1f, 1f, 0.75f);
        float dropY = -26f - LifeRowHeight - 6f;
        float cell = dropIconCell;
        float iconHeight = cell * 3f + 2f;
        GameObject iconObj = new GameObject("DropIcon", typeof(RectTransform));
        RectTransform icon = iconObj.GetComponent<RectTransform>();
        icon.SetParent(root, false);
        Place(icon, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, dropY - 15f), new Vector2(cell * 2f + 1f, iconHeight));
        Vector2Int[] shape = { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) }; // 아래에서 위로
        for (int i = 0; i < shape.Length; i++)
        {
            Image c = CreateImage("Cell_" + i, icon, dropColor);
            RectTransform r = c.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(shape[i].x * (cell + 1f), shape[i].y * (cell + 1f));
            r.sizeDelta = new Vector2(cell, cell);
            AddEdge(c);
        }

        dropText = CreateText("DropChance", root, dropTextSize, TextAlignmentOptions.Left);
        dropText.color = dropColor;
        dropText.richText = true;
        Place(dropText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f + cell * 2f + 10f, dropY), new Vector2(360f, 30f));
    }

    private void RebuildLifeIcons(int maxLives)
    {
        shownMaxLives = maxLives;
        shownLives = -1;
        for (int i = livesRow.childCount - 1; i >= 0; i--) Destroy(livesRow.GetChild(i).gameObject);

        lifeIcons = new Image[Mathf.Max(0, maxLives)];
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (theme.hudLifeIcon != null)
            {
                // 하트 아이콘 (검정 외곽선이 그림에 포함)
                Image heart = CreateImage("Life_" + i, livesRow, heartColor);
                theme.ApplySprite(heart, theme.hudLifeIcon, 1f);
                heart.rectTransform.sizeDelta = new Vector2(lifeIconSize, lifeIconSize);
                lifeIcons[i] = heart;
                continue;
            }

            // 레이아웃은 바깥 칸 크기로 정렬, 안쪽 사각형을 45도 돌려 마름모
            GameObject slot = new GameObject("Life_" + i, typeof(RectTransform));
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(livesRow, false);
            slotRect.sizeDelta = new Vector2(lifeSize * 1.5f, lifeSize * 1.5f);

            Image diamond = CreateImage("Diamond", slotRect, lifeColor);
            RectTransform d = diamond.rectTransform;
            d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f);
            d.sizeDelta = new Vector2(lifeSize, lifeSize);
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddEdge(diamond);
            lifeIcons[i] = diamond;
        }
    }

    // 알림 배너: 글자 + 양옆 가는 선 (배경 없음) / 픽셀 스킨이면 글자를 감싸는 모서리 괄호
    private void BuildBanner()
    {
        GameObject obj = new GameObject("NoticeBanner", typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(1200f, 60f));

        if (theme.hudBannerFrameSprite != null)
        {
            bannerFrame = CreateImage("Frame", rect, Color.white);
            theme.ApplySprite(bannerFrame, theme.hudBannerFrameSprite);
        }

        bannerText = CreateText("Text", rect, bannerTextSize, TextAlignmentOptions.Center);
        bannerText.fontStyle = FontStyles.Bold;
        bannerText.characterSpacing = 18f;
        Stretch(bannerText.rectTransform);

        bannerLineLeft = CreateImage("LineLeft", rect, Color.white);
        bannerLineRight = CreateImage("LineRight", rect, Color.white);
        if (bannerFrame != null)
        {
            bannerLineLeft.enabled = false;
            bannerLineRight.enabled = false;
        }
        bannerRoot = obj;
        obj.SetActive(false);
    }

    // 글자 폭에 맞춰 양옆 선 배치 (선 길이 80, 글자와 24 간격)
    private void LayoutBannerLines()
    {
        bannerText.ForceMeshUpdate();
        float half = bannerText.preferredWidth * 0.5f + 24f;
        const float length = 80f;
        foreach ((Image line, float sign) in new[] { (bannerLineLeft, -1f), (bannerLineRight, 1f) })
        {
            RectTransform r = line.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(sign < 0f ? 1f : 0f, 0.5f);
            r.anchoredPosition = new Vector2(sign * half, 0f);
            r.sizeDelta = new Vector2(length, 2.5f);
        }
    }

    // 글자 크기에 맞춰 모서리 괄호 크기 조절
    private void LayoutBannerFrame()
    {
        bannerText.ForceMeshUpdate();
        RectTransform r = bannerFrame.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(bannerText.preferredWidth + bannerFramePadding.x * 2f, bannerText.preferredHeight + bannerFramePadding.y * 2f);
    }

    // HUD 픽셀 배율로 스프라이트 적용 (같은 스프라이트면 그대로)
    private void SetSprite(Image image, Sprite sprite)
    {
        if (image.sprite == sprite) return;
        theme.ApplySprite(image, sprite, theme.hudPixelScale);
    }

    // 얇은 검정 테두리 (밝은 바닥 위에서도 선·마름모가 보이게)
    private void AddEdge(Graphic graphic)
    {
        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = edgeColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = false;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Image CreateImage(string objName, Transform parent, Color color)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 배경 없는 글자라 바닥 위에서도 읽히도록 얇은 외곽선
    private static TextMeshProUGUI CreateText(string objName, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, UIFontRole.Heading); // 외곽선(재질) 설정 전에 폰트부터
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        return text;
    }

    private static Sprite whiteSprite;

    // Filled 타입은 스프라이트가 있어야 동작
    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    #endregion
}
