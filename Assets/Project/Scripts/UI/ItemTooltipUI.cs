using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
        if (tooltipPanel.activeSelf)
        {
            // 마우스 커서 위치를 따라다니는 툴팁
            Vector2 mousePos = Input.mousePosition;
            transform.position = mousePos + new Vector2(15f, -15f);
        }
    }

    // 무기 정보 툴팁 표시
    public void ShowWeaponTooltip(WeaponDataSO weapon)
    {
        if (weapon == null) return;

        tooltipPanel.SetActive(true);
        itemNameText.text = weapon.weaponName;
        itemNameText.color = weapon.GetRarityColor();
        itemRarityText.text = $"[{weapon.rarity}] {weapon.weaponType}";

        string traitsStr = weapon.traits.Count > 0 ? string.Join(", ", weapon.traits) : "없음";
        itemStatsText.text = $"• 공격력: {weapon.baseDamage}\n" +
                             $"• 연사 간격: {weapon.fireRate}초\n" +
                             $"• 탄창: {weapon.maxAmmo}발\n" +
                             $"• [고유 특성]: {traitsStr}";

        itemDescriptionText.text = "주요 전투에 사용되는 기본 장착 무기입니다.";
    }

    // 강화모듈 정보 툴팁 표시
    public void ShowModuleTooltip(ModuleDataSO module)
    {
        if (module == null) return;

        tooltipPanel.SetActive(true);
        itemNameText.text = module.moduleName;
        itemNameText.color = GetRarityColor(module.rarity);
        itemRarityText.text = $"[{module.rarity}] 강화 모듈 ({module.width}x{module.height})";

        float finalStat = module.GetFinalStatValue();
        string statSign = module.statType.ToString().Contains("Percent") ? $"{finalStat * 100}%" : $"+{finalStat}";

        itemStatsText.text = $"• 스탯 효과: {module.statType} [{statSign}]\n" +
                             $"• 점유 공간: {module.width * module.height} 칸";

        itemDescriptionText.text = module.isUniqueModule ? "★ 유니크 교차 메커니즘 모듈" : "그리드에 배치 시 스탯이 적용됩니다.";
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
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