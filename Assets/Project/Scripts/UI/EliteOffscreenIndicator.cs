using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 화면 밖 엘리트 방향 표시 (D1) — GameplayBootstrap이 생성
//   화면 가장자리에 보라색 화살표: 등장 예고 중(EliteSpawnTelegraph)은 크게 맥박, 살아 있는 엘리트(EnemyBase.ActiveElites)는 작게
//   대상이 화면 안이면 숨김, 정비·결과 화면(timeScale 0)에서는 전체 숨김
//   화살표 그림은 코드로 만든 삼각형 텍스처 (리소스 불필요)
public class EliteOffscreenIndicator : MonoBehaviour
{
    public static EliteOffscreenIndicator Instance { get; private set; }

    public Color color = new Color(0.75f, 0.4f, 1f, 1f);
    public float edgeMargin = 60f;          // 화면 가장자리에서 띄우는 거리 (기준 해상도 픽셀)
    public float telegraphSize = 56f;
    public float eliteSize = 36f;

    private Canvas canvas;
    private RectTransform root;
    private Sprite arrowSprite;
    private readonly List<Image> pool = new List<Image>();

    public static EliteOffscreenIndicator EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("EliteOffscreenIndicator");
        return go.AddComponent<EliteOffscreenIndicator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)transform;

        arrowSprite = CreateArrowSprite();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (arrowSprite != null)
        {
            Destroy(arrowSprite.texture);
            Destroy(arrowSprite);
        }
    }

    private void LateUpdate()
    {
        int used = 0;
        Camera cam = Camera.main;
        bool visible = cam != null && Time.timeScale > 0f;
        if (canvas.enabled != visible) canvas.enabled = visible;

        if (visible)
        {
            float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
            foreach (EliteSpawnTelegraph telegraph in EliteSpawnTelegraph.Active)
            {
                if (telegraph == null) continue;
                if (Place(cam, telegraph.transform.position, telegraphSize * (1f + 0.25f * pulse), Mathf.Lerp(0.6f, 1f, pulse), used)) used++;
            }
            foreach (EnemyBase elite in EnemyBase.ActiveElites)
            {
                if (elite == null || elite.IsDead) continue;
                if (Place(cam, elite.transform.position, eliteSize, 0.85f, used)) used++;
            }
        }

        for (int i = used; i < pool.Count; i++)
        {
            if (pool[i].enabled) pool[i].enabled = false;
        }
    }

    // 대상이 화면 밖이면 화면 중앙→대상 방향으로 가장자리에 화살표 배치, 화면 안이면 false
    private bool Place(Camera cam, Vector3 worldPosition, float size, float alpha, int index)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPosition + Vector3.up);
        if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f) return false;

        Vector2 canvasSize = root.rect.size;
        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) dir = -dir;
        if (dir.sqrMagnitude < 0.000001f) dir = Vector2.up;

        // 가장자리 사각형(여백 제외)에 닿도록 방향 벡터 늘리기 (캔버스 좌표, 중앙 기준)
        Vector2 half = canvasSize * 0.5f - Vector2.one * edgeMargin;
        Vector2 d = new Vector2(dir.x * canvasSize.x, dir.y * canvasSize.y);
        float scale = Mathf.Min(
            Mathf.Abs(d.x) > 0.0001f ? half.x / Mathf.Abs(d.x) : float.MaxValue,
            Mathf.Abs(d.y) > 0.0001f ? half.y / Mathf.Abs(d.y) : float.MaxValue);
        Vector2 pos = d * scale;

        Image arrow = GetArrow(index);
        arrow.enabled = true;
        RectTransform rect = arrow.rectTransform;
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f); // 그림은 위(+y)를 가리킴
        Color c = color;
        c.a = alpha;
        arrow.color = c;
        return true;
    }

    private Image GetArrow(int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = new GameObject("EliteArrow", typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = go.GetComponent<Image>();
            image.sprite = arrowSprite;
            image.raycastTarget = false;
            image.enabled = false;
            pool.Add(image);
        }
        return pool[index];
    }

    // 위를 가리키는 삼각형 (밑변 가운데가 살짝 파인 화살촉), 가장자리 부드럽게
    private static Sprite CreateArrowSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "EliteArrow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        Vector2 top = new Vector2(0.5f, 0.95f);
        Vector2 left = new Vector2(0.08f, 0.08f);
        Vector2 right = new Vector2(0.92f, 0.08f);
        Vector2 notch = new Vector2(0.5f, 0.3f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                bool inside = InTriangle(p, top, left, right) && !InTriangle(p, notch, left, right);
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
