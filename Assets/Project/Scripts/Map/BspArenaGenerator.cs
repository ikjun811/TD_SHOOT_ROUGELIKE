using System.Collections.Generic;
using UnityEngine;

// BSP(이진 공간 분할) 기반 절차적 아레나 생성기 (GDD 1장 기술 주제 #2, 8-5 #5)
// 미로형 방·복도 대신 "넓은 공간 + 장애물" 구조:
//   1) 전체 영역을 BSP로 재귀 분할해 구역(잎)을 만듦
//   2) 분할선을 따라 폭 laneWidth의 빈 통로(레인)를 남기고, 구역 안쪽에만 장애물 배치 → 레인이 격자처럼 이어져 모든 공간이 항상 연결됨
//   3) 구역 중앙은 비워 두어 시작 지점·탈출 구역·보급 상자 배치 자리로 사용 (M3)
// 1m 격자(occupied)에 장애물 점유를 기록하고, 3D로는 큐브(또는 지정 머티리얼)로 바닥·외곽 벽·장애물을 만든다
// 에디터: 컴포넌트 우클릭 메뉴 Generate / Clear로 미리 보기 (생성물은 씬에 저장되지 않음)
public class BspArenaGenerator : MonoBehaviour
{
    [Header("Seed")]
    [Tooltip("0이면 매번 무작위, 그 외 값이면 같은 맵 재현")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool generateOnStart = false;
    [Tooltip("마지막으로 생성한 맵의 시드 (읽기 전용 확인용). 이 값을 Seed에 넣으면 같은 맵이 다시 생성됨")]
    [SerializeField] private int lastGeneratedSeed;

    [Header("Arena")]
    [SerializeField] private int arenaSize = 90;          // 정사각 한 변 (m)
    [SerializeField] private int minLeafSize = 18;        // 이보다 작게는 나누지 않음
    [SerializeField] private int maxDepth = 4;            // 최대 분할 깊이 (잎 최대 16개)
    [Range(0.3f, 0.5f)][SerializeField] private float minSplitRatio = 0.35f; // 한쪽이 너무 얇아지지 않게
    [SerializeField] private int laneWidth = 6;           // 분할선을 따라 비우는 통로 폭
    [SerializeField] private int centerClearSize = 8;     // 구역 중앙 빈 공간 한 변

    [Header("Obstacles")]
    [SerializeField] private Vector2Int obstaclesPerLeaf = new Vector2Int(0, 2); // 0이면 장애물 없는 탁 트인 구역
    [SerializeField] private int obstacleSpacing = 3;     // 장애물끼리 최소 간격
    [SerializeField] private Vector2Int wallLengthRange = new Vector2Int(4, 7);
    [SerializeField] private int pillarSize = 2;
    [SerializeField] private float obstacleHeight = 3f;
    [SerializeField] private int placementAttemptsPerObstacle = 15;

    [Header("Boundary")]
    [SerializeField] private float boundaryHeight = 4f;
    [SerializeField] private float boundaryThickness = 1f;

    [Header("Visual (비우면 기본 머티리얼)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private string wallLayerName = "Walls";

    // ===== 생성 결과 =====
    public class Leaf
    {
        public RectInt rect;       // 분할 영역 (격자 좌표)
        public RectInt interior;   // 레인을 뺀 장애물 배치 영역
        public RectInt centerClear;
        public Leaf left, right;
        public bool IsLeaf => left == null && right == null;
    }

    private Leaf rootLeaf;
    private readonly List<Leaf> leaves = new List<Leaf>();
    private bool[,] occupied;
    private Transform generatedRoot;
    private int usedSeed;

    public IReadOnlyList<Leaf> Leaves => leaves;
    public int UsedSeed => usedSeed;

    // 구역 중앙(월드 좌표) — 시작 지점·탈출 구역·상자 배치용 (M3)
    public List<Vector3> GetZoneCenters()
    {
        List<Vector3> centers = new List<Vector3>();
        foreach (Leaf leaf in leaves) centers.Add(CellCenterToWorld(leaf.centerClear.center));
        return centers;
    }

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();

        usedSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
        lastGeneratedSeed = usedSeed;
        System.Random rng = new System.Random(usedSeed);

        occupied = new bool[arenaSize, arenaSize];
        leaves.Clear();

        rootLeaf = new Leaf { rect = new RectInt(0, 0, arenaSize, arenaSize) };
        Split(rootLeaf, 0, rng);
        CollectLeaves(rootLeaf);

        generatedRoot = new GameObject("GeneratedArena").transform;
        generatedRoot.SetParent(transform, false);

        BuildFloorAndBoundary();
        foreach (Leaf leaf in leaves) PlaceObstacles(leaf, rng);

        // 에디터 미리 보기는 씬에 저장되지 않도록 생성물 전체에 표시
        if (!Application.isPlaying)
        {
            foreach (Transform t in generatedRoot.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.DontSave;
            }
        }

        Debug.Log($"BspArenaGenerator: seed {usedSeed}, zones {leaves.Count}");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (generatedRoot == null)
        {
            Transform existing = transform.Find("GeneratedArena");
            if (existing != null) generatedRoot = existing;
        }
        if (generatedRoot == null) return;

        if (Application.isPlaying) Destroy(generatedRoot.gameObject);
        else DestroyImmediate(generatedRoot.gameObject);
        generatedRoot = null;
    }

    #region BSP

    // 긴 쪽을 우선 분할, 양쪽이 minLeafSize 이상일 때만 분할
    private void Split(Leaf node, int depth, System.Random rng)
    {
        if (depth >= maxDepth) return;

        RectInt r = node.rect;
        bool canSplitH = r.height >= minLeafSize * 2; // 가로선으로 위아래 분할
        bool canSplitV = r.width >= minLeafSize * 2;  // 세로선으로 좌우 분할
        if (!canSplitH && !canSplitV) return;

        bool splitVertical;
        if (canSplitH && canSplitV) splitVertical = r.width > r.height ? true : r.height > r.width ? false : rng.NextDouble() < 0.5;
        else splitVertical = canSplitV;

        int length = splitVertical ? r.width : r.height;
        int min = Mathf.Max(minLeafSize, Mathf.RoundToInt(length * minSplitRatio));
        int max = length - min;
        if (max < min) return;
        int cut = rng.Next(min, max + 1);

        if (splitVertical)
        {
            node.left = new Leaf { rect = new RectInt(r.x, r.y, cut, r.height) };
            node.right = new Leaf { rect = new RectInt(r.x + cut, r.y, r.width - cut, r.height) };
        }
        else
        {
            node.left = new Leaf { rect = new RectInt(r.x, r.y, r.width, cut) };
            node.right = new Leaf { rect = new RectInt(r.x, r.y + cut, r.width, r.height - cut) };
        }

        Split(node.left, depth + 1, rng);
        Split(node.right, depth + 1, rng);
    }

    private void CollectLeaves(Leaf node)
    {
        if (node == null) return;
        if (node.IsLeaf)
        {
            // 레인: 구역 가장자리에서 laneWidth/2씩 비움 → 분할선 양쪽이 합쳐져 laneWidth 통로, 외곽 벽 옆은 laneWidth/2 통로
            int half = Mathf.CeilToInt(laneWidth / 2f);
            node.interior = new RectInt(node.rect.x + half, node.rect.y + half,
                                        Mathf.Max(0, node.rect.width - half * 2), Mathf.Max(0, node.rect.height - half * 2));

            Vector2Int c = Vector2Int.RoundToInt(node.interior.center);
            node.centerClear = new RectInt(c.x - centerClearSize / 2, c.y - centerClearSize / 2, centerClearSize, centerClearSize);

            leaves.Add(node);
            return;
        }
        CollectLeaves(node.left);
        CollectLeaves(node.right);
    }

    #endregion

    #region Obstacles

    private void PlaceObstacles(Leaf leaf, System.Random rng)
    {
        int count = rng.Next(obstaclesPerLeaf.x, obstaclesPerLeaf.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < placementAttemptsPerObstacle; attempt++)
            {
                List<RectInt> parts = CreateObstacleShape(rng);
                if (!TryPlaceShape(leaf, parts, rng, out List<RectInt> placed)) continue;

                foreach (RectInt part in placed)
                {
                    MarkOccupied(part);
                    CreateBlock(part, obstacleHeight, "Obstacle");
                }
                break;
            }
        }
    }

    // 원점 기준 모양: 기둥 / 직선 벽 / ㄱ자 벽
    private List<RectInt> CreateObstacleShape(System.Random rng)
    {
        List<RectInt> parts = new List<RectInt>();
        int type = rng.Next(0, 3);
        int len = rng.Next(wallLengthRange.x, wallLengthRange.y + 1);
        bool horizontal = rng.NextDouble() < 0.5;

        switch (type)
        {
            case 0: // 기둥
                parts.Add(new RectInt(0, 0, pillarSize, pillarSize));
                break;
            case 1: // 직선 벽 (두께 1)
                parts.Add(horizontal ? new RectInt(0, 0, len, 1) : new RectInt(0, 0, 1, len));
                break;
            default: // ㄱ자 벽 (가로 + 세로, 모서리에서 이어짐)
                int len2 = rng.Next(wallLengthRange.x - 1, wallLengthRange.y);
                parts.Add(new RectInt(0, 0, len, 1));
                bool flipX = rng.NextDouble() < 0.5;
                bool flipY = rng.NextDouble() < 0.5;
                int vx = flipX ? len - 1 : 0;
                int vy = flipY ? -(len2 - 1) : 0;
                parts.Add(new RectInt(vx, vy, 1, len2));
                break;
        }
        return parts;
    }

    // 구역 interior 안, 중앙 빈 공간·다른 장애물(간격 포함)과 겹치지 않는 위치에 배치
    private bool TryPlaceShape(Leaf leaf, List<RectInt> shape, System.Random rng, out List<RectInt> placed)
    {
        placed = new List<RectInt>();

        RectInt bounds = GetBounds(shape);
        int minX = leaf.interior.xMin - bounds.xMin;
        int maxX = leaf.interior.xMax - bounds.xMax;
        int minY = leaf.interior.yMin - bounds.yMin;
        int maxY = leaf.interior.yMax - bounds.yMax;
        if (maxX < minX || maxY < minY) return false;

        int ox = rng.Next(minX, maxX + 1);
        int oy = rng.Next(minY, maxY + 1);

        foreach (RectInt part in shape)
        {
            RectInt moved = new RectInt(part.x + ox, part.y + oy, part.width, part.height);
            if (moved.Overlaps(leaf.centerClear)) return false;

            RectInt padded = new RectInt(moved.x - obstacleSpacing, moved.y - obstacleSpacing,
                                         moved.width + obstacleSpacing * 2, moved.height + obstacleSpacing * 2);
            if (IsAnyOccupied(padded)) return false;

            placed.Add(moved);
        }
        return true;
    }

    private static RectInt GetBounds(List<RectInt> parts)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        foreach (RectInt p in parts)
        {
            xMin = Mathf.Min(xMin, p.xMin); yMin = Mathf.Min(yMin, p.yMin);
            xMax = Mathf.Max(xMax, p.xMax); yMax = Mathf.Max(yMax, p.yMax);
        }
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private bool IsAnyOccupied(RectInt area)
    {
        for (int x = Mathf.Max(0, area.xMin); x < Mathf.Min(arenaSize, area.xMax); x++)
            for (int y = Mathf.Max(0, area.yMin); y < Mathf.Min(arenaSize, area.yMax); y++)
                if (occupied[x, y]) return true;
        return false;
    }

    private void MarkOccupied(RectInt area)
    {
        for (int x = Mathf.Max(0, area.xMin); x < Mathf.Min(arenaSize, area.xMax); x++)
            for (int y = Mathf.Max(0, area.yMin); y < Mathf.Min(arenaSize, area.yMax); y++)
                occupied[x, y] = true;
    }

    #endregion

    #region 3D Build

    private Vector3 Origin => transform.position - new Vector3(arenaSize / 2f, 0f, arenaSize / 2f);

    private Vector3 CellCenterToWorld(Vector2 cell)
    {
        return Origin + new Vector3(cell.x, 0f, cell.y);
    }

    private void BuildFloorAndBoundary()
    {
        // 바닥 (Default 레이어, 윗면이 y=0)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(generatedRoot, false);
        floor.transform.position = transform.position + new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(arenaSize, 0.2f, arenaSize);
        if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        // 외곽 벽 4면 (Walls 레이어)
        float half = arenaSize / 2f + boundaryThickness / 2f;
        float span = arenaSize + boundaryThickness * 2f;
        CreateWall("Boundary_N", new Vector3(0f, boundaryHeight / 2f, half), new Vector3(span, boundaryHeight, boundaryThickness));
        CreateWall("Boundary_S", new Vector3(0f, boundaryHeight / 2f, -half), new Vector3(span, boundaryHeight, boundaryThickness));
        CreateWall("Boundary_E", new Vector3(half, boundaryHeight / 2f, 0f), new Vector3(boundaryThickness, boundaryHeight, span));
        CreateWall("Boundary_W", new Vector3(-half, boundaryHeight / 2f, 0f), new Vector3(boundaryThickness, boundaryHeight, span));
    }

    private void CreateWall(string objName, Vector3 localPos, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = objName;
        wall.transform.SetParent(generatedRoot, false);
        wall.transform.position = transform.position + localPos;
        wall.transform.localScale = size;
        ApplyWallStyle(wall);
    }

    // 격자 사각형 → 큐브 장애물
    private void CreateBlock(RectInt cells, float height, string objName)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objName;
        block.transform.SetParent(generatedRoot, false);
        block.transform.position = CellCenterToWorld(cells.center) + Vector3.up * (height / 2f);
        block.transform.localScale = new Vector3(cells.width, height, cells.height);
        ApplyWallStyle(block);
    }

    private void ApplyWallStyle(GameObject obj)
    {
        int layer = LayerMask.NameToLayer(wallLayerName);
        if (layer >= 0) obj.layer = layer;
        if (wallMaterial != null) obj.GetComponent<Renderer>().sharedMaterial = wallMaterial;
    }

    #endregion

    #region Gizmos

    // 선택 시: 구역(노랑) / 장애물 배치 영역(청록) / 중앙 빈 공간(초록)
    private void OnDrawGizmosSelected()
    {
        if (leaves.Count == 0) return;

        foreach (Leaf leaf in leaves)
        {
            DrawRect(leaf.rect, new Color(1f, 0.85f, 0.2f));
            DrawRect(leaf.interior, new Color(0.2f, 0.9f, 0.9f, 0.6f));
            DrawRect(leaf.centerClear, new Color(0.3f, 1f, 0.3f));
        }
    }

    private void DrawRect(RectInt r, Color color)
    {
        Gizmos.color = color;
        Vector3 center = Origin + new Vector3(r.center.x, 0.05f, r.center.y);
        Gizmos.DrawWireCube(center, new Vector3(r.width, 0.1f, r.height));
    }

    #endregion
}
