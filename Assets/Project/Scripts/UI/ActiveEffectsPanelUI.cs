using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 적용 효과 패널 (U4): 그리드 아래 구역(ActiveEffectsArea)에 붙이면 안쪽에 스크롤 목록을 만들어 채움
//   스탯: 종류별 합산 최종값 (0이면 숨김) / 작동·특수·유니크: 모듈마다 한 줄, 중복이라 효과 없는 모듈은 회색
//   줄에 마우스를 올리면 그리드에서 해당 모듈(스탯은 기여하는 모듈 전부)을 강조 (GridPanelUI.SetHighlightedModules)
// 정비 화면이 열릴 때(OnEnable)와 모듈 배치·제거 때(GridInventorySystem.OnModulesChanged) 갱신
public class ActiveEffectsPanelUI : MonoBehaviour
{
    [SerializeField] private float padding = 10f;

    private RectTransform content;

    private static readonly ModuleStatType[] StatOrder =
    {
        ModuleStatType.AttackDamagePercent, ModuleStatType.FireRatePercent, ModuleStatType.CriticalChance, ModuleStatType.ReloadSpeedPercent,
        ModuleStatType.MaxHealthBonus, ModuleStatType.ArmorBonus, ModuleStatType.MoveSpeedPercent, ModuleStatType.SkillCooldownReduce
    };

    private void OnEnable()
    {
        GridInventorySystem.OnModulesChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GridInventorySystem.OnModulesChanged -= Refresh;
        if (GridPanelUI.Instance != null) GridPanelUI.Instance.SetHighlightedModules(null);
    }

    public void Refresh()
    {
        EnsureBuilt();
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        if (GridInventorySystem.Instance == null) return;

        UITheme theme = UITheme.Current;
        var placed = GridInventorySystem.Instance.placedModules;

        // 스탯 합산 (기여 모듈도 기록)
        Dictionary<ModuleStatType, float> totals = new Dictionary<ModuleStatType, float>();
        Dictionary<ModuleStatType, List<GridInventorySystem.PlacedModule>> contributors = new Dictionary<ModuleStatType, List<GridInventorySystem.PlacedModule>>();
        foreach (var pm in placed)
        {
            if (pm.moduleData == null || pm.moduleData.category != ModuleCategory.Stat) continue;
            ModuleStatType type = pm.moduleData.statType;
            totals.TryGetValue(type, out float value);
            totals[type] = value + pm.moduleData.GetFinalStatValue();
            if (!contributors.TryGetValue(type, out var list)) contributors[type] = list = new List<GridInventorySystem.PlacedModule>();
            list.Add(pm);
        }

        if (totals.Count > 0)
        {
            AddHeader("스탯", theme);
            foreach (ModuleStatType type in StatOrder)
            {
                if (!totals.TryGetValue(type, out float value) || Mathf.Approximately(value, 0f)) continue;
                AddRow(ItemTooltipUI.GetModuleStatText(type, value), theme.GetStatIcon(type), theme.GetCategoryColor(ModuleCategory.Stat),
                    theme.effectsTextColor, contributors[type], theme);
            }
        }

        AddModuleSection("작동", placed, m => m.category == ModuleCategory.Mechanism, theme);
        AddModuleSection("특수 · 유니크", placed, m => m.category == ModuleCategory.Special || m.category == ModuleCategory.Unique, theme);

        if (content.childCount == 0) AddHeader("배치된 모듈 없음", theme, theme.effectsInactiveColor);
    }

    private void AddModuleSection(string title, List<GridInventorySystem.PlacedModule> placed, System.Predicate<ModuleDataSO> filter, UITheme theme)
    {
        bool headerAdded = false;
        foreach (var pm in placed)
        {
            ModuleDataSO m = pm.moduleData;
            if (m == null || !filter(m)) continue;
            if (!headerAdded) { AddHeader(title, theme); headerAdded = true; }

            bool active = GridInventorySystem.Instance.IsEffectActive(pm);
            string text = $"{m.moduleName} · {ItemTooltipUI.GetTargetName(m.target)}";
            if (!active) text += "  (중복 – 효과 없음)";
            AddRow(text, null, theme.GetCategoryColor(m.category), active ? theme.effectsTextColor : theme.effectsInactiveColor,
                new List<GridInventorySystem.PlacedModule> { pm }, theme);
        }
    }

    private void AddHeader(string text, UITheme theme, Color? color = null)
    {
        GameObject obj = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
        obj.transform.SetParent(content, false);
        obj.GetComponent<LayoutElement>().preferredHeight = theme.effectsRowHeight;

        TextMeshProUGUI label = CreateText(obj.transform, text, theme.effectsFontSize, color ?? theme.effectsHeaderColor);
        Stretch(label.rectTransform, 0f);
    }

    // 왼쪽: 아이콘(있으면) 또는 계열 색 막대, 오른쪽: 글자. 줄 전체가 마우스 올림을 받음
    private void AddRow(string text, Sprite icon, Color accent, Color textColor, List<GridInventorySystem.PlacedModule> modules, UITheme theme)
    {
        float h = theme.effectsRowHeight;
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ActiveEffectRowHover));
        row.transform.SetParent(content, false);
        row.GetComponent<LayoutElement>().preferredHeight = h;
        Image hitArea = row.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f); // 보이지 않지만 마우스는 받음
        row.GetComponent<ActiveEffectRowHover>().modules = modules;

        float markerWidth;
        Image marker = CreateImage("Marker", row.transform);
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0f, 0.5f);
        markerRect.anchoredPosition = new Vector2(8f, 0f);
        if (icon != null)
        {
            marker.sprite = icon;
            marker.preserveAspect = true;
            marker.color = accent;
            markerWidth = h - 6f;
            markerRect.sizeDelta = new Vector2(markerWidth, markerWidth);
        }
        else
        {
            marker.color = accent;
            markerWidth = 5f;
            markerRect.sizeDelta = new Vector2(markerWidth, h - 8f);
        }

        TextMeshProUGUI label = CreateText(row.transform, text, theme.effectsFontSize, textColor);
        Stretch(label.rectTransform, 0f);
        label.rectTransform.offsetMin = new Vector2(8f + markerWidth + 8f, 0f);
    }

    private void EnsureBuilt()
    {
        if (content != null) return;

        // 스크롤 영역 (마우스 휠로 스크롤, 넘치는 부분은 잘라 냄)
        GameObject scrollObj = new GameObject("EffectsScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollObj.transform.SetParent(transform, false);
        RectTransform viewport = scrollObj.GetComponent<RectTransform>();
        Stretch(viewport, padding);

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(scrollObj.transform, false);
        content = contentObj.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2f;
        contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}

// 적용 효과 패널의 한 줄: 마우스를 올리면 그리드에서 해당 모듈 강조 (ActiveEffectsPanelUI가 생성)
public class ActiveEffectRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public List<GridInventorySystem.PlacedModule> modules;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GridPanelUI.Instance != null) GridPanelUI.Instance.SetHighlightedModules(modules);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GridPanelUI.Instance != null) GridPanelUI.Instance.SetHighlightedModules(null);
    }

    private void OnDestroy()
    {
        // 목록을 다시 그릴 때 마우스가 올라가 있던 줄이 사라지면 강조 해제
        if (GridPanelUI.Instance != null) GridPanelUI.Instance.SetHighlightedModules(null);
    }
}
