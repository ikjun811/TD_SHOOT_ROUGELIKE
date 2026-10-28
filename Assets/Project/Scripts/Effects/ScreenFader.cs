using UnityEngine;
using UnityEngine.UI;

// 화면 전체 검정 페이드 (D3 탈출 성공 등) — 첫 호출 시 자동 생성 (씬 연결 불필요)
//   실제 시간 기준이라 timeScale 0(정비)·슬로모션 중에도 일정 속도. 클릭은 막지 않음 (raycastTarget 끔)
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;

    private Canvas canvas;
    private Image image;
    private float from;
    private float to;
    private float duration;
    private float elapsed;

    // alpha(0 = 투명, 1 = 검정)까지 duration초 동안
    public static void FadeTo(float alpha, float duration)
    {
        if (instance == null)
        {
            if (alpha <= 0f) return; // 이미 투명
            GameObject go = new GameObject("ScreenFader");
            instance = go.AddComponent<ScreenFader>();
        }
        instance.Begin(Mathf.Clamp01(alpha), duration);
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 모든 UI 위

        GameObject obj = new GameObject("Black", typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image = obj.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Begin(float alpha, float time)
    {
        from = image.color.a;
        to = alpha;
        duration = Mathf.Max(0f, time);
        elapsed = 0f;
        canvas.enabled = true;
        if (duration <= 0f) SetAlpha(to);
    }

    private void Update()
    {
        if (duration <= 0f || elapsed >= duration) return;
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        SetAlpha(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
    }

    private void SetAlpha(float alpha)
    {
        image.color = new Color(0f, 0f, 0f, alpha);
        canvas.enabled = alpha > 0.001f;
    }
}
