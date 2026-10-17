using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMesh 사용을 위해 필수

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int currentRound = 1;
    [SerializeField] private int maxRound = 15; // 최종 라운드 (이 라운드 탈출 = 게임 클리어, GDD 8-4 #15)
    [SerializeField] private int baseKillsPerRound = 10; // 1라운드 목표 처치 수
    [SerializeField] private int extraKillsPerRound = 5; // 라운드당 추가 처치 수

    [Header("Multiple Enemy Pools")]
    [Tooltip("일반 적 프리팹 리스트 (여러 종 등록 가능)")]
    [SerializeField] private List<GameObject> normalEnemyPrefabs = new List<GameObject>();
    [Tooltip("엘리트 적 프리팹 리스트 (여러 종 등록 가능)")]
    [SerializeField] private List<GameObject> eliteEnemyPrefabs = new List<GameObject>();

    [Header("Off-screen Spawn Settings")]
    [SerializeField] private float minSpawnDistance = 18f; // 화면 밖 최소 거리
    [SerializeField] private float maxSpawnDistance = 25f; // 화면 밖 최대 거리

    [Header("Round Scaling (GDD 8-4 #15)")]
    [SerializeField] private int baseMaxConcurrentEnemies = 12;          // 동시 최대 = 기본 + 라운드당 증가 x 라운드
    [SerializeField] private int maxConcurrentPerRound = 1;
    [SerializeField] private float baseSpawnInterval = 2.0f;             // 1라운드 스폰 간격 (초)
    [SerializeField] private float spawnIntervalReductionPerRound = 0.08f;
    [SerializeField] private float minSpawnInterval = 0.8f;

    // 현재 라운드 기준 필드 최대 동시 적 수 / 스폰 간격
    private int MaxConcurrentEnemies => baseMaxConcurrentEnemies + maxConcurrentPerRound * currentRound;
    private float SpawnInterval => Mathf.Max(minSpawnInterval, baseSpawnInterval - spawnIntervalReductionPerRound * (currentRound - 1));

    [Header("Current Wave Status (Read Only)")]
    [SerializeField] private int targetKillsThisRound;
    [SerializeField] private int currentKillsCount;
    [SerializeField] private int currentEnemiesAlive;
    [SerializeField] private bool isWaveActive = false;
    [SerializeField] private bool hasTriggered50PercentEvent = false;

    [Header("Round Clear Delay Settings")]
    [SerializeField] private float roundClearDelay = 2.5f; // 라운드 클리어 후 대기시간 (초)

    private Transform playerTransform;

    public int CurrentRound => currentRound;
    public int MaxRound => maxRound;

    // 탈출 성공 시 호출 (사망 시에는 호출하지 않아 실패한 라운드를 다시 시도, GDD 6-4)
    public void AdvanceRound()
    {
        // 최종 라운드 탈출 = 게임 클리어 (클리어 화면은 미구현, GDD 8-4 #10)
        if (currentRound >= maxRound)
        {
            Debug.Log($"WaveManager: Final round {maxRound} extracted -> GAME CLEAR (클리어 화면 미구현)");
            return;
        }

        currentRound++;
        Debug.Log($"WaveManager: Round advanced -> {currentRound} (max enemies {MaxConcurrentEnemies}, spawn interval {SpawnInterval:0.##}s)");
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 플레이어 트랜스폼 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // 첫 번째 라운드 시작
        StartRound(currentRound);
    }

    public void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        currentKillsCount = 0;
        currentEnemiesAlive = 0;
        hasTriggered50PercentEvent = false;

        // 라운드별 목표 처치 수 계산
        targetKillsThisRound = baseKillsPerRound + (currentRound - 1) * extraKillsPerRound;
        isWaveActive = true;

        Debug.Log($"=============== [ROUND {currentRound} START] ===============");
        Debug.Log($"목표 처치 수: {targetKillsThisRound} 마리");

        // 10라운드 단위 메이저 라운드 체크
        if (currentRound % 10 == 0)
        {
            Debug.Log($"⚠️ [메이저 라운드!] 10단위 라운드입니다. 보스전이 진행됩니다!");
        }

        // 스폰 루틴 시작
        StartCoroutine(SpawnEnemyRoutine());
    }

    // 적 무한 스폰 루틴 (10마리 누적 제한 삭제)
    private IEnumerator SpawnEnemyRoutine()
    {
        while (isWaveActive)
        {
            // 필드에 동시 존재하는 적 수가 최대 동시 스폰 제한 수보다 적으면 영구 지속 스폰
            if (currentEnemiesAlive < MaxConcurrentEnemies)
            {
                SpawnRandomNormalEnemy();
            }

            yield return new WaitForSeconds(SpawnInterval);
        }
    }

    // 일반 적 무작위 스폰
    private void SpawnRandomNormalEnemy()
    {
        if (normalEnemyPrefabs == null || normalEnemyPrefabs.Count == 0)
        {
            Debug.LogWarning("WaveManager: Normal Enemy Prefabs 리스트가 비어있습니다!");
            return;
        }

        // 1. 일반 적 리스트 중 무작위 하나 선택
        GameObject selectedPrefab = normalEnemyPrefabs[Random.Range(0, normalEnemyPrefabs.Count)];

        // 2. 화면 밖 무작위 위치 계산
        Vector3 spawnPosition = GetRandomOffscreenSpawnPosition();

        // 3. 적 생성
        Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        currentEnemiesAlive++;
    }

    // 엘리트 적 무작위 스폰
    private void SpawnRandomEliteEnemy()
    {
        if (eliteEnemyPrefabs == null || eliteEnemyPrefabs.Count == 0)
        {
            Debug.LogWarning("WaveManager: Elite Enemy Prefabs 리스트가 비어있습니다!");
            return;
        }

        // 1. 엘리트 적 리스트 중 무작위 하나 선택
        GameObject selectedPrefab = eliteEnemyPrefabs[Random.Range(0, eliteEnemyPrefabs.Count)];

        // 2. 화면 밖 무작위 위치 계산
        Vector3 spawnPosition = GetRandomOffscreenSpawnPosition();

        // 3. 엘리트 적 생성
        Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        currentEnemiesAlive++;
    }

    // 화면 밖 무작위 좌표 계산 함수 (NavMesh 보정 포함)
    // 카메라 화면 안에 들어오는 위치는 다시 뽑음 (카메라 각도·크기·룩어헤드가 바뀌어도 자동 대응)
    private Vector3 GetRandomOffscreenSpawnPosition()
    {
        if (playerTransform == null) return transform.position;

        Camera cam = Camera.main;
        Vector3 candidate = playerTransform.position;

        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            // 플레이어 기준 360도 무작위 방향
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);

            Vector3 spawnOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * randomDistance;
            candidate = playerTransform.position + spawnOffset;

            // NavMesh 바닥 유효 좌표로 자동 보정
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8.0f, NavMesh.AllAreas))
            {
                candidate = hit.position;
            }

            if (cam == null || !IsVisibleOnScreen(cam, candidate)) return candidate;
        }

        return candidate; // 모든 시도가 화면 안이면 마지막 좌표 사용
    }

    private const int MaxSpawnAttempts = 10;
    private const float ScreenMargin = 0.1f; // 화면 가장자리 여유 (뷰포트 비율)

    // 적 몸통 높이(1m) 기준으로 화면(가장자리 여유 포함) 안에 보이는지 검사
    private bool IsVisibleOnScreen(Camera cam, Vector3 position)
    {
        Vector3 vp = cam.WorldToViewportPoint(position + Vector3.up);
        return vp.z > 0f
            && vp.x > -ScreenMargin && vp.x < 1f + ScreenMargin
            && vp.y > -ScreenMargin && vp.y < 1f + ScreenMargin;
    }

    // 적 사망 시 호출 (10마리 제한 삭제 및 무한 스폰 유지)
    public void OnEnemyKilled()
    {
        if (!isWaveActive) return;

        currentKillsCount++;
        currentEnemiesAlive = Mathf.Max(0, currentEnemiesAlive - 1);

        // 5마리 처치 시 엘리트 적 1마리 스폰 트리거 유지
        if (currentKillsCount >= 5 && !hasTriggered50PercentEvent)
        {
            hasTriggered50PercentEvent = true;
            On50PercentProgressTriggered();
        }


    }

    private void On50PercentProgressTriggered()
    {
        Debug.Log("⚠️ [경고!] 웨이브 50% 달성! 엘리트 적이 화면 밖에서 스폰됩니다!");
        SpawnRandomEliteEnemy();
    }

    private void CompleteRound()
    {
        isWaveActive = false;
        StopAllCoroutines(); // 스폰 루틴 정지

        // 2.5초 유예 대기 연출 루틴 시작
        StartCoroutine(RoundClearSequence());
    }

    private IEnumerator RoundClearSequence()
    {
        Debug.Log($"🎉 [ROUND {currentRound} CLEAR!] 라운드를 클리어했습니다.");

        // 1. 맵에 떨어진 아이템들을 먼저 자석으로 흡수 연출!
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.CollectAllRemainingDropsOnField();
        }

        // 2. 지정된 대기시간(2.5초) 동안 플레이어가 여운을 즐기도록 대기
        Debug.Log($"⏳ {roundClearDelay}초 후 정비 단계로 진입합니다...");
        yield return new WaitForSeconds(roundClearDelay);

        // 3. 2.5초 후 정비 단계 진입 및 일시정지!
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
    }


    // 맵 상에 활성화되어 있는 모든 적 개체 즉시 디스폰 처리
    public static void DespawnAllActiveEnemies()
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
        Debug.Log($"WaveManager: Despawned {enemies.Length} active enemies.");
    }

    // ⭐ 다음 라운드 진입 시 적 스폰 루틴 재가동 함수
    public void RestartWaveSpawning()
    {
        DespawnAllActiveEnemies(); // 잔여 적 제거
        currentKillsCount = 0;
        currentEnemiesAlive = 0;
        hasTriggered50PercentEvent = false;
        isWaveActive = true;

        StopAllCoroutines();
        StartCoroutine(SpawnEnemyRoutine()); // 무한 적 스폰 코루틴 재가동
        Debug.Log($"WaveManager: Restarted wave spawning. Round {currentRound}");
    }
}