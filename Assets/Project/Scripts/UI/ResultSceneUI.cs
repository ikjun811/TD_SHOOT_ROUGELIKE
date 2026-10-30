using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 결과 씬 (ResultScene): RunResultData를 에디터에서 배치한 UI에 채움 (GDD 8-6 #10)
//   클리어/실패 제목·색, 전투 기록 카드(총 피해량·최고 한 방·사망·획득 모듈), 라운드·시간·처치, 최고 기록 + NEW
//   최종 빌드: 장착 무기 아이콘(레어도 테두리 + 작동 모듈 변형 실루엣) + 그리드 미리보기(모듈 경계선)
//   씬은 Tools > TD Shoot > Create Result Scene으로 만들 수 있음
public class ResultSceneUI : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Color clearColor = new Color(1f, 0.82f, 0.2f);
    [SerializeField] private Color failColor = new Color(1f, 0.3f, 0.3f);

    [Header("Combat Records (큰 카드)")]
    [SerializeField] private TextMeshProUGUI totalDamageText;
    [SerializeField] private TextMeshProUGUI maxHitText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI modulesText;

    [Header("Run Summary")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI bestRecordText;
    [SerializeField] private GameObject newRoundBadge;
    [SerializeField] private GameObject newTimeBadge;

    [Header("Final Build")]
    [SerializeField] private Image[] weaponSlots;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private TextMeshProUGUI buildSummaryText;

    [Header("Buttons")]
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GameSession.LoadMainMenu);
    }

    private void Start()
    {
        if (!RunResultData.HasData)
        {
            // 결과 씬을 직접 실행한 경우 (기록 없음)
            if (titleText != null) titleText.text = "NO RECORD";
            if (subtitleText != null) subtitleText.text = "게임을 플레이하면 결과가 표시됩니다";
            return;
        }

        bool cleared = RunResultData.Cleared;
        if (titleText != null)
        {
            titleText.text = cleared ? "GAME CLEAR" : "GAME OVER";
            titleText.color = cleared ? clearColor : failColor;
        }
        if (subtitleText != null)
        {
            subtitleText.text = cleared
                ? $"{RunResultData.MaxRound}라운드 탈출 성공"
                : $"{RunResultData.Round}라운드에서 작전 실패";
        }

        SetText(totalDamageText, $"{RunResultData.TotalDamage:N0}");
        SetText(maxHitText, $"{RunResultData.MaxHit:N0}");
        SetText(deathsText, $"{RunResultData.Deaths}");
        SetText(modulesText, $"{RunResultData.ModulesCollected}");

        SetText(roundText, $"{RunResultData.Round} / {RunResultData.MaxRound}");
        SetText(timeText, RunResultData.FormatTime(RunResultData.CombatTime));
        SetText(killsText, $"{RunResultData.NormalKills + RunResultData.EliteKills}  (엘리트 {RunResultData.EliteKills})");
        if (newRoundBadge != null) newRoundBadge.SetActive(RunResultData.NewBestRound);
        if (newTimeBadge != null) newTimeBadge.SetActive(RunResultData.NewBestClearTime);

        string bestClear = RunResultData.BestClearTime > 0f ? RunResultData.FormatTime(RunResultData.BestClearTime) : "-";
        SetText(bestRecordText, $"최고 기록   라운드 {RunResultData.BestRound}   ·   최단 클리어 {bestClear}");

        ShowWeapons();
        BuildGridPreview();
    }

    private void ShowWeapons()
    {
        if (weaponSlots == null) return;
        UITheme theme = UITheme.Current;
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            Image slot = weaponSlots[i];
            if (slot == null) continue;
            WeaponDataSO weapon = i < RunResultData.Weapons.Length ? RunResultData.Weapons[i] : null;
            SlotContentView view = SlotContentView.Get(slot);

            if (weapon == null)
            {
                if (view != null) view.Clear();
                theme.ApplyEmptySlot(slot);
                continue;
            }

            theme.ApplyRaritySlot(slot, weapon.rarity);
            var mechanisms = i < RunResultData.WeaponMechanisms.Length ? RunResultData.WeaponMechanisms[i] : null;
            int covered = 0;
            Sprite icon = mechanisms != null ? weapon.GetIcon(mechanisms, out covered) : weapon.weaponIcon;
            if (view != null) view.ShowWeapon(weapon, icon, mechanisms != null && mechanisms.Count > covered);
        }
    }

    // 그리드 미리보기: 빈 칸 = 테마 빈 칸 스프라이트, 모듈 칸 = 레어도 색, 다른 모듈·빈 칸과 맞닿은 변에만 경계선 (정비 화면과 같은 규칙)
    private void BuildGridPreview()
    {
        int w = RunResultData.GridWidth, h = RunResultData.GridHeight;
        if (gridContainer == null || w <= 0 || h <= 0) return;

        UITheme theme = UITheme.Current;
        int[,] owner = new int[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) owner[x, y] = -1;

        for (int i = 0; i < RunResultData.Modules.Count; i++)
        {
            var m = RunResultData.Modules[i];
            for (int r = 0; r < m.height; r++)
            {
                for (int c = 0; c < m.width; c++)
                {
                    if (m.shape == null || !m.shape[r * m.width + c]) continue;
                    int gx = m.x + c, gy = m.y + r;
                    if (gx >= 0 && gx < w && gy >= 0 && gy < h) owner[gx, gy] = i;
                }
            }
        }

        Vector2 area = gridContainer.rect.size;
        float cell = Mathf.Floor(Mathf.Min(area.x / w, area.y / h));
        Vector2 origin = new Vector2(-cell * w / 2f, cell * h / 2f); // 왼쪽 위 (y는 위 → 아래 증가)
        float scale = cell / 60f; // 정비 화면 칸 크기 기준으로 선 두께 비례
        bool outlined = theme.gridOutlinedStyle;
        float border = Mathf.Max(1f, (outlined ? theme.gridOutlinedBorderWidth : theme.gridModuleBorderWidth) * scale);
        float innerWidth = outlined && theme.gridOutlinedInnerWidth > 0f ? Mathf.Max(1f, theme.gridOutlinedInnerWidth * scale) : 0f;
        float innerInset = border + theme.gridOutlinedInnerGap * scale;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2 pos = origin + new Vector2((x + 0.5f) * cell, -(y + 0.5f) * cell);
                Image image = CreateCell($"Cell_{x}_{y}", pos, new Vector2(cell, cell));
                int id = owner[x, y];
                if (id < 0)
                {
                    if (!theme.ApplySprite(image, theme.gridEmptySprite, theme.gridPixelScale)) image.sprite = null;
                    image.color = outlined ? theme.gridOutlinedEmptyColor : theme.gridEmptyColor;
                    continue;
                }

                // 정비 화면(GridPanelUI)과 같은 스타일: 외곽선 = 남색에 레어도 색을 섞은 바탕 + 레어도 색 외곽선 + 안쪽 흐린 선
                Color rarity = theme.GetRarityColor(RunResultData.Modules[id].module.rarity);
                Color edgeColor;
                image.sprite = null;
                if (outlined)
                {
                    Color fill = Color.Lerp(theme.gridOutlinedBaseColor, rarity, theme.gridOutlinedFill);
                    fill.a = theme.gridOutlinedFillAlpha;
                    image.color = fill;
                    edgeColor = rarity;
                }
                else
                {
                    Color fill = rarity * theme.gridFillBrightness;
                    fill.a = 1f;
                    image.color = fill;
                    edgeColor = theme.gridModuleBorderColor;
                }

                // 위·아래·왼·오른쪽 이웃이 다른 모듈·빈 칸·그리드 밖이면 경계선
                bool top = Owner(owner, x, y - 1, w, h) != id;
                bool bottom = Owner(owner, x, y + 1, w, h) != id;
                bool left = Owner(owner, x - 1, y, w, h) != id;
                bool right = Owner(owner, x + 1, y, w, h) != id;
                CreateCellLine(pos, cell, 0, top, 0f, border, 0f, 0f, edgeColor);
                CreateCellLine(pos, cell, 1, bottom, 0f, border, 0f, 0f, edgeColor);
                CreateCellLine(pos, cell, 2, left, 0f, border, 0f, 0f, edgeColor);
                CreateCellLine(pos, cell, 3, right, 0f, border, 0f, 0f, edgeColor);

                if (innerWidth > 0f)
                {
                    Color inner = new Color(rarity.r, rarity.g, rarity.b, theme.gridOutlinedInnerAlpha);
                    CreateCellLine(pos, cell, 0, top, innerInset, innerWidth, left ? innerInset : 0f, right ? innerInset : 0f, inner);
                    CreateCellLine(pos, cell, 1, bottom, innerInset, innerWidth, left ? innerInset : 0f, right ? innerInset : 0f, inner);
                    CreateCellLine(pos, cell, 2, left, innerInset, innerWidth, top ? innerInset : 0f, bottom ? innerInset : 0f, inner);
                    CreateCellLine(pos, cell, 3, right, innerInset, innerWidth, top ? innerInset : 0f, bottom ? innerInset : 0f, inner);
                }
            }
        }

        SetText(buildSummaryText, BuildSummary());
    }

    private static int Owner(int[,] owner, int x, int y, int w, int h)
    {
        return x < 0 || y < 0 || x >= w || y >= h ? -2 : owner[x, y];
    }

    private string BuildSummary()
    {
        int legendary = 0, mechanism = 0, special = 0;
        foreach (var m in RunResultData.Modules)
        {
            if (m.module.rarity == RarityType.Legendary) legendary++;
            if (m.module.category == ModuleCategory.Mechanism) mechanism++;
            if (m.module.category == ModuleCategory.Special || m.module.category == ModuleCategory.Unique) special++;
        }
        return $"모듈 {RunResultData.Modules.Count}개  ·  작동 {mechanism}  ·  특수 {special}  ·  레전더리 {legendary}";
    }

    private Image CreateCell(string name, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(gridContainer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // 칸 안쪽 선 1개. edge 0 위·1 아래·2 왼쪽·3 오른쪽, inset = 칸 가장자리에서 떨어진 거리,
    // start·end = 길이 방향 양 끝을 비키는 거리 (가로선은 왼쪽·오른쪽, 세로선은 위·아래)
    private void CreateCellLine(Vector2 pos, float cell, int edge, bool visible, float inset, float thickness, float start, float end, Color color)
    {
        if (!visible) return;

        float half = cell / 2f;
        float length = cell - start - end;
        Vector2 center, size;
        switch (edge)
        {
            case 0: center = new Vector2((start - end) / 2f, half - inset - thickness / 2f); size = new Vector2(length, thickness); break;
            case 1: center = new Vector2((start - end) / 2f, -half + inset + thickness / 2f); size = new Vector2(length, thickness); break;
            case 2: center = new Vector2(-half + inset + thickness / 2f, (end - start) / 2f); size = new Vector2(thickness, length); break;
            default: center = new Vector2(half - inset - thickness / 2f, (end - start) / 2f); size = new Vector2(thickness, length); break;
        }

        Image line = CreateCell("Edge", pos + center, size);
        line.color = color;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }
}
