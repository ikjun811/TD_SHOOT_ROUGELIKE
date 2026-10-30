using UnityEngine;
using UnityEngine.AI;

// 적 발밑 원형 위치 표시 (사용자 요청 2026-10-30) — EnemyBase.Start가 Attach
//   바닥에 눕힌 은은한 고리 (일반 붉은색 / 엘리트 보라), 크기 = NavMeshAgent 반경 x 크기 배율
//   코드 생성 고리 텍스처 + SpriteRenderer (메시 렌더러가 아니라 피격 번쩍임·실루엣·디졸브 대상에서 빠짐), 사망하면 숨김
//   적 회전과 무관하게 매 프레임 발밑 바닥 높이에 수평으로 둠
//   어두운 곳 위치 파악용이라 살짝 발광 (재질 색 HDR glow 배율 → 블룸, 사용자 2026-10-30)
public class EnemyFootRing : MonoBehaviour
{
    public static Color normalColor = new Color(1f, 0.3f, 0.25f, 0.4f);
    public static Color eliteColor = new Color(0.72f, 0.38f, 1f, 0.55f);
    public static float radiusScale = 1.5f; // 에이전트 반경 대비
    public static float groundOffset = 0.03f;
    public static float glow = 1.8f; // 1 = 발광 없음

    private static Sprite ringSprite;
    private static Material glowMaterial;

    private EnemyBase enemy;
    private NavMeshAgent agent;
    private Transform ring;
    private SpriteRenderer ringRenderer;

    public static void Attach(EnemyBase enemy)
    {
        if (enemy.GetComponent<EnemyFootRing>() != null) return;
        enemy.gameObject.AddComponent<EnemyFootRing>();
    }

    private void Start()
    {
        enemy = GetComponent<EnemyBase>();
        agent = GetComponent<NavMeshAgent>();

        GameObject go = new GameObject("FootRing");
        ring = go.transform;
        ring.SetParent(transform, false);
        ringRenderer = go.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = GetSprite();
        ringRenderer.sharedMaterial = GetMaterial();
        ringRenderer.color = enemy != null && enemy.IsElite ? eliteColor : normalColor;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (ring == null) return;
        if (enemy != null && enemy.IsDead)
        {
            if (ringRenderer.enabled) ringRenderer.enabled = false;
            return;
        }

        float scale = transform.lossyScale.x;
        float baseOffset = agent != null ? agent.baseOffset * transform.lossyScale.y : 0f;
        float radius = (agent != null ? agent.radius : 0.5f) * scale * radiusScale;

        Vector3 foot = transform.position - Vector3.up * baseOffset;
        ring.SetPositionAndRotation(foot + Vector3.up * groundOffset, Quaternion.Euler(90f, 0f, 0f));
        // 스프라이트 한 변 = 1 유닛 → 지름에 맞춤 (부모 크기 배율 상쇄)
        float diameter = radius * 2f;
        ring.localScale = Vector3.one * (diameter / Mathf.Max(0.001f, scale));
    }

    // Sprites/Default + _Color HDR 배율 → 블룸 (CrateMarker와 같은 방식)
    private static Material GetMaterial()
    {
        if (glowMaterial != null) return glowMaterial;
        glowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EnemyFootRingGlow", hideFlags = HideFlags.DontSave };
        glowMaterial.color = new Color(glow, glow, glow, 1f);
        return glowMaterial;
    }

    // 64px 부드러운 고리 + 아주 옅은 안쪽 채움 (한 변 1 유닛)
    private static Sprite GetSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "EnemyFootRing",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half; // 0 가운데 ~ 1 가장자리
                float ringAlpha = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
                float fill = d < 0.86f ? 0.12f * d : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(ringAlpha, fill));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        ringSprite.name = "EnemyFootRing";
        ringSprite.hideFlags = HideFlags.DontSave;
        return ringSprite;
    }
}
