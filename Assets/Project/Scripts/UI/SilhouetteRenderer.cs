using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 벽 뒤에 가려진 캐릭터를 단색 실루엣으로 표시 (플레이어: GameplayBootstrap, 적: EnemyBase가 자동 부착)
// 캐릭터 아래 MeshRenderer·SkinnedMeshRenderer마다 실루엣 셰이더를 쓰는 복제 렌더러를 자식으로 만듦
// 복제본은 표시용(스텐실, 큐 3100)·색칠용(큐 3101) 2개: 모든 메시의 표시가 끝난 뒤 색칠해야
// 여러 메시로 된 모델(관절·등에 멘 총 등)에서 다른 부위에 가려진 부분이 칠해지지 않음
// 같은 메시·뼈를 공유해 애니메이션·래그돌을 따라가고, 콜라이더 없음·레이어 유지라 물리·JU에 영향 없음
public class SilhouetteRenderer : MonoBehaviour
{
    private const string ShaderName = "TDShoot/Silhouette";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const int MaskQueue = 3100;
    private const int ColorQueue = 3101;
    private static Shader shader;

    public Color color = new Color(0.3f, 0.85f, 1f, 0.6f);

    // 원본이 꺼지면(무기 교체 등) 실루엣도 끄도록 짝을 기록
    private readonly List<(Renderer source, Renderer proxy)> pairs = new List<(Renderer, Renderer)>();
    private Material maskMaterial;
    private Material colorMaterial;

    public static void Attach(GameObject target, Color color)
    {
        if (target == null) return;
        SilhouetteRenderer silhouette = target.GetComponent<SilhouetteRenderer>();
        if (silhouette == null) silhouette = target.AddComponent<SilhouetteRenderer>();
        silhouette.color = color;
    }

    private void Start()
    {
        if (shader == null) shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"SilhouetteRenderer: {ShaderName} 셰이더를 찾지 못했습니다 (Resources/Shaders/Silhouette.shader 확인).");
            return;
        }

        maskMaterial = new Material(shader) { name = "SilhouetteMask", renderQueue = MaskQueue };
        maskMaterial.SetShaderPassEnabled("UniversalForward", false);

        colorMaterial = new Material(shader) { name = "SilhouetteColor", renderQueue = ColorQueue };
        colorMaterial.SetShaderPassEnabled("SRPDefaultUnlit", false);
        colorMaterial.SetColor(ColorId, color);

        foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
        {
            if (!(source is SkinnedMeshRenderer) && !(source is MeshRenderer)) continue; // 파티클·트레일·라인 등은 제외
            AddProxy(source, maskMaterial);
            AddProxy(source, colorMaterial);
        }
    }

    private void AddProxy(Renderer source, Material material)
    {
        Renderer proxy = source switch
        {
            SkinnedMeshRenderer skinned => CreateSkinnedProxy(skinned, material),
            MeshRenderer mesh => CreateMeshProxy(mesh, material),
            _ => null
        };
        if (proxy != null) pairs.Add((source, proxy));
    }

    private void LateUpdate()
    {
        foreach ((Renderer source, Renderer proxy) in pairs)
        {
            if (source == null || proxy == null) continue;
            if (proxy.enabled != source.enabled) proxy.enabled = source.enabled;
        }
    }

    private void OnDestroy()
    {
        if (maskMaterial != null) Destroy(maskMaterial);
        if (colorMaterial != null) Destroy(colorMaterial);
    }

    private Renderer CreateSkinnedProxy(SkinnedMeshRenderer source, Material material)
    {
        if (source.sharedMesh == null) return null;

        SkinnedMeshRenderer proxy = CreateProxyObject(source.transform).AddComponent<SkinnedMeshRenderer>();
        proxy.sharedMesh = source.sharedMesh;
        proxy.bones = source.bones;
        proxy.rootBone = source.rootBone;
        proxy.localBounds = source.localBounds;
        proxy.updateWhenOffscreen = source.updateWhenOffscreen;
        SetupProxy(proxy, source.sharedMesh.subMeshCount, material);
        return proxy;
    }

    private Renderer CreateMeshProxy(MeshRenderer source, Material material)
    {
        MeshFilter filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return null;

        GameObject obj = CreateProxyObject(source.transform);
        obj.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        MeshRenderer proxy = obj.AddComponent<MeshRenderer>();
        SetupProxy(proxy, filter.sharedMesh.subMeshCount, material);
        return proxy;
    }

    private static GameObject CreateProxyObject(Transform parent)
    {
        GameObject obj = new GameObject("Silhouette");
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    // 서브메시마다 같은 실루엣 재질, 그림자 없음
    private static void SetupProxy(Renderer proxy, int subMeshCount, Material material)
    {
        Material[] materials = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        proxy.sharedMaterials = materials;
        proxy.shadowCastingMode = ShadowCastingMode.Off;
        proxy.receiveShadows = false;
        proxy.lightProbeUsage = LightProbeUsage.Off;
        proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
}
