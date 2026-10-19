using UnityEngine;
using UnityEngine.UI;
using JU;

// 플레이어 발밑 바닥 화살표 가이드 (전투 씬 로드 시 GameplayBootstrap이 탈출 구역용·보급 상자용 2개 자동 생성, 씬 배치 불필요)
// 화살표는 플레이어 주위를 돌며 대상 쪽에 놓임
// - 탈출 구역: 구역 안에 있거나 사망 시 숨김
// - 보급 상자: 열리지 않은 상자 중 가장 가까운 1개를 가리킴, 열기 범위 안이거나 남은 상자가 없으면 숨김
public class ExtractionIndicator : MonoBehaviour
{
    public enum GuideTarget { Extraction, SupplyCrate }

    [Header("Target Reference")]
    [SerializeField] private GuideTarget guideTarget = GuideTarget.Extraction;
    [SerializeField] private ExtractionZone extractionZone;
    [SerializeField] private float crateHideDistance = 2.2f; // 보급 상자 열기 범위 안이면 숨김

    [Header("Arrow Settings")]
    [SerializeField] private float orbitRadius = 1.3f;   // 플레이어 중심에서 화살표까지 거리 (m)
    [SerializeField] private float arrowSize = 0.9f;     // 화살표 크기 (m)
    [SerializeField] private float groundOffset = 0.05f; // 바닥과 겹침(깜빡임) 방지 높이
    [SerializeField] private Color arrowColor = new Color(0.3f, 0.8f, 1f, 0.9f);
    [SerializeField] private float pulseSpeed = 3f;
    [Range(0f, 1f)][SerializeField] private float pulseMinAlpha = 0.45f;
    [Range(0f, 0.4f)][SerializeField] private float notchDepth = 0.18f; // 밑변 파임 깊이 (0 = 완전한 삼각형)

    private const int TextureSize = 128;

    private Transform playerTransform;
    private JUHealth playerHealth;
    private GameObject arrowRoot;
    private Image arrowImage;

    // GameplayBootstrap이 전투 씬 로드마다 호출
    public static void EnsureCreated()
    {
        bool hasExtraction = false;
        bool hasCrate = false;
        foreach (ExtractionIndicator indicator in FindObjectsByType<ExtractionIndicator>())
        {
            if (indicator.guideTarget == GuideTarget.Extraction) hasExtraction = true;
            else hasCrate = true;
        }

        if (!hasExtraction)
        {
            new GameObject("ExtractionIndicator").AddComponent<ExtractionIndicator>();
        }

        if (!hasCrate)
        {
            // 노란색, 탈출 화살표보다 바깥쪽에 조금 작게 (같은 방향일 때 겹침 방지)
            ExtractionIndicator crateGuide = new GameObject("SupplyCrateIndicator").AddComponent<ExtractionIndicator>();
            crateGuide.guideTarget = GuideTarget.SupplyCrate;
            crateGuide.arrowColor = new Color(1f, 0.8f, 0.15f, 0.9f);
            crateGuide.orbitRadius = 1.9f;
            crateGuide.arrowSize = 0.7f;
        }
    }

    private void Start()
    {
        BuildArrow();
    }

    private void BuildArrow()
    {
        arrowRoot = new GameObject("ExtractionArrow", typeof(RectTransform), typeof(Canvas));
        arrowRoot.transform.SetParent(transform, false);

        Canvas canvas = arrowRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;

        RectTransform rootRect = arrowRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100f, 100f);
        arrowRoot.transform.localScale = Vector3.one * (arrowSize / 100f);

        GameObject imgObj = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        RectTransform imgRect = imgObj.GetComponent<RectTransform>();
        imgRect.SetParent(rootRect, false);
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        arrowImage = imgObj.GetComponent<Image>();
        arrowImage.sprite = CreateArrowSprite();
        arrowImage.color = arrowColor;
        arrowImage.raycastTarget = false;
    }

    // 위쪽(+Y)을 가리키는 꼬리 없는 삼각형 화살표 텍스처 생성 (밑변 가운데가 살짝 파인 내비게이션식)
    private Sprite CreateArrowSprite()
    {
        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        const float tip = 0.92f;          // 꼭짓점 높이 (비율)
        const float baseY = 0.12f;        // 밑변 양 끝 높이
        const float baseHalfWidth = 0.42f;

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (x + 0.5f) / TextureSize;
                float v = (y + 0.5f) / TextureSize;
                float dx = Mathf.Abs(u - 0.5f);

                // 양 옆 변: 꼭짓점에서 밑변 양 끝까지
                float halfWidth = baseHalfWidth * (tip - v) / (tip - baseY);
                // 밑변: 가운데로 갈수록 notchDepth만큼 위로 파임 (0이면 완전한 삼각형)
                float bottom = baseY + notchDepth * (1f - dx / baseHalfWidth);

                bool inside = v <= tip && v >= bottom && dx <= halfWidth;
                pixels[y * TextureSize + x] = inside ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
    }

    private void LateUpdate()
    {
        if (arrowRoot == null) return;

        if (playerTransform == null) FindPlayer();

        bool playerDead = playerHealth != null && playerHealth.IsDead;
        Vector3 targetPosition = Vector3.zero;
        bool hasTarget = playerTransform != null && !playerDead && TryGetTargetPosition(out targetPosition);

        Vector3 direction = Vector3.zero;
        if (hasTarget)
        {
            direction = targetPosition - playerTransform.position;
            direction.y = 0f;
        }

        bool visible = hasTarget && direction.sqrMagnitude > 0.01f;

        if (arrowRoot.activeSelf != visible) arrowRoot.SetActive(visible);
        if (!visible) return;

        direction.Normalize();

        // 플레이어 발밑 둘레, 탈출 구역 방향에 눕혀서 배치 (캔버스 위쪽이 진행 방향을 향함)
        arrowRoot.transform.position = playerTransform.position + direction * orbitRadius + Vector3.up * groundOffset;
        arrowRoot.transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

        // 천천히 밝아졌다 어두워지는 펄스
        float pulse = Mathf.Lerp(pulseMinAlpha, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
        Color c = arrowColor;
        c.a = arrowColor.a * pulse;
        arrowImage.color = c;
    }

    // 가리킬 대상 위치. 숨겨야 하는 상황(구역 안, 상자 열기 범위 안, 남은 상자 없음)이면 false
    private bool TryGetTargetPosition(out Vector3 position)
    {
        position = Vector3.zero;

        if (guideTarget == GuideTarget.Extraction)
        {
            if (extractionZone == null) extractionZone = FindAnyObjectByType<ExtractionZone>();
            if (extractionZone == null || extractionZone.IsPlayerInside) return false;

            position = extractionZone.transform.position;
            return true;
        }

        if (SupplyCrateManager.Instance == null) return false;

        SupplyCrate crate = SupplyCrateManager.Instance.GetNearestCrate(playerTransform.position);
        if (crate == null) return false;

        Vector3 offset = crate.transform.position - playerTransform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= crateHideDistance * crateHideDistance) return false;

        position = crate.transform.position;
        return true;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        playerTransform = playerObj.transform;
        playerHealth = playerObj.GetComponent<JUHealth>();
    }
}
