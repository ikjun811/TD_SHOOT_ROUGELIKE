using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 런 기록 수집 + 결과 화면 (클리어 / 게임 오버) — 전투 씬 로드 시 GameplayBootstrap이 자동 생성
// 결과 화면은 코드로 생성 (에디터 작업 불필요). 표시 중에는 시간 정지·플레이어 입력 차단·전투 HUD 숨김
public class RunResultManager : MonoBehaviour
{
    public static RunResultManager Instance { get; private set; }

    // 메인 화면 씬 이름 (빌드 설정에 있을 때만 "메인 화면" 버튼 표시)
    public const string MainMenuSceneName = GameSession.MainMenuSceneName;

    [Header("Run Stats (Read Only)")]
    [SerializeField] private float combatTime;   // 정비·결과 화면(시간 정지) 제외
    [SerializeField] private int normalKills;
    [SerializeField] private int eliteKills;
    [SerializeField] private float totalDamage;   // 적에게 준 피해 합계 (실제 깎인 체력)
    [SerializeField] private float maxHit;        // 한 번에 준 최대 피해 (방어 적용 전 값 — 마지막 일격이 남은 체력으로 잘리지 않게)
    [SerializeField] private int deaths;
    [SerializeField] private int modulesCollected;

    private bool isShowing;
    public bool IsShowing => isShowing; // 결과 화면 표시 중 (PauseMenuUI가 일시정지 막음)

    public static void EnsureCreated()
    {
        if (Instance == null) new GameObject("RunResultManager").AddComponent<RunResultManager>();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (!isShowing && Time.timeScale > 0f) combatTime += Time.deltaTime;
    }

    // WaveManager가 적 처치 시 호출
    public void RecordKill(bool isElite)
    {
        if (isElite) eliteKills++;
        else normalKills++;
    }

    // PlayerInventory.AddModule이 모듈 획득 시 호출
    public void RecordModulePickup()
    {
        modulesCollected++;
    }

    private void OnEnable()
    {
        JU.IHealth.OnAnyDamaged += OnAnyDamaged;
    }

    private void OnDisable()
    {
        JU.IHealth.OnAnyDamaged -= OnAnyDamaged;
    }

    // 플레이어가 맞으면 사망 횟수만, 그 외(적)는 피해 기록 (적끼리는 서로 공격하지 않음)
    private void OnAnyDamaged(JU.IHealth.DamageResultInfo result)
    {
        if (isShowing) return;
        Component target = result.DamagedObject as Component;
        if (target == null) return;

        if (target.CompareTag("Player"))
        {
            if (result.WasKilled) deaths++;
            return;
        }
        if (result.RealDamage <= 0f) return;
        // 적이 준 피해(봄버 폭발 등)는 플레이어 기록에서 제외
        if (result.DamageOwner != null && result.DamageOwner.GetComponentInParent<EnemyBase>() != null) return;
        totalDamage += result.RealDamage;
        maxHit = Mathf.Max(maxHit, result.SuggestiveDamage);
    }

    // 최종 라운드 탈출(cleared = true) 또는 목숨 소진(false) 시 호출
    public void ShowResult(bool cleared)
    {
        if (isShowing) return;
        isShowing = true;

        Time.timeScale = 0f;
        if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(false);
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
            StageHUDUI.Instance.SetHUDActive(false);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        FillResultData(cleared);

        // 결과 씬이 빌드 설정에 있으면 그 씬으로, 없으면(전투 씬 단독 시험) 코드 결과 화면
        if (Application.CanStreamedLevelBeLoaded(GameSession.ResultSceneName)) GameSession.LoadResultScene();
        else BuildResultScreen(cleared);
        Debug.Log($"RunResultManager: {(cleared ? "GAME CLEAR" : "GAME OVER")} — time {FormatTime(combatTime)}, kills {normalKills}/{eliteKills}");
    }

    // 결과 씬으로 넘길 기록: 수치 + 최종 빌드(장착 무기·작동 모듈·그리드 배치) + 최고 기록 갱신
    private void FillResultData(bool cleared)
    {
        RunResultData.Cleared = cleared;
        RunResultData.Round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        RunResultData.MaxRound = WaveManager.Instance != null ? WaveManager.Instance.MaxRound : 15;
        RunResultData.CombatTime = combatTime;
        RunResultData.NormalKills = normalKills;
        RunResultData.EliteKills = eliteKills;
        RunResultData.TotalDamage = totalDamage;
        RunResultData.MaxHit = maxHit;
        RunResultData.Deaths = deaths;
        RunResultData.ModulesCollected = modulesCollected;

        GridInventorySystem grid = GridInventorySystem.Instance;
        RunResultData.Modules.Clear();
        if (grid != null)
        {
            RunResultData.GridWidth = grid.gridWidth;
            RunResultData.GridHeight = grid.gridHeight;
            foreach (var pm in grid.placedModules)
            {
                if (pm == null || pm.moduleData == null) continue;
                RunResultData.Modules.Add(new RunResultData.ModuleSnapshot
                {
                    module = pm.moduleData, x = pm.startX, y = pm.startY,
                    width = pm.currentWidth, height = pm.currentHeight, shape = (bool[])pm.currentShape.Clone()
                });
            }
        }

        WeaponDataSO[] equipped = PlayerInventory.Instance != null ? PlayerInventory.Instance.equippedWeapons : new WeaponDataSO[0];
        RunResultData.Weapons = (WeaponDataSO[])equipped.Clone();
        RunResultData.WeaponMechanisms = new System.Collections.Generic.List<ModuleEffect>[equipped.Length];
        for (int i = 0; i < equipped.Length; i++)
        {
            var effects = new System.Collections.Generic.List<ModuleEffect>();
            if (equipped[i] != null)
            {
                foreach (var snapshot in RunResultData.Modules)
                {
                    ModuleDataSO m = snapshot.module;
                    if (m.category == ModuleCategory.Mechanism && m.TargetsWeapon(equipped[i].weaponType) && !effects.Contains(m.effect)) effects.Add(m.effect);
                }
            }
            RunResultData.WeaponMechanisms[i] = effects;
        }

        RunResultData.MarkFilled();
    }

    #region UI

    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    private static readonly Color ClearColor = new Color(1f, 0.82f, 0.2f);
    private static readonly Color GameOverColor = new Color(1f, 0.3f, 0.3f);

    private void BuildResultScreen(bool cleared)
    {
        EnsureEventSystem();

        GameObject canvasObj = new GameObject("ResultScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObj.GetComponent<RectTransform>();

        // 화면 전체 어둡게
        RectTransform dim = CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim);

        // 가운데 패널
        RectTransform panel = CreateImage("Panel", root, PanelColor);
        panel.sizeDelta = new Vector2(760f, 640f);

        CreateText("Title", panel, cleared ? "SURFACE REACHED" : "MISSION FAILED", 60f, cleared ? ClearColor : GameOverColor,
                   new Vector2(0f, 250f), new Vector2(700f, 90f), FontStyles.Bold, role: UIFontRole.Heading);

        CreateText("Stats", panel, BuildStatsText(), 30f, Color.white,
                   new Vector2(0f, 20f), new Vector2(660f, 340f), FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // 결과 화면은 메인 화면으로만 (사용자 결정 2026-10-19). 메인 화면 씬이 빌드 설정에 없을 때(전투 씬 단독 시험)만 다시 시작
        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            CreateButton("MainMenuButton", panel, "메인 화면", new Vector2(0f, -250f), GoToMainMenu);
        else
            CreateButton("RestartButton", panel, "다시 시작", new Vector2(0f, -250f), RestartRun);
    }

    private string BuildStatsText()
    {
        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        int maxRound = WaveManager.Instance != null ? WaveManager.Instance.MaxRound : 15;

        int depth = RunResultData.Cleared ? 0 : DepthDisplay.FromRound(round, maxRound); // 깊이 표시 (DEPTH ZERO 컨셉)
        string text = $"도달 깊이     {DepthDisplay.Label(depth)}{(RunResultData.NewBestRound ? "  <color=#FFD040>NEW</color>" : "")}\n" +
                      $"전투 시간     {FormatTime(combatTime)}{(RunResultData.NewBestClearTime ? "  <color=#FFD040>NEW</color>" : "")}\n" +
                      $"처치 수       일반 {normalKills}  /  엘리트 {eliteKills}\n" +
                      $"총 피해량     {totalDamage:N0}   최고 한 방 {maxHit:N0}\n" +
                      $"사망 {deaths}회   획득 모듈 {modulesCollected}개\n\n" +
                      "<b>최종 빌드</b>\n";

        // 장착 무기 (레어도 색)
        if (PlayerInventory.Instance != null)
        {
            for (int i = 0; i < PlayerInventory.Instance.equippedWeapons.Length; i++)
            {
                WeaponDataSO w = PlayerInventory.Instance.equippedWeapons[i];
                string weaponText = w != null
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(w.GetRarityColor())}>{w.weaponName} [{GetRarityName(w.rarity)}]</color>"
                    : "<color=#888888>비어 있음</color>";
                text += $"무기 {i + 1}        {weaponText}\n";
            }
        }

        // 그리드 모듈 수 / 레전더리 수
        int moduleCount = 0;
        int legendaryCount = 0;
        if (GridInventorySystem.Instance != null)
        {
            foreach (var pm in GridInventorySystem.Instance.placedModules)
            {
                if (pm == null || pm.moduleData == null) continue;
                moduleCount++;
                if (pm.moduleData.rarity == RarityType.Legendary) legendaryCount++;
            }
        }
        text += $"모듈          {moduleCount}개 (레전더리 {legendaryCount}개)";

        return text;
    }

    private static string GetRarityName(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => "일반",
            RarityType.Rare => "희귀",
            RarityType.Elite => "엘리트",
            RarityType.Legendary => "레전더리",
            _ => rarity.ToString()
        };
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateImage(string objName, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string objName, RectTransform parent, string content, float size, Color color,
                                              Vector2 position, Vector2 boxSize, FontStyles style,
                                              TextAlignmentOptions alignment = TextAlignmentOptions.Center,
                                              UIFontRole role = UIFontRole.Body)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(tmp, role);
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void CreateButton(string objName, RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateImage(objName, parent, new Color(0.25f, 0.25f, 0.3f, 1f));
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 70f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.5f, 1f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateText("Label", rect, label, 36f, Color.white, Vector2.zero, rect.sizeDelta, FontStyles.Bold, role: UIFontRole.Heading);
    }

    #endregion

    // 씬 전환은 GameSession으로 통일 (timeScale 1 복구, 다시 시작은 같은 모드 유지)
    private void RestartRun()
    {
        GameSession.StartGame(GameSession.Mode);
    }

    private void GoToMainMenu()
    {
        GameSession.LoadMainMenu();
    }
}
