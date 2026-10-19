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
        if (itemRarityText != null) itemRarityText.text = $"[{GetRarityName(weapon.rarity)}] {GetWeaponTypeName(weapon.weaponType)}";

        if (itemStatsText != null)
        {
            itemStatsText.text = $"• 공격력: {weapon.GetFinalDamage():0.#}\n" +
                                 $"• 연사 간격: {weapon.fireRate}초\n" +
                                 $"• 탄창: {weapon.maxAmmo}발";
        }

        if (itemDescriptionText != null) itemDescriptionText.text = "주요 전투에 사용되는 기본 장착 무기입니다.";
    }

    // 강화모듈 정보 툴팁 표시 (placed: 그리드에 배치된 모듈에 마우스를 올렸을 때)
    public void ShowModuleTooltip(ModuleDataSO module, GridInventorySystem.PlacedModule placed = null)
    {
        if (module == null || tooltipPanel == null) return;

        tooltipPanel.SetActive(true);
        if (itemNameText != null)
        {
            itemNameText.text = module.moduleName;
            itemNameText.color = GetRarityColor(module.rarity);
        }
        if (itemRarityText != null) itemRarityText.text = $"[{GetRarityName(module.rarity)}] {GetModuleTypeText(module)} ({module.width}x{module.height})";

        // 모양에서 실제로 채워진 칸 수 (L자 등은 가로x세로보다 작음)
        int cellCount = 0;
        if (module.shapeGrid != null)
        {
            foreach (bool filled in module.shapeGrid)
            {
                if (filled) cellCount++;
            }
        }

        if (itemStatsText != null)
        {
            string text = "";
            if (module.category == ModuleCategory.Stat)
            {
                text += $"• 스탯 효과: {GetModuleStatText(module.statType, module.GetFinalStatValue())}\n";
            }
            text += $"• 점유 공간: {cellCount} 칸";
            text += GetPlacementStatusText(module, placed);
            itemStatsText.text = text;
        }

        if (itemDescriptionText != null)
        {
            if (!string.IsNullOrEmpty(module.description)) itemDescriptionText.text = module.description;
            else itemDescriptionText.text = module.category == ModuleCategory.Stat ? "그리드에 배치 시 스탯이 적용됩니다." : "효과 구현 예정";
        }
    }

    // 충돌·중복 상태 (빨간 글씨)
    private string GetPlacementStatusText(ModuleDataSO module, GridInventorySystem.PlacedModule placed)
    {
        var grid = GridInventorySystem.Instance;
        if (grid == null) return "";

        if (placed != null)
        {
            // 배치된 모듈: 같은 효과 중 적용되지 않는 쪽이면 표시
            if (grid.FindSameEffect(module, placed) != null && !grid.IsEffectActive(placed))
                return "\n<color=#FF5555>• 중복 – 효과 없음 (레어도 높은 1개만 적용)</color>";
            return "";
        }

        var conflict = grid.FindConflict(module);
        if (conflict != null)
            return $"\n<color=#FF5555>• 충돌: 배치된 {conflict.moduleData.moduleName} (같은 {GetSlotName(module.mechanismSlot)} 칸) – 배치 불가</color>";

        if (grid.FindSameEffect(module) != null)
            return "\n<color=#FF5555>• 중복: 같은 효과가 이미 배치됨 (레어도 높은 1개만 적용)</color>";

        return "";
    }

    // 예: "스탯 모듈", "작동 · UMP 전용 · 사격 방식", "특수 · 공용"
    private static string GetModuleTypeText(ModuleDataSO module)
    {
        string category = module.category switch
        {
            ModuleCategory.Stat => "스탯",
            ModuleCategory.Mechanism => "작동",
            ModuleCategory.Special => "특수",
            ModuleCategory.Unique => "★ 유니크",
            _ => module.category.ToString()
        };
        if (module.category == ModuleCategory.Stat) return $"{category} 모듈";

        string text = $"{category} · {GetTargetName(module.target)}";
        if (module.mechanismSlot != MechanismSlot.None)
            text += $" · {GetSlotName(module.mechanismSlot)}";
        return text;
    }

    public static string GetTargetName(ModuleTarget target)
    {
        return target switch
        {
            ModuleTarget.All => "공용",
            ModuleTarget.SMG => "UMP 전용",
            ModuleTarget.Pistol => "P226 전용",
            ModuleTarget.DualPistols => "P226 Dual 전용",
            ModuleTarget.Shotgun => "Benelli 전용",
            ModuleTarget.Sniper => "M82 전용",
            ModuleTarget.Grenade => "수류탄",
            ModuleTarget.Character => "캐릭터",
            _ => target.ToString()
        };
    }

    private static string GetSlotName(MechanismSlot slot)
    {
        return slot switch
        {
            MechanismSlot.FireMode => "사격 방식",
            MechanismSlot.Magazine => "탄창",
            MechanismSlot.Reload => "장전",
            MechanismSlot.GrenadeThrow => "투척 방식",
            MechanismSlot.BulletPath => "탄 궤적",
            _ => ""
        };
    }

    private static string GetRarityName(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => "일반",
            RarityType.Rare => "희귀",
            RarityType.Elite => "엘리트",
            RarityType.Legendary => "레전더리",
            _ => rarity.ToString()
        };
    }

    private static string GetWeaponTypeName(WeaponType type)
    {
        return type switch
        {
            WeaponType.SMG => "기관단총",
            WeaponType.Pistol => "권총",
            WeaponType.DualPistols => "쌍권총",
            WeaponType.Shotgun => "산탄총",
            WeaponType.Sniper => "저격총",
            _ => type.ToString()
        };
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            tooltipPanel.SetActive(false);
        }
    }

    // 모듈 스탯 한글 표시 (% 스탯은 0.1 = 10%, 체력·방어력은 고정 수치). 적용 효과 패널(ActiveEffectsPanelUI)도 사용
    public static string GetModuleStatText(ModuleStatType statType, float value)
    {
        float percent = value * 100f;
        return statType switch
        {
            ModuleStatType.AttackDamagePercent => $"공격력 +{percent:0.#}%",
            ModuleStatType.MaxHealthBonus => $"최대 체력 +{value:0.#}",
            ModuleStatType.ArmorBonus => $"방어력 +{value:0.#}",
            ModuleStatType.FireRatePercent => $"연사력 +{percent:0.#}%",
            ModuleStatType.ReloadSpeedPercent => $"장전 속도 +{percent:0.#}%",
            ModuleStatType.CriticalChance => $"치명타 확률 +{percent:0.#}%",
            ModuleStatType.MoveSpeedPercent => $"이동 속도 +{percent:0.#}%",
            ModuleStatType.SkillCooldownReduce => $"수류탄 쿨타임 -{percent:0.#}%",
            _ => $"{statType} +{value:0.##}"
        };
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return UITheme.Current.GetRarityColor(rarity); // 색은 UI 테마에서 (Resources/UITheme)
    }
}