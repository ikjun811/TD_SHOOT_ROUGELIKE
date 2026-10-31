using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 벽 뒤에 가려진 캐릭터를 단색 실루엣으로 표시 (플레이어: GameplayBootstrap, 적: EnemyBase가 자동 부착)
// 실루엣 셰이더 재질 2개: 표시용(스텐실, 큐 3100)·색칠용(큐 3101) — 모든 메시의 표시가 끝난 뒤 색칠해야
// 여러 메시로 된 모델(관절·등에 멘 총 등)에서 다른 부위에 가려진 부분이 칠해지지 않음
// 최적화 (2026-10-31): 서브메시가 1개인 렌더러는 원래 몸체 재질 뒤에 두 재질을 덧붙여 그림
//   → 복제 SkinnedMeshRenderer가 스키닝을 따로 하던 비용(몸 계산 3배)이 없어짐. 그림자·깊이 패스가 없는 셰이더라 그림자 영향 없음
//   서브메시가 여러 개면(덧붙인 재질이 마지막 조각에만 그려짐) 기존처럼 같은 메시·뼈를 공유하는 복제 렌더러
//   피격 번쩍임(EnemyHitFlash)이 재질을 통째로 바꾸므로 그쪽에 덧붙일 재질을 알려 번쩍임 중에도 유지
// 콜라이더 없음·레이어 유지라 물리·JU에 영향 없음
public class SilhouetteRenderer : MonoBehaviour
{
    private const string ShaderName = "TDShoot/Silhouette";
    private static readonly int ColorId = Shader.PropertyToID("_SilhouetteColor");
    private const int MaskQueue = 3100;
    private const int ColorQueue = 3101;
    private static Shader shader;

    public Color color = new Color(0.3f, 0.85f, 1f, 0.6f);

    // 복제 방식: 원본이 꺼지면(무기 교체 등) 실루엣도 끄도록 짝을 기록
    private readonly List<(Renderer source, Renderer proxy)> pairs = new List<(Renderer, Renderer)>();
    // 덧붙임 방식: 같은 렌더러라 켜짐·꺼짐은 자동으로 따라감
    private readonly List<Renderer> appended = new List<Renderer>();
    private Material maskMaterial;
    private Material colorMaterial;
    private Material[] trailing;
    private EnemyHitFlash hitFlash;
    private bool started;

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

        trailing = new[] { maskMaterial, colorMaterial };
        hitFlash = GetComponent<EnemyHitFlash>();

        foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
        {
            if (!(source is SkinnedMeshRenderer) && !(source is MeshRenderer)) continue; // 파티클·트레일·라인 등은 제외
            if (IsSilhouetteMaterial(source.sharedMaterial)) continue;
            if (CanAppend(source))
            {
                appended.Add(source);
            }
            else
            {
                AddProxy(source, maskMaterial);
                AddProxy(source, colorMaterial);
            }
        }

        started = true;
        SetAppended(true);
    }

    private static bool IsSilhouetteMaterial(Material mat)
    {
        return mat != null && mat.shader == shader;
    }

    // 서브메시 1개·재질 1개면 덧붙여도 몸 전체에 그려짐
    private static bool CanAppend(Renderer source)
    {
        Mesh mesh = source switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            MeshRenderer => source.GetComponent<MeshFilter>() != null ? source.GetComponent<MeshFilter>().sharedMesh : null,
            _ => null
        };
        return mesh != null && mesh.subMeshCount == 1 && source.sharedMaterials.Length == 1;
    }

    // 몸체 재질 뒤에 실루엣 재질을 붙이거나(on) 떼기 — 번쩍임 컴포넌트가 관리하는 렌더러는 그쪽에 맡김
    private void SetAppended(bool on)
    {
        if (trailing == null) return;
        foreach (Renderer r in appended)
        {
            if (r == null) continue;
            if (hitFlash != null && hitFlash.SetTrailingMaterials(r, on ? trailing : null)) continue;

            List<Material> mats = new List<Material>(r.sharedMaterials);
            mats.RemoveAll(m => m == maskMaterial || m == colorMaterial);
            if (on) mats.AddRange(trailing);
            r.sharedMaterials = mats.ToArray();
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

    private void OnEnable()
    {
        if (started) SetAppended(true);
    }

    // 컴포넌트를 끄면 실루엣 전체 숨김 (적 시체 등), 다시 켜면 덧붙임 복구·복제본은 LateUpdate가 원본 상태로 맞춤
    private void OnDisable()
    {
        foreach ((Renderer _, Renderer proxy) in pairs)
        {
            if (proxy != null) proxy.enabled = false;
        }
        SetAppended(false);
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
