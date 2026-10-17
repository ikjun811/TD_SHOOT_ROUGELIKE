using UnityEngine;
using UnityEngine.UI;
using JU;

// 플레이어 발밑에서 탈출 구역 방향을 가리키는 바닥 화살표 (게임 시작 시 자동 생성, 씬 배치 불필요)
// 화살표는 플레이어 주위를 돌며 탈출 구역 쪽에 놓이고, 구역 안에 있거나 사망 시 숨김
public class ExtractionIndicator : MonoBehaviour
{
    [Header("Target Reference")]
    [SerializeField] private ExtractionZone extractionZone;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<ExtractionIndicator>() == null)
        {
            new GameObject("ExtractionIndicator").AddComponent<ExtractionIndicator>();
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
        if (extractionZone == null) extractionZone = FindFirstObjectByType<ExtractionZone>();

        Vector3 direction = Vector3.zero;
        if (playerTransform != null && extractionZone != null)
        {
            direction = extractionZone.transform.position - playerTransform.position;
            direction.y = 0f;
        }

        bool playerDead = playerHealth != null && playerHealth.IsDead;
        bool visible = playerTransform != null && extractionZone != null
                       && !extractionZone.IsPlayerInside && !playerDead
                       && direction.sqrMagnitude > 0.01f;

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

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        playerTransform = playerObj.transform;
        playerHealth = playerObj.GetComponent<JUHealth>();
    }
}
