using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemRarityText;
    [SerializeField] private TextMeshProUGUI itemStatsText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;

    private RectTransform rectTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rectTransform = GetComponent<RectTransform>();
        HideTooltip();
    }

    private void Update()
    {
        // ⭐ [안전망 1] 무기나 모듈을 마우스로 잡고/드래그 중일 때는 툴팁 무조건 숨김!
        bool isDraggingModule = ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging;
        bool isDraggingWeapon = WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging;

        if (isDraggingModule || isDraggingWeapon)
        {
            HideTooltip();
            return;
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            // 마우스 커서 추종
            Vector2 mousePos = Input.mousePosition;
            transform.position = mousePos + new Vector2(15f, -15f);
        }
    }

    // 무기 정보 툴팁 표시
    public void ShowWeaponTooltip(WeaponDataSO weapon)
    {
        if (weapon == null || tooltipPanel == null) return;

        tooltipPanel.SetActive(true);
        if (itemNameText != null)
        {
            itemNameText.text = weapon.weaponName;
            itemNameText.color = weapon.GetRarityColor();
        }
        if (itemRarityText != null) itemRarityText.text = $"[{weapon.rarity}] {weapon.weaponType}";

        string traitsStr = weapon.traits.Count > 0 ? string.Join(", ", weapon.traits) : "없음";
        if (itemStatsText != null)
        {
            itemStatsText.text = $"• 공격력: {weapon.baseDamage}\n" +
                                 $"• 연사 간격: {weapon.fireRate}초\n" +
                                 $"• 탄창: {weapon.maxAmmo}발\n" +
                                 $"• [고유 특성]: {traitsStr}";
        }

        if (itemDescriptionText != null) itemDescriptionText.text = "주요 전투에 사용되는 기본 장착 무기입니다.";
    }

    // 강화모듈 정보 툴팁 표시
    public void ShowModuleTooltip(ModuleDataSO module)
    {
        if (module == null || tooltipPanel == null) return;

        tooltipPanel.SetActive(true);
        if (itemNameText != null)
        {
            itemNameText.text = module.moduleName;
            itemNameText.color = GetRarityColor(module.rarity);
        }
        if (itemRarityText != null) itemRarityText.text = $"[{module.rarity}] 강화 모듈 ({module.width}x{module.height})";

        float finalStat = module.GetFinalStatValue();
        string statSign = module.statType.ToString().Contains("Percent") ? $"{finalStat * 100}%" : $"+{finalStat}";

        if (itemStatsText != null)
        {
            itemStatsText.text = $"• 스탯 효과: {module.statType} [{statSign}]\n" +
                                 $"• 점유 공간: {module.width * module.height} 칸";
        }

        if (itemDescriptionText != null) itemDescriptionText.text = module.isUniqueModule ? "★ 유니크 교차 메커니즘 모듈" : "그리드에 배치 시 스탯이 적용됩니다.";
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => Color.white,
            RarityType.Rare => new Color(0.2f, 0.6f, 1f),
            RarityType.Elite => new Color(0.7f, 0.3f, 0.9f),
            RarityType.Legendary => new Color(1f, 0.8f, 0.1f),
            _ => Color.white
        };
    }
}