using UnityEngine;
using UnityEngine.EventSystems;

public class LootItemButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private WeaponDataSO weaponData;
    private ModuleDataSO moduleData;

    public void Setup(WeaponDataSO weapon)
    {
        weaponData = weapon;
        moduleData = null;
    }

    public void Setup(ModuleDataSO module)
    {
        moduleData = module;
        weaponData = null;
    }

    //  버튼 클릭 시 모듈 선택/드래그 시작
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (moduleData != null && ModuleDragHandler.Instance != null)
            {
                ModuleDragHandler.Instance.StartDragModule(moduleData);
            }
            else if (weaponData != null)
            {
                Debug.Log($"⚔️ [무기 클릭] {weaponData.weaponName}");
            }
        }
    }

    // 마우스 호버 툴팁
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
}
