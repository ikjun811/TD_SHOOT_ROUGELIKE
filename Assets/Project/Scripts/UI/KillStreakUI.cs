using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연속 처치 카운터 (D5) — GameplayBootstrap이 생성, EnemyBase.OnAnyEnemyDeath 구독 (처치 원인 무관, 봄버 자폭은 처치 아님)
//   처치 후 streakWindow초 안에 다음 처치 → 연속 이어짐, minStreak부터 화면 오른쪽 위에 "×N KILLS" + 남은 시간 막대
//   처치마다 숫자가 커졌다 돌아옴, 연속 수에 따라 색 단계 (흰 → 노랑 → 주황 → 빨강), 끊기면 서서히 사라짐
//   연속 시간은 게임 시간 기준(히트 스톱 중 줄지 않음), 정비·결과 화면(timeScale 0)에서는 숨김, 라운드가 끝나면 초기화
//   HUD와 같은 미니멀 스타일 (배경 없음, 글자 외곽선, 막대 검정 테두리)
public class KillStreakUI : MonoBehaviour
{
    public static KillStreakUI Instance { get; private set; }

    public float streakWindow = 2f;
    public int minStreak = 2;
    public float fadeTime = 0.4f;
    public Vector2 anchoredPosition = new Vector2(-60f, -40f); // 화면 오른쪽 위 기준 (1920x1080)
    public float countFontSize = 52f;
    public float labelFontSize = 20f;
    public Vector2 barSize = new Vector2(150f, 4f);
    public float popScale = 1.3f;
    public float popTime = 0.15f;
    public Color[] tierColors =
    {
        Color.white,                       // 2~4
        new Color(1f, 0.85f, 0.25f),       // 5~9
        new Color(1f, 0.55f, 0.15f),       // 10~19
        new Color(1f, 0.25f, 0.2f),        // 20+
    };
    public int[] tierThresholds = { 5, 10, 20 };

    public int CurrentStreak => streak;
    public int BestStreak { get; private set; }

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform countRect;
    private TextMeshProUGUI countText;
    private TextMeshProUGUI labelText;
    private Image barFill;

    private int streak;
    private float lastKillTime = -100f;
    private float popElapsed = 100f;

    public static KillStreakUI EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("KillStreakUI");
        return go.AddComponent<KillStreakUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Build();
        EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
        Instance = null;
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        if (Time.time - lastKillTime > streakWindow) streak = 0;
        streak++;
        lastKillTime = Time.time;
        if (streak > BestStreak) BestStreak = streak;

        if (streak < minStreak) return;
        countText.text = $"×{streak}";
        Color color = GetTierColor(streak);
        countText.color = color;
        labelText.color = new Color(color.r, color.g, color.b, 0.75f);
        barFill.color = color;
        popElapsed = 0f;
    }

    private Color GetTierColor(int count)
    {
        int tier = 0;
        for (int i = 0; i < tierThresholds.Length; i++)
        {
            if (count >= tierThresholds[i]) tier = i + 1;
        }
        return tierColors[Mathf.Clamp(tier, 0, tierColors.Length - 1)];
    }

    private void Update()
    {
        // 라운드가 끝나 적이 정리되면(탈출·사망) 다음 라운드로 이어지지 않게 — 정비 중엔 숨김
        if (PauseMenuUI.IsPaused) { canvas.enabled = false; return; } // 일시정지는 라운드 종료가 아님 → 연속 처치 유지
        bool running = Time.timeScale > 0f;
        if (!running) streak = 0; // 정비 진입 = 라운드 종료
        float elapsed = Time.time - lastKillTime;
        bool active = streak >= minStreak && elapsed <= streakWindow + fadeTime;
        canvas.enabled = running && active;
        if (!canvas.enabled) return;

        float remaining = Mathf.Clamp01(1f - elapsed / streakWindow);
        barFill.rectTransform.anchorMax = new Vector2(remaining, 1f);
        group.alpha = elapsed <= streakWindow ? 1f : 1f - (elapsed - streakWindow) / fadeTime;

        popElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(popElapsed / popTime);
        float scale = Mathf.Lerp(popScale, 1f, 1f - (1f - t) * (1f - t));
        countRect.localScale = new Vector3(scale, scale, 1f);
    }

    #region 생성

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObj = new GameObject("Streak", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform root = rootObj.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = anchoredPosition;
        root.sizeDelta = new Vector2(260f, countFontSize + labelFontSize + 24f);
        group = rootObj.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // 큰 숫자 (오른쪽 정렬, 커질 때 오른쪽 끝 기준)
        countText = CreateText("Count", root, countFontSize, TextAlignmentOptions.Right);
        countText.fontStyle = FontStyles.Bold;
        countRect = countText.rectTransform;
        countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 1f);
        countRect.pivot = new Vector2(1f, 1f);
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(260f, countFontSize * 1.1f);

        labelText = CreateText("Label", root, labelFontSize, TextAlignmentOptions.Right);
        labelText.text = "KILLS";
        labelText.characterSpacing = 12f;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(1f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -countFontSize * 1.05f);
        labelRect.sizeDelta = new Vector2(260f, labelFontSize * 1.3f);

        // 남은 시간 막대 (오른쪽 정렬, 바탕 흐림 + 채움)
        Image barBg = CreateImage("Bar", root, new Color(1f, 1f, 1f, 0.25f));
        RectTransform barRect = barBg.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -countFontSize * 1.05f - labelFontSize * 1.3f - 4f);
        barRect.sizeDelta = barSize;
        Outline outline = barBg.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        barFill = CreateImage("Fill", barRect, Color.white);
        RectTransform fill = barFill.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        canvas.enabled = false;
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

    private static TextMeshProUGUI CreateText(string objName, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        return text;
    }

    #endregion
}
