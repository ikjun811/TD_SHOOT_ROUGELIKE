using System.IO;
using System.Text;
using JU;
using UnityEngine;

// 밸런스 기록 (B1, 2026-10-30) — GameplayBootstrap이 본 게임에서만 생성 (튜토리얼 제외), 게임 화면에는 변화 없음
//   층이 끝날 때마다(탈출 성공·사망) 한 줄, 판이 끝나면(씬을 떠날 때) 요약 한 줄을 파일에 추가
//   에디터: 프로젝트/Logs/BalanceLog_날짜시간.txt / 빌드: persistentDataPath/BalanceLogs/ (첫 기록 때 경로를 콘솔에 출력)
//   층 구분: 전투가 진행 중(timeScale > 0)이면 층 진행 → 탈출 연출 시작·플레이어 사망 = 층 끝 → 정비 등(timeScale 0)을 지나 다시 진행되면 다음 층
//   기록: 깊이·머문 시간(게임 시간)·결과 / 처치(일반·엘리트) / 받은 피해·최저 체력 / 드랍(모듈 레어도별·유니크·상자 무기)·층 끝 드랍률·천장 발동
//         그 시점 적 배율(시간 x 층) / 남은 목숨 / 빌드(장착 무기·그리드 모듈 수·사용 칸)
public class BalanceLogger : MonoBehaviour
{
    public static BalanceLogger Instance { get; private set; }

    private enum State { WaitingForCombat, InFloor, FloorEnded }
    private State state = State.WaitingForCombat;
    private bool passedPause; // 층이 끝난 뒤 정비(timeScale 0)를 지났는지

    private string filePath;
    private ExtractionZone extractionZone;
    private JUHealth playerHealth;
    private bool lastSequenceActive;

    // 이번 층
    private int floorRound;
    private float floorTime;
    private int kills, eliteKills;
    private float damageTaken;
    private float minHealthRatio;
    private readonly int[] moduleDrops = new int[4]; // 레어도별 (일반·희귀·엘리트·레전더리)
    private int uniqueDrops, weaponDrops;

    // 판 전체
    private int floorsLogged, deaths, totalKills, bestDepthCleared = -1;
    private float totalTime;
    private int maxRoundReached;

    public static void EnsureCreated()
    {
        if (Instance != null) return;
        new GameObject("BalanceLogger").AddComponent<BalanceLogger>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        EnemyBase.OnAnyEnemyDeath += OnEnemyDeath;
        IHealth.OnAnyDamaged += OnAnyDamaged;
        ItemDropManager.OnItemDropped += OnItemDropped;
    }

    private void OnDisable()
    {
        EnemyBase.OnAnyEnemyDeath -= OnEnemyDeath;
        IHealth.OnAnyDamaged -= OnAnyDamaged;
        ItemDropManager.OnItemDropped -= OnItemDropped;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        WriteSummary();
        Instance = null;
    }

    private void Update()
    {
        if (extractionZone == null) extractionZone = FindAnyObjectByType<ExtractionZone>();
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<JUHealth>();
        }

        bool sequenceActive = extractionZone != null && extractionZone.IsSequenceActive;
        bool combat = Time.timeScale > 0f && !sequenceActive && (playerHealth == null || !playerHealth.IsDead);

        switch (state)
        {
            case State.WaitingForCombat:
                if (combat) BeginFloor();
                break;

            case State.InFloor:
                floorTime += Time.deltaTime;
                if (playerHealth != null && playerHealth.MaxHealth > 0f)
                    minHealthRatio = Mathf.Min(minHealthRatio, playerHealth.Health / playerHealth.MaxHealth);
                if (sequenceActive && !lastSequenceActive) EndFloor("EXTRACTED");
                break;

            case State.FloorEnded:
                if (Time.timeScale <= 0f) passedPause = true;
                if (passedPause && combat) BeginFloor();
                break;
        }
        lastSequenceActive = sequenceActive;
    }

    private void BeginFloor()
    {
        state = State.InFloor;
        passedPause = false;
        floorRound = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        maxRoundReached = Mathf.Max(maxRoundReached, floorRound);
        floorTime = 0f;
        kills = eliteKills = 0;
        damageTaken = 0f;
        minHealthRatio = 1f;
        System.Array.Clear(moduleDrops, 0, moduleDrops.Length);
        uniqueDrops = weaponDrops = 0;
    }

    private void OnEnemyDeath(EnemyBase enemy)
    {
        if (state != State.InFloor || enemy == null) return;
        if (enemy.IsElite) eliteKills++;
        else kills++;
    }

    private void OnAnyDamaged(IHealth.DamageResultInfo result)
    {
        if (state != State.InFloor) return;
        Component target = result.DamagedObject as Component;
        if (target == null || !target.CompareTag("Player")) return;
        damageTaken += result.RealDamage;
        if (result.WasKilled)
        {
            minHealthRatio = 0f;
            EndFloor("DIED");
        }
    }

    private void OnItemDropped(WeaponDataSO weapon, ModuleDataSO module)
    {
        if (state != State.InFloor) return;
        if (weapon != null) weaponDrops++;
        if (module == null) return;
        if (module.category == ModuleCategory.Unique) uniqueDrops++;
        else moduleDrops[Mathf.Clamp((int)module.rarity, 0, moduleDrops.Length - 1)]++;
    }

    private void EndFloor(string result)
    {
        state = State.FloorEnded;
        passedPause = false;

        int depth = DepthDisplay.FromRound(floorRound);
        floorsLogged++;
        totalTime += floorTime;
        totalKills += kills + eliteKills;
        if (result == "DIED") deaths++;
        else bestDepthCleared = bestDepthCleared < 0 ? depth : Mathf.Min(bestDepthCleared, depth);

        StageDifficultyManager sdm = StageDifficultyManager.Instance;
        string dropInfo = sdm != null ? $"drop end {sdm.currentDropChance * 100f:0.#}%, pity {sdm.currentPityUsedCount}" : "drop -";
        string enemyInfo = sdm != null
            ? $"enemy HP x{sdm.GetEnemyHpMultiplier() * sdm.GetRoundHpMultiplier(floorRound):0.00} ATK x{sdm.GetEnemyDamageMultiplier() * sdm.GetRoundDamageMultiplier(floorRound):0.00} SPD x{sdm.GetEnemySpeedMultiplier():0.00}"
            : "enemy -";
        // 사망은 대기 연출 뒤 목숨이 깎이므로 이번 사망을 미리 반영
        int lives = sdm != null ? sdm.currentLives - (result == "DIED" ? 1 : 0) : -1;

        int moduleTotal = moduleDrops[0] + moduleDrops[1] + moduleDrops[2] + moduleDrops[3] + uniqueDrops;
        string line =
            $"[{DepthDisplay.Label(depth)} | R{floorRound:00}] {result,-9} {FormatTime(floorTime)}" +
            $" | kills {kills + eliteKills} (elite {eliteKills})" +
            $" | dmg taken {damageTaken:0}, min HP {minHealthRatio * 100f:0}%" +
            $" | modules {moduleTotal} (C{moduleDrops[0]} R{moduleDrops[1]} E{moduleDrops[2]} L{moduleDrops[3]} U{uniqueDrops}), weapons {weaponDrops}" +
            $" | {dropInfo} | {enemyInfo} | lives {lives}" +
            $" | build {BuildInfo()}";
        Append(line);
    }

    // 장착 무기 + 그리드 모듈 수·사용 칸 (층 시작 전 정비 결과)
    private static string BuildInfo()
    {
        StringBuilder sb = new StringBuilder();
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.equippedWeapons.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                WeaponDataSO w = inventory.equippedWeapons[i];
                sb.Append(w != null ? $"{w.weaponName}[{w.rarity}]" : "-");
            }
        }

        GridInventorySystem grid = GridInventorySystem.Instance;
        if (grid != null)
        {
            int cells = 0;
            foreach (GridInventorySystem.PlacedModule placed in grid.placedModules)
            {
                if (placed?.currentShape == null) continue;
                foreach (bool filled in placed.currentShape) if (filled) cells++;
            }
            sb.Append($", grid {grid.placedModules.Count} modules / {cells} of {grid.gridWidth * grid.gridHeight} cells");
        }
        return sb.ToString();
    }

    // 판 요약 (씬을 떠날 때 — 결과 화면·메인 화면·재시작·종료)
    private void WriteSummary()
    {
        if (floorsLogged == 0) return;
        string reached = maxRoundReached > 0 ? DepthDisplay.Label(DepthDisplay.FromRound(maxRoundReached)) : "-";
        string cleared = bestDepthCleared < 0 ? "-" : DepthDisplay.Label(bestDepthCleared - 1);
        Append($"== RUN END | floors logged {floorsLogged} | combat time {FormatTime(totalTime)} | reached {reached}, highest after extraction {cleared} | deaths {deaths} | kills {totalKills}");
    }

    private void Append(string line)
    {
        try
        {
            if (filePath == null)
            {
                string folder = Application.isEditor
                    ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs")
                    : Path.Combine(Application.persistentDataPath, "BalanceLogs");
                Directory.CreateDirectory(folder);
                filePath = Path.Combine(folder, $"BalanceLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
                BspArenaGenerator arena = FindAnyObjectByType<BspArenaGenerator>();
                File.AppendAllText(filePath, $"== RUN START {System.DateTime.Now:yyyy-MM-dd HH:mm} | first map seed {(arena != null ? arena.UsedSeed : 0)}\n", Encoding.UTF8);
                Debug.Log($"BalanceLogger: 기록 파일 {filePath}");
            }
            File.AppendAllText(filePath, line + "\n", Encoding.UTF8);
            Debug.Log("BalanceLogger: " + line);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"BalanceLogger: 기록 실패 — {e.Message}");
        }
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }
}
