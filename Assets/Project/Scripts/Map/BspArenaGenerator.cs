using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// BSP(이진 공간 분할) 기반 절차적 아레나 생성기 (GDD 1장 기술 주제 #2, 8-5 #5)
// 미로형 방·복도 대신 "넓은 공간 + 장애물" 구조:
//   1) 전체 영역을 BSP로 재귀 분할해 구역(잎)을 만듦
//   2) 분할선을 따라 폭 laneWidth의 빈 통로(레인)를 남기고, 구역 안쪽에만 장애물 배치 → 레인이 격자처럼 이어져 모든 공간이 항상 연결됨
//   3) 구역 중앙은 비워 두어 시작 지점·탈출 구역·보급 상자 배치 자리로 사용 (M3)
// 1m 격자(occupied)에 장애물 점유를 기록하고, 3D로는 큐브(또는 지정 머티리얼)로 바닥·외곽 벽·장애물을 만든다
// 생성 직후 NavMesh를 런타임 베이크하고(벽·장애물 윗면은 Not Walkable), 플레이 중이면 플레이어를 무작위 구역 중앙으로 옮긴다
// 에디터: 컴포넌트 우클릭 메뉴 Generate / Clear로 미리 보기 (생성물은 씬에 저장되지 않음)
public class BspArenaGenerator : MonoBehaviour
{
    [Header("Seed")]
    [Tooltip("0이면 매번 무작위, 그 외 값이면 같은 맵 재현")]
    [SerializeField] private int seed = 0;
    [Tooltip("켜면 씬 시작 시(Awake) 생성 — 다른 스크립트가 NavMesh를 조회하기 전에 맵이 준비됨")]
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
    [SerializeField] private float boundaryThickness = 2f;
    [Tooltip("바닥 두께 (윗면은 y=0 고정). 적에게 밀려 바닥을 뚫고 떨어지지 않도록 두껍게")]
    [SerializeField] private float floorThickness = 2f;
    [Tooltip("외곽 벽 바깥으로 더 까는 바닥 폭 (걷기 영역 아님). 벽을 뚫고 밀려나도 바로 떨어지지 않게")]
    [SerializeField] private float outerFloorMargin = 4f;

    [Header("Visual (비우면 기본 머티리얼)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private string wallLayerName = "Walls";

    [Header("Visual Prefabs (비우면 큐브 표시)")]
    [Tooltip("1x1x1m·중심 피벗 기준으로 만든 프리팹. 큐브 크기에 맞게 늘어남. 충돌은 큐브 콜라이더가 담당하고 프리팹 콜라이더는 꺼짐")]
    [SerializeField] private GameObject pillarPrefab;
    [Tooltip("직선 벽·ㄱ자 벽의 각 토막에 사용")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject boundaryPrefab;

    [Header("Kit Floor (W3a, 비우면 큐브 바닥 표시)")]
    [Tooltip("바닥 타일 프리팹 (기준점 위치 무관, 메시 바운드 기준으로 윗면 y=0에 맞춤). 여러 개면 칸마다 무작위")]
    [SerializeField] private GameObject[] floorTilePrefabs;
    [Tooltip("타일 한 칸 크기 (Warehouse 6, 산업 에셋 도로 타일 10). 타일 실제 크기와 다르면 가로·세로를 맞춰 늘림")]
    [SerializeField] private float floorTileSize = 6f;
    [Tooltip("타일마다 90도 단위 무작위 회전 (반복 무늬 완화)")]
    [SerializeField] private bool randomFloorRotation = true;
    [Tooltip("바깥 바닥 띠 밖으로 더 까는 타일 폭 (겉모습만). 바닥 끝 너머로 바닥 아래(묻힌 벽)가 보이지 않게")]
    [SerializeField] private float kitFloorExtraMargin = 18f;

    [Header("Grid Floor (가독성 — 사용자 채택 2026-10-29: 바닥·인물·장애물 구분)")]
    [Tooltip("켜면 타일 대신 코드로 만든 전술형 격자 바닥 한 장 (짙은 숯색 + 칸 선·굵은 선, 선은 약하게 발광). 끄면 위 키트 타일")]
    [SerializeField] private bool useGridFloor = true;
    [Tooltip("격자 한 칸 (m). 굵은 선은 2칸마다, 텍스처 한 장 = 4x4칸")]
    [SerializeField] private float gridCellSize = 3f;
    [SerializeField] private Color gridBaseColor = new Color(0.16f, 0.17f, 0.18f);
    [SerializeField] private Color gridLineColor = new Color(0.3f, 0.42f, 0.45f);
    [Tooltip("선 발광 (어두운 곳에서도 격자가 은은하게 보이게)")]
    [SerializeField] private Color gridLineEmission = new Color(0.03f, 0.09f, 0.1f);
    [Tooltip("칸마다 밝기 차이 (단조로움 완화)")]
    [Range(0f, 0.2f)] [SerializeField] private float gridCellVariation = 0.04f;
    [Range(0f, 1f)] [SerializeField] private float gridSmoothness = 0.25f;
    [Tooltip("바닥 텍스처 (비우면 위 코드 격자). 우주정거장 짙은 육각 — Tools > TD Shoot > Apply SpaceStation Floor가 연결 (사용자 선택)")]
    [SerializeField] private Texture2D floorTexture;
    [SerializeField] private Texture2D floorNormalMap;
    [Tooltip("바닥 텍스처 한 장이 덮는 크기 (m). 우주정거장 에셋 원래 값 = 월드 UV 0.2 → 5m")]
    [SerializeField] private float floorTextureWorldSize = 5f;
    [SerializeField] private float floorNormalScale = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float floorMetallic = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float floorSmoothness = 0.4f;

    private Material gridMaterial;
    private Texture2D gridAlbedo;
    private Texture2D gridEmissionMask;

    [Header("Kit Boundary Walls (W3b, 비우면 큐브·Boundary Prefab 표시)")]
    [Tooltip("기본 벽 (메시 바운드 기준: 긴 축 = 길이, 짧은 축 = 두께). 첫 번째 벽 길이로 한 변 개수를 정하고 길이 방향만 늘리거나 줄여 맞춤")]
    [SerializeField] private GameObject[] boundaryWallPrefabs;
    [Tooltip("가끔 섞는 벽 (창문·환풍기·셔터문 등)")]
    [SerializeField] private GameObject[] boundaryAccentPrefabs;
    [Range(0f, 1f)][SerializeField] private float boundaryAccentChance = 0.2f;
    [Tooltip("벽 아래 끝을 바닥 아래로 내리는 깊이 (보이는 높이 = 벽 높이 - 이 값). 늘림이 아니라 무늬가 유지됨. Warehouse 벽(높이 10) 6 = 보이는 4m, 콘크리트 펜스는 0")]
    [SerializeField] private float kitWallSink = 6f;
    [Tooltip("벽 앞뒤 뒤집기 (장식 면이 바깥을 보면 켬)")]
    [SerializeField] private bool flipWallFacing = false;
    [Tooltip("네 모서리 틈을 덮는 기둥")]
    [SerializeField] private GameObject boundaryCornerPrefab;

    [Header("Kit Obstacle Props (W3c, 비우면 큐브·Pillar/Wall Prefab 표시)")]
    [Tooltip("직선·ㄱ자 벽(폭 1m)을 채울 소품 (상자 등). 장애물마다 하나 골라 줄지어 쌓음")]
    [SerializeField] private GameObject[] wallPropPrefabs;
    [Tooltip("기둥(2x2)을 채울 소품 (짐 실은 팔레트 등)")]
    [SerializeField] private GameObject[] pillarPropPrefabs;
    [Tooltip("쌓아 올릴 목표 높이 (탄 높이보다 높게 → 막히는 모습이 판정과 맞음)")]
    [SerializeField] private float propTargetHeight = 1.8f;
    [Tooltip("칸에 맞추려고 늘리거나 줄이는 최대 배율 (넘으면 가운데 정렬로 여백)")]
    [SerializeField] private float propMaxStretch = 1.3f;
    [Tooltip("소품마다 무작위로 살짝 돌리는 각도 (한 변 1.5m 미만 소품만 — 펜스·컨테이너 등 큰 소품은 이음새가 어긋나므로 제외)")]
    [SerializeField] private float propYawJitter = 3f;
    [Tooltip("장애물 콜라이더를 실제 소품 크기에 맞춤 (보이지 않는 여백 판정 제거, 칸 배치 규칙은 그대로)")]
    [SerializeField] private bool fitColliderToProps = true;
    [Tooltip("대형 장애물 소품 (컨테이너·덤프스터·발전기 등). 소품 크기만큼 칸을 차지하는 별도 장애물 종류")]
    [SerializeField] private GameObject[] largePropPrefabs;
    [Range(0f, 1f)][SerializeField] private float largeObstacleChance = 0.25f;
    [Tooltip("소품 최대 높이 (넘으면 세로로만 줄임, 0 = 제한 없음). 우주정거장 벽·기둥 4m → 2.5m (사용자 결정: 탑다운 가림 줄이기)")]
    [SerializeField] private float propMaxHeight = 0f;
    [Tooltip("상자 더미 (대형 장애물 자리에 쓰는 소품, 있으면 largePropPrefabs 대신). 상자를 격자로 1~2단 무작위로 쌓음 (사용자 결정)")]
    [SerializeField] private GameObject[] largePilePrefabs;
    [Tooltip("더미 가로·세로 상자 수 (최소, 최대)")]
    [SerializeField] private Vector2Int pileCountX = new Vector2Int(2, 3);
    [SerializeField] private Vector2Int pileCountZ = new Vector2Int(2, 2);
    [Range(0f, 1f)][SerializeField] private float pileTallChance = 0.5f; // 2단으로 쌓을 확률

    [Header("Player Start")]
    [Tooltip("플레이어를 구역 중앙에 놓을 때 바닥 위 높이")]
    [SerializeField] private float playerSpawnHeight = 0.1f;

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
    private Vector3 playerStartPosition;

    public IReadOnlyList<Leaf> Leaves => leaves;

    // 실행 중 맵 생성 완료 알림 (구역 조명 재배치 등, AtmosphereLighting)
    public static event System.Action<BspArenaGenerator> OnGenerated;
    public int UsedSeed => usedSeed;
    public int ArenaSize => arenaSize;
    public Vector3 PlayerStartPosition => playerStartPosition; // 플레이어 시작 지점 (구역 중앙, M3에서 탈출·상자 배치 시 거리 기준)

    // 구역 중앙(월드 좌표) — 시작 지점·탈출 구역·상자 배치용 (M3)
    public List<Vector3> GetZoneCenters()
    {
        List<Vector3> centers = new List<Vector3>();
        foreach (Leaf leaf in leaves) centers.Add(CellCenterToWorld(leaf.centerClear.center));
        return centers;
    }

    // 월드 좌표가 속한 구역 번호 (Leaves·GetZoneCenters와 같은 순서), 맵 밖이면 -1
    public int GetZoneIndexAt(Vector3 worldPos)
    {
        Vector3 local = worldPos - Origin;
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.z));
        for (int i = 0; i < leaves.Count; i++)
        {
            if (leaves[i].rect.Contains(cell)) return i;
        }
        return -1;
    }

    // 월드 좌표가 외곽 벽 안쪽(맵 범위 margin m 안쪽)인지 — 구역·분할선 레인 모두 포함
    public bool IsInsideArena(Vector3 worldPos, float margin = 0f)
    {
        Vector3 local = worldPos - transform.position;
        float half = arenaSize / 2f - margin;
        return Mathf.Abs(local.x) <= half && Mathf.Abs(local.z) <= half;
    }

    // 격자 바닥 재질·텍스처는 코드로 만든 것이라 직접 정리
    private void OnDestroy()
    {
        if (gridMaterial != null) DestroyImmediate(gridMaterial);
        if (gridAlbedo != null) DestroyImmediate(gridAlbedo);
        if (gridEmissionMask != null) DestroyImmediate(gridEmissionMask);
    }

    private void Awake()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        // 생성·NavMesh 베이크 소요 시간 측정 (라운드 시작 순간 멈춤 확인용)
        System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();

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
        BuildKitFloor(new System.Random(usedSeed + 1)); // 별도 난수 → 같은 시드의 구역·장애물 배치는 그대로
        BuildKitBoundary(new System.Random(usedSeed + 2));
        propRng = new System.Random(usedSeed + 3);
        foreach (Leaf leaf in leaves) PlaceObstacles(leaf, rng);

        System.Diagnostics.Stopwatch navMeshWatch = System.Diagnostics.Stopwatch.StartNew();
        BuildNavMesh();
        navMeshWatch.Stop();

        // 시작 구역: 무작위 구역의 중앙 빈 공간
        Leaf startLeaf = leaves[rng.Next(leaves.Count)];
        playerStartPosition = CellCenterToWorld(startLeaf.centerClear.center) + Vector3.up * playerSpawnHeight;
        if (Application.isPlaying) MovePlayerToStart();

        // 에디터 미리 보기는 씬에 저장되지 않도록 생성물 전체에 표시
        if (!Application.isPlaying)
        {
            foreach (Transform t in generatedRoot.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.DontSave;
            }
        }

        totalWatch.Stop();
        Debug.Log($"BspArenaGenerator: seed {usedSeed}, zones {leaves.Count}, build {totalWatch.ElapsedMilliseconds}ms (navmesh {navMeshWatch.ElapsedMilliseconds}ms)");
        if (Application.isPlaying) OnGenerated?.Invoke(this);
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

        if (Application.isPlaying)
        {
            // Destroy는 프레임 끝에 실행되므로 먼저 꺼서 이전 NavMesh·콜라이더를 즉시 제거 (같은 프레임의 재배치가 새 맵 기준으로 동작)
            generatedRoot.gameObject.SetActive(false);
            Destroy(generatedRoot.gameObject);
        }
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
                List<RectInt> parts = CreateObstacleShape(rng, out GameObject largePrefab, out bool pile);
                if (!TryPlaceShape(leaf, parts, rng, out List<RectInt> placed)) continue;

                for (int p = 0; p < placed.Count; p++)
                {
                    RectInt part = placed[p];
                    MarkOccupied(part);

                    if (pile)
                    {
                        GameObject pileBlock = CreateBlock(part, obstacleHeight, "Pile", null);
                        FillPile(pileBlock, part, largePrefab);
                        continue;
                    }

                    if (largePrefab != null)
                    {
                        GameObject large = CreateBlock(part, obstacleHeight, "Large", null);
                        FillWithProps(large, part, ToRect(part), new[] { largePrefab });
                        continue;
                    }

                    bool isPillar = part.width == pillarSize && part.height == pillarSize;
                    GameObject block = CreateBlock(part, obstacleHeight, isPillar ? "Pillar" : "Wall", isPillar ? pillarPrefab : wallPrefab);

                    // 소품 채우기: ㄱ자 벽의 두 번째 부분은 겹치는 모서리 칸 절반을 빼서 첫 부분 중심선에 맞물리게
                    Rect visualPart = p > 0 ? TrimOverlap(part, placed[0]) : ToRect(part);
                    FillWithProps(block, part, visualPart, isPillar ? pillarPropPrefabs : wallPropPrefabs);
                }
                break;
            }
        }
    }

    // 원점 기준 모양: 기둥 / 직선 벽 / ㄱ자 벽
    private List<RectInt> CreateObstacleShape(System.Random rng, out GameObject largePrefab, out bool pile)
    {
        List<RectInt> parts = new List<RectInt>();
        pile = false;

        // 상자 더미: 상자 수 x 간격(소품 긴 변, 90도 돌려도 안 겹침)만큼 칸 차지
        largePrefab = null;
        if (largePilePrefabs != null && largePilePrefabs.Length > 0 && rng.NextDouble() < largeObstacleChance)
        {
            GameObject candidate = largePilePrefabs[rng.Next(largePilePrefabs.Length)];
            if (candidate != null && TryGetPropBounds(candidate, out Bounds pb))
            {
                float spacing = PileSpacing(pb);
                int nx = rng.Next(pileCountX.x, Mathf.Max(pileCountX.x, pileCountX.y) + 1);
                int nz = rng.Next(pileCountZ.x, Mathf.Max(pileCountZ.x, pileCountZ.y) + 1);
                int w = Mathf.Max(1, Mathf.CeilToInt(nx * spacing - 0.05f));
                int d = Mathf.Max(1, Mathf.CeilToInt(nz * spacing - 0.05f));
                bool turn = rng.NextDouble() < 0.5;
                parts.Add(turn ? new RectInt(0, 0, d, w) : new RectInt(0, 0, w, d));
                largePrefab = candidate;
                pile = true;
                return parts;
            }
        }

        // 대형 장애물 (상자 더미가 없을 때): 소품 크기(올림)만큼 칸 차지, 방향 무작위
        bool usePile = largePilePrefabs != null && largePilePrefabs.Length > 0;
        if (!usePile && largePropPrefabs != null && largePropPrefabs.Length > 0 && rng.NextDouble() < largeObstacleChance)
        {
            GameObject candidate = largePropPrefabs[rng.Next(largePropPrefabs.Length)];
            if (candidate != null && TryGetPropBounds(candidate, out Bounds lb))
            {
                int w = Mathf.Max(1, Mathf.CeilToInt(lb.size.x - 0.05f));
                int d = Mathf.Max(1, Mathf.CeilToInt(lb.size.z - 0.05f));
                bool turn = rng.NextDouble() < 0.5;
                parts.Add(turn ? new RectInt(0, 0, d, w) : new RectInt(0, 0, w, d));
                largePrefab = candidate;
                return parts;
            }
        }
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

    #region NavMesh / Player

    // 생성물 루트의 자식(바닥·벽·장애물) 콜라이더로 NavMesh 베이크. 루트가 지워지면 NavMesh도 함께 제거됨
    private void BuildNavMesh()
    {
        Physics.SyncTransforms(); // 소품 크기에 맞춰 줄인 장애물 콜라이더를 베이크 전에 반영
        NavMeshSurface surface = generatedRoot.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
    }

    private void MovePlayerToStart()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        playerObj.transform.position = playerStartPosition;
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = playerStartPosition;
            rb.linearVelocity = Vector3.zero;
        }
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
        CreateFloor("Floor", Vector3.zero, new Vector2(arenaSize, arenaSize));

        // 외곽 벽 아래·바깥 바닥 4면 (Default 레이어라 JU 바닥 판정은 되지만 NavMesh 걷기 영역에서는 제외)
        float arenaHalf = arenaSize / 2f;
        float outer = arenaHalf + boundaryThickness + outerFloorMargin;
        float stripWidth = outer - arenaHalf;
        float stripCenter = arenaHalf + stripWidth / 2f;
        MarkNotWalkable(CreateFloor("OuterFloor_N", new Vector3(0f, 0f, stripCenter), new Vector2(outer * 2f, stripWidth)));
        MarkNotWalkable(CreateFloor("OuterFloor_S", new Vector3(0f, 0f, -stripCenter), new Vector2(outer * 2f, stripWidth)));
        MarkNotWalkable(CreateFloor("OuterFloor_E", new Vector3(stripCenter, 0f, 0f), new Vector2(stripWidth, arenaSize)));
        MarkNotWalkable(CreateFloor("OuterFloor_W", new Vector3(-stripCenter, 0f, 0f), new Vector2(stripWidth, arenaSize)));

        // 외곽 벽 4면 (Walls 레이어)
        float half = arenaHalf + boundaryThickness / 2f;
        float span = arenaSize + boundaryThickness * 2f;
        CreateWall("Boundary_N", new Vector3(0f, boundaryHeight / 2f, half), new Vector3(span, boundaryHeight, boundaryThickness));
        CreateWall("Boundary_S", new Vector3(0f, boundaryHeight / 2f, -half), new Vector3(span, boundaryHeight, boundaryThickness));
        CreateWall("Boundary_E", new Vector3(half, boundaryHeight / 2f, 0f), new Vector3(boundaryThickness, boundaryHeight, span));
        CreateWall("Boundary_W", new Vector3(-half, boundaryHeight / 2f, 0f), new Vector3(boundaryThickness, boundaryHeight, span));
    }

    // 키트 바닥 타일 (W3a): 바닥 큐브(안쪽 + 바깥 띠) 렌더러를 끄고, 바깥 띠까지 덮는 범위에 타일을 깖
    // 판정·NavMesh는 기존 바닥 큐브 그대로 (타일 콜라이더는 끔)
    // 기준점 위치와 무관하게 메시 바운드 기준으로 배치 (Warehouse = 모서리 기준점, 산업 에셋 = 가운데 기준점 모두 대응)
    private void BuildKitFloor(System.Random rng)
    {
        if (useGridFloor)
        {
            BuildGridFloor();
            return;
        }
        if (floorTilePrefabs == null || floorTilePrefabs.Length == 0 || floorTileSize <= 0.01f) return;

        foreach (Transform child in generatedRoot)
        {
            if (child.name == "Floor" || child.name.StartsWith("OuterFloor")) child.GetComponent<Renderer>().enabled = false;
        }

        Transform floorRoot = new GameObject("KitFloor").transform;
        floorRoot.SetParent(generatedRoot, false);

        float outer = arenaSize / 2f + boundaryThickness + outerFloorMargin + Mathf.Max(0f, kitFloorExtraMargin);
        int count = Mathf.CeilToInt(outer * 2f / floorTileSize);
        float start = -count * floorTileSize / 2f; // 가운데 정렬 (90m 맵 + 바깥 띠 6m + 추가 18m = 138m → 6m 타일 23칸)

        for (int x = 0; x < count; x++)
        {
            for (int z = 0; z < count; z++)
            {
                GameObject prefab = floorTilePrefabs[rng.Next(floorTilePrefabs.Length)];
                if (prefab == null || !TryGetPropBounds(prefab, out Bounds b)) continue;

                // 타일 크기가 floorTileSize와 다르면 가로·세로만 맞춤, 윗면 = y 0
                Vector3 scale = new Vector3(floorTileSize / Mathf.Max(0.01f, b.size.x), 1f, floorTileSize / Mathf.Max(0.01f, b.size.z));
                Quaternion rot = Quaternion.Euler(0f, randomFloorRotation ? rng.Next(4) * 90f : 0f, 0f);
                Vector3 center = transform.position + new Vector3(start + (x + 0.5f) * floorTileSize, -b.size.y / 2f, start + (z + 0.5f) * floorTileSize);
                GameObject tile = Instantiate(prefab, center - rot * Vector3.Scale(scale, b.center), rot, floorRoot);
                tile.transform.localScale = scale;
                foreach (Collider col in tile.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            }
        }

        // 움직이지 않는 타일 수백 개 → 정적 배칭으로 그리기 호출 줄임 (실행 중에만)
        if (Application.isPlaying) StaticBatchingUtility.Combine(floorRoot.gameObject);
    }

    // 격자 바닥 (floorTexture가 있으면 텍스처 바닥): 큐브 바닥 겉모습을 끄고 맵 + 바깥 여백을 덮는 판 한 장 (판정·NavMesh는 큐브 그대로)
    // 격자는 맵 모서리(Origin) 기준 정렬, 판 크기는 텍스처 한 장(4칸) 단위로 늘려 이음매 없이
    private void BuildGridFloor()
    {
        foreach (Transform child in generatedRoot)
        {
            if (child.name == "Floor" || child.name.StartsWith("OuterFloor")) child.GetComponent<Renderer>().enabled = false;
        }

        float period = Mathf.Max(0.5f, gridCellSize) * 4f;
        float extra = boundaryThickness + outerFloorMargin + Mathf.Max(0f, kitFloorExtraMargin);
        int k = Mathf.CeilToInt(extra / period);
        float size = arenaSize + 2f * k * period;
        Vector3 corner = Origin - new Vector3(k * period, 0f, k * period);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "GridFloor";
        quad.transform.SetParent(generatedRoot, true);
        quad.transform.position = corner + new Vector3(size / 2f, 0.002f, size / 2f);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 앞면 위, u = +X·v = +Z, 원점 = 모서리
        quad.transform.localScale = new Vector3(size, size, 1f);
        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null) quadCollider.enabled = false;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetGridMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        float tiles = size / (floorTexture != null ? Mathf.Max(0.5f, floorTextureWorldSize) : period);
        renderer.sharedMaterial.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
    }

    private Material GetGridMaterial()
    {
        if (gridMaterial != null) return gridMaterial;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        // 바닥 텍스처가 있으면 그것 (알베도 + 노멀, 발광 없음)
        if (floorTexture != null)
        {
            gridMaterial = new Material(lit) { name = "TextureFloor", hideFlags = HideFlags.DontSave };
            gridMaterial.SetTexture("_BaseMap", floorTexture);
            gridMaterial.SetColor("_BaseColor", Color.white);
            gridMaterial.SetFloat("_Metallic", floorMetallic);
            gridMaterial.SetFloat("_Smoothness", floorSmoothness);
            if (floorNormalMap != null)
            {
                gridMaterial.SetTexture("_BumpMap", floorNormalMap);
                gridMaterial.SetFloat("_BumpScale", floorNormalScale);
                gridMaterial.EnableKeyword("_NORMALMAP");
            }
            return gridMaterial;
        }

        BuildGridTextures();
        gridMaterial = new Material(lit) { name = "GridFloor", hideFlags = HideFlags.DontSave };
        gridMaterial.SetTexture("_BaseMap", gridAlbedo);
        gridMaterial.SetColor("_BaseColor", Color.white);
        gridMaterial.SetFloat("_Smoothness", gridSmoothness);
        gridMaterial.SetFloat("_Metallic", 0f);
        gridMaterial.SetTexture("_EmissionMap", gridEmissionMask);
        gridMaterial.SetColor("_EmissionColor", gridLineEmission);
        gridMaterial.EnableKeyword("_EMISSION");
        gridMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return gridMaterial;
    }

    // 4x4칸 텍스처: 칸마다 밝기 살짝 다르게 + 칸 경계 가는 선(양쪽 1px) + 2칸마다 굵은 선(양쪽 2px), 발광 마스크는 선만
    private void BuildGridTextures()
    {
        const int cellPx = 64;
        const int cells = 4;
        const int texSize = cellPx * cells;
        gridAlbedo = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true) { name = "GridFloorAlbedo", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        gridEmissionMask = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true) { name = "GridFloorEmission", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        Color32[] albedo = new Color32[texSize * texSize];
        Color32[] emission = new Color32[texSize * texSize];
        System.Random variation = new System.Random(11);
        float[] cellShade = new float[cells * cells];
        for (int i = 0; i < cellShade.Length; i++) cellShade[i] = 1f + ((float)variation.NextDouble() * 2f - 1f) * gridCellVariation;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float line = Mathf.Max(LineStrength(x, cellPx), LineStrength(y, cellPx));
                Color baseColor = gridBaseColor * cellShade[(y / cellPx) * cells + x / cellPx];
                baseColor.a = 1f;
                albedo[y * texSize + x] = Color.Lerp(baseColor, gridLineColor, line);
                byte e = (byte)(line * 255f);
                emission[y * texSize + x] = new Color32(e, e, e, 255);
            }
        }
        gridAlbedo.SetPixels32(albedo);
        gridAlbedo.Apply(true);
        gridEmissionMask.SetPixels32(emission);
        gridEmissionMask.Apply(true);
    }

    // 칸 경계까지 거리로 선 세기: 2칸마다 굵은 선 1.0 (양쪽 2px), 나머지 가는 선 0.6 (양쪽 1px)
    private static float LineStrength(int p, int cellPx)
    {
        int inCell = p % cellPx;
        int toEdge = Mathf.Min(inCell, cellPx - 1 - inCell);
        bool nearMajor = (inCell < cellPx / 2) ? (p / cellPx) % 2 == 0 : (p / cellPx) % 2 == 1;
        if (nearMajor && toEdge < 2) return 1f;
        if (toEdge < 1) return 0.6f;
        return 0f;
    }

    // 키트 외곽 벽 (W3b): 외곽 큐브 겉모습을 끄고, 안쪽 면을 맵 경계에 맞춰 벽을 이어 붙임
    // 메시 바운드 기준: 긴 축 = 길이, 짧은 축 = 두께. 첫 기본 벽 길이로 한 변 개수를 정하고 길이 방향만 늘리거나 줄여 딱 맞춤
    // 판정은 기존 외곽 큐브(Walls 레이어) 그대로, 벽 콜라이더는 끔
    private void BuildKitBoundary(System.Random rng)
    {
        if (boundaryWallPrefabs == null || boundaryWallPrefabs.Length == 0 || boundaryWallPrefabs[0] == null) return;
        if (!TryGetPropBounds(boundaryWallPrefabs[0], out Bounds baseBounds)) return;

        foreach (Transform child in generatedRoot)
        {
            if (!child.name.StartsWith("Boundary_")) continue;
            child.GetComponent<Renderer>().enabled = false;
            foreach (Transform visual in child) visual.gameObject.SetActive(false); // Boundary Prefab 표시도 끔
        }

        Transform wallRoot = new GameObject("KitBoundary").transform;
        wallRoot.SetParent(generatedRoot, false);

        float half = arenaSize / 2f;
        float baseLength = Mathf.Max(baseBounds.size.x, baseBounds.size.z);
        float baseThickness = Mathf.Min(baseBounds.size.x, baseBounds.size.z);
        int count = Mathf.Max(1, Mathf.RoundToInt(arenaSize / baseLength));
        float segment = (float)arenaSize / count;
        float start = -arenaSize / 2f;
        Vector3[] inwardNormals = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right }; // 북·남·동·서 벽의 안쪽 방향

        foreach (Vector3 inward in inwardNormals)
        {
            Vector3 sideCenter = -inward * half; // 맵 경계선 위의 점
            Vector3 run = Vector3.Cross(inward, Vector3.up); // LookRotation(run)이면 프리팹 Z → run, X → 안쪽

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = PickWallPrefab(rng);
                if (prefab == null || !TryGetPropBounds(prefab, out Bounds b)) continue;

                // 길이 축이 X인 프리팹은 90도 돌려 길이를 run에 맞춤, 뒤집기는 180도 (가운데 기준이라 위치 그대로)
                bool lengthIsZ = b.size.z >= b.size.x;
                Quaternion rot = Quaternion.LookRotation(run, Vector3.up)
                                 * Quaternion.Euler(0f, (lengthIsZ ? 0f : -90f) + (flipWallFacing ? 180f : 0f), 0f);
                float length = lengthIsZ ? b.size.z : b.size.x;
                float thickness = lengthIsZ ? b.size.x : b.size.z;
                float k = segment / Mathf.Max(0.01f, length);
                Vector3 scale = lengthIsZ ? new Vector3(1f, 1f, k) : new Vector3(k, 1f, 1f);

                // 안쪽 면 = 맵 경계, 바닥 = -kitWallSink
                Vector3 center = transform.position + sideCenter + run * (start + (i + 0.5f) * segment) - inward * (thickness / 2f)
                                 + Vector3.up * (b.size.y / 2f - kitWallSink);
                GameObject wall = Instantiate(prefab, center - rot * Vector3.Scale(scale, b.center), rot, wallRoot);
                wall.transform.localScale = scale;
                foreach (Collider col in wall.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            }
        }

        if (boundaryCornerPrefab != null && TryGetPropBounds(boundaryCornerPrefab, out Bounds cb))
        {
            float c = half + baseThickness / 2f;
            foreach (Vector2 sign in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
            {
                Vector3 center = transform.position + new Vector3(sign.x * c, cb.size.y / 2f - kitWallSink, sign.y * c);
                GameObject corner = Instantiate(boundaryCornerPrefab, center - cb.center, Quaternion.identity, wallRoot);
                foreach (Collider col in corner.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            }
        }

        if (Application.isPlaying) StaticBatchingUtility.Combine(wallRoot.gameObject);
    }

    private GameObject PickWallPrefab(System.Random rng)
    {
        bool accent = boundaryAccentPrefabs != null && boundaryAccentPrefabs.Length > 0 && rng.NextDouble() < boundaryAccentChance;
        GameObject[] pool = accent ? boundaryAccentPrefabs : boundaryWallPrefabs;
        return pool[rng.Next(pool.Length)];
    }

    // 윗면이 y=0인 바닥 블록 (size: x·z 크기)
    private GameObject CreateFloor(string objName, Vector3 localPos, Vector2 size)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = objName;
        floor.transform.SetParent(generatedRoot, false);
        floor.transform.position = transform.position + localPos + Vector3.down * (floorThickness / 2f);
        floor.transform.localScale = new Vector3(size.x, floorThickness, size.y);
        if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        return floor;
    }

    private void CreateWall(string objName, Vector3 localPos, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = objName;
        wall.transform.SetParent(generatedRoot, false);
        wall.transform.position = transform.position + localPos;
        wall.transform.localScale = size;
        ApplyWallStyle(wall);
        ApplyVisualPrefab(wall, boundaryPrefab);
    }

    // 폭 1인 막대(rect)가 다른 사각형과 끝 칸에서 겹치면 그 칸을 뺌 (ㄱ자 벽 모서리 중복 채우기 방지)
    // 겹치는 끝 칸의 절반만 빼서 첫 부분 중심선까지 닿게 함 (얇은 펜스가 모서리에서 틈 없이 맞물림)
    private static Rect TrimOverlap(RectInt rect, RectInt other)
    {
        if (!rect.Overlaps(other)) return ToRect(rect);
        if (rect.width == 1 && rect.height > 1)
        {
            if (other.Contains(new Vector2Int(rect.x, rect.yMin))) return new Rect(rect.x, rect.y + 0.5f, 1f, rect.height - 0.5f);
            if (other.Contains(new Vector2Int(rect.x, rect.yMax - 1))) return new Rect(rect.x, rect.y, 1f, rect.height - 0.5f);
        }
        else if (rect.height == 1 && rect.width > 1)
        {
            if (other.Contains(new Vector2Int(rect.xMin, rect.y))) return new Rect(rect.x + 0.5f, rect.y, rect.width - 0.5f, 1f);
            if (other.Contains(new Vector2Int(rect.xMax - 1, rect.y))) return new Rect(rect.x, rect.y, rect.width - 0.5f, 1f);
        }
        return ToRect(rect);
    }

    private static Rect ToRect(RectInt r) => new Rect(r.x, r.y, r.width, r.height);

    // 키트 소품으로 장애물 겉모습 채우기 (W3c): 큐브 렌더러·늘린 프리팹 표시를 끄고, 칸 영역에 소품을 줄지어 쌓음
    // 장애물마다 소품 하나를 골라 긴 쪽을 맞춰 배치, 칸에 맞게 propMaxStretch 안에서 늘리거나 줄임, 높이는 propTargetHeight까지 쌓음
    private System.Random propRng;
    private static readonly Dictionary<GameObject, Bounds> propBoundsCache = new Dictionary<GameObject, Bounds>();

    private void FillWithProps(GameObject block, RectInt blockCells, Rect cells, GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0 || cells.width <= 0f || cells.height <= 0f) return;
        GameObject prefab = prefabs[propRng.Next(prefabs.Length)];
        if (prefab == null || !TryGetPropBounds(prefab, out Bounds b)) return;

        block.GetComponent<Renderer>().enabled = false;
        foreach (Transform visual in block.transform) visual.gameObject.SetActive(false); // Pillar/Wall Prefab 표시도 끔

        // 소품 긴 쪽을 칸의 긴 쪽에 맞춤 (90도 회전 시 소품 X → 월드 -Z, 소품 Z → 월드 X)
        float width = cells.width, depth = cells.height;
        bool rotate = (b.size.x >= b.size.z) != (width >= depth);
        bool jitter = Mathf.Max(b.size.x, b.size.z) < 1.5f;
        float footX = rotate ? b.size.z : b.size.x;
        float footZ = rotate ? b.size.x : b.size.z;

        int nx = Mathf.Max(1, Mathf.RoundToInt(width / footX));
        int nz = Mathf.Max(1, Mathf.RoundToInt(depth / footZ));
        float kx = Mathf.Clamp(width / (nx * footX), 1f / propMaxStretch, propMaxStretch);
        float kz = Mathf.Clamp(depth / (nz * footZ), 1f / propMaxStretch, propMaxStretch);
        float ky = HeightScale(b);
        Vector3 localScale = rotate ? new Vector3(kz, ky, kx) : new Vector3(kx, ky, kz);
        float stepX = footX * kx, stepZ = footZ * kz;
        float propHeight = b.size.y * ky;
        int layers = Mathf.Clamp(Mathf.CeilToInt(propTargetHeight / Mathf.Max(0.05f, propHeight) - 0.15f), 1, 4);

        // 소품은 늘린 큐브 아래가 아니라 스케일 없는 묶음에 둠 (늘린 부모 아래에서 회전하면 모양이 비틀림)
        Transform propRoot = new GameObject(block.name + "_Props").transform;
        propRoot.SetParent(generatedRoot, false);

        Vector3 rectCenter = CellCenterToWorld(cells.center);
        float x0 = rectCenter.x - stepX * nx / 2f + stepX / 2f;
        float z0 = rectCenter.z - stepZ * nz / 2f + stepZ / 2f;

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    float yaw = (rotate ? 90f : 0f) + (jitter ? (float)(propRng.NextDouble() * 2.0 - 1.0) * propYawJitter : 0f);
                    Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 center = new Vector3(x0 + ix * stepX, transform.position.y + propHeight * (layer + 0.5f), z0 + iz * stepZ);
                    Vector3 pos = center - rot * Vector3.Scale(localScale, b.center); // 소품 바운드 중심을 목표 위치에

                    GameObject prop = Instantiate(prefab, pos, rot, propRoot);
                    prop.transform.localScale = localScale;
                    foreach (Collider col in prop.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                }
            }
        }

        if (fitColliderToProps) FitBlockToProps(block, blockCells, propRoot);
        if (Application.isPlaying) StaticBatchingUtility.Combine(propRoot.gameObject);
    }

    // propMaxHeight를 넘는 소품은 세로로만 줄임
    private float HeightScale(Bounds b)
    {
        return propMaxHeight > 0.01f && b.size.y > propMaxHeight ? propMaxHeight / b.size.y : 1f;
    }

    // 더미 간격: 소품 긴 변 (90도 돌려 놓아도 이웃과 겹치지 않게)
    private static float PileSpacing(Bounds b) => Mathf.Max(b.size.x, b.size.z) + 0.05f;

    // 상자 더미: 칸 영역에 상자를 격자로, 칸마다 1~2단 무작위·90도 단위 회전 + 살짝 비틀기 (쌓아 둔 느낌)
    private void FillPile(GameObject block, RectInt blockCells, GameObject prefab)
    {
        if (prefab == null || !TryGetPropBounds(prefab, out Bounds b)) return;
        block.GetComponent<Renderer>().enabled = false;
        foreach (Transform visual in block.transform) visual.gameObject.SetActive(false);

        float spacing = PileSpacing(b);
        Rect cells = ToRect(blockCells);
        int nx = Mathf.Max(1, Mathf.FloorToInt((cells.width + 0.05f) / spacing));
        int nz = Mathf.Max(1, Mathf.FloorToInt((cells.height + 0.05f) / spacing));
        float ky = HeightScale(b);
        Vector3 localScale = new Vector3(1f, ky, 1f);
        float propHeight = b.size.y * ky;

        Transform propRoot = new GameObject(block.name + "_Props").transform;
        propRoot.SetParent(generatedRoot, false);
        Vector3 rectCenter = CellCenterToWorld(cells.center);
        float x0 = rectCenter.x - spacing * nx / 2f + spacing / 2f;
        float z0 = rectCenter.z - spacing * nz / 2f + spacing / 2f;

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                int layers = propRng.NextDouble() < pileTallChance ? 2 : 1;
                for (int layer = 0; layer < layers; layer++)
                {
                    float yaw = propRng.Next(4) * 90f + (float)(propRng.NextDouble() * 2.0 - 1.0) * (layer > 0 ? 8f : 4f);
                    Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 center = new Vector3(x0 + ix * spacing, transform.position.y + propHeight * (layer + 0.5f), z0 + iz * spacing);
                    GameObject prop = Instantiate(prefab, center - rot * Vector3.Scale(localScale, b.center), rot, propRoot);
                    prop.transform.localScale = localScale;
                    foreach (Collider col in prop.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                }
            }
        }

        if (fitColliderToProps) FitBlockToProps(block, blockCells, propRoot);
        if (Application.isPlaying) StaticBatchingUtility.Combine(propRoot.gameObject);
    }

    // 장애물 큐브(콜라이더)의 가로·세로를 원래 칸 영역과 소품 영역이 겹치는 부분으로 줄임 (높이는 그대로)
    private void FitBlockToProps(GameObject block, RectInt blockCells, Transform propRoot)
    {
        Renderer[] renderers = propRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds visual = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) visual.Encapsulate(renderers[i].bounds);

        Vector3 cellMin = Origin + new Vector3(blockCells.xMin, 0f, blockCells.yMin);
        Vector3 cellMax = Origin + new Vector3(blockCells.xMax, 0f, blockCells.yMax);
        float minX = Mathf.Max(cellMin.x, visual.min.x), maxX = Mathf.Min(cellMax.x, visual.max.x);
        float minZ = Mathf.Max(cellMin.z, visual.min.z), maxZ = Mathf.Min(cellMax.z, visual.max.z);
        if (maxX - minX < 0.1f || maxZ - minZ < 0.1f) return;

        Vector3 pos = block.transform.position;
        block.transform.position = new Vector3((minX + maxX) / 2f, pos.y, (minZ + maxZ) / 2f);
        block.transform.localScale = new Vector3(maxX - minX, block.transform.localScale.y, maxZ - minZ);
    }

    // 프리팹 루트 기준 메시 바운드 (크기·기준점 위치)
    private static bool TryGetPropBounds(GameObject prefab, out Bounds bounds)
    {
        if (propBoundsCache.TryGetValue(prefab, out bounds)) return true;

        Matrix4x4 rootInverse = Matrix4x4.TRS(prefab.transform.position, prefab.transform.rotation, Vector3.one).inverse;
        bool has = false;
        bounds = new Bounds();
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = rootInverse * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
                else bounds.Encapsulate(p);
            }
        }
        if (has) propBoundsCache[prefab] = bounds;
        return has;
    }

    // 격자 사각형 → 큐브 장애물
    private GameObject CreateBlock(RectInt cells, float height, string objName, GameObject visualPrefab)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objName;
        block.transform.SetParent(generatedRoot, false);
        block.transform.position = CellCenterToWorld(cells.center) + Vector3.up * (height / 2f);
        block.transform.localScale = new Vector3(cells.width, height, cells.height);
        ApplyWallStyle(block);
        ApplyVisualPrefab(block, visualPrefab);
        return block;
    }

    // 큐브 렌더러를 끄고 프리팹을 자식으로 붙여 큐브 크기에 맞춤 (부모 스케일을 그대로 받음)
    // 프리팹 콜라이더는 꺼서 NavMesh·벽 판정은 큐브 콜라이더만 사용
    private static void ApplyVisualPrefab(GameObject block, GameObject prefab)
    {
        if (prefab == null) return;

        block.GetComponent<Renderer>().enabled = false;

        GameObject visual = Instantiate(prefab, block.transform);
        visual.name = prefab.name;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        foreach (Collider col in visual.GetComponentsInChildren<Collider>(true)) col.enabled = false;
    }

    private void ApplyWallStyle(GameObject obj)
    {
        int layer = LayerMask.NameToLayer(wallLayerName);
        if (layer >= 0) obj.layer = layer;
        if (wallMaterial != null) obj.GetComponent<Renderer>().sharedMaterial = wallMaterial;

        // 윗면이 걷기 영역이 되지 않게 (SamplePosition이 벽 꼭대기로 붙는 것 방지)
        MarkNotWalkable(obj);
    }

    private static void MarkNotWalkable(GameObject obj)
    {
        NavMeshModifier modifier = obj.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = NavMesh.GetAreaFromName("Not Walkable");
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
