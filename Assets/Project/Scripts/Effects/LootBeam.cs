using UnityEngine;

// 드랍 빛기둥 (C1) — DroppedItem이 SetupVisuals에서 부착, 레어도 색 기둥으로 멀리서도 위치·등급이 보이게
//   일반: 없음 (수가 많아 화면이 어지러움 → 기존 조명만) / 희귀 3m / 엘리트 5m / 레전더리 8m + 흰 심지 + 바닥 맥박 고리 / 유니크 = 레전더리 + 큰 고리
//   LineRenderer(Sprites/Default, 카메라를 향함) 바닥 → 위로 투명해지는 그라데이션, 굵기가 은은하게 맥박
//   아이템이 회전·자석 비행을 하므로 위치는 매 프레임 월드 좌표로 다시 계산, 자석 비행 시작 시 숨김
public class LootBeam : MonoBehaviour
{
    private const int RingSegments = 40;
    private static Material sharedMaterial;

    private DroppedItem item;
    private LineRenderer beam;
    private LineRenderer core;
    private LineRenderer ring;
    private float height;
    private float width;
    private float ringRadius;
    private Color color;
    private float phase;

    public static void Attach(DroppedItem item, RarityType rarity, bool isUnique, Color color)
    {
        // SetupVisuals가 여러 번 불려도(Setup* → Start) 하나만 유지
        LootBeam lootBeam = item.GetComponent<LootBeam>();
        if (lootBeam != null) lootBeam.ClearLines();

        if (!isUnique && rarity == RarityType.Common)
        {
            if (lootBeam != null) Destroy(lootBeam);
            return;
        }

        if (lootBeam == null) lootBeam = item.gameObject.AddComponent<LootBeam>();
        lootBeam.Setup(item, rarity, isUnique, color);
    }

    private void ClearLines()
    {
        if (beam != null) Destroy(beam.gameObject);
        if (core != null) Destroy(core.gameObject);
        if (ring != null) Destroy(ring.gameObject);
        beam = core = ring = null;
    }

    private void Setup(DroppedItem owner, RarityType rarity, bool isUnique, Color beamColor)
    {
        item = owner;
        color = beamColor;
        phase = Random.Range(0f, 10f);

        switch (rarity)
        {
            case RarityType.Rare: height = 3f; width = 0.18f; break;
            case RarityType.Elite: height = 5f; width = 0.26f; break;
            default: height = 8f; width = 0.4f; break; // 레전더리
        }
        if (isUnique)
        {
            height = 9f;
            width = 0.45f;
        }

        beam = CreateLine("LootBeam", 2);
        beam.widthMultiplier = width;
        beam.colorGradient = MakeGradient(color, 0.75f);

        if (isUnique || rarity == RarityType.Legendary)
        {
            core = CreateLine("LootBeamCore", 2);
            core.widthMultiplier = width * 0.35f;
            core.colorGradient = MakeGradient(Color.Lerp(color, Color.white, 0.7f), 0.9f);

            ringRadius = isUnique ? 1.2f : 0.8f;
            ring = CreateLine("LootBeamRing", RingSegments);
            ring.loop = true;
            ring.widthMultiplier = 0.06f;
            ring.startColor = ring.endColor = color;
        }
        LateUpdate();
    }

    private LineRenderer CreateLine(string lineName, int points)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 2;
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) sharedMaterial = new Material(shader) { name = "LootBeam", hideFlags = HideFlags.DontSave };
        }
        if (sharedMaterial != null) line.sharedMaterial = sharedMaterial;
        return line;
    }

    // 바닥은 진하게, 위로 갈수록 투명
    private static Gradient MakeGradient(Color c, float bottomAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(bottomAlpha, 0f), new GradientAlphaKey(bottomAlpha * 0.5f, 0.35f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private void LateUpdate()
    {
        if (beam == null) return;
        bool visible = item != null && !item.IsFlyingToPlayer;
        SetVisible(visible);
        if (!visible) return;

        // 빛기둥은 아이템 위치에서 위로 (바닥부터 솟으면 어색 — 사용자), 맥박 고리는 바닥
        Vector3 ground = transform.position;
        ground.y = 0.05f;
        Vector3 bottom = transform.position;
        Vector3 top = bottom + Vector3.up * height;
        float pulse = 1f + 0.12f * Mathf.Sin((Time.time + phase) * 3f);

        beam.SetPosition(0, bottom);
        beam.SetPosition(1, top);
        beam.widthMultiplier = width * pulse;

        if (core != null)
        {
            core.SetPosition(0, bottom);
            core.SetPosition(1, top);
        }

        if (ring != null)
        {
            // 고리가 바깥으로 퍼지며 흐려지는 맥박 (1.2초 주기)
            float t = Mathf.Repeat((Time.time + phase) / 1.2f, 1f);
            float radius = ringRadius * Mathf.Lerp(0.4f, 1.4f, t);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            Color c = color;
            c.a = 0.9f * (1f - t);
            ring.startColor = ring.endColor = c;
        }
    }

    private void SetVisible(bool visible)
    {
        if (beam != null && beam.enabled != visible) beam.enabled = visible;
        if (core != null && core.enabled != visible) core.enabled = visible;
        if (ring != null && ring.enabled != visible) ring.enabled = visible;
    }

    private void OnDestroy()
    {
        ClearLines();
    }
}
