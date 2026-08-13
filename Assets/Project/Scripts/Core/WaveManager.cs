using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMesh 사용을 위해 필수

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int currentRound = 1;
    [SerializeField] private int maxRound = 50;
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
    [SerializeField] private float spawnInterval = 2.0f;   // 스폰 간격
    [SerializeField] private int maxConcurrentEnemies = 20;// 필드 최대 동시 존재 적 수

    [Header("Current Wave Status (Read Only)")]
    [SerializeField] private int targetKillsThisRound;
    [SerializeField] private int currentKillsCount;
    [SerializeField] private int currentEnemiesAlive;
    [SerializeField] private bool isWaveActive = false;
    [SerializeField] private bool hasTriggered50PercentEvent = false;

    private Transform playerTransform;

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

    private IEnumerator SpawnEnemyRoutine()
    {
        while (isWaveActive)
        {
            int totalSpawned = currentKillsCount + currentEnemiesAlive;
            if (currentEnemiesAlive < maxConcurrentEnemies && totalSpawned < targetKillsThisRound)
            {
                SpawnRandomNormalEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
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
    private Vector3 GetRandomOffscreenSpawnPosition()
    {
        if (playerTransform == null) return transform.position;

        // 플레이어 기준 360도 무작위 방향
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);

        Vector3 spawnOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * randomDistance;
        Vector3 targetSpawnPos = playerTransform.position + spawnOffset;

        // NavMesh 바닥 유효 좌표로 자동 보정
        if (NavMesh.SamplePosition(targetSpawnPos, out NavMeshHit hit, 8.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return targetSpawnPos; // 보정 실패 시 원본 좌표
    }

    // 적 사망 시 호출
    public void OnEnemyKilled()
    {
        if (!isWaveActive) return;

        currentKillsCount++;
        currentEnemiesAlive = Mathf.Max(0, currentEnemiesAlive - 1);

        Debug.Log($"[Wave Progress] {currentKillsCount} / {targetKillsThisRound} 처치 완료");

        // 50% 진행도 트리거 (엘리트 스폰)
        float progress = (float)currentKillsCount / targetKillsThisRound;
        if (progress >= 0.5f && !hasTriggered50PercentEvent)
        {
            hasTriggered50PercentEvent = true;
            On50PercentProgressTriggered();
        }

        // 라운드 클리어
        if (currentKillsCount >= targetKillsThisRound)
        {
            CompleteRound();
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
        StopAllCoroutines();

        Debug.Log($"🎉 [ROUND {currentRound} CLEAR!] 라운드를 클리어했습니다.");

        //  정비 단계 진입 호출
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
    }
}