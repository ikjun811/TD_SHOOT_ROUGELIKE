using UnityEngine;
using UnityEngine.UI;
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

    private RectTransform panelRect;
    private Image divider;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        SetupLayout();
        HideTooltip();
    }

    // U6a: 씬의 고정 크기(280x220) 대신 폭은 테마 값, 높이는 내용에 맞춰 늘어나도록 레이아웃 설정
    // 순서: 이름 → 레어도·종류 → 구분선 → 수치 → 설명
    private void SetupLayout()
    {
        if (tooltipPanel == null) return;
        UITheme theme = UITheme.Current;
        panelRect = tooltipPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f); // 늘림 앵커면 폭 지정이 달라지므로 고정
        panelRect.pivot = new Vector2(0f, 1f); // 왼쪽 위 기준 (화면 가장자리 판단용)
        panelRect.sizeDelta = new Vector2(theme.tooltipWidth, panelRect.sizeDelta.y);

        VerticalLayoutGroup layout = tooltipPanel.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = tooltipPanel.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(theme.tooltipPadding);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = theme.tooltipSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = tooltipPanel.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = tooltipPanel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 툴팁이 마우스를 가려 호버가 끊기는(깜빡임) 일 방지
        Image background = tooltipPanel.GetComponent<Image>();
        if (background != null) background.raycastTarget = false;

        // 구분선 (코드 생성)
        GameObject dividerObj = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        dividerObj.transform.SetParent(tooltipPanel.transform, false);
        divider = dividerObj.GetComponent<Image>();
        divider.color = theme.tooltipDividerColor;
        divider.raycastTarget = false;
        dividerObj.GetComponent<LayoutElement>().preferredHeight = 2f;
        dividerObj.SetActive(theme.tooltipDividerColor.a > 0f);

        SetupText(itemNameText, theme.tooltipTitleFontSize, 0);
        SetupText(itemRarityText, theme.tooltipBodyFontSize, 1);
        dividerObj.transform.SetSiblingIndex(2);
        SetupText(itemStatsText, theme.tooltipBodyFontSize, 3);
        SetupText(itemDescriptionText, theme.tooltipBodyFontSize, 4);
        if (itemRarityText != null) itemRarityText.color = theme.tooltipSubTextColor;
        if (itemDescriptionText != null) itemDescriptionText.color = theme.tooltipSubTextColor;
    }

    private void SetupText(TextMeshProUGUI text, float fontSize, int order)
    {
        if (text == null) return;
        text.transform.SetSiblingIndex(order);
        if (fontSize > 0f) text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        // 씬에서 고정 높이를 준 LayoutElement가 있으면 해제 (TMP 선호 높이를 쓰도록)
        LayoutElement element = text.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minHeight = -1f;
            element.preferredHeight = -1f;
            element.flexibleHeight = -1f;
        }
    }

    // 내용이 바뀐 직후 크기 다시 계산 + 위치 (한 프레임 이전 위치에 보이는 것 방지)
    private void RefreshLayout()
    {
        if (itemDescriptionText != null) itemDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(itemDescriptionText.text));
        if (panelRect == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        FollowMouse();
    }

    // 마우스 오른쪽 아래에 표시, 화면 오른쪽·아래를 넘으면 마우스 반대쪽으로
    private void FollowMouse()
    {
        if (panelRect == null) return;
        Vector2 offset = UITheme.Current.tooltipCursorOffset;
        Vector2 mouse = Input.mousePosition;
        Vector2 size = panelRect.rect.size * (Vector2)panelRect.lossyScale; // 화면 픽셀 크기 (Screen Space Overlay 기준)

        float x = mouse.x + offset.x;
        if (x + size.x > Screen.width) x = mouse.x - offset.x - size.x;
        float y = mouse.y - offset.y; // 패널 위쪽 변
        if (y - size.y < 0f) y = mouse.y + offset.y + size.y;

        x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - size.x));
        y = Mathf.Clamp(y, Mathf.Min(size.y, Screen.height), Screen.height);
        panelRect.position = new Vector2(x, y);
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
            FollowMouse();
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
            // 지금 그리드 기준 최종 수치 (실제 적용과 같은 계산). 플레이어가 없으면 기본값만
            if (PlayerStatAndWeaponSync.Instance != null && PlayerStatAndWeaponSync.Instance.TryPreviewWeaponStats(weapon, out WeaponStatCalculator.Result stats))
            {
                string pellets = stats.pellets > 1 ? $" × {stats.pellets}발" : "";
                string text = StatLine("발당 피해", weapon.GetFinalDamage(), stats.damage, "0.#", "", true) + pellets + "\n"
                            + StatLine("연사 간격", weapon.fireRate, stats.FireInterval, "0.###", "초", false) + "\n"
                            + StatLine("탄창", weapon.maxAmmo, stats.magazine, "0", "발", true) + "\n"
                            + StatLine("장전", weapon.reloadTime, stats.FinalReloadTime, "0.##", "초", false) + "\n"
                            + $"• 사격: {GetFireModeName(stats.fireMode)}";
                if (stats.critChance > 0f) text += $" · 치명타 {stats.critChance * 100f:0.#}%";
                if (stats.mechanisms.Count > 0)
                {
                    text += "\n• 작동: " + string.Join(", ", stats.mechanisms.ConvertAll(m => m.moduleName));
                }
                itemStatsText.text = text;
            }
            else
            {
                itemStatsText.text = $"• 공격력: {weapon.GetFinalDamage():0.#}\n" +
                                     $"• 연사 간격: {weapon.fireRate}초\n" +
                                     $"• 탄창: {weapon.maxAmmo}발";
            }
        }

        if (itemDescriptionText != null) itemDescriptionText.text = "주요 전투에 사용되는 기본 장착 무기입니다.";
        RefreshLayout();
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
        RefreshLayout();
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

    // 값이 바뀌었으면 "기본 → 최종" (좋아지면 초록, 나빠지면 빨강), 같으면 최종값만
    private static string StatLine(string label, float baseValue, float finalValue, string format, string unit, bool higherIsBetter)
    {
        string finalText = finalValue.ToString(format) + unit;
        if (Mathf.Abs(finalValue - baseValue) < 0.0005f) return $"• {label}: {finalText}";

        bool better = higherIsBetter ? finalValue > baseValue : finalValue < baseValue;
        string color = better ? "#7CFC9A" : "#FF7070";
        return $"• {label}: {baseValue.ToString(format)}{unit} → <color={color}>{finalText}</color>";
    }

    private static string GetFireModeName(JUTPS.WeaponSystem.Weapon.WeaponFireMode mode)
    {
        return mode switch
        {
            JUTPS.WeaponSystem.Weapon.WeaponFireMode.Auto => "자동",
            JUTPS.WeaponSystem.Weapon.WeaponFireMode.SemiAuto => "반자동",
            JUTPS.WeaponSystem.Weapon.WeaponFireMode.BoltAction => "볼트액션",
            JUTPS.WeaponSystem.Weapon.WeaponFireMode.Shotgun => "산탄",
            _ => mode.ToString()
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