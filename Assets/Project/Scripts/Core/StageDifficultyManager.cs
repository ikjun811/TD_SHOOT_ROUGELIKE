using UnityEngine;

// 스테이지 실시간 난이도 상승, 드랍률 감쇄, 목숨 관리 매니저 클래스
public class StageDifficultyManager : MonoBehaviour
{
    public static StageDifficultyManager Instance { get; private set; }

    [Header("Player Lives")]
    public int maxLives = 3;
    public int currentLives = 3;

    [Header("Drop Probability Settings")]
    public float baseDropChance = 0.30f;      // 시작 기본 드랍률 (30%, GDD 8-4 #15)
    public float dropChanceReduction = 0.05f; // 획득당 감소율 (-5%)
    public float minDropChance = 0.005f;      // 최소 보장 드랍률 (0.5%) 
    public float currentDropChance;

    [Header("Limited Early Protection Pity")]
    public int maxEarlyPityCount = 2; // 스테이지당 천장 발동 최대 2회
    public int pityMissThreshold = 3; // 연속 미드랍 3회 후 다음 처치 확정 드랍
    public int currentPityUsedCount = 0; // 천장으로 확정 드랍된 횟수
    public int consecutiveMissCount = 0; // 현재 연속 미드랍 횟수

    [Header("Endless Difficulty Scaling")]
    public float elapsedTime = 0f;
    public float hpScalePerMinute = 0.25f;     // 분당 적 HP +25% (무한)
    public float damageScalePerMinute = 0.20f; // 분당 적 공격력 +20% (무한)
    public float speedScalePerMinute = 0.05f;  // 분당 적 이동속도 +5%
    public float maxSpeedScaleCap = 0.30f;     // 이동속도 최대 상승 상한선 (+30%)

    [Header("Round Scaling (GDD 8-4 #15)")]
    public float hpGrowthPerRound = 1.17f;      // 적 체력 x1.17^(라운드-1) (15라운드 약 9배)
    public float damageIncreasePerRound = 0.08f; // 적 공격력 x(1 + 0.08 x (라운드-1)) (15라운드 약 2.1배)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        currentLives = maxLives;
    }

    private void Start()
    {
        ResetStageDifficulty();
    }

    // 스테이지 재진입 시 난이도 및 드랍률 초기화
    public void ResetStageDifficulty()
    {
        elapsedTime = 0f;
        currentDropChance = baseDropChance;
        currentPityUsedCount = 0;
        consecutiveMissCount = 0;
    }

    private void Update()
    {
        if (Time.timeScale > 0f)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    // 아이템 획득 시 드랍률 감소 연산 (최소 0.5% 보장)
    public void RegisterItemObtained()
    {
        currentDropChance = Mathf.Max(minDropChance, currentDropChance - dropChanceReduction);
        Debug.Log($"StageDifficultyManager: Item obtained. Current drop chance: {currentDropChance * 100f}%.");
    }

    // 현재 경과 시간에 따른 적 HP 배율 연산
    public float GetEnemyHpMultiplier()
    {
        float minutes = elapsedTime / 60f;
        return 1f + (minutes * hpScalePerMinute);
    }

    // 현재 경과 시간에 따른 적 공격력 배율 연산
    public float GetEnemyDamageMultiplier()
    {
        float minutes = elapsedTime / 60f;
        return 1f + (minutes * damageScalePerMinute);
    }

    // 라운드에 따른 적 체력 배율 (복리)
    public float GetRoundHpMultiplier(int round)
    {
        return Mathf.Pow(hpGrowthPerRound, Mathf.Max(0, round - 1));
    }

    // 라운드에 따른 적 공격력 배율 (선형)
    public float GetRoundDamageMultiplier(int round)
    {
        return 1f + damageIncreasePerRound * Mathf.Max(0, round - 1);
    }

    // 현재 경과 시간에 따른 적 이동속도 배율 연산 (최대 +30% 상한선)
    public float GetEnemySpeedMultiplier()
    {
        float minutes = elapsedTime / 60f;
        float rawScale = minutes * speedScalePerMinute;
        float cappedScale = Mathf.Min(maxSpeedScaleCap, rawScale);
        return 1f + cappedScale;
    }

    // 플레이어 사망 시 목숨 차감 처리
    public bool DeductLifeOnPlayerDeath()
    {
        currentLives--;
        Debug.Log($"StageDifficultyManager: Player died. Remaining lives: {currentLives} / {maxLives}.");

        if (currentLives <= 0)
        {
            Debug.Log("StageDifficultyManager: Game Over. All lives depleted.");
            return true; // 최종 게임 오버
        }

        return false; // 목숨 남아있음
    }
}