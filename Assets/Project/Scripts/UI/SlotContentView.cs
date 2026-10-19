using UnityEngine;
using UnityEngine.UI;

// 장착·금고 슬롯 안의 그림 (텍스트 대신) — U3d
//   무기: 실루엣 아이콘 (WeaponDataSO.weaponIcon, 레어도 색)
//   모듈: 모양 미니 칸(레어도 색) + 아래 계열 색 띠 + 스탯 모듈이면 오른쪽 위 스탯 아이콘
// 슬롯 Image 오브젝트에 자동으로 붙고(Get), 자식 오브젝트는 코드가 생성. 클릭은 슬롯이 받음 (자식은 raycast 끔)
public class SlotContentView : MonoBehaviour
{
    private RectTransform content;   // 테두리 안쪽 영역
    private Image weaponIcon;
    private RectTransform shapeRoot;
    private Image categoryBar;
    private Image statIcon;
    private Image mechanismPip; // 아이콘에 반영 안 된 작동 모듈 표시
    private Image dropHighlight; // 들고 있는 아이템을 놓을 자리 표시 (슬롯 바깥쪽까지 약간 크게)

    public static SlotContentView Get(Image slotImage)
    {
        if (slotImage == null) return null;
        SlotContentView view = slotImage.GetComponent<SlotContentView>();
        if (view == null) view = slotImage.gameObject.AddComponent<SlotContentView>();
        view.EnsureBuilt();
        return view;
    }

    // 표시할 그림이 있으면 true (호출 쪽은 텍스트를 비움)
    // iconOverride: 작동 모듈 변형 아이콘 (없으면 기본), showPip: 아이콘에 반영 안 된 작동 모듈이 있음
    public bool ShowWeapon(WeaponDataSO weapon, Sprite iconOverride = null, bool showPip = false)
    {
        Clear();
        Sprite icon = iconOverride != null ? iconOverride : (weapon != null ? weapon.weaponIcon : null);
        if (weapon == null || icon == null) return false;

        UITheme theme = UITheme.Current;
        content.gameObject.SetActive(true);
        weaponIcon.gameObject.SetActive(true);
        weaponIcon.sprite = icon;
        weaponIcon.color = theme.tintWeaponIconByRarity ? theme.GetRarityColor(weapon.rarity) : Color.white;

        if (showPip && theme.showMechanismPip)
        {
            mechanismPip.gameObject.SetActive(true);
            mechanismPip.color = theme.GetCategoryColor(ModuleCategory.Mechanism);
            mechanismPip.rectTransform.sizeDelta = new Vector2(theme.mechanismPipSize, theme.mechanismPipSize);
        }
        return true;
    }

    public bool ShowModule(ModuleDataSO module)
    {
        Clear();
        if (module == null || module.shapeGrid == null) return false;

        UITheme theme = UITheme.Current;
        content.gameObject.SetActive(true);

        // 계열 색 띠 (아래)
        categoryBar.gameObject.SetActive(true);
        categoryBar.color = theme.GetCategoryColor(module.category);
        categoryBar.rectTransform.sizeDelta = new Vector2(0f, theme.categoryBarHeight);

        // 스탯 아이콘 (오른쪽 위)
        Sprite icon = module.category == ModuleCategory.Stat ? theme.GetStatIcon(module.statType) : null;
        if (icon != null)
        {
            statIcon.gameObject.SetActive(true);
            statIcon.sprite = icon;
            statIcon.color = theme.GetCategoryColor(ModuleCategory.Stat);
            float size = icon.rect.width * theme.statIconScale;
            statIcon.rectTransform.sizeDelta = new Vector2(size, size);
        }

        BuildShape(module, theme, icon != null);
        return true;
    }

    // 무기·모듈을 들고 이 슬롯 위에 올렸을 때 (드래그가 끝나면 Update에서 자동으로 숨김)
    public void SetDropHighlight(bool on)
    {
        EnsureBuilt();
        UITheme theme = UITheme.Current;
        if (on && !theme.ApplySprite(dropHighlight, theme.dropHighlightSprite)) on = false;
        dropHighlight.gameObject.SetActive(on);
        if (on) dropHighlight.transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (dropHighlight == null || !dropHighlight.gameObject.activeSelf) return;
        bool dragging = (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            || (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging);
        if (!dragging) dropHighlight.gameObject.SetActive(false);
    }

    public void Clear()
    {
        EnsureBuilt();
        content.gameObject.SetActive(false);
        weaponIcon.gameObject.SetActive(false);
        categoryBar.gameObject.SetActive(false);
        statIcon.gameObject.SetActive(false);
        mechanismPip.gameObject.SetActive(false);
        for (int i = shapeRoot.childCount - 1; i >= 0; i--) Destroy(shapeRoot.GetChild(i).gameObject);
    }

    // 모양: 모듈 칸 수에 맞춰 칸 크기를 정해 가운데 정렬 (띠 위쪽 영역 안에)
    private void BuildShape(ModuleDataSO module, UITheme theme, bool hasStatIcon)
    {
        Rect area = content.rect;
        float barSpace = theme.categoryBarHeight + 4f;
        float iconSpace = hasStatIcon ? statIcon.rectTransform.sizeDelta.x * 0.5f : 0f; // 아이콘과 겹치지 않게 약간 왼쪽으로
        float availableW = Mathf.Max(10f, area.width - iconSpace * 2f);
        float availableH = Mathf.Max(10f, area.height - barSpace);

        const float spacing = 2f;
        int w = Mathf.Max(1, module.width), h = Mathf.Max(1, module.height);
        float tile = Mathf.Floor(Mathf.Min((availableW - spacing * (w - 1)) / w, (availableH - spacing * (h - 1)) / h));
        tile = Mathf.Clamp(tile, 4f, 24f);

        float totalW = w * tile + (w - 1) * spacing;
        float totalH = h * tile + (h - 1) * spacing;
        shapeRoot.sizeDelta = new Vector2(totalW, totalH);
        shapeRoot.anchoredPosition = new Vector2(-iconSpace, barSpace * 0.5f);

        Color color = theme.GetRarityColor(module.rarity);
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                int index = r * w + c;
                if (index >= module.shapeGrid.Length || !module.shapeGrid[index]) continue;

                Image cell = CreateImage($"Cell_{c}_{r}", shapeRoot);
                RectTransform rect = cell.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(tile, tile);
                rect.anchoredPosition = new Vector2(c * (tile + spacing), -r * (tile + spacing));
                cell.color = color;
            }
        }
    }

    private void EnsureBuilt()
    {
        if (content != null) return;

        UITheme theme = UITheme.Current;
        GameObject root = new GameObject("SlotContent", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        content = root.GetComponent<RectTransform>();
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        float pad = theme.slotContentPadding;
        content.offsetMin = new Vector2(pad, pad);
        content.offsetMax = new Vector2(-pad, -pad);

        weaponIcon = CreateImage("WeaponIcon", content);
        Stretch(weaponIcon.rectTransform);
        weaponIcon.preserveAspect = true;

        GameObject shapeObj = new GameObject("Shape", typeof(RectTransform));
        shapeObj.transform.SetParent(content, false);
        shapeRoot = shapeObj.GetComponent<RectTransform>();
        shapeRoot.anchorMin = shapeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        shapeRoot.pivot = new Vector2(0.5f, 0.5f);

        categoryBar = CreateImage("CategoryBar", content);
        RectTransform bar = categoryBar.rectTransform;
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = Vector2.zero;

        statIcon = CreateImage("StatIcon", content);
        RectTransform iconRect = statIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.pivot = new Vector2(1f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        statIcon.preserveAspect = true;

        mechanismPip = CreateImage("MechanismPip", content);
        RectTransform pipRect = mechanismPip.rectTransform;
        pipRect.anchorMin = pipRect.anchorMax = new Vector2(0f, 1f);
        pipRect.pivot = new Vector2(0f, 1f);
        pipRect.anchoredPosition = Vector2.zero;

        content.SetAsLastSibling(); // 슬롯의 기존 텍스트보다 위

        // 놓을 자리 표시: content 바깥(슬롯 테두리보다 약간 크게)
        dropHighlight = CreateImage("DropHighlight", transform);
        RectTransform highlight = dropHighlight.rectTransform;
        highlight.anchorMin = Vector2.zero;
        highlight.anchorMax = Vector2.one;
        highlight.offsetMin = new Vector2(-4f, -4f);
        highlight.offsetMax = new Vector2(4f, 4f);
        dropHighlight.gameObject.SetActive(false);
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
