using System.Collections.Generic;
using JUTPS;
using UnityEngine;

// 대시 잔상 (D4) — GameplayBootstrap이 플레이어에 부착 (JU 비수정: JUCharacterController.IsDashing만 읽음)
//   대시 중 interval마다 지금 보이는 몸(SkinnedMeshRenderer → BakeMesh로 그 순간 자세)과 총(MeshRenderer)을 복제해 남김
//   잔상은 Resources/Shaders/Afterimage(청록 반투명·가장자리 밝게·가산 혼합), fadeTime 동안 사라짐, 게임 시간 기준
//   숨겨진 JU 몸 메시·꺼진 무기·실루엣 등 TDShoot 셰이더 렌더러는 제외. 잔상 오브젝트는 풀에서 재사용
public class DashAfterimage : MonoBehaviour
{
    public float interval = 0.035f;
    public float fadeTime = 0.3f;
    public Color color = new Color(0.3f, 0.85f, 1f, 0.55f);
    public int maxGhosts = 12;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private class Part
    {
        public GameObject go;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Mesh bakedMesh; // 스킨 메시용 (재사용)
    }

    private class Ghost
    {
        public GameObject root;
        public readonly List<Part> parts = new List<Part>();
        public int usedParts;
        public float startTime = -100f;
        public bool active;
    }

    private JUCharacterController controller;
    private Material material;
    private MaterialPropertyBlock block;
    private readonly List<Ghost> ghosts = new List<Ghost>();
    private readonly List<Renderer> sources = new List<Renderer>();
    private float nextSpawnTime;
    private bool wasDashing;

    public static void EnsureOnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<DashAfterimage>() == null) player.AddComponent<DashAfterimage>();
    }

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
        Shader shader = Shader.Find("TDShoot/Afterimage");
        if (shader == null)
        {
            Debug.LogWarning("DashAfterimage: TDShoot/Afterimage 셰이더를 찾지 못했습니다 (Resources/Shaders/Afterimage.shader 확인).");
            enabled = false;
            return;
        }
        material = new Material(shader) { name = "DashAfterimage" };
        block = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        foreach (Ghost ghost in ghosts)
        {
            foreach (Part part in ghost.parts)
            {
                if (part.bakedMesh != null) Destroy(part.bakedMesh);
            }
            if (ghost.root != null) Destroy(ghost.root);
        }
        if (material != null) Destroy(material);
    }

    private void Update()
    {
        bool dashing = controller != null && controller.IsDashing && Time.timeScale > 0f;
        if (dashing && (!wasDashing || Time.time >= nextSpawnTime))
        {
            SpawnGhost();
            nextSpawnTime = Time.time + interval;
        }
        wasDashing = dashing;

        UpdateGhosts();
    }

    private void UpdateGhosts()
    {
        foreach (Ghost ghost in ghosts)
        {
            if (!ghost.active) continue;
            float t = (Time.time - ghost.startTime) / fadeTime;
            if (t >= 1f || Time.timeScale <= 0f)
            {
                ghost.active = false;
                ghost.root.SetActive(false);
                continue;
            }
            Color c = color;
            c.a *= (1f - t) * (1f - t);
            block.SetColor(ColorId, c);
            for (int i = 0; i < ghost.usedParts; i++) ghost.parts[i].renderer.SetPropertyBlock(block);
        }
    }

    private void SpawnGhost()
    {
        CollectSources();
        if (sources.Count == 0) return;

        Ghost ghost = GetFreeGhost();
        ghost.usedParts = 0;
        foreach (Renderer source in sources)
        {
            Part part = GetPart(ghost, ghost.usedParts);
            Transform from = source.transform;
            part.go.transform.SetPositionAndRotation(from.position, from.rotation);

            if (source is SkinnedMeshRenderer skinned)
            {
                if (part.bakedMesh == null) part.bakedMesh = new Mesh { name = "AfterimageBaked" };
                skinned.BakeMesh(part.bakedMesh, true); // 크기 반영된 그 순간 자세
                part.filter.sharedMesh = part.bakedMesh;
                part.go.transform.localScale = Vector3.one;
            }
            else
            {
                MeshFilter meshFilter = source.GetComponent<MeshFilter>();
                part.filter.sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
                part.go.transform.localScale = from.lossyScale;
            }
            part.go.SetActive(part.filter.sharedMesh != null);
            ghost.usedParts++;
        }
        for (int i = ghost.usedParts; i < ghost.parts.Count; i++) ghost.parts[i].go.SetActive(false);

        ghost.startTime = Time.time;
        ghost.active = true;
        ghost.root.SetActive(true);
        UpdateGhosts();
    }

    // 지금 보이는 몸·총 렌더러 (숨긴 JU 몸, 꺼진 무기, 실루엣·번쩍임 등 TDShoot 셰이더 제외)
    private void CollectSources()
    {
        sources.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            Material shared = r.sharedMaterial;
            if (shared != null && shared.shader != null && shared.shader.name.StartsWith("TDShoot/")) continue;
            if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh == null) continue;
            sources.Add(r);
        }
    }

    private Ghost GetFreeGhost()
    {
        Ghost oldest = null;
        foreach (Ghost ghost in ghosts)
        {
            if (!ghost.active) return ghost;
            if (oldest == null || ghost.startTime < oldest.startTime) oldest = ghost;
        }
        if (ghosts.Count >= maxGhosts && oldest != null) return oldest;

        Ghost created = new Ghost { root = new GameObject("DashAfterimage") };
        ghosts.Add(created);
        return created;
    }

    private Part GetPart(Ghost ghost, int index)
    {
        while (ghost.parts.Count <= index)
        {
            GameObject go = new GameObject("Part");
            go.transform.SetParent(ghost.root.transform, true);
            Part part = new Part
            {
                go = go,
                filter = go.AddComponent<MeshFilter>(),
                renderer = go.AddComponent<MeshRenderer>()
            };
            part.renderer.sharedMaterial = material;
            part.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.renderer.receiveShadows = false;
            ghost.parts.Add(part);
        }
        return ghost.parts[index];
    }
}
