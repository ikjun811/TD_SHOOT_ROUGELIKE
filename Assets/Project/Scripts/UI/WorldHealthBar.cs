using UnityEngine;
using UnityEngine.UI;
using JU;

// 캐릭터 머리 위 체력바 (World Space 캔버스를 런타임에 생성, 프리팹 불필요)
// 플레이어: 에디터에서 직접 부착 (항상 표시) / 적: EnemyBase가 Start에서 자동 부착 (피격 후에만 표시)
// 외형(2026-10-31): 다른 UI와 같은 남색 바탕 + 가는 테두리 + 일정 체력마다 눈금, 맞으면 채움이 잠깐 흰빛, 잔상 흰색
//   플레이어 = 체력에 따라 청록 → 빨강 / 적 = SetEnemyStyle(고정 색, 얇게, 눈금 없음, 엘리트는 조금 크게)
[RequireComponent(typeof(JUHealth))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("Display")]
    public bool showOnlyWhenDamaged = false;
    public float visibleDuration = 3f;          // 피격 후 표시 시간 (showOnlyWhenDamaged일 때)

    [Header("Style (씬에 저장된 옛 색 값이 덮지 않도록 이름 변경)")]
    public Color healthHighColor = new Color(0.55f, 0.92f, 1f);
    public Color healthLowColor = new Color(1f, 0.28f, 0.22f);
    [Tooltip("체력 비율이 이 값 아래로 내려가면 낮은 색 쪽으로 섞이기 시작")]
    [Range(0f, 1f)] public float colorBlendStart = 0.6f;
    [Tooltip("이 비율 이하에서 완전히 낮은 색")]
    [Range(0f, 1f)] public float colorBlendEnd = 0.25f;
    public Color barTrailColor = new Color(1f, 1f, 1f, 0.8f);
    public Color barBackColor = new Color(0.02f, 0.035f, 0.05f, 0.85f);
    public Color barFrameColor = new Color(0.35f, 0.8f, 0.9f, 0.7f);
    public Color tickColor = new Color(0.02f, 0.035f, 0.05f, 0.8f);
    [Tooltip("이 체력마다 눈금 (0 = 없음). 눈금이 maxTicks보다 많으면 단위를 두 배씩 늘림")]
    public float tickHealth = 25f;
    public int maxTicks = 12;
    public float hitFlashTime = 0.12f;

    [Header("Size & Position")]
    public Vector2 barSize = new Vector2(1.0f, 0.12f); // 월드 단위 (m)
    public float extraHeight = 0.4f;                    // 콜라이더 꼭대기 위 여백
    public float trailSpeed = 0.8f;                     // 잔상 감소 속도 (초당 비율)
    [Tooltip("테두리 두께 (cm)")] public float frameWidth = 0.8f;
    [Tooltip("테두리와 채움 사이 여백 (cm)")] public float innerPadding = 0.6f;

    private JUHealth health;
    private Camera mainCamera;
    private Collider bodyCollider;
    private const float DefaultHeight = 2.2f; // 콜라이더가 없을 때 발밑 기준 높이
    private const float TickWidth = 0.5f;     // cm

    private GameObject barRoot;
    private RectTransform innerRect;
    private RectTransform fillRect;
    private RectTransform trailRect;
    private Image fillImage;
    private GameObject tickRoot;
    private float tickMaxHealth = -1f;

    private bool fixedColor;
    private Color enemyColor;

    private float lastHealth;
    private float trailRatio = 1f;
    private float visibleTimer = 0f;
    private float flashTimer = 0f;

    // 적 체력바: 고정 색·얇게·눈금 없음 (AddComponent 직후, Start 전에 호출)
    public void SetEnemyStyle(Color color, bool elite)
    {
        fixedColor = true;
        enemyColor = color;
        barSize = elite ? new Vector2(1.2f, 0.11f) : new Vector2(0.9f, 0.08f);
        frameWidth = elite ? 0.7f : 0.5f;
        innerPadding = elite ? 0.5f : 0.4f;
        tickHealth = 0f;
        Color frame = Color.Lerp(barBackColor, color, elite ? 0.8f : 0.55f);
        frame.a = 0.85f;
        barFrameColor = frame;
    }

    private void Awake()
    {
        health = GetComponent<JUHealth>();
    }

    private void Start()
    {
        mainCamera = Camera.main;

        // 체력바 높이는 매 프레임 콜라이더 꼭대기 기준으로 계산 (엘리트처럼 시작 직후 크기가 바뀌어도 머리 위 유지)
        bodyCollider = GetComponent<Collider>();

        BuildBar();

        lastHealth = health.Health;
        trailRatio = GetHealthRatio();
    }

    private void BuildBar()
    {
        // 캐릭터 회전·래그돌 영향을 받지 않도록 부모 없이 생성하고 LateUpdate에서 따라감
        barRoot = new GameObject($"{name}_HealthBar", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = barRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        // 캔버스 1단위 = 1cm (barSize(m) x 100)
        RectTransform rootRect = barRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = barSize * 100f;
        barRoot.transform.localScale = Vector3.one * 0.01f;

        CreateBarImage("Frame", barFrameColor, rootRect, 0f);
        CreateBarImage("Background", barBackColor, rootRect, frameWidth);
        innerRect = CreateBarImage("Inner", Color.clear, rootRect, frameWidth + innerPadding);
        trailRect = CreateBarImage("Trail", barTrailColor, innerRect, 0f);
        fillRect = CreateBarImage("Fill", fixedColor ? enemyColor : healthHighColor, innerRect, 0f);
        fillImage = fillRect.GetComponent<Image>();

        RebuildTicks();

        // 벽 뒤에서도 보이게
        WorldUIOverlay.Apply(barRoot);
    }

    // 앵커로 채움 비율을 조절하므로 스프라이트 없이 동작 (inset = 부모 가장자리에서 안쪽 거리)
    private RectTransform CreateBarImage(string objName, Color color, RectTransform parent, float inset)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);

        Image img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    // 최대 체력마다 눈금 위치가 달라서 바뀌면 다시 만듦 (체력 모듈)
    private void RebuildTicks()
    {
        tickMaxHealth = health.MaxHealth;
        if (tickRoot != null) Destroy(tickRoot);
        tickRoot = null;
        if (tickHealth <= 0f || health.MaxHealth <= 0f) return;

        float unit = tickHealth;
        while (health.MaxHealth / unit > maxTicks + 1) unit *= 2f;

        tickRoot = new GameObject("Ticks", typeof(RectTransform));
        RectTransform rootRect = tickRoot.GetComponent<RectTransform>();
        rootRect.SetParent(innerRect, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        for (float h = unit; h < health.MaxHealth - unit * 0.25f; h += unit)
        {
            float x = h / health.MaxHealth;
            GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            RectTransform rect = tick.GetComponent<RectTransform>();
            rect.SetParent(rootRect, false);
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.sizeDelta = new Vector2(TickWidth, 0f);
            Image img = tick.GetComponent<Image>();
            img.color = tickColor;
            img.raycastTarget = false;
        }
    }

    private void LateUpdate()
    {
        if (barRoot == null || health == null) return;

        float ratio = GetHealthRatio();

        // 피격 감지 (체력이 줄면 표시 타이머 갱신 + 흰빛)
        if (health.Health < lastHealth)
        {
            visibleTimer = visibleDuration;
            flashTimer = hitFlashTime;
        }
        lastHealth = health.Health;
        if (visibleTimer > 0f) visibleTimer -= Time.deltaTime;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        bool visible = health.Health > 0f && (!showOnlyWhenDamaged || visibleTimer > 0f);
        if (barRoot.activeSelf != visible) barRoot.SetActive(visible);
        if (!visible)
        {
            trailRatio = ratio;
            return;
        }

        if (!Mathf.Approximately(tickMaxHealth, health.MaxHealth))
        {
            RebuildTicks();
            if (tickRoot != null) WorldUIOverlay.Apply(tickRoot);
        }

        // 잔상: 줄어들 때는 천천히, 회복 시에는 즉시 맞춤
        trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;

        fillRect.anchorMax = new Vector2(ratio, 1f);
        trailRect.anchorMax = new Vector2(trailRatio, 1f);

        Color color = fixedColor
            ? enemyColor
            : Color.Lerp(healthHighColor, healthLowColor, Mathf.InverseLerp(colorBlendStart, colorBlendEnd, ratio));
        if (flashTimer > 0f && hitFlashTime > 0f) color = Color.Lerp(color, Color.white, flashTimer / hitFlashTime);
        fillImage.color = color;

        // 머리 위 위치 + 카메라를 향하도록 회전
        float barY = bodyCollider != null ? bodyCollider.bounds.max.y + extraHeight : transform.position.y + DefaultHeight;
        barRoot.transform.position = new Vector3(transform.position.x, barY, transform.position.z);
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) barRoot.transform.rotation = mainCamera.transform.rotation;
    }

    private float GetHealthRatio()
    {
        if (health.MaxHealth <= 0f) return 0f;
        return Mathf.Clamp01(health.Health / health.MaxHealth);
    }

    private void OnDisable()
    {
        if (barRoot != null) barRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (barRoot != null) Destroy(barRoot);
    }
}
