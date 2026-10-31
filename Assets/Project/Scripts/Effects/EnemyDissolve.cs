using System.Collections.Generic;
using UnityEngine;

// 적 사망 디졸브 (비잔혹 처치 연출, 피 연출 B4 대신 — 사용자 2026-10-29) — EnemyAnimator가 쓰러지는 동작 뒤 Begin
//   모델의 Mesh·SkinnedMesh 재질을 Resources/Shaders/Dissolve(원래 텍스처·색 + 노이즈로 잘라냄 + 빛나는 경계)로 바꾸고
//   duration 동안 _Progress 0 → 1.05 (전부 사라짐), 그동안 경계 색 불씨가 피어오름. 게임 시간 기준
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
    private static Material emberMaterial;

    public static float emberIntensity = 2.5f; // 불씨 밝기 배율 (블룸 임계 0.95 넘게)
    public static int emberCount = 20;         // 불씨 수 (몸 크기 1 기준)

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
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name.StartsWith("TDShoot/")) continue; // 덧붙인 실루엣 재질
                    mats[i] = GetDissolveMaterial(mats[i]);
                }
                r.sharedMaterials = mats;
            }
        }
        Apply(0f);
        SpawnEmbers(); // 경계 색 불씨 (VfxLibrary.enemyDeath는 미사용)
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

    // 불씨 (코드 파티클, Vefects 먼지·검은 연기 대체): 디졸브 경계 색으로 몸통 범위에서 디졸브 동안 계속 피어올라
    //   흔들리며 떠오르다 작아져 꺼짐. 재질 _Color를 HDR 배율로 둬 블룸으로 빛남 (파티클 색은 0~1이라 색상만 담당)
    private void SpawnEmbers()
    {
        if (emberMaterial == null)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DissolveEmber", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            emberMaterial = new Material(Shader.Find("Sprites/Default")) { name = "DissolveEmber", mainTexture = tex, hideFlags = HideFlags.DontSave };
            emberMaterial.color = new Color(emberIntensity, emberIntensity, emberIntensity, 1f);
        }

        // 몸 크기 (엘리트 1.3배 등) — 모델 배율로 범위·개수 조절
        EnemyBase enemy = GetComponentInParent<EnemyBase>();
        float s = Mathf.Clamp(enemy != null ? enemy.transform.lossyScale.y : 1f, 0.6f, 2f);

        // 경계 색(HDR)을 0~1 색상으로 (밝기는 재질 배율)
        float max = Mathf.Max(edgeColor.r, Mathf.Max(edgeColor.g, edgeColor.b), 0.001f);
        Color hue = new Color(edgeColor.r / max, edgeColor.g / max, edgeColor.b / max, 1f);
        Color hueLight = Color.Lerp(hue, Color.white, 0.45f);

        GameObject go = new GameObject("DissolveEmbers");
        go.transform.position = transform.position + Vector3.up * (0.9f * s);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f * s, 0.11f * s);
        main.gravityModifier = -0.3f; // 열기로 떠오름
        main.startColor = new ParticleSystem.MinMaxGradient(hue, hueLight);
        main.maxParticles = 64;

        int total = Mathf.RoundToInt(emberCount * s);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = total * 0.7f / duration;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(total * 0.3f))) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 1.6f, 0.6f) * s;

        // 흔들림 (불티처럼 좌우로)
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 0.6f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = emberMaterial;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;

        ps.Play();
        Destroy(go, duration + 1.6f);
    }
}
