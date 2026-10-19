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

        // U6b: 금고 슬롯과 같은 모습: 레어도 테두리 + 모양 칸·계열 색 띠·스탯 아이콘 (이름은 툴팁). 테마가 없으면 기존 색·글자
        Image btnBg = GetComponent<Image>();
        bool framed = UITheme.Current.ApplyRaritySlot(btnBg, module.rarity);
        if (!framed && btnBg != null) btnBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        SlotContentView view = SlotContentView.Get(btnBg);
        bool hasPicture = view != null && view.ShowModule(module);

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = hasPicture ? "" : module.moduleName;
            btnText.fontSize = 11;
            btnText.color = GetRarityColor(module.rarity);
            btnText.alignment = TextAlignmentOptions.Bottom;
        }
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
            if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
            {
                ModuleDragHandler.Instance.CancelDrag();
            }

            if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            {
                WeaponDragHandler.Instance.CancelDrag();
            }

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