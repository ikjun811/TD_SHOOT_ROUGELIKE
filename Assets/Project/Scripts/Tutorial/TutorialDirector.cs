using System.Collections;
using System.Collections.Generic;
using JU;
using JUTPS;
using UnityEngine;

// 튜토리얼 진행 (GDD 9·10장 — GameplayScene 재사용 + 튜토리얼 모드, 진행 로직은 이 한 곳에)
//   GameplayBootstrap이 GameSession.IsTutorial일 때 생성 (씬 오브젝트 Awake 뒤·Start 전이라 매니저 Start보다 먼저 잠금)
//   기존 매니저에는 튜토리얼 분기 없이 공개 설정값만 사용:
//     WaveManager 자동 스폰·엘리트·라운드 배너 끔 / SupplyCrateManager 자동 상자 끔 / ItemDropManager 무작위 드랍 끔·상자 무기 고정
//     ExtractionZone 숨김 / GrenadeSkill G키 잠금 / 맵 = 고정 시드(GameSession.ArenaSeedOverride)
//   사망 없음: 체력이 1 밑으로 내려가지 않고, 안 맞으면 잠시 뒤 전부 회복 (위기 화면이 계속 남지 않게)
//   단계 진행은 Start 코루틴 한 줄기 (안내 = TutorialUI 목표 카드·정지 팝업·알림)
//   구현: T1 기반·잠금, T2 시작 팝업 + A 기본 조작, T3 B 모듈 드랍 ~ F 탈출. 정비(T4)·2라운드(T5)는 이어서
public class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    // 고정 맵 시드 (GameSession.StartGame이 씬 로드 전에 넘김) — 맵이 마음에 안 들면 이 값만 바꾸면 됨
    public const int ArenaSeed = 20261030;

    // 체험 보장용 고정 아이템 (ItemDropManager에 등록된 에셋 이름)
    public const string FixedModuleAsset = "Module_Special_MultiThrow_Rare"; // 다중 투척 — 수류탄으로 바로 체감
    public const string FixedWeaponAsset = "Benelli M4";                     // 시작 무기 UMP와 확실히 다른 산탄총
    public const float ExtractionTime = 10f;

    [Header("사망 없음")]
    [Tooltip("이 시간(초) 동안 안 맞으면 회복 시작")]
    public float regenDelay = 3f;
    [Tooltip("최대 체력을 이 시간(초)에 걸쳐 회복")]
    public float regenFullTime = 1.5f;

    public ModuleDataSO FixedModule { get; private set; }
    public WeaponDataSO FixedWeapon { get; private set; }

    private JUHealth playerHealth;

    public static TutorialDirector EnsureCreated()
    {
        if (Instance != null) return Instance;
        return new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ApplyLocks();
        SetupPlayerHealth();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (playerHealth != null) playerHealth.UnsubscribeOnBeforeDamaged(KeepAlive);
        Instance = null;
    }

    // 뒤 단계 요소를 먼저 경험하지 않도록 전부 잠금 (단계가 열릴 때 하나씩 해제)
    private void ApplyLocks()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.AutoSpawnEnabled = false;
            WaveManager.Instance.ShowRoundBanner = false;
        }

        // SupplyCrateManager는 Bootstrap이 이 오브젝트보다 먼저 만들지만 상자 생성은 Start 다음 프레임
        if (SupplyCrateManager.Instance != null) SupplyCrateManager.Instance.AutoSpawnEnabled = false;

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.RandomDropsEnabled = false;
            FixedModule = ItemDropManager.Instance.FindModule(FixedModuleAsset);
            FixedWeapon = ItemDropManager.Instance.FindWeapon(FixedWeaponAsset);
            ItemDropManager.Instance.ForcedCrateWeapon = FixedWeapon;
        }
        if (FixedModule == null) Debug.LogWarning($"TutorialDirector: 고정 모듈 '{FixedModuleAsset}'이 ItemDropManager 목록에 없습니다.");
        if (FixedWeapon == null) Debug.LogWarning($"TutorialDirector: 고정 무기 '{FixedWeaponAsset}'이 ItemDropManager 목록에 없습니다.");

        ExtractionZone zone = FindAnyObjectByType<ExtractionZone>();
        if (zone != null)
        {
            zone.SetHidden(true);
            zone.RequiredTimeOverride = ExtractionTime;
        }

        GrenadeSkill grenade = FindAnyObjectByType<GrenadeSkill>();
        if (grenade != null) grenade.InputLocked = true;

        // HUD: 깊이 대신 TRAINING, 15층 눈금·목숨(쓰러지지 않음)·드랍률(고정 드랍) 숨김. 탈출 막대는 구역이 공개되면 자동으로 보임
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.DepthLabelOverride = "TRAINING";
            StageHUDUI.Instance.ShowRoundTicks = false;
            StageHUDUI.Instance.ShowLivesAndDrop = false;
        }

        Debug.Log($"TutorialDirector: 잠금 적용 (seed {ArenaSeed}, module {(FixedModule != null ? FixedModule.name : "없음")}, weapon {(FixedWeapon != null ? FixedWeapon.name : "없음")})");
    }

    private void SetupPlayerHealth()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;
        playerHealth = playerObj.GetComponent<JUHealth>();
        if (playerHealth == null) return;

        playerHealth.SubscribeOnBeforeDamaged(KeepAlive);
        playerHealth.HealthRegeneration = new JUHealth.HealthRegenerationSettings
        {
            Delay = regenDelay,
            Speed = playerHealth.MaxHealth / Mathf.Max(0.1f, regenFullTime),
        };
    }

    // 체력이 1 밑으로 내려가는 피해는 깎아서 1에 멈춤 (JU 비수정 — 피해 직전 이벤트)
    private IHealth.DamageInfo KeepAlive(IHealth.DamageInfo info)
    {
        if (playerHealth != null) info.Damage = Mathf.Min(info.Damage, Mathf.Max(0f, playerHealth.Health - 1f));
        return info;
    }

    #region 단계 진행

    private const string Accent = "#5FD8F2"; // 안내 글 강조 (TutorialUI 청록)
    private const string Gold = "#FFD24A";   // 중요한 규칙 강조

    private TutorialUI ui;
    private JUCharacterController controller;
    private bool popupDone;

    private IEnumerator Start()
    {
        yield return null; // WaveManager.Start(플레이어 찾기) 이후
        ui = TutorialUI.EnsureCreated();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) controller = playerObj.GetComponent<JUCharacterController>();

        yield return Popup("TRAINING FLOOR",
            $"괴물이 침입한 지하 시설의 가장 아래층, <color={Accent}>DEPTH 16 훈련 구역</color>입니다.\n" +
            "지상까지 올라가기 전에 살아남는 데 필요한 기본기를 익히세요.\n\n" +
            "<size=22><alpha=#AA>훈련 중에는 쓰러지지 않습니다. 언제든 ESC로 나갈 수 있습니다.</size>",
            "훈련 시작");
        AlertBanner.ShowCustom("TRAINING FLOOR", "DEPTH 16  —  BASIC TRAINING", false);

        yield return StepBasics();
        yield return StepModuleDrop();
        yield return StepGrenade();
        yield return StepSupplyCrate();
        yield return StepExtraction();
        yield return StepMaintenance();

        // 2라운드: 정비 결과를 직접 써 봄
        AlertBanner.ShowCustom("TRAINING FLOOR 2", "TEST YOUR NEW LOADOUT", false);
        if (StageHUDUI.Instance != null) StageHUDUI.Instance.DepthLabelOverride = "TRAINING 2";
        yield return new WaitForSeconds(1.5f);
        yield return StepMultiThrow();
        yield return StepWeaponSwap();
        yield return StepComplete();
    }

    // G 다중 투척 체험: 배치한 모듈로 수류탄이 여러 개 → 큰 무리 처치
    private IEnumerator StepMultiThrow()
    {
        GrenadeSkill grenade = FindAnyObjectByType<GrenadeSkill>();
        if (grenade != null) grenade.RefillCharges();

        ui.ShowObjective("STEP 06", "모듈 효과", "그리드에 배치한 다중 투척이 적용되었습니다. 수류탄이 한 번에 여러 개 날아갑니다.",
            new TutorialUI.ObjectiveItem("G", "수류탄 던지기"),
            new TutorialUI.ObjectiveItem("", "무리 처치"));

        List<EnemyBase> pack = SpawnOffscreen(EnemyType.Runner, 9, true);
        int lastCharges = grenade != null ? grenade.CurrentCharges : 0;
        while (true)
        {
            if (grenade != null)
            {
                if (grenade.CurrentCharges < lastCharges) ui.SetItemDone(0);
                lastCharges = grenade.CurrentCharges;
            }
            int killed = 0;
            foreach (EnemyBase enemy in pack) if (enemy == null || enemy.IsDead) killed++;
            if (killed >= pack.Count) ui.SetItemDone(1);
            else ui.SetItemProgress(1, $"{killed} / {pack.Count}");
            if (ui.AllItemsDone) break;
            yield return null;
        }
        yield return CompleteStep();
    }

    // H 무기 교체: E키로 바꿔 들며 장착한 두 무기로 각각 처치 (처치 순간 들고 있던 무기 기준)
    private IEnumerator StepWeaponSwap()
    {
        const int killsEach = 3;
        WeaponDataSO[] equipped = PlayerInventory.Instance != null ? PlayerInventory.Instance.equippedWeapons : new WeaponDataSO[2];
        string nameA = equipped.Length > 0 && equipped[0] != null ? equipped[0].weaponName : "첫 번째 무기";
        string nameB = equipped.Length > 1 && equipped[1] != null ? equipped[1].weaponName : "두 번째 무기";

        ui.ShowObjective("STEP 07", "무기 교체", "E키로 무기를 바꿔 들 수 있습니다. 두 무기로 각각 적을 처치하세요.",
            new TutorialUI.ObjectiveItem("E", "무기 교체"),
            new TutorialUI.ObjectiveItem("", $"{nameA}로 처치"),
            new TutorialUI.ObjectiveItem("", $"{nameB}로 처치"));

        int[] kills = new int[2];
        System.Action<EnemyBase> onDeath = _ =>
        {
            int slot = HeldWeaponSlot(equipped);
            if (slot >= 0) kills[slot]++;
        };
        EnemyBase.OnAnyEnemyDeath += onDeath;

        Object startWeapon = controller != null ? controller.RightHandWeapon : null;
        List<EnemyBase> wave = new List<EnemyBase>();
        float nextSpawn = 0f;
        while (!ui.AllItemsDone)
        {
            if (controller != null && controller.RightHandWeapon != startWeapon && controller.RightHandWeapon != null) ui.SetItemDone(0);
            for (int i = 0; i < 2; i++)
            {
                if (kills[i] >= killsEach) ui.SetItemDone(i + 1);
                else ui.SetItemProgress(i + 1, $"{kills[i]} / {killsEach}");
            }

            // 화면 밖에서 러너가 계속 (최대 5마리)
            wave.RemoveAll(e => e == null || e.IsDead);
            if (Time.time >= nextSpawn && wave.Count < 5 && WaveManager.Instance != null)
            {
                EnemyBase enemy = WaveManager.Instance.SpawnEnemyOffscreen(EnemyType.Runner);
                if (enemy != null) wave.Add(enemy);
                nextSpawn = Time.time + 1f;
            }
            yield return null;
        }
        EnemyBase.OnAnyEnemyDeath -= onDeath;
        yield return CompleteStep();
    }

    // 지금 오른손 무기가 장착 슬롯 몇 번인지 (JU 아이템 이름 = WeaponDataSO.juItemName), 모르면 -1
    private int HeldWeaponSlot(WeaponDataSO[] equipped)
    {
        if (controller == null || controller.RightHandWeapon == null) return -1;
        string held = controller.RightHandWeapon.ItemName;
        for (int i = 0; i < equipped.Length && i < 2; i++)
        {
            if (equipped[i] != null && equipped[i].juItemName == held) return i;
        }
        return -1;
    }

    // 훈련 완료: 적 정리 → 완료 기록 → 팝업 → 메인 화면 (결과 화면·최고 기록은 남기지 않음)
    private IEnumerator StepComplete()
    {
        WaveManager.EndRoundAndDespawn();
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
        AlertBanner.ShowCustom("TRAINING COMPLETE", "READY FOR DEPLOYMENT", true, 2f);
        yield return new WaitForSeconds(1.2f);

        // 실전 수칙: 시간·층에 따라 강해지는 적 + 획득할수록 줄어드는 드랍률 → 효율적인 파밍과 탈출 시점 (StageDifficultyManager 규칙)
        yield return Popup("실전에서 기억할 것",
            "한 층에 오래 머물수록 적이 점점 강해집니다.\n" +
            $"모듈 드랍률은 층마다 <color={Accent}>30%</color>에서 시작해, 모듈을 얻을 때마다 낮아집니다.\n" +
            "<size=22><alpha=#AA>현재 드랍률은 화면 왼쪽 위 MODULE DROP에 표시됩니다.</size>\n\n" +
            $"<color={Gold}>드랍률이 높을 때 효율적으로 파밍하고,\n적이 너무 강해지기 전에 탈출하세요.</color>",
            "다음");
        yield return Popup("훈련 완료",
            "기본 훈련을 모두 마쳤습니다.\n\n" +
            $"실전에서는 쓰러지면 <color={Accent}>목숨</color>을 1개 잃고 그 층을 다시 시작합니다.\n" +
            $"보라색 <color={Accent}>엘리트</color>는 강하지만 더 좋은 모듈을 떨어뜨릴 수 있습니다.\n\n" +
            $"이제 <color={Accent}>DEPTH 15</color>에서 한 층씩 올라가 지상에 도달하세요.\n" +
            $"<color={Gold}>지상에 가까워질수록 더 많은 적을 상대해야 할 것입니다.</color>",
            "메인 화면으로");
        GameSession.LoadMainMenu();
    }

    // 튜토리얼 완료 기록 (메인 메뉴 버튼 CLEAR 표시)
    public const string CompletedKey = "TDS_TUTORIAL_DONE";
    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    // 정비: 정지 팝업으로 차례대로 안내 → 다중 투척이 그리드에 있고 Benelli 포함 장착 슬롯 2개가 차야 NEXT ROUND
    private IEnumerator StepMaintenance()
    {
        if (MaintenanceManager.Instance != null) MaintenanceManager.Instance.NextRoundBlocker = CheckMaintenanceReady;

        yield return Popup("정비 시간",
            "탈출에 성공하면 정비 시간이 됩니다.\n" +
            $"이번 층에서 얻은 모듈과 무기가 <color={Accent}>전리품 목록</color>에 모여 있습니다.\n\n" +
            $"<color={Gold}>다음 층으로 올라가면 장착하거나 배치하지 않은 전리품은 사라집니다.</color>");
        yield return Popup("모듈 배치",
            $"전리품의 <color={Accent}>다중 투척</color> 모듈을 끌어서 그리드에 놓으세요.\n" +
            $"끄는 중에 <color={Accent}>R</color>키로 회전, 오른쪽 클릭으로 되돌립니다.\n\n" +
            "다중 투척은 수류탄을 한 번에 여러 개 던지게 해 줍니다.");
        yield return Popup("무기 장착",
            $"<color={Accent}>Benelli M4</color>(산탄총)를 비어 있는 장착 슬롯으로 끌어 놓으세요.\n" +
            $"무기는 2개까지 장착하고, 전투 중 <color={Accent}>E</color>키로 바꿔 듭니다.");
        yield return Popup("금고",
            "금고 3칸에 넣은 아이템은 다음 층으로 올라가도 사라지지 않습니다.\n" +
            "지금 쓰지 않지만 나중에 쓸 무기·모듈을 보관하세요.\n\n" +
            $"준비가 끝나면 <color={Accent}>NEXT ROUND</color>를 누르세요.");

        // NEXT ROUND로 전투가 다시 시작될 때까지
        while (Time.timeScale <= 0f) yield return null;
        if (MaintenanceManager.Instance != null) MaintenanceManager.Instance.NextRoundBlocker = null;
    }

    // NEXT ROUND 조건 확인 — 모자라면 팝업으로 알려 주고 이유를 돌려줌 (null = 통과)
    private string CheckMaintenanceReady()
    {
        bool modulePlaced = GridInventorySystem.Instance != null
            && GridInventorySystem.Instance.placedModules.Exists(m => m != null && m.moduleData == FixedModule);

        WeaponDataSO[] equipped = PlayerInventory.Instance != null ? PlayerInventory.Instance.equippedWeapons : new WeaponDataSO[2];
        bool bothEquipped = equipped.Length >= 2 && equipped[0] != null && equipped[1] != null;
        bool fixedEquipped = System.Array.IndexOf(equipped, FixedWeapon) >= 0;

        string reason = null;
        if (!modulePlaced) reason = "다중 투척 모듈을 그리드에 배치하세요";
        else if (!fixedEquipped) reason = "Benelli M4를 장착 슬롯에 놓으세요";
        else if (!bothEquipped) reason = "장착 슬롯 2개를 모두 채우세요 (기존 무기도 장착)";
        if (reason == null) return null;

        ui.ShowPopup("아직 준비가 끝나지 않았습니다",
            $"{reason}.\n\n<size=22><alpha=#AA>다음 라운드에서 모듈 효과와 무기 교체를 직접 써 봅니다.</size>");
        return reason;
    }

    // 정지 팝업을 띄우고 닫힐 때까지 대기
    private IEnumerator Popup(string title, string body, string button = "확인")
    {
        popupDone = false;
        ui.ShowPopup(title, body, () => popupDone = true, button);
        while (!popupDone) yield return null;
    }

    // A 기본 조작: 이동·사격·대시·재장전을 한 번씩 + 제자리 표적 3마리 처치 (순서 무관, 실시간)
    private IEnumerator StepBasics()
    {
        const int moveItem = 0, shootItem = 1, dashItem = 2, reloadItem = 3, targetItem = 4;
        const float moveGoal = 6f;
        const int targetCount = 3;

        ui.ShowObjective("STEP 01", "기본 조작", "아래 항목을 모두 한 번씩 해 보세요. 순서는 상관없습니다.",
            new TutorialUI.ObjectiveItem("WASD", "이동"),
            new TutorialUI.ObjectiveItem("좌클릭", "사격 (마우스로 조준)"),
            new TutorialUI.ObjectiveItem("Space", "대시"),
            new TutorialUI.ObjectiveItem("R", "재장전 (탄을 쏜 뒤)"),
            new TutorialUI.ObjectiveItem("", "표적 처치"));

        System.Collections.Generic.List<EnemyBase> targets = SpawnTargets(targetCount);

        bool shot = false;
        System.Action<JUTPS.WeaponSystem.Weapon> onShot = _ => shot = true;
        WeaponModuleRuntime.OnAnyPlayerShot += onShot;

        Transform player = controller != null ? controller.transform : null;
        Vector3 lastPos = player != null ? player.position : Vector3.zero;
        float moved = 0f;
        float lastReloadKeyTime = -10f;

        while (!ui.AllItemsDone)
        {
            if (player != null)
            {
                Vector3 delta = player.position - lastPos;
                delta.y = 0f;
                if (delta.magnitude < 3f) moved += delta.magnitude; // 순간 이동(맵 복귀 등) 제외
                lastPos = player.position;
            }
            if (moved >= moveGoal) ui.SetItemDone(moveItem);
            if (shot) ui.SetItemDone(shootItem);
            if (controller != null && controller.IsDashing) ui.SetItemDone(dashItem);
            // 수동 재장전만 인정 (탄창이 비어 시작되는 자동 재장전 제외): R을 누른 직후 시작된 재장전
            if (Input.GetKeyDown(KeyCode.R)) lastReloadKeyTime = Time.unscaledTime;
            if (controller != null && controller.IsReloading && Time.unscaledTime - lastReloadKeyTime < 0.5f) ui.SetItemDone(reloadItem);

            int killed = 0;
            foreach (EnemyBase target in targets) if (target == null || target.IsDead) killed++;
            if (killed >= targets.Count) ui.SetItemDone(targetItem);
            else ui.SetItemProgress(targetItem, $"{killed} / {targets.Count}");
            yield return null;
        }

        WeaponModuleRuntime.OnAnyPlayerShot -= onShot;
        ui.FlashComplete();
        yield return new WaitForSecondsRealtime(1.2f);
        ui.HideObjective();
    }

    // B 모듈 드랍: 몰려오는 러너 처치 → 마지막 처치 자리에 다중 투척 확정 드랍
    private IEnumerator StepModuleDrop()
    {
        yield return Popup("모듈",
            $"적을 처치하면 <color={Accent}>모듈</color>이 떨어집니다.\n" +
            "모듈은 정비 시간에 그리드에 끼워 무기와 능력을 강화하는 부품입니다.\n\n" +
            $"<color={Gold}>떨어진 아이템은 직접 줍지 않아도\n탈출에 성공하면 모두 자동으로 흡수됩니다.</color>");

        const int killGoal = 4;
        ui.ShowObjective("STEP 02", "모듈 획득", "다가오는 적을 처치하세요. 이번에는 반드시 모듈이 떨어집니다.",
            new TutorialUI.ObjectiveItem("", "적 처치"),
            new TutorialUI.ObjectiveItem("", "모듈 드랍 확인"));

        List<EnemyBase> enemies = SpawnOffscreen(EnemyType.Runner, killGoal, false);
        Vector3 lastDeath = controller != null ? controller.transform.position : Vector3.zero;
        int killed = 0;
        System.Action<EnemyBase> onDeath = e =>
        {
            if (!enemies.Contains(e)) return;
            killed++;
            lastDeath = e.transform.position;
        };
        EnemyBase.OnAnyEnemyDeath += onDeath;
        while (killed < enemies.Count)
        {
            ui.SetItemProgress(0, $"{killed} / {enemies.Count}");
            yield return null;
        }
        EnemyBase.OnAnyEnemyDeath -= onDeath;
        ui.SetItemDone(0);

        if (ItemDropManager.Instance != null) ItemDropManager.Instance.DropModule(lastDeath, FixedModule);
        ui.ShowNotice("모듈 획득!  줍지 않아도 탈출에 성공하면 자동으로 흡수됩니다", 4f);
        yield return new WaitForSeconds(1.5f);
        ui.SetItemDone(1);
        yield return CompleteStep();
    }

    // C 수류탄: G키 해제·충전 채움 → 한쪽에서 몰려오는 무리 처치
    private IEnumerator StepGrenade()
    {
        yield return Popup("수류탄",
            $"<color={Accent}>G</color>키로 마우스 위치에 수류탄을 던집니다.\n" +
            "쓴 수류탄은 시간이 지나면 다시 충전됩니다. (장탄 링 오른쪽 막대)\n\n" +
            "적이 뭉쳐 있을 때 던지면 한 번에 처리할 수 있습니다.");

        GrenadeSkill grenade = FindAnyObjectByType<GrenadeSkill>();
        if (grenade != null)
        {
            grenade.InputLocked = false;
            grenade.RefillCharges();
        }

        ui.ShowObjective("STEP 03", "수류탄", "한쪽에서 적 무리가 몰려옵니다.",
            new TutorialUI.ObjectiveItem("G", "수류탄 던지기"),
            new TutorialUI.ObjectiveItem("", "무리 처치"));

        List<EnemyBase> pack = SpawnOffscreen(EnemyType.Runner, 6, true);
        int lastCharges = grenade != null ? grenade.CurrentCharges : 0;
        while (true)
        {
            if (grenade != null)
            {
                if (grenade.CurrentCharges < lastCharges) ui.SetItemDone(0);
                lastCharges = grenade.CurrentCharges;
            }
            int killed = 0;
            foreach (EnemyBase enemy in pack) if (enemy == null || enemy.IsDead) killed++;
            if (killed >= pack.Count) ui.SetItemDone(1);
            else ui.SetItemProgress(1, $"{killed} / {pack.Count}");
            if (ui.AllItemsDone) break;
            yield return null;
        }
        yield return CompleteStep();
    }

    // D 보급 상자: 상자 1개 생성(노란 화살표가 가리킴) → 열면 Benelli 확정
    private IEnumerator StepSupplyCrate()
    {
        yield return Popup("보급 상자",
            $"무기는 적에게서 떨어지지 않습니다. <color={Accent}>보급 상자</color> 옆에 잠시 머물러 열어야 얻을 수 있습니다.\n" +
            "노란 화살표가 가장 가까운 상자를 가리킵니다.\n\n" +
            $"<color={Gold}>무기도 탈출에 성공하면 자동으로 흡수됩니다.</color>\n새 무기는 정비 시간에 장착합니다.");

        crateZonePosition = PickZoneCenter(20f, null);
        SupplyCrate crate = SupplyCrateManager.Instance != null ? SupplyCrateManager.Instance.SpawnCrateAt(crateZonePosition) : null;

        ui.ShowObjective("STEP 04", "보급 상자", "노란 화살표를 따라가 상자 옆에서 잠시 기다리세요.",
            new TutorialUI.ObjectiveItem("", "보급 상자 열기"));

        while (crate != null && !crate.IsOpened) yield return null;
        ui.SetItemDone(0);
        ui.ShowNotice("무기 획득!  정비 시간에 장착할 수 있습니다", 4f);
        yield return CompleteStep();
    }

    // E 탈출 구역 공개 → F 들어가면 파밍 권장 알림, 10초 버티는 동안 적 계속 등장 → 탈출 성공 시 정비
    private IEnumerator StepExtraction()
    {
        ExtractionZone zone = FindAnyObjectByType<ExtractionZone>();
        if (zone == null) yield break;

        zone.PlaceAt(PickZoneCenter(30f, crateZonePosition));
        zone.SuccessBannerOverride = hold => AlertBanner.ShowCustom("TRAINING CLEARED", "PREPARE IN MAINTENANCE", true, hold);
        zone.SetHidden(false);

        yield return Popup("탈출 구역",
            $"초록 화살표가 가리키는 <color={Accent}>탈출 구역</color> 안에 머물면 게이지가 찹니다.\n" +
            "게이지가 가득 차면 이 층을 벗어나 위층으로 올라갑니다. 구역 밖으로 나가면 게이지는 멈춥니다.\n\n" +
            "버티는 동안 적이 계속 몰려옵니다.");

        ui.ShowObjective("STEP 05", "탈출", "탈출 구역으로 이동해 게이지를 채우세요.",
            new TutorialUI.ObjectiveItem("", "탈출 구역으로 이동"),
            new TutorialUI.ObjectiveItem("", "버티기"));

        while (!zone.IsPlayerInside && !zone.IsSequenceActive) yield return null;
        ui.SetItemDone(0);
        ui.ShowNotice("탈출하면 이 층의 아이템은 더 얻을 수 없습니다 — 탈출 전에 모듈과 무기를 충분히 파밍하세요", 5f);

        // 버티는 동안 화면 밖에서 러너가 계속 (최대 6마리)
        List<EnemyBase> wave = new List<EnemyBase>();
        float nextSpawn = 0f;
        while (!zone.IsSequenceActive)
        {
            ui.SetItemProgress(1, $"{Mathf.CeilToInt(zone.RemainingTime)}s");
            wave.RemoveAll(e => e == null || e.IsDead);
            if (Time.time >= nextSpawn && wave.Count < 6 && WaveManager.Instance != null)
            {
                EnemyBase enemy = WaveManager.Instance.SpawnEnemyOffscreen(EnemyType.Runner);
                if (enemy != null) wave.Add(enemy);
                nextSpawn = Time.time + 1.2f;
            }
            yield return null;
        }
        ui.SetItemDone(1);
        ui.FlashComplete();

        // 정비 진입(timeScale 0) 뒤 탈출 구역 다시 숨김 (다음 라운드 재배치 때 보이지 않게)
        while (Time.timeScale > 0f) yield return null;
        zone.SetHidden(true);
        zone.SuccessBannerOverride = null;
        ui.HideObjective();
    }

    // 단계 완료 표시 후 잠시 뒤 카드 숨김
    private IEnumerator CompleteStep()
    {
        ui.FlashComplete();
        yield return new WaitForSecondsRealtime(1.2f);
        ui.HideObjective();
        yield return new WaitForSeconds(0.6f);
    }

    private Vector3 crateZonePosition;

    // 플레이어 시작 구역(과 avoid 구역)을 뺀 구역 중앙 중 플레이어와 거리가 targetDistance에 가장 가까운 곳 (고정 시드라 매번 같은 자리)
    private Vector3 PickZoneCenter(float targetDistance, Vector3? avoid)
    {
        Vector3 playerPos = controller != null ? controller.transform.position : Vector3.zero;
        BspArenaGenerator arena = FindAnyObjectByType<BspArenaGenerator>();
        if (arena == null || arena.Leaves.Count == 0) return playerPos + Vector3.forward * targetDistance;

        List<Vector3> centers = arena.GetZoneCenters();
        int playerZone = arena.GetZoneIndexAt(playerPos);
        int avoidZone = avoid.HasValue ? arena.GetZoneIndexAt(avoid.Value) : -1;
        Vector3 best = centers[0];
        float bestScore = float.MaxValue;
        for (int i = 0; i < centers.Count; i++)
        {
            if (i == playerZone || i == avoidZone) continue;
            Vector3 offset = centers[i] - playerPos;
            offset.y = 0f;
            float score = Mathf.Abs(offset.magnitude - targetDistance);
            if (score < bestScore)
            {
                bestScore = score;
                best = centers[i];
            }
        }
        return best;
    }

    // 화면 밖에 count마리 (grouped면 첫 위치 주변에 뭉쳐서 — 수류탄 연습용 무리)
    private List<EnemyBase> SpawnOffscreen(EnemyType type, int count, bool grouped)
    {
        List<EnemyBase> list = new List<EnemyBase>();
        WaveManager wave = WaveManager.Instance;
        if (wave == null) return list;

        EnemyBase first = wave.SpawnEnemyOffscreen(type);
        if (first != null) list.Add(first);
        for (int i = 1; i < count; i++)
        {
            EnemyBase enemy = grouped && first != null
                ? wave.SpawnEnemy(type, first.transform.position + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)))
                : wave.SpawnEnemyOffscreen(type);
            if (enemy != null) list.Add(enemy);
        }
        return list;
    }

    // 플레이어 앞(맵 가운데 쪽) 7m에 부채꼴로 제자리 표적
    private System.Collections.Generic.List<EnemyBase> SpawnTargets(int count)
    {
        System.Collections.Generic.List<EnemyBase> targets = new System.Collections.Generic.List<EnemyBase>();
        if (WaveManager.Instance == null || controller == null) return targets;

        Vector3 origin = controller.transform.position;
        BspArenaGenerator arena = FindAnyObjectByType<BspArenaGenerator>();
        Vector3 forward = arena != null ? arena.transform.position - origin : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 1f ? forward.normalized : Vector3.forward;

        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * 25f;
            Vector3 position = origin + Quaternion.Euler(0f, angle, 0f) * forward * 7f;
            EnemyBase target = WaveManager.Instance.SpawnEnemy(EnemyType.Runner, position);
            if (target == null) continue;
            target.MakeStationaryTarget();
            target.transform.rotation = Quaternion.LookRotation(-forward);
            targets.Add(target);
        }
        return targets;
    }

    #endregion
}
