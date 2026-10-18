using UnityEngine;

// 관통·도탄 궤적 선 (BulletChain이 맞은 지점 → 다음 대상까지 그림)
// JU 탄 궤적(Trail)은 0.02초라 거의 보이지 않아, 연쇄가 일어났는지 알 수 있도록 짧게 선을 그렸다가 흐려지며 사라짐
public class ChainTracer : MonoBehaviour
{
    private const float Duration = 0.15f;
    private const float Width = 0.07f;
    private static Material sharedMaterial;

    private LineRenderer line;
    private Color color;
    private float elapsed;

    public static void Draw(Vector3 from, Vector3 to, Color color)
    {
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            sharedMaterial = new Material(shader) { name = "ChainTracer" };
        }

        GameObject obj = new GameObject("ChainTracer");
        ChainTracer tracer = obj.AddComponent<ChainTracer>();
        tracer.color = color;

        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.sharedMaterial = sharedMaterial;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = Width;
        line.endWidth = Width * 0.6f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        tracer.line = line;
        tracer.ApplyColor(1f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / Duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        ApplyColor(1f - t);
    }

    private void ApplyColor(float alpha)
    {
        Color c = color;
        c.a *= alpha;
        line.startColor = c;
        line.endColor = c;
    }
}
