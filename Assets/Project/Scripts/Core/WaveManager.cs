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

    [Header("Enemy Types (GDD 8-6 #18, 임시값)")]
    [Tooltip("종류별 등장 라운드·가중치. 가중치 = min(최대, 시작 + 라운드당 증가 x (r - 등장 라운드)). 같은 종류 프리팹이 여러 개면 나눠 가짐, 프리팹이 없는 종류는 제외")]
    [SerializeField] private List<EnemyTypeWeight> enemyTypeWeights = new List<EnemyTypeWeight>
    {
        new EnemyTypeWeight { type = EnemyType.Runner,  unlockRound = 1,  startWeight = 10f,  weightPerRound = 0f,   maxWeight = 10f },
        new EnemyTypeWeight { type = EnemyType.Shooter, unlockRound = 3,  startWeight = 2f,   weightPerRound = 0.3f, maxWeight = 4f },
        new EnemyTypeWeight { type = EnemyType.Brute,   unlockRound = 5,  startWeight = 1.5f, weightPerRound = 0.3f, maxWeight = 3.5f },
        new EnemyTypeWeight { type = EnemyType.Bomber,  unlockRound = 7,  startWeight = 1.5f, weightPerRound = 0.3f, maxWeight = 3f },
        new EnemyTypeWeight { type = EnemyType.Charger, unlockRound = 10, startWeight = 1.5f, weightPerRound = 0.2f, maxWeight = 2.5f },
    };
    [Tooltip("엘리트도 일반 적 목록에서 종류 가중치로 고름 (브루트 엘리트 등). 일반 목록이 비면 엘리트 목록 사용")]
    [SerializeField] private bool elitesUseTypeWeights = true;

    [System.Serializable]
    public class EnemyTypeWeight
    {
        public EnemyType type;
        public int unlockRound = 1;
        public float startWeight = 1f;
        public float weightPerRound = 0f;
        public float maxWeight = 1f;

        public float GetWeight(int round)
        {
            if (round < unlockRound) return 0f;
            return Mathf.Min(maxWeight, startWeight + weightPerRound * (round - unlockRound));
        }
    }

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
    [Tooltip("엘리트 등장 예고 시간 (바닥 보라색 원, D1)")]
    [SerializeField] private float eliteTelegraphDuration = 1.5f;
    [SerializeField] private float eliteTelegraphRadius = 1.5f;

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

    // 튜토리얼(TutorialDirector)용: 끄면 일반 적 자동 스폰·엘리트 등장을 멈추고 SpawnEnemy로만 생성
    public bool AutoSpawnEnabled { get; set; } = true;
    // 끄면 라운드 시작 경고 띠(DEPTH 표시)를 띄우지 않음 (튜토리얼은 자체 배너)
    public bool ShowRoundBanner { get; set; } = true;

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
        if (ShowRoundBanner) AlertBanner.ShowRound(currentRound, maxRound); // 라운드 시작 경고 띠 (D2)
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
        if (!isWaveActive || !AutoSpawnEnabled || Time.timeScale <= 0f) return;

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

        if (cachedExtractionZone == null) cachedExtractionZone = FindAnyObjectByType<ExtractionZone>();
        if (cachedExtractionZone != null && cachedExtractionZone.ProgressPercent >= 1f) return false;

        return true;
    }

    // 적 무한 스폰 루틴 (10마리 누적 제한 삭제)
    private IEnumerator SpawnEnemyRoutine()
    {
        while (isWaveActive)
        {
            // 필드에 동시 존재하는 적 수가 최대 동시 스폰 제한 수보다 적으면 영구 지속 스폰
            if (AutoSpawnEnabled && currentEnemiesAlive < MaxConcurrentEnemies)
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

        // 1. 라운드별 종류 가중치로 선택
        GameObject selectedPrefab = PickEnemyPrefab(normalEnemyPrefabs);
        if (selectedPrefab == null) return;

        // 2. 화면 밖 무작위 위치 계산 (NavMesh 위 위치를 못 찾으면 이번 스폰은 건너뜀, 다음 간격에 재시도)
        if (!TryGetOffscreenSpawnPosition(out Vector3 spawnPosition)) return;

        // 3. 적 생성
        Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        currentEnemiesAlive++;
    }

    // 지정 종류 적 1마리를 지정 위치(NavMesh 보정)에 생성 — 튜토리얼 등 스크립트 진행용. 실패하면 null
    public EnemyBase SpawnEnemy(EnemyType type, Vector3 position)
    {
        GameObject prefab = normalEnemyPrefabs.Find(p => p != null && GetPrefabType(p) == type);
        if (prefab == null)
        {
            Debug.LogWarning($"WaveManager: {type} 프리팹이 일반 적 목록에 없습니다.");
            return null;
        }
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return null;

        GameObject enemy = Instantiate(prefab, hit.position, Quaternion.identity);
        currentEnemiesAlive++;
        return enemy.GetComponent<EnemyBase>();
    }

    // 지정 종류 적 1마리를 화면 밖 무작위 위치에 생성 (일반 스폰과 같은 위치 규칙). 실패하면 null
    public EnemyBase SpawnEnemyOffscreen(EnemyType type)
    {
        if (!TryGetOffscreenSpawnPosition(out Vector3 position)) return null;
        return SpawnEnemy(type, position);
    }

    // 엘리트 적 무작위 스폰
    private void SpawnRandomEliteEnemy()
    {
        // 1. 일반 적 목록에서 종류 가중치로 선택 (일반 목록이 비었거나 옵션을 끄면 엘리트 목록에서 무작위)
        GameObject selectedPrefab = null;
        if (elitesUseTypeWeights) selectedPrefab = PickEnemyPrefab(normalEnemyPrefabs);
        if (selectedPrefab == null && eliteEnemyPrefabs != null && eliteEnemyPrefabs.Count > 0)
        {
            selectedPrefab = eliteEnemyPrefabs[Random.Range(0, eliteEnemyPrefabs.Count)];
        }
        if (selectedPrefab == null)
        {
            Debug.LogWarning("WaveManager: 엘리트로 스폰할 적 프리팹이 없습니다!");
            return;
        }

        // 2. 화면 밖 무작위 위치 계산 (못 찾으면 보류 수를 되돌려 다음 프레임에 재시도)
        if (!TryGetOffscreenSpawnPosition(out Vector3 spawnPosition))
        {
            pendingElites++;
            return;
        }

        // 3. 등장 예고 (D1): 자리를 먼저 차지(동시 최대 계산에 포함)하고 예고가 끝나면 생성. 일반 적 수와 따로 셈
        //    라운드가 끝나면 EliteSpawnTelegraph.ClearAll로 예고째 취소 (eliteAlive는 다음 라운드 ResetEliteState에서 초기화)
        eliteAlive++;
        EliteSpawnTelegraph.Create(spawnPosition, eliteTelegraphDuration, eliteTelegraphRadius, position =>
        {
            if (this == null || !isWaveActive) return;
            // 엘리트 생성 (Start 전에 엘리트로 지정 → 배율·외형 적용)
            GameObject elite = Instantiate(selectedPrefab, position, Quaternion.identity);
            EnemyBase eliteBase = elite.GetComponent<EnemyBase>();
            if (eliteBase != null) eliteBase.MarkAsElite();
            // 경고는 실제로 생성된 뒤에 (사용자: 위치가 확정된 엘리트만 알림 — 화살표도 살아 있는 엘리트만 가리킴)
            AlertBanner.ShowElite();
        });

        Debug.Log($"WaveManager: ELITE incoming (alive {eliteAlive}/{EliteMaxConcurrent}, pending {pendingElites}, stage {stageTime:0}s)");
    }

    // 현재 라운드의 종류 가중치로 프리팹 선택 (프리팹 가중치 = 종류 가중치 / 그 종류 프리팹 수)
    // EnemyBase가 없는 프리팹은 러너로 취급. 이번 라운드에 나올 종류가 하나도 없으면 목록에서 무작위
    private readonly Dictionary<EnemyType, int> typeCountBuffer = new Dictionary<EnemyType, int>();
    private readonly List<float> prefabWeightBuffer = new List<float>();

    private GameObject PickEnemyPrefab(List<GameObject> prefabs)
    {
        if (prefabs == null || prefabs.Count == 0) return null;

        typeCountBuffer.Clear();
        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            EnemyType type = GetPrefabType(prefab);
            typeCountBuffer.TryGetValue(type, out int count);
            typeCountBuffer[type] = count + 1;
        }

        prefabWeightBuffer.Clear();
        float total = 0f;
        foreach (GameObject prefab in prefabs)
        {
            float weight = 0f;
            if (prefab != null)
            {
                EnemyType type = GetPrefabType(prefab);
                weight = GetTypeWeight(type, currentRound) / typeCountBuffer[type];
            }
            prefabWeightBuffer.Add(weight);
            total += weight;
        }

        if (total <= 0f)
        {
            GameObject fallback = prefabs[Random.Range(0, prefabs.Count)];
            return fallback;
        }

        float roll = Random.Range(0f, total);
        for (int i = 0; i < prefabs.Count; i++)
        {
            roll -= prefabWeightBuffer[i];
            if (roll < 0f && prefabWeightBuffer[i] > 0f) return prefabs[i];
        }
        for (int i = prefabs.Count - 1; i >= 0; i--)
        {
            if (prefabWeightBuffer[i] > 0f) return prefabs[i];
        }
        return null;
    }

    private static EnemyType GetPrefabType(GameObject prefab)
    {
        EnemyBase enemy = prefab.GetComponent<EnemyBase>();
        return enemy != null ? enemy.Type : EnemyType.Runner;
    }

    private float GetTypeWeight(EnemyType type, int round)
    {
        foreach (EnemyTypeWeight entry in enemyTypeWeights)
        {
            if (entry != null && entry.type == type) return entry.GetWeight(round);
        }
        return 0f;
    }

    // 화면 밖 무작위 좌표 계산 함수 (NavMesh 보정 포함)
    // 카메라 화면 안에 들어오는 위치는 다시 뽑음 (카메라 각도·크기·룩어헤드가 바뀌어도 자동 대응)
    // NavMesh 보정에 실패한 위치(맵 밖 등)는 버림. 모든 시도가 화면 안이면 NavMesh 위였던 마지막 위치 사용, 그것도 없으면 false
    private bool TryGetOffscreenSpawnPosition(out Vector3 position)
    {
        position = transform.position;
        if (playerTransform == null) return false;

        Camera cam = Camera.main;
        bool hasOnMeshFallback = false;
        if (arena == null) arena = FindAnyObjectByType<BspArenaGenerator>();
        if (spawnPath == null) spawnPath = new NavMeshPath();

        // 경로 검사 기준: 플레이어 발밑 NavMesh 지점 (플레이어는 베이크 제외라 위치 보정)
        bool hasPlayerNav = NavMesh.SamplePosition(playerTransform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas);
        int rejectedOutside = 0;
        int rejectedUnreachable = 0;

        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            // 플레이어 기준 360도 무작위 방향
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);

            Vector3 spawnOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * randomDistance;
            Vector3 candidate = playerTransform.position + spawnOffset;

            // NavMesh 바닥 유효 좌표로 자동 보정, 실패하면(맵 밖) 다시 뽑음
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8.0f, NavMesh.AllAreas)) continue;
            candidate = hit.position;

            // 외곽 벽 밖 NavMesh 조각 제외
            if (arena != null && !arena.IsInsideArena(candidate))
            {
                rejectedOutside++;
                continue;
            }

            // 플레이어까지 걸어갈 수 없는 끊긴 NavMesh 조각(장애물 틈·막힌 구석) 제외
            if (hasPlayerNav && (!NavMesh.CalculatePath(candidate, playerHit.position, NavMesh.AllAreas, spawnPath)
                                 || spawnPath.status != NavMeshPathStatus.PathComplete))
            {
                rejectedUnreachable++;
                continue;
            }

            position = candidate;
            hasOnMeshFallback = true;
            if (cam == null || !IsVisibleOnScreen(cam, candidate))
            {
                LogRejectedSpawns(rejectedOutside, rejectedUnreachable);
                return true;
            }
        }

        LogRejectedSpawns(rejectedOutside, rejectedUnreachable);
        return hasOnMeshFallback; // 모든 시도가 화면 안이면 NavMesh 위였던 마지막 좌표 사용
    }

    private BspArenaGenerator arena;
    private NavMeshPath spawnPath;

    // 확인용: 버린 스폰 후보가 있으면 원인 수 기록 (원인 확인 후 끌 것)
    [SerializeField] private bool logRejectedSpawns = true;

    private void LogRejectedSpawns(int outside, int unreachable)
    {
        if (!logRejectedSpawns || outside + unreachable == 0) return;
        Debug.Log($"WaveManager: 스폰 후보 제외 — 맵 밖 {outside}, 경로 없음 {unreachable}");
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

    // 처치가 아닌 제거 (봄버 자폭) — 필드 적 수만 줄임
    public void OnEnemyRemoved(bool isElite)
    {
        if (isElite) eliteAlive = Mathf.Max(0, eliteAlive - 1);
        else currentEnemiesAlive = Mathf.Max(0, currentEnemiesAlive - 1);
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
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
        EnemyProjectile.ClearAll(); // 날아가는 적 탄도 함께 (탈출·사망 연출 중 피격 방지)
        EliteSpawnTelegraph.ClearAll(); // 엘리트 등장 예고 취소
        Debug.Log($"WaveManager: Despawned {enemies.Length} active enemies.");
    }

    // 라운드 종료(탈출 성공·사망): 스폰을 멈추고 필드 적 제거 (적만 지우면 연출 중에 스폰 루틴이 새 적을 계속 만들던 문제)
    // 다음 라운드 시작 시 RestartWaveSpawning이 다시 켬
    public static void EndRoundAndDespawn()
    {
        if (Instance != null)
        {
            Instance.isWaveActive = false;
            Instance.pendingElites = 0;
            Instance.StopAllCoroutines(); // 스폰 루틴 정지
        }
        DespawnAllActiveEnemies();
    }

    // ⭐ 다음 라운드 진입 시 적 스폰 루틴 재가동 함수
    public void RestartWaveSpawning()
    {
        DespawnAllActiveEnemies(); // 잔여 적 제거
        RoundCleanup.ClearAll();   // 지난 라운드 수류탄·폭발 이펙트 (탈출 직전 것이 새 맵에 보이던 문제)
        currentKillsCount = 0;
        currentEnemiesAlive = 0;
        ResetEliteState();
        isWaveActive = true;

        StopAllCoroutines();
        StartCoroutine(SpawnEnemyRoutine()); // 무한 적 스폰 코루틴 재가동
        if (ShowRoundBanner) AlertBanner.ShowRound(currentRound, maxRound); // 라운드 시작 경고 띠 (D2, 정비 중 호출되면 전투 재개 후 표시)
        Debug.Log($"WaveManager: Restarted wave spawning. Round {currentRound}");
    }
}