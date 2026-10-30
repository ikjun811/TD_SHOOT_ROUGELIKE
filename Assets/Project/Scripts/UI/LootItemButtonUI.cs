using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 전리품 가방 내부 개별 무기/모듈 버튼의 UI 표현, 마우스 호버 툴팁, 클릭 이벤트를 제어하는 클래스입니다.
/// </summary>
public class LootItemButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private WeaponDataSO weaponData;
    private ModuleDataSO moduleData;

    [Header("Mini Shape Parent")]
    [SerializeField] private Transform miniShapeParent;

    public void Setup(WeaponDataSO weapon)
    {
        weaponData = weapon;
        moduleData = null;

        ClearMiniShape();

        // 장착·금고 슬롯과 같은 모습: 레어도 테두리 + 무기 실루엣 (이름은 툴팁). 테마·아이콘이 없으면 기존 색·글자
        Image btnBg = GetComponent<Image>();
        bool framed = UITheme.Current.ApplyRaritySlot(btnBg, weapon.rarity);
        if (!framed && btnBg != null) btnBg.color = weapon.GetRarityColor();

        SlotContentView view = SlotContentView.Get(btnBg);
        bool hasIcon = view != null && view.ShowWeapon(weapon);

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = hasIcon ? "" : $"[{weapon.weaponType}]\n{weapon.weaponName}";
            btnText.fontSize = 13;
            btnText.alignment = TextAlignmentOptions.Left;
        }
    }

    public void Setup(ModuleDataSO module)
    {
        moduleData = module;
        weaponData = null;

        ClearMiniShape();

        // F4c-5 가로 카드 (무기 카드와 같은 폭): 레어도 테두리 + 왼쪽 정사각 칸에 모양·계열 띠 + 오른쪽 이름(레어도 색)·계열·효과
        //   이전 80x80 정사각 카드는 작고 모양이 뭉개져 알아보기 어려웠음 (사용자 지적 2026-10-30)
        Image btnBg = GetComponent<Image>();
        bool framed = UITheme.Current.ApplyRaritySlot(btnBg, module.rarity);
        if (!framed && btnBg != null) btnBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = "";

        BuildModuleCard(module);
    }

    private void BuildModuleCard(ModuleDataSO module)
    {
        UITheme theme = UITheme.Current;
        RectTransform rect = GetComponent<RectTransform>();
        float boxSize = rect != null ? rect.sizeDelta.y : 76f;

        // 왼쪽 모양 칸 (카드 높이만큼 정사각, SlotContentView가 테두리 여백 안에 모양을 그림)
        GameObject boxObj = new GameObject("ShapeBox", typeof(RectTransform), typeof(Image));
        boxObj.transform.SetParent(transform, false);
        RectTransform box = boxObj.GetComponent<RectTransform>();
        box.anchorMin = new Vector2(0f, 0f);
        box.anchorMax = new Vector2(0f, 1f);
        box.pivot = new Vector2(0f, 0.5f);
        box.sizeDelta = new Vector2(boxSize, 0f);
        box.anchoredPosition = Vector2.zero;
        Image boxImage = boxObj.GetComponent<Image>();
        boxImage.color = Color.clear;
        boxImage.raycastTarget = false;
        SlotContentView view = SlotContentView.Get(boxImage);
        if (view != null) view.ShowModule(module, false);

        // 오른쪽 글자: 이름 / 계열 · 효과(스탯) 또는 대상(작동·특수)
        float textLeft = boxSize + 4f;
        float pad = theme.slotContentPadding;
        TextMeshProUGUI nameText = CreateCardText("Name", new Vector2(0f, 0.5f), new Vector2(1f, 1f),
            new Vector2(textLeft, 0f), new Vector2(-pad, -pad * 0.5f), 20f, TextAlignmentOptions.BottomLeft);
        nameText.text = module.moduleName;
        nameText.color = GetRarityColor(module.rarity);
        nameText.fontStyle = FontStyles.Bold;

        string categoryHex = "#" + ColorUtility.ToHtmlStringRGB(theme.GetCategoryColor(module.category));
        string detail = module.category == ModuleCategory.Stat
            ? ItemTooltipUI.GetModuleStatText(module.statType, module.GetFinalStatValue())
            : ItemTooltipUI.GetTargetName(module.target);
        TextMeshProUGUI subText = CreateCardText("Detail", new Vector2(0f, 0f), new Vector2(1f, 0.5f),
            new Vector2(textLeft, pad * 0.5f), new Vector2(-pad, -2f), 16f, TextAlignmentOptions.TopLeft);
        subText.text = $"<color={categoryHex}>{ItemTooltipUI.GetCategoryName(module.category)}</color>  ·  {detail}";
        subText.color = theme.tooltipLabelColor;
    }

    private TextMeshProUGUI CreateCardText(string objName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
                                           float size, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, UIFontRole.Body);
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    // 예전 미니 모양(MiniShapeContainer) 정리 — 프리팹에 연결돼 있을 수 있어 필드는 유지
    private void ClearMiniShape()
    {
        if (miniShapeParent != null)
        {
            foreach (Transform child in miniShapeParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 버튼 클릭 시 전리품 가방 출처(DragSource.LootList) 지정을 포함하여 드래그 시작
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 손에 든 게 있으면 이 카드를 집지 않고 들고 있던 것을 가방에 넣음
            // (이전: 들고 있던 것을 원래 자리로 취소하고 이 카드를 집음 → 가방이 가득하면 빈 곳이 없어 넣기 어려웠음)
            if (LootPanelDropHandler.DepositHeldItem()) return;

            // ⭐ 전리품 가방 출처(DragSource.LootList) 인자 전달
            if (moduleData != null && ModuleDragHandler.Instance != null)
            {
                ModuleDragHandler.Instance.StartDragModule(moduleData, DragSource.LootList);
            }
            else if (weaponData != null && WeaponDragHandler.Instance != null)
            {
                WeaponDragHandler.Instance.StartDragWeapon(weaponData, DragSource.LootList);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance == null) return;

        if (weaponData != null)
            ItemTooltipUI.Instance.ShowWeaponTooltip(weaponData);
        else if (moduleData != null)
            ItemTooltipUI.Instance.ShowModuleTooltip(moduleData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.HideTooltip();
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return UITheme.Current.GetRarityColor(rarity); // 색은 UI 테마에서 (Resources/UITheme)
    }
}