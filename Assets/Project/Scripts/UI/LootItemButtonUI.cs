using UnityEngine;
using UnityEngine.EventSystems;

public class LootItemButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    // 마우스가 버튼 위에 올라갔을 때 툴팁 출력
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance == null) return;

        if (weaponData != null)
        {
            ItemTooltipUI.Instance.ShowWeaponTooltip(weaponData);
        }
        else if (moduleData != null)
        {
            ItemTooltipUI.Instance.ShowModuleTooltip(moduleData);
        }
    }

    // 마우스가 버튼에서 나갔을 때 툴팁 숨김
    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }
    }
}