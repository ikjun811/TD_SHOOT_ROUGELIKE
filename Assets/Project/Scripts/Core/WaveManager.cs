using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int currentRound = 1;
    [SerializeField] private int maxRound = 50;
    [SerializeField] private int baseKillsPerRound = 10; // 1라운드 목표 처치 수
    [SerializeField] private int extraKillsPerRound = 5; // 라운드당 추가 처치 수

    [Header("Spawning Settings")]
    [SerializeField] private GameObject normalEnemyPrefab;
    [SerializeField] private GameObject eliteEnemyPrefab; // 엘리트 적 프리팹 (선택)
    [SerializeField] private Transform[] spawnPoints;     // 적 스폰 위치들
    [SerializeField] private float spawnInterval = 2.0f;  // 스폰 간격
    [SerializeField] private int maxConcurrentEnemies = 15; // 필드 최대 동시 존재 적 수

    [Header("Current Wave Status (Read Only)")]
    [SerializeField] private int targetKillsThisRound;
    [SerializeField] private int currentKillsCount;
    [SerializeField] private int currentEnemiesAlive;
    [SerializeField] private bool isWaveActive = false;
    [SerializeField] private bool hasTriggered50PercentEvent = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 첫 번째 라운드 시작
        StartRound(currentRound);
    }

    public void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        currentKillsCount = 0;
        currentEnemiesAlive = 0;
        hasTriggered50PercentEvent = false;

        // 라운드별 목표 처치 수 계산 (예: 1R=10마리, 2R=15마리, 3R=20마리...)
        targetKillsThisRound = baseKillsPerRound + (currentRound - 1) * extraKillsPerRound;
        isWaveActive = true;

        Debug.Log($"=============== [ROUND {currentRound} START] ===============");
        Debug.Log($"목표 처치 수: {targetKillsThisRound} 마리");

        // 10라운드 단위 메이저 라운드 체크
        if (currentRound % 10 == 0)
        {
            Debug.Log($"⚠️ [메이저 라운드!] 10단위 라운드입니다. 강력한 보스가 스폰됩니다!");
        }

        // 스폰 루틴 시작
        StartCoroutine(SpawnEnemyRoutine());
    }

    private IEnumerator SpawnEnemyRoutine()
    {
        while (isWaveActive)
        {
            // 필드에 스폰된 적 수가 최대 동시 수보다 적고, 아직 총 스폰 목표가 남았을 때
            int totalSpawned = currentKillsCount + currentEnemiesAlive;
            if (currentEnemiesAlive < maxConcurrentEnemies && totalSpawned < targetKillsThisRound)
            {
                SpawnNormalEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnNormalEnemy()
    {
        if (spawnPoints.Length == 0 || normalEnemyPrefab == null) return;

        // 무작위 스폰 포인트 선택
        Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Instantiate(normalEnemyPrefab, randomSpawnPoint.position, randomSpawnPoint.rotation);

        currentEnemiesAlive++;
    }

    // 적이 사망했을 때 EnemyBase에서 호출
    public void OnEnemyKilled()
    {
        if (!isWaveActive) return;

        currentKillsCount++;
        currentEnemiesAlive = Mathf.Max(0, currentEnemiesAlive - 1);

        Debug.Log($"[Wave Progress] {currentKillsCount} / {targetKillsThisRound} 처치 완료");

        // 50% 진행도 트리거 (엘리트 스폰 등)
        float progress = (float)currentKillsCount / targetKillsThisRound;
        if (progress >= 0.5f && !hasTriggered50PercentEvent)
        {
            hasTriggered50PercentEvent = true;
            On50PercentProgressTriggered();
        }

        // 라운드 클리어 조건 확인
        if (currentKillsCount >= targetKillsThisRound)
        {
            CompleteRound();
        }
    }

    private void On50PercentProgressTriggered()
    {
        Debug.Log("⚠️ [경고!] 웨이브 50% 달성! 엘리트 적이 출현합니다!");
        if (eliteEnemyPrefab != null && spawnPoints.Length > 0)
        {
            Transform randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(eliteEnemyPrefab, randomSpawn.position, randomSpawn.rotation);
            currentEnemiesAlive++;
        }
    }

    private void CompleteRound()
    {
        isWaveActive = false;
        StopAllCoroutines();

        Debug.Log($"🎉 [ROUND {currentRound} CLEAR!] 라운드를 클리어했습니다.");
        Debug.Log("--> 정비 단계(Maintenance Stage)로 이동합니다. (미사용 장비 소멸 로직 대기)");

        // TODO: 정비 단계 UI 열기 및 정비 완료 시 다음 라운드(StartRound(currentRound + 1)) 진행
    }
}