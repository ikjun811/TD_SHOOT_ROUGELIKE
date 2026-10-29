using UnityEngine;

// 보급 상자 표식 (사용자 채택 2026-10-29, A+B: 어두운 맵에서 상자 구분이 어려움) — SupplyCrate.Start가 Create
//   A 바닥 원: 실제 열기 범위(openRadius) 노란 링, 바닥에 눕힘·맥박, 플레이어가 범위 안이면 밝게 + 안쪽 진행 호(12시부터 시계 방향)
//     → 상자 위 진행 막대 대체 (탈출 구역 ExtractionZoneVisual과 같은 시각 언어)
//   B 위 꺾쇠: 상자 위에서 아래를 가리키는 ⌄가 위아래로 천천히 움직임, 블룸으로 빛남 (멀리·어둠 속에서도 보임)
//   상자의 떨림·부풂에 흔들리지 않도록 상자와 분리된 오브젝트, 열림(Hide)·상자 제거 시 함께 사라짐. 실제 시간 기준 맥박
public class CrateMarker : MonoBehaviour
{
    private const int RingSegments = 48;
    private const int ArcSegments = 48;

    public static Color color = new Color(1f, 0.8f, 0.25f, 1f);
    public static float ringWidth = 0.08f;
    public static float arcWidth = 0.16f;
    public static float arcInset = 0.22f;
    public static float chevronHeight = 2.6f;
    public static float chevronSize = 0.32f;
    public static float chevronWidth = 0.09f;
    public static float bobAmount = 0.15f;

    private static Material lineMaterial;
    private static Material glowMaterial;

    private SupplyCrate crate;
    private Transform player;
    private Vector3 center;
    private LineRenderer ring;
    private LineRenderer arc;
    private LineRenderer chevron;
    private float insideBlend;

    public static CrateMarker Create(SupplyCrate crate, Transform player)
    {
        EnsureMaterials();
        GameObject go = new GameObject("CrateMarker");
        CrateMarker marker = go.AddComponent<CrateMarker>();
        marker.crate = crate;
        marker.player = player;
        marker.center = new Vector3(crate.transform.position.x, 0f, crate.transform.position.z);
        go.transform.position = marker.center;
        marker.ring = marker.CreateFlatLine("Ring", lineMaterial, true, RingSegments);
        marker.arc = marker.CreateFlatLine("ProgressArc", glowMaterial, false, 2);
        marker.chevron = marker.CreateChevron();
        marker.SetCircle(marker.ring, crate.openRadius, RingSegments);
        return marker;
    }

    public void Hide()
    {
        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (crate == null)
        {
            Destroy(gameObject);
            return;
        }
        bool paused = Time.timeScale <= 0f;
        ring.enabled = arc.enabled = chevron.enabled = !paused;
        if (paused) return;

        float radius = crate.openRadius;
        bool inside = false;
        if (player != null)
        {
            Vector3 offset = player.position - center;
            offset.y = 0f;
            inside = offset.sqrMagnitude <= radius * radius;
        }
        insideBlend = Mathf.MoveTowards(insideBlend, inside ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * (inside ? 7f : 3f));

        // A 바닥 원
        Color ringColor = color;
        ringColor.a = Mathf.Lerp(0.55f, 1f, insideBlend) * pulse;
        ring.startColor = ring.endColor = ringColor;
        ring.widthMultiplier = ringWidth * (1f + 0.6f * insideBlend);

        // 진행 호
        float progress = crate.Progress;
        arc.enabled = progress > 0.001f;
        if (arc.enabled)
        {
            float arcRadius = Mathf.Max(0.3f, radius - arcInset);
            int count = Mathf.Max(2, Mathf.CeilToInt(ArcSegments * progress) + 1);
            arc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = (90f - 360f * progress * i / (count - 1)) * Mathf.Deg2Rad;
                arc.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * arcRadius, 0.04f, Mathf.Sin(angle) * arcRadius));
            }
            Color arcColor = color;
            arcColor.a = Mathf.Lerp(0.6f, 1f, insideBlend);
            arc.startColor = arc.endColor = arcColor;
            arc.widthMultiplier = arcWidth;
        }

        // B 위 꺾쇠: 위아래로 천천히, 카메라를 향함
        Camera cam = Camera.main;
        float y = chevronHeight + bobAmount * Mathf.Sin(Time.unscaledTime * 2.2f);
        Vector3 top = center + Vector3.up * y;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        chevron.SetPosition(0, top + (-right + up) * chevronSize);
        chevron.SetPosition(1, top);
        chevron.SetPosition(2, top + (right + up) * chevronSize);
        Color chevronColor = color;
        chevronColor.a = Mathf.Lerp(0.75f, 1f, pulse);
        chevron.startColor = chevron.endColor = chevronColor;
    }

    private void SetCircle(LineRenderer line, float r, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r));
        }
    }

    // 바닥에 눕힌 선 (TransformZ 정렬 + 아래를 보는 회전)
    private LineRenderer CreateFlatLine(string name, Material material, bool loop, int count)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = count;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = loop ? 0 : 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        return line;
    }

    private LineRenderer CreateChevron()
    {
        GameObject go = new GameObject("Chevron");
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 3;
        line.widthMultiplier = chevronWidth;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = glowMaterial;
        return line;
    }

    // 재질: Sprites/Default + _Color HDR 배율 → 블룸 (링은 은은하게, 진행 호·꺾쇠는 더 밝게)
    private static void EnsureMaterials()
    {
        if (lineMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader) { name = "CrateMarkerRing", hideFlags = HideFlags.DontSave };
        lineMaterial.color = new Color(1.6f, 1.6f, 1.6f, 1f);
        glowMaterial = new Material(shader) { name = "CrateMarkerGlow", hideFlags = HideFlags.DontSave };
        glowMaterial.color = new Color(2.6f, 2.6f, 2.6f, 1f);
    }
}
