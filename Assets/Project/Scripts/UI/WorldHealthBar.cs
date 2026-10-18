using UnityEngine;
using UnityEngine.UI;
using JU;

// 캐릭터 머리 위 체력바 (World Space 캔버스를 런타임에 생성, 프리팹 불필요)
// 플레이어: 에디터에서 직접 부착 (항상 표시) / 적: EnemyBase가 Start에서 자동 부착 (피격 후에만 표시)
[RequireComponent(typeof(JUHealth))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("Display")]
    public bool showOnlyWhenDamaged = false;
    public float visibleDuration = 3f;          // 피격 후 표시 시간 (showOnlyWhenDamaged일 때)
    public Color fillColor = new Color(0.3f, 0.85f, 0.3f);
    public Color trailColor = new Color(1f, 0.85f, 0.2f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Size & Position")]
    public Vector2 barSize = new Vector2(1.0f, 0.12f); // 월드 단위 (m)
    public float extraHeight = 0.4f;                    // 콜라이더 꼭대기 위 여백
    public float trailSpeed = 0.8f;                     // 잔상 감소 속도 (초당 비율)

    private JUHealth health;
    private Camera mainCamera;
    private Collider bodyCollider;
    private const float DefaultHeight = 2.2f; // 콜라이더가 없을 때 발밑 기준 높이

    private GameObject barRoot;
    private RectTransform fillRect;
    private RectTransform trailRect;

    private float lastHealth;
    private float trailRatio = 1f;
    private float visibleTimer = 0f;

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

        // 100 x 10 픽셀 캔버스를 축소해 barSize(m)에 맞춤
        RectTransform rootRect = barRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100f, 10f);
        barRoot.transform.localScale = new Vector3(barSize.x / 100f, barSize.y / 10f, 1f);

        CreateBarImage("Background", backgroundColor, rootRect);
        trailRect = CreateBarImage("Trail", trailColor, rootRect);
        fillRect = CreateBarImage("Fill", fillColor, rootRect);

        // 벽 뒤에서도 보이게
        WorldUIOverlay.Apply(barRoot);
    }

    // 앵커로 채움 비율을 조절하므로 스프라이트 없이 동작
    private RectTransform CreateBarImage(string objName, Color color, RectTransform parent)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    private void LateUpdate()
    {
        if (barRoot == null || health == null) return;

        float ratio = GetHealthRatio();

        // 피격 감지 (체력이 줄면 표시 타이머 갱신)
        if (health.Health < lastHealth) visibleTimer = visibleDuration;
        lastHealth = health.Health;
        if (visibleTimer > 0f) visibleTimer -= Time.deltaTime;

        bool visible = health.Health > 0f && (!showOnlyWhenDamaged || visibleTimer > 0f);
        if (barRoot.activeSelf != visible) barRoot.SetActive(visible);
        if (!visible)
        {
            trailRatio = ratio;
            return;
        }

        // 잔상: 줄어들 때는 천천히, 회복 시에는 즉시 맞춤
        trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;

        fillRect.anchorMax = new Vector2(ratio, 1f);
        trailRect.anchorMax = new Vector2(trailRatio, 1f);

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
