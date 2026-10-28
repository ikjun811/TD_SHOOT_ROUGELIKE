using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 금고 개별 슬롯의 클릭, 드롭, 마우스 호버 이벤트를 처리하는 컨트롤러 클래스입니다.
/// </summary>
public class VaultSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Vault Slot Index")]
    public int vaultIndex = 0;

    [Header("UI Components")]
    [SerializeField] private Image slotBgImage;
    [SerializeField] private TextMeshProUGUI slotText;

    public void RefreshSlotVisual()
    {
        if (MaintenanceManager.Instance == null || vaultIndex >= MaintenanceManager.Instance.vaultSlots.Length) return;

        var slotData = MaintenanceManager.Instance.vaultSlots[vaultIndex];

        // 테마에 레어도 테두리가 있으면 테두리 + 레어도 색 글자, 없으면 기존처럼 배경을 레어도 색으로
        UITheme theme = UITheme.Current;
        if (slotData.weapon != null || slotData.module != null)
        {
            RarityType rarity = slotData.weapon != null ? slotData.weapon.rarity : slotData.module.rarity;
            bool framed = theme.ApplyRaritySlot(slotBgImage, rarity);
            if (!framed && slotBgImage != null) slotBgImage.color = GetRarityColor(rarity);
            // 그림(무기 아이콘 / 모듈 모양·계열 띠·스탯 아이콘)이 있으면 텍스트 없이 (이름은 툴팁)
            SlotContentView view = SlotContentView.Get(slotBgImage);
            bool hasPicture = view != null && (slotData.weapon != null ? view.ShowWeapon(slotData.weapon) : view.ShowModule(slotData.module));
            if (slotText != null)
            {
                slotText.text = hasPicture ? "" : (slotData.weapon != null ? $"[무기] {slotData.weapon.weaponName}" : $"[모듈] {slotData.module.moduleName}");
                if (framed) slotText.color = GetRarityColor(rarity);
            }
        }
        else
        {
            // 빈 슬롯: 글자 없이 왼쪽 위 작은 번호만 (사용자 결정)
            SlotContentView view = SlotContentView.Get(slotBgImage);
            if (view != null) view.ShowEmpty((vaultIndex + 1).ToString());

            bool framed = theme.ApplyEmptySlot(slotBgImage);
            if (!framed && slotBgImage != null) slotBgImage.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            if (slotText != null) slotText.text = view != null ? "" : $"[{vaultIndex + 1}]";
        }
    }

    /// <summary>
    /// 금고 슬롯 클릭 처리 (보관, 꺼내기, 스왑 시 DragSource.VaultSlot 인자 전달)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (MaintenanceManager.Instance == null) return;

        var slotData = MaintenanceManager.Instance.vaultSlots[vaultIndex];

        // 1. 손에 무기를 들고 금고 슬롯을 클릭한 경우
        if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
        {
            WeaponDataSO newWeapon = WeaponDragHandler.Instance.selectedWeapon;
            WeaponDataSO oldWeapon = slotData.weapon;
            ModuleDataSO oldModule = slotData.module;

            slotData.weapon = newWeapon;
            slotData.module = null;

            WeaponDragHandler.Instance.EndDrag();

            // ⭐ 기존 스왑 아이템 발생 시 DragSource.VaultSlot 전달
            if (oldWeapon != null)
            {
                WeaponDragHandler.Instance.StartDragWeapon(oldWeapon, DragSource.VaultSlot);
            }
            else if (oldModule != null)
            {
                if (ModuleDragHandler.Instance != null)
                    ModuleDragHandler.Instance.StartDragModule(oldModule, DragSource.VaultSlot);
            }

            RefreshAllUI();
        }
        // 2. 손에 모듈을 들고 금고 슬롯을 클릭한 경우
        else if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
        {
            ModuleDataSO newModule = ModuleDragHandler.Instance.selectedModule;
            WeaponDataSO oldWeapon = slotData.weapon;
            ModuleDataSO oldModule = slotData.module;

            slotData.module = newModule;
            slotData.weapon = null;

            ModuleDragHandler.Instance.EndDrag();

            // ⭐ 기존 스왑 아이템 발생 시 DragSource.VaultSlot 전달
            if (oldWeapon != null)
            {
                if (WeaponDragHandler.Instance != null)
                    WeaponDragHandler.Instance.StartDragWeapon(oldWeapon, DragSource.VaultSlot);
            }
            else if (oldModule != null)
            {
                ModuleDragHandler.Instance.StartDragModule(oldModule, DragSource.VaultSlot);
            }

            RefreshAllUI();
        }
        // 3. 맨손 상태에서 금고 슬롯을 클릭하여 아이템을 꺼내는 경우
        else
        {
            // ⭐ 금고에서 꺼낼 때 DragSource.VaultSlot 전달
            if (slotData.weapon != null)
            {
                WeaponDataSO w = slotData.weapon;
                slotData.Clear();
                WeaponDragHandler.Instance.StartDragWeapon(w, DragSource.VaultSlot);
                RefreshAllUI();
            }
            else if (slotData.module != null)
            {
                ModuleDataSO m = slotData.module;
                slotData.Clear();
                ModuleDragHandler.Instance.StartDragModule(m, DragSource.VaultSlot);
                RefreshAllUI();
            }
        }
    }

    private void RefreshAllUI()
    {
        if (MaintenanceUI.Instance != null)
        {
            MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            MaintenanceUI.Instance.RefreshLootPanel();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 무기·모듈을 들고 있으면 놓을 자리 표시
        bool dragging = (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            || (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging);
        if (dragging)
        {
            SlotContentView view = SlotContentView.Get(slotBgImage);
            if (view != null) view.SetDropHighlight(true);
            return;
        }

        if (ItemTooltipUI.Instance == null || MaintenanceManager.Instance == null) return;

        var slotData = MaintenanceManager.Instance.vaultSlots[vaultIndex];
        if (slotData.weapon != null)
        {
            ItemTooltipUI.Instance.ShowWeaponTooltip(slotData.weapon);
        }
        else if (slotData.module != null)
        {
            ItemTooltipUI.Instance.ShowModuleTooltip(slotData.module);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SlotContentView view = slotBgImage != null ? slotBgImage.GetComponent<SlotContentView>() : null;
        if (view != null) view.SetDropHighlight(false);

        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return UITheme.Current.GetRarityColor(rarity); // 색은 UI 테마에서 (Resources/UITheme)
    }
}