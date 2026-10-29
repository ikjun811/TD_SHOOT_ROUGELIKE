using System.Collections.Generic;
using UnityEngine;

// 드랍 아이템 외형 (사용자 채택 2026-10-29) — DroppedItem.SetupVisuals가 호출, 예전 정육면체 대신
//   무기: WeaponDataSO.weaponPrefab(JU 무기 프리팹, Tools > TD Shoot > Link Weapon Drop Models로 연결)에서 메시만 복사
//     (스크립트·파티클은 가져오지 않음) → 긴 변 modelLength m로 맞춰 가운데 정렬
//   모듈: 그 모듈의 테트리스 모양(width x height, shapeGrid) 그대로 작은 블록 조각, 레어도 색 + 발광(블룸)
//   둘 다 떠서 위아래로 천천히 움직임 (회전은 DroppedItem 루트가 담당)
public class DroppedItemVisual : MonoBehaviour
{
    public static float modelLength = 0.85f;
    public static float cellSize = 0.24f;
    public static float cellGap = 0.035f;
    public static float bobAmount = 0.08f;
    public static float bobSpeed = 2.2f;

    private static readonly Dictionary<Color, Material> blockMaterials = new Dictionary<Color, Material>();

    private Vector3 baseLocalPosition;
    private float phase;

    // 무기 모델 (프리팹 없으면 null → 호출 쪽이 기존 정육면체 유지)
    public static GameObject BuildWeapon(Transform item, GameObject prefab)
    {
        if (prefab == null) return null;
        DroppedItemVisual visual = Create(item);
        Transform container = new GameObject("Model").transform;
        container.SetParent(visual.transform, false);

        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        int count = 0;
        foreach (Renderer source in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;
            if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
            else if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            if (mesh == null || !source.enabled || !source.gameObject.activeSelf) continue;

            Matrix4x4 m = toRoot * source.transform.localToWorldMatrix;
            GameObject part = new GameObject(source.name);
            part.transform.SetParent(container, false);
            part.transform.localPosition = m.GetPosition();
            part.transform.localRotation = m.rotation;
            part.transform.localScale = m.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            count++;
        }
        if (count == 0)
        {
            Destroy(visual.gameObject);
            return null;
        }

        // 긴 변 맞춤 + 가운데 정렬 (모델 기준 바운드)
        Bounds bounds = GetLocalBounds(container, visual.transform);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.0001f) container.localScale = Vector3.one * (modelLength / longest);
        bounds = GetLocalBounds(container, visual.transform);
        container.localPosition -= bounds.center;
        return visual.gameObject;
    }

    // 모듈: 테트리스 모양 블록 조각
    public static GameObject BuildModule(Transform item, ModuleDataSO module, Color color)
    {
        if (module == null || module.shapeGrid == null || module.width <= 0 || module.height <= 0) return null;
        DroppedItemVisual visual = Create(item);
        Material material = GetBlockMaterial(color);

        float step = cellSize + cellGap;
        Vector3 offset = new Vector3((module.width - 1) * step * 0.5f, 0f, (module.height - 1) * step * 0.5f);
        int count = 0;
        for (int y = 0; y < module.height; y++)
        {
            for (int x = 0; x < module.width; x++)
            {
                int index = y * module.width + x;
                if (index >= module.shapeGrid.Length || !module.shapeGrid[index]) continue;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cell.GetComponent<Collider>());
                cell.name = "Cell";
                cell.transform.SetParent(visual.transform, false);
                // 그리드 y는 위→아래 증가 (정비 화면과 같은 모양이 위에서 보이게 -z)
                cell.transform.localPosition = new Vector3(x * step, 0f, -y * step) - new Vector3(offset.x, 0f, -offset.z);
                cell.transform.localScale = Vector3.one * cellSize;
                MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                count++;
            }
        }
        if (count == 0)
        {
            Destroy(visual.gameObject);
            return null;
        }
        visual.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); // 살짝 기울여 모양이 잘 보이게
        return visual.gameObject;
    }

    private static DroppedItemVisual Create(Transform item)
    {
        GameObject go = new GameObject("DropVisual");
        go.transform.SetParent(item, false);
        DroppedItemVisual visual = go.AddComponent<DroppedItemVisual>();
        visual.baseLocalPosition = Vector3.zero;
        visual.phase = Random.Range(0f, 10f);
        return visual;
    }

    private void Update()
    {
        transform.localPosition = baseLocalPosition + Vector3.up * (bobAmount * Mathf.Sin((Time.time + phase) * bobSpeed));
    }

    // 레어도 색 블록 재질 (색마다 1개 공유): URP Lit + 발광 → 블룸
    private static Material GetBlockMaterial(Color color)
    {
        if (blockMaterials.TryGetValue(color, out Material cached) && cached != null) return cached;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(lit != null ? lit : Shader.Find("Sprites/Default")) { name = "DropModuleBlock", hideFlags = HideFlags.DontSave };
        if (lit != null)
        {
            mat.SetColor("_BaseColor", color * 0.7f);
            mat.SetFloat("_Smoothness", 0.6f);
            mat.SetFloat("_Metallic", 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.4f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.color = color;
        }
        blockMaterials[color] = mat;
        return mat;
    }

    // 자식 렌더러들의 바운드 (space 기준 로컬 좌표)
    private static Bounds GetLocalBounds(Transform container, Transform space)
    {
        bool has = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in container.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds mb = filter.sharedMesh.bounds;
            Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
                else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }
}
