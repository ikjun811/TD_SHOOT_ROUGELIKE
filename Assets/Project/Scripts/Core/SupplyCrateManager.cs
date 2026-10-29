using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 보급 상자 생성·관리 매니저 (전투 씬 로드 시 GameplayBootstrap이 자동 생성, 씬 배치 불필요, GDD 8-6 #13)
// 외형 모델을 쓰려면 Tools > TD Shoot > Apply Supply Crate Model 이 씬에 매니저를 두고 프리팹 연결 (씬에 있으면 자동 생성 안 함)
// 스테이지 시작 시 상자를 맵 무작위 위치에 생성하고, 다음 라운드 시작 시 남은 상자를 지우고 다시 생성
public class SupplyCrateManager : MonoBehaviour
{
    public static SupplyCrateManager Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private Vector2Int crateCountRange = new Vector2Int(2, 3); // 스테이지마다 이 범위에서 무작위
    [SerializeField] private float minSpawnRadius = 10f;          // 맵 중심 기준 생성 반경 (탈출 구역과 같은 범위)
    [SerializeField] private float maxSpawnRadius = 35f;
    [SerializeField] private float minDistanceFromPlayer = 15f;
    [SerializeField] private float minDistanceFromOthers = 10f;   // 다른 상자와의 최소 거리
    [Tooltip("탈출 구역 중심과의 최소 거리. 탈출 구역 반지름(10m) + 여는 범위 + 여유 → 탈출하면서 상자를 열 수 없게")]
    [SerializeField] private float minDistanceFromExtraction = 16f;
    [SerializeField] private int maxPlacementAttempts = 30;
    [Tooltip("BSP 맵: 구역 중앙에서 이 거리(m) 안으로 위치를 흔듦 (중앙 빈 공간 8x8 안)")]
    [SerializeField] private float zoneCenterJitter = 2f;

    [Header("Open")]
    [SerializeField] private float openRadius = 2f;
    [SerializeField] private float openTime = 3f;

    [Header("Visual")]
    [Tooltip("상자 외형 모델. 비우면 노란 큐브 사용")]
    [SerializeField] private GameObject crateModel;
    [Tooltip("상자 모델 크기 배율 (Crate Long 0.64x1.27m는 플레이어에 비해 작아 키움)")]
    [SerializeField] private float crateModelScale = 1.4f;
    [Tooltip("상자 모델 색조 (텍스처에 곱함, 무기 모델은 제외) — 회색 장애물과 구분되는 보급품 노랑")]
    [SerializeField] private Color crateModelTint = new Color(1f, 0.78f, 0.3f);
    [Tooltip("상자 위에 올릴 무기 모델 (비우면 없음) — 긴 축을 상자 길이 방향으로, 납작한 면을 아래로 자동 눕힘")]
    [SerializeField] private GameObject crateWeaponModel;
    [Tooltip("무기 길이 = 상자 긴 변 x 이 비율")]
    [SerializeField] private float crateWeaponLengthRatio = 0.85f;
    [Tooltip("무기를 상자 위에서 비스듬히 돌리는 각도 (도)")]
    [SerializeField] private float crateWeaponYaw = 18f;
    [SerializeField] private Color crateColor = new Color(1f, 0.75f, 0.1f);

    private readonly List<SupplyCrate> activeCrates = new List<SupplyCrate>();

    // GameplayBootstrap이 전투 씬 로드마다 호출
    public static void EnsureCreated()
    {
        if (FindAnyObjectByType<SupplyCrateManager>() == null)
        {
            new GameObject("SupplyCrateManager").AddComponent<SupplyCrateManager>();
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private IEnumerator Start()
    {
        // 플레이어·탈출 구역 위치가 정해진 뒤 첫 스테이지 상자 생성
        yield return null;
        RespawnCrates();
    }

    // 다음 라운드 시작 시 MaintenanceManager가 호출 (플레이어·탈출 구역 재배치 이후)
    public void RespawnCrates()
    {
        ClearCrates();

        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        ExtractionZone zone = FindAnyObjectByType<ExtractionZone>();

        int count = Random.Range(crateCountRange.x, crateCountRange.y + 1);

        BspArenaGenerator arena = FindAnyObjectByType<BspArenaGenerator>();
        if (arena != null && arena.Leaves.Count > 0)
        {
            SpawnCratesInZones(arena, count, player, zone);
        }
        else
        {
            // 기존 고정 맵: 맵 중심 기준 반경 무작위
            for (int i = 0; i < count; i++)
            {
                if (TryFindSpawnPosition(player, zone, out Vector3 position))
                {
                    activeCrates.Add(CreateCrate(position));
                }
            }
        }

        Debug.Log($"SupplyCrateManager: Spawned {activeCrates.Count} crates.");
    }

    // 아직 열리지 않은 상자 중 가장 가까운 것 (가이드 화살표용)
    public SupplyCrate GetNearestCrate(Vector3 from)
    {
        SupplyCrate nearest = null;
        float best = float.MaxValue;

        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate == null || crate.IsOpened) continue;
            float dist = (crate.transform.position - from).sqrMagnitude;
            if (dist < best)
            {
                best = dist;
                nearest = crate;
            }
        }
        return nearest;
    }

    private void ClearCrates()
    {
        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null) Destroy(crate.gameObject);
        }
        activeCrates.Clear();
    }

    // BSP 맵: 구역 중앙 빈 공간에 구역당 최대 1개. 플레이어 시작 구역·탈출 구역이 있는 구역은 제외
    // 1차는 거리 조건 적용, 개수가 모자라면 2차에서 거리 조건만 풀고 남은 구역에 배치
    private void SpawnCratesInZones(BspArenaGenerator arena, int count, Transform player, ExtractionZone zone)
    {
        List<Vector3> centers = arena.GetZoneCenters();
        int playerZone = player != null ? arena.GetZoneIndexAt(player.position) : -1;
        int extractionZone = zone != null ? arena.GetZoneIndexAt(zone.transform.position) : -1;

        List<int> order = new List<int>();
        for (int i = 0; i < centers.Count; i++)
        {
            if (i != playerZone && i != extractionZone) order.Add(i);
        }
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        HashSet<int> usedZones = new HashSet<int>();
        for (int pass = 0; pass < 2 && activeCrates.Count < count; pass++)
        {
            foreach (int index in order)
            {
                if (activeCrates.Count >= count) break;
                if (usedZones.Contains(index)) continue;

                Vector3 candidate = centers[index] + new Vector3(Random.Range(-zoneCenterJitter, zoneCenterJitter), 0f, Random.Range(-zoneCenterJitter, zoneCenterJitter));
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                candidate = hit.position;

                if (pass == 0)
                {
                    if (player != null && HorizontalDistance(candidate, player.position) < minDistanceFromPlayer) continue;
                    if (zone != null && HorizontalDistance(candidate, zone.transform.position) < minDistanceFromExtraction) continue;
                }

                usedZones.Add(index);
                activeCrates.Add(CreateCrate(candidate));
            }
        }

        if (activeCrates.Count < count)
        {
            Debug.LogWarning($"SupplyCrateManager: 빈 구역이 부족해 상자를 {activeCrates.Count}/{count}개만 생성했습니다.");
        }
    }

    private bool TryFindSpawnPosition(Transform player, ExtractionZone zone, out Vector3 position)
    {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 candidate = new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas)) continue;
            candidate = hit.position;

            if (player != null && HorizontalDistance(candidate, player.position) < minDistanceFromPlayer) continue;
            if (zone != null && HorizontalDistance(candidate, zone.transform.position) < minDistanceFromExtraction) continue;

            bool tooClose = false;
            foreach (SupplyCrate other in activeCrates)
            {
                if (other != null && HorizontalDistance(candidate, other.transform.position) < minDistanceFromOthers)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            position = candidate;
            return true;
        }

        position = Vector3.zero;
        Debug.LogWarning("SupplyCrateManager: 상자 위치를 찾지 못했습니다. 생성 반경·거리 조건을 확인하세요.");
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private SupplyCrate CreateCrate(Vector3 position)
    {
        GameObject crateObj;
        NavMeshObstacle obstacle;
        if (crateModel != null)
        {
            crateObj = BuildModelCrate(position, out Bounds localBounds);
            obstacle = crateObj.AddComponent<NavMeshObstacle>();
            obstacle.center = localBounds.center;
            obstacle.size = localBounds.size;
        }
        else
        {
            // 기본 외형: 노란 큐브 (바닥 위에 올려 둠)
            crateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle = null;
            crateObj.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            crateObj.transform.SetPositionAndRotation(position + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            Renderer rend = crateObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = crateColor;
                if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", crateColor);
            }
        }
        crateObj.name = "SupplyCrate";

        // 적이 상자를 피해 다니도록
        if (crateModel == null) obstacle = crateObj.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;

        SupplyCrate crate = crateObj.AddComponent<SupplyCrate>();
        crate.openRadius = openRadius;
        crate.openTime = openTime;
        return crate;
    }

    // 모델 상자: 바닥 높이 루트(무작위 회전) 아래에 모델을 가운데·바닥 기준으로 맞춤 (기준점이 모서리인 Crate Long 대응)
    //   + 위에 무기 모델 (긴 축 = 상자 긴 변 방향, 가장 얇은 축 = 위아래, 비스듬히). localBounds = 루트 기준 상자 범위 (NavMesh 장애물)
    private GameObject BuildModelCrate(Vector3 position, out Bounds localBounds)
    {
        GameObject root = new GameObject("SupplyCrate");
        root.transform.position = position;

        GameObject model = Instantiate(crateModel, root.transform);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = crateModel.transform.localScale * crateModelScale;
        model.transform.localPosition = Vector3.zero;
        Bounds b = GetWorldBounds(model);
        model.transform.localPosition = new Vector3(-b.center.x + position.x, -b.min.y + position.y, -b.center.z + position.z);
        b = GetWorldBounds(model);
        localBounds = new Bounds(b.center - position, b.size);
        TintRenderers(model, crateModelTint);

        if (crateWeaponModel != null)
        {
            // 상자 긴 변이 루트 Z가 되도록 모델을 돌려 둠
            if (b.size.x > b.size.z)
            {
                model.transform.RotateAround(position, Vector3.up, 90f);
                b = GetWorldBounds(model);
                localBounds = new Bounds(b.center - position, b.size);
            }

            GameObject weapon = Instantiate(crateWeaponModel, root.transform);
            foreach (Collider c in weapon.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Animator a in weapon.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = crateWeaponModel.transform.localScale;

            // 긴 축 → Z, 가장 얇은 축 → Y
            Vector3 size = GetWorldBounds(weapon).size;
            Quaternion lay = size.x >= size.y && size.x >= size.z ? Quaternion.Euler(0f, 90f, 0f)
                           : size.y >= size.z ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            weapon.transform.localRotation = lay;
            size = GetWorldBounds(weapon).size;
            if (size.y > size.x) weapon.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * lay;

            // 길이 맞춤 후 상자 윗면 가운데에 올리고 비스듬히
            size = GetWorldBounds(weapon).size;
            float length = Mathf.Max(0.01f, size.z);
            weapon.transform.localScale *= localBounds.size.z * crateWeaponLengthRatio / length;
            weapon.transform.localRotation = Quaternion.Euler(0f, crateWeaponYaw, 0f) * weapon.transform.localRotation;
            Bounds wb = GetWorldBounds(weapon);
            Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
            weapon.transform.position += new Vector3(top.x - wb.center.x, top.y - wb.min.y + 0.01f, top.z - wb.center.z);
        }

        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        return root;
    }

    // 재질 복사 없이 렌더러별 PropertyBlock으로 기본 색만 바꿈 (URP _BaseColor, Built-in _Color)
    private static void TintRenderers(GameObject obj, Color tint)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            Material mat = r.sharedMaterial;
            if (mat == null) continue;
            r.GetPropertyBlock(block);
            if (mat.HasProperty("_BaseColor")) block.SetColor("_BaseColor", mat.GetColor("_BaseColor") * tint);
            if (mat.HasProperty("_Color")) block.SetColor("_Color", mat.GetColor("_Color") * tint);
            r.SetPropertyBlock(block);
        }
    }

    private static Bounds GetWorldBounds(GameObject obj)
    {
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }
}
