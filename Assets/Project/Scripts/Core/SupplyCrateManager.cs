using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 보급 상자 생성·관리 매니저 (전투 씬 로드 시 GameplayBootstrap이 자동 생성, 씬 배치 불필요, GDD 8-6 #13)
// 스테이지 시작 시 상자를 맵 무작위 위치에 생성하고, 다음 라운드 시작 시 남은 상자를 지우고 다시 생성
public class SupplyCrateManager : MonoBehaviour
{
    public static SupplyCrateManager Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private int cratesPerStage = 2;
    [SerializeField] private float minSpawnRadius = 10f;          // 맵 중심 기준 생성 반경 (탈출 구역과 같은 범위)
    [SerializeField] private float maxSpawnRadius = 35f;
    [SerializeField] private float minDistanceFromPlayer = 15f;
    [SerializeField] private float minDistanceFromOthers = 10f;   // 탈출 구역·다른 상자와의 최소 거리
    [SerializeField] private int maxPlacementAttempts = 30;

    [Header("Open")]
    [SerializeField] private float openRadius = 2f;
    [SerializeField] private float openTime = 3f;

    [Header("Visual")]
    [Tooltip("상자 외형 모델. 비우면 노란 큐브 사용")]
    [SerializeField] private GameObject crateModel;
    [SerializeField] private Color crateColor = new Color(1f, 0.75f, 0.1f);

    private readonly List<SupplyCrate> activeCrates = new List<SupplyCrate>();

    // GameplayBootstrap이 전투 씬 로드마다 호출
    public static void EnsureCreated()
    {
        if (FindFirstObjectByType<SupplyCrateManager>() == null)
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
        ExtractionZone zone = FindFirstObjectByType<ExtractionZone>();

        for (int i = 0; i < cratesPerStage; i++)
        {
            if (TryFindSpawnPosition(player, zone, out Vector3 position))
            {
                activeCrates.Add(CreateCrate(position));
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

    private bool TryFindSpawnPosition(Transform player, ExtractionZone zone, out Vector3 position)
    {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 candidate = new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas)) continue;
            candidate = hit.position;

            if (player != null && HorizontalDistance(candidate, player.position) < minDistanceFromPlayer) continue;
            if (zone != null && HorizontalDistance(candidate, zone.transform.position) < minDistanceFromOthers) continue;

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
        if (crateModel != null)
        {
            crateObj = Instantiate(crateModel, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }
        else
        {
            // 기본 외형: 노란 큐브 (바닥 위에 올려 둠)
            crateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
        NavMeshObstacle obstacle = crateObj.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;

        SupplyCrate crate = crateObj.AddComponent<SupplyCrate>();
        crate.openRadius = openRadius;
        crate.openTime = openTime;
        return crate;
    }
}
