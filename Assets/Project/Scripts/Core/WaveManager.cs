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

    [Header("Elite Spawn (GDD 8-4 #15, 임시값)")]
    [SerializeField] private float eliteGracePeriod = 20f;              // 스테이지 시작 후 엘리트 없음 (초)
    [SerializeField] private float eliteTimeBaseInterval = 60f;         // 시간 기반 첫 간격 = max(최소 첫 간격, 기본 - 라운드당 감소 x (r-1))
    [SerializeField] private float eliteTimeReductionPerRound = 2.5f;
    [SerializeField] private float eliteTimeMinFirstInterval = 25f;
    [SerializeField] private float eliteTimeAcceleration = 0.85f;       // 등장할 때마다 다음 간격에 곱함
    [SerializeField] private float eliteTimeMinInterval = 15f;
    [SerializeField] private int eliteKillBase = 25;                     // 필요 처치 = max(최소, 기본 - 30초당 감소 x 경과 - 라운드당 감소 x (r-1))
    [SerializeField] private int eliteKillReductionPer30s = 2;
    [SerializeField] private int eliteKillReductionPerRound = 1;
    [SerializeField] private int eliteKillMinEarly = 5;
    [SerializeField] private int eliteKillMinLate = 3;
    [SerializeField] private int eliteKillLateFromRound = 8;
    [SerializeField] private int eliteMaxCap = 4;                        // 엘리트 동시 최대 = min(상한, 1 + ⌊r / 라운드 간격⌋)
    [SerializeField] private int eliteMaxRoundStep = 3;

    [Header("Elite Status (Read Only)")]
    [SerializeField] private float stageTime;
    [SerializeField] private int eliteAlive;
    [SerializeField] private int pendingElites;      // 조건은 찼지만 동시 최대·유예 등으로 보류된 엘리트 수
    [SerializeField] private int killsSinceLastElite;
    private float nextTimeEliteAt;
    private float currentEliteTimeInterval;

    private int EliteMaxConcurrent => Mathf.Min(eliteMaxCap, 1 + currentRound / Mathf.Max(1, eliteMaxRoundStep));
    private int EliteKillsRequired
    {
        get
        {
            int min = currentRound >= eliteKillLateFromRound ? eliteKillMinLate : eliteKillMinEarly;
            int required = eliteKillBase - eliteKillReductionPer30s * Mathf.FloorToInt(stageTime / 30f) - eliteKillReductionPerRound * (currentRound - 1);
            return Mathf.Max(min, required);
        }
    }

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
            Debug.Log($"WaveManager: Final round {maxRound} reached (클리어 처리는 ExtractionZone → RunResultManager)");
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
        ResetEliteState();

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

    // 스테이지 시작 시 엘리트 카운터 초기화
    private void ResetEliteState()
    {
        stageTime = 0f;
        eliteAlive = 0;
        pendingElites = 0;
        killsSinceLastElite = 0;
        currentEliteTimeInterval = Mathf.Max(eliteTimeMinFirstInterval, eliteTimeBaseInterval - eliteTimeReductionPerRound * (currentRound - 1));
        nextTimeEliteAt = currentEliteTimeInterval;
    }

    // 엘리트 스폰 규칙: 시간 기반 + 처치 기반을 따로 세고, 유예·동시 최대·연출 중이면 보류 (GDD 8-6 #4, 8-4 #15)
    private void Update()
    {
        if (!isWaveActive || Time.timeScale <= 0f) return;

        stageTime += Time.deltaTime;

        // 시간 기반
        if (stageTime >= nextTimeEliteAt)
        {
            pendingElites++;
            currentEliteTimeInterval = Mathf.Max(eliteTimeMinInterval, currentEliteTimeInterval * eliteTimeAcceleration);
            nextTimeEliteAt += currentEliteTimeInterval;
        }

        // 처치 기반 (유예 중에 찬 조건은 유예가 끝난 뒤 등장)
        if (killsSinceLastElite >= EliteKillsRequired)
        {
            pendingElites++;
            killsSinceLastElite = 0;
        }

        if (pendingElites > 0 && stageTime >= eliteGracePeriod && eliteAlive < EliteMaxConcurrent && CanSpawnEliteNow())
        {
            pendingElites--;
            SpawnRandomEliteEnemy();
        }
    }

    // 탈출 성공 연출 중이거나 플레이어 사망 중이면 보류
    private JU.JUHealth cachedPlayerHealth;
    private ExtractionZone cachedExtractionZone;

    private bool CanSpawnEliteNow()
    {
        if (cachedPlayerHealth == null && playerTransform != null) cachedPlayerHealth = playerTransform.GetComponent<JU.JUHealth>();
        if (cachedPlayerHealth != null && cachedPlayerHealth.IsDead) return false;

        if (cachedExtractionZone == null) cachedExtractionZone = FindFirstObjectByType<ExtractionZone>();
        if (cachedExtractionZone != null && cachedExtractionZone.ProgressPercent >= 1f) return false;

        return true;
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

        // 3. 엘리트 적 생성 (Start 전에 엘리트로 지정 → 배율·외형 적용). 일반 적 수와 따로 셈
        GameObject elite = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        EnemyBase eliteBase = elite.GetComponent<EnemyBase>();
        if (eliteBase != null) eliteBase.MarkAsElite();
        eliteAlive++;

        Debug.Log($"WaveManager: ELITE spawned (alive {eliteAlive}/{EliteMaxConcurrent}, pending {pendingElites}, stage {stageTime:0}s)");
        if (StageHUDUI.Instance != null) StageHUDUI.Instance.ShowTimedNotice(EliteBannerText, new Color(0.75f, 0.4f, 1f), 2f);
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

    private const string EliteBannerText = "ELITE INCOMING";

    // 적 사망 시 호출 (엘리트는 일반 적 수와 따로 셈, 처치 기반 엘리트 카운터는 모든 처치를 셈)
    public void OnEnemyKilled(bool isElite = false)
    {
        if (!isWaveActive) return;

        currentKillsCount++;
        killsSinceLastElite++;

        if (isElite) eliteAlive = Mathf.Max(0, eliteAlive - 1);
        else currentEnemiesAlive = Mathf.Max(0, currentEnemiesAlive - 1);

        if (RunResultManager.Instance != null) RunResultManager.Instance.RecordKill(isElite);
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
        ResetEliteState();
        isWaveActive = true;

        StopAllCoroutines();
        StartCoroutine(SpawnEnemyRoutine()); // 무한 적 스폰 코루틴 재가동
        Debug.Log($"WaveManager: Restarted wave spawning. Round {currentRound}");
    }
}