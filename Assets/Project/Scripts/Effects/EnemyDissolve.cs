using System.Collections.Generic;
using UnityEngine;

// 적 사망 디졸브 (비잔혹 처치 연출, 피 연출 B4 대신 — 사용자 2026-10-29) — EnemyAnimator가 쓰러지는 동작 뒤 Begin
//   모델의 Mesh·SkinnedMesh 재질을 Resources/Shaders/Dissolve(원래 텍스처·색 + 노이즈로 잘라냄 + 빛나는 경계)로 바꾸고
//   duration 동안 _Progress 0 → 1.05 (전부 사라짐), 시작 때 검은 연기 조금. 게임 시간 기준
//   디졸브 재질은 원래 재질마다 하나 만들어 공유 (적이 많아도 재질 수 고정), 진행도·경계 색은 렌더러별 PropertyBlock
//   (엘리트 보라 _BaseColor PropertyBlock은 그대로 유지)
public class EnemyDissolve : MonoBehaviour
{
    private const string ShaderName = "TDShoot/Dissolve";
    private const float EndProgress = 1.05f;
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Shader shader;
    private static readonly Dictionary<Material, Material> materialCache = new Dictionary<Material, Material>();
    private static Material smokeMaterial;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock block;
    private Color edgeColor;
    private float duration;
    private float elapsed;

    public static void Begin(GameObject target, Color edgeColor, float duration)
    {
        if (target == null) return;
        EnemyDissolve dissolve = target.AddComponent<EnemyDissolve>();
        dissolve.edgeColor = edgeColor;
        dissolve.duration = Mathf.Max(0.05f, duration);
        dissolve.Setup();
    }

    private void Setup()
    {
        if (shader == null) shader = Shader.Find(ShaderName);
        block = new MaterialPropertyBlock();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            Material first = r.sharedMaterial;
            if (first != null && first.shader != null && first.shader.name.StartsWith("TDShoot/")) continue; // 실루엣 복제본 등
            renderers.Add(r);
        }

        if (shader != null)
        {
            foreach (Renderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = GetDissolveMaterial(mats[i]);
                r.sharedMaterials = mats;
            }
        }
        Apply(0f);
        if (!VfxSpawner.Play(VfxLibrary.Current.enemyDeath, transform.position)) SpawnSmoke(transform.position); // VfxLibrary 적 사망 칸, 없으면 코드 연기
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Apply(t * EndProgress);

        // 셰이더가 없으면 끝날 때 숨기기만
        if (t >= 1f && shader == null)
        {
            foreach (Renderer r in renderers) if (r != null) r.enabled = false;
        }
    }

    private void Apply(float progress)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(ProgressId, progress);
            block.SetColor(EdgeColorId, edgeColor);
            r.SetPropertyBlock(block);
        }
    }

    private static Material GetDissolveMaterial(Material source)
    {
        if (source == null) return null;
        if (materialCache.TryGetValue(source, out Material cached) && cached != null) return cached;

        Material mat = new Material(shader) { name = source.name + "_Dissolve", hideFlags = HideFlags.DontSave };
        Texture baseMap = source.HasProperty(BaseMapId) ? source.GetTexture(BaseMapId) : source.mainTexture;
        mat.SetTexture(BaseMapId, baseMap);
        if (source.HasProperty(BaseMapId))
        {
            mat.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
            mat.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
        }
        Color color = source.HasProperty(BaseColorId) ? source.GetColor(BaseColorId) : (source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
        mat.SetColor(BaseColorId, color);
        materialCache[source] = mat;
        return mat;
    }

    // 검은 연기: 몸통 높이에서 천천히 피어오르며 커지고 사라짐 (코드 파티클)
    private static void SpawnSmoke(Vector3 position)
    {
        if (smokeMaterial == null)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DissolveSmoke", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            smokeMaterial = new Material(Shader.Find("Sprites/Default")) { name = "DissolveSmoke", mainTexture = tex, hideFlags = HideFlags.DontSave };
        }

        GameObject go = new GameObject("DissolveSmoke");
        go.transform.position = position + Vector3.up * 0.4f;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.gravityModifier = -0.15f; // 천천히 위로
        main.startColor = new Color(0.05f, 0.05f, 0.06f, 0.55f);
        main.maxParticles = 24;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2f));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = smokeMaterial;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        Destroy(go, 2f);
    }
}
