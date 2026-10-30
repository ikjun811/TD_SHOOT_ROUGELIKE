using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 정비 단계 UI 메인 제어 클래스입니다.
/// </summary>
public class MaintenanceUI : MonoBehaviour
{
    public static MaintenanceUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject maintenancePanel;
    [SerializeField] private Transform lootContainer;
    [SerializeField] private GameObject lootItemButtonPrefab;

    [Header("Equipped Weapon Slots (시각적 배경 및 텍스트)")]
    [SerializeField] private Image weapon1SlotImage;
    [SerializeField] private TextMeshProUGUI weapon1Text;
    [SerializeField] private Image weapon2SlotImage;
    [SerializeField] private TextMeshProUGUI weapon2Text;

    [Header("Vault Slots")]
    [SerializeField] private VaultSlotUI[] vaultSlotUIArray = new VaultSlotUI[3];

    [Header("Warning (선택 연결)")]
    [SerializeField] private TextMeshProUGUI warningText; // 장착 무기 없이 NEXT ROUND 시 경고 표시

    [Header("조작 안내 (비우면 정비 패널 아래 가운데에 자동 생성)")]
    [SerializeField] private TextMeshProUGUI controlsHintText;
    [SerializeField] private Vector2 controlsHintPosition = new Vector2(0f, 18f); // 패널 아래 가운데 기준
    [SerializeField] private string controlsHint =
        "<b>좌클릭</b> 집기·놓기     <b>R</b> 모듈 회전     <b>우클릭</b> 취소     <b>휠클릭</b> 즉시 해제(가방으로)";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        CreateControlsHint();
    }

    // 정비 패널 아래에 조작 키 안내 한 줄 (흐린 글자)
    private void CreateControlsHint()
    {
        if (controlsHintText == null && maintenancePanel != null)
        {
            GameObject obj = new GameObject("ControlsHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(maintenancePanel.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = controlsHintPosition;
            rect.sizeDelta = new Vector2(1400f, 32f);
            controlsHintText = obj.GetComponent<TextMeshProUGUI>();
            UITheme.Current.ApplyFont(controlsHintText, UIFontRole.Body);
            controlsHintText.fontSize = 20f;
            controlsHintText.alignment = TextAlignmentOptions.Center;
            controlsHintText.color = new Color(0.75f, 0.82f, 0.88f, 0.75f);
            controlsHintText.raycastTarget = false;
            controlsHintText.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (controlsHintText != null) controlsHintText.text = controlsHint;

        // 경고 글이 씬에 연결돼 있지 않으면 조작 안내 바로 위에 생성 (이전엔 NEXT ROUND가 막혀도 아무 표시가 없었음)
        if (warningText == null && maintenancePanel != null)
        {
            GameObject obj = new GameObject("NextRoundWarning", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(maintenancePanel.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = controlsHintPosition + new Vector2(0f, 38f);
            rect.sizeDelta = new Vector2(1400f, 36f);
            warningText = obj.GetComponent<TextMeshProUGUI>();
            UITheme.Current.ApplyFont(warningText, UIFontRole.Body);
            warningText.fontSize = 26f;
            warningText.fontStyle = FontStyles.Bold;
            warningText.alignment = TextAlignmentOptions.Center;
            warningText.color = new Color(1f, 0.4f, 0.35f, 1f);
            warningText.raycastTarget = false;
            warningText.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    private void Start()
    {
        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(false);
        }

        SetWarning(false);
    }

    public void OpenUI()
    {
        SetWarning(false);

        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(true);
            Debug.Log("MaintenanceUI: Opened.");
        }

        RefreshLootPanel();
        RefreshEquipAndVaultUI();
    }

    public void RefreshLootPanel()
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }

        if (lootContainer == null) return;

        Transform weaponSection = lootContainer.Find("WeaponSection");
        Transform moduleSection = lootContainer.Find("ModuleSection");

        if (weaponSection == null)
        {
            GameObject wObj = new GameObject("WeaponSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            wObj.transform.SetParent(lootContainer, false);
            weaponSection = wObj.transform;

            RectTransform wRect = wObj.GetComponent<RectTransform>();
            wRect.anchorMin = new Vector2(0f, 1f);
            wRect.anchorMax = new Vector2(1f, 1f);
            wRect.pivot = new Vector2(0.5f, 1f);
            wRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = wObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = wObj.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (moduleSection == null)
        {
            // F4c-5: 모듈도 무기처럼 한 줄에 하나씩 가로 카드 (이전: 80x80 정사각 3열)
            GameObject mObj = new GameObject("ModuleSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            mObj.transform.SetParent(lootContainer, false);
            moduleSection = mObj.transform;

            RectTransform mRect = mObj.GetComponent<RectTransform>();
            mRect.anchorMin = new Vector2(0f, 1f);
            mRect.anchorMax = new Vector2(1f, 1f);
            mRect.pivot = new Vector2(0.5f, 1f);
            mRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup mvlg = mObj.GetComponent<VerticalLayoutGroup>();
            mvlg.spacing = 6f;
            mvlg.childControlWidth = true;
            mvlg.childForceExpandWidth = true;
            mvlg.childControlHeight = false;
            mvlg.childForceExpandHeight = false;

            ContentSizeFitter csf = mObj.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        for (int i = weaponSection.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(weaponSection.GetChild(i).gameObject);
        }
        for (int i = moduleSection.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(moduleSection.GetChild(i).gameObject);
        }

        if (PlayerInventory.Instance == null) return;

        // 표시 순서: 레어도 높은 순 → 계열(모듈: 스탯·작동·특수·유니크 / 무기: 무기군) → 이름 (인벤토리 목록 자체는 그대로)
        List<WeaponDataSO> weapons = PlayerInventory.Instance.collectedWeapons.FindAll(w => w != null);
        weapons.Sort((a, b) =>
        {
            int c = b.rarity.CompareTo(a.rarity);
            if (c == 0) c = a.weaponType.CompareTo(b.weaponType);
            if (c == 0) c = string.CompareOrdinal(a.weaponName, b.weaponName);
            return c;
        });
        foreach (var weapon in weapons)
        {
            CreateWeaponBannerButton(weapon, weaponSection);
        }

        List<ModuleDataSO> modules = PlayerInventory.Instance.collectedModules.FindAll(m => m != null);
        modules.Sort((a, b) =>
        {
            int c = b.rarity.CompareTo(a.rarity);
            if (c == 0) c = a.category.CompareTo(b.category);
            if (c == 0) c = string.CompareOrdinal(a.moduleName, b.moduleName);
            return c;
        });
        foreach (var module in modules)
        {
            CreateModuleSquareButton(module, moduleSection);
        }

        Canvas.ForceUpdateCanvases();
        if (weaponSection != null) LayoutRebuilder.ForceRebuildLayoutImmediate(weaponSection.GetComponent<RectTransform>());
        if (moduleSection != null) LayoutRebuilder.ForceRebuildLayoutImmediate(moduleSection.GetComponent<RectTransform>());
        if (lootContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(lootContainer.GetComponent<RectTransform>());
    }

    private void CreateWeaponBannerButton(WeaponDataSO weapon, Transform parent)
    {
        if (lootItemButtonPrefab == null || parent == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(260f, 85f);
        }

        Image bgImage = btnObj.GetComponent<Image>();
        TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

        LootItemButtonUI hoverUI = btnObj.GetComponent<LootItemButtonUI>();
        if (hoverUI == null) hoverUI = btnObj.AddComponent<LootItemButtonUI>();

        if (bgImage != null) bgImage.color = weapon.GetRarityColor();
        if (nameText != null)
        {
            nameText.text = $"[{weapon.weaponType}]\n{weapon.weaponName}";
            nameText.fontSize = 14;
            nameText.alignment = TextAlignmentOptions.Left;
        }

        hoverUI.Setup(weapon);
    }

    private const float ModuleCardHeight = 76f;

    private void CreateModuleSquareButton(ModuleDataSO module, Transform parent)
    {
        if (lootItemButtonPrefab == null || parent == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(260f, ModuleCardHeight); // 폭은 레이아웃이 패널에 맞춤
        }

        Image bgImage = btnObj.GetComponent<Image>();
        TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

        LootItemButtonUI hoverUI = btnObj.GetComponent<LootItemButtonUI>();
        if (hoverUI == null) hoverUI = btnObj.AddComponent<LootItemButtonUI>();

        Color rarityColor = GetRarityColor(module.rarity);
        if (bgImage != null) bgImage.color = rarityColor;
        if (nameText != null) nameText.text = module.moduleName;

        hoverUI.Setup(module);
    }

    public void OnClickStartNextRound()
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }

        if (MaintenanceManager.Instance == null) return;

        bool started = MaintenanceManager.Instance.CompleteMaintenanceAndStartNextRound();

        if (started)
        {
            SetWarning(false);
            if (maintenancePanel != null)
            {
                maintenancePanel.SetActive(false);
            }
        }
        else
        {
            // 들고 있던 아이템이 출처로 복귀했을 수 있으므로 화면 갱신
            RefreshLootPanel();
            RefreshEquipAndVaultUI();
            SetWarning(true);
        }
    }

    private void SetWarning(bool show)
    {
        if (warningText == null) return;

        string reason = MaintenanceManager.Instance != null ? MaintenanceManager.Instance.LastBlockReason : null;
        warningText.text = string.IsNullOrEmpty(reason) ? "무기를 1개 이상 장착하세요" : reason;
        warningText.gameObject.SetActive(show);
    }

    // 장착 슬롯 놓을 자리 표시 (WeaponSlotUI가 무기를 든 채 마우스를 올리면 호출)
    public void SetEquipSlotDropHighlight(int slotIndex, bool on)
    {
        Image slotImage = slotIndex == 0 ? weapon1SlotImage : (slotIndex == 1 ? weapon2SlotImage : null);
        SlotContentView view = SlotContentView.Get(slotImage);
        if (view != null) view.SetDropHighlight(on);
    }

    // 그리드에 배치된 이 무기군 대상 작동 모듈 효과
    private static List<ModuleEffect> GetActiveMechanismEffects(WeaponType weaponType)
    {
        List<ModuleEffect> effects = new List<ModuleEffect>();
        if (GridInventorySystem.Instance == null) return effects;

        foreach (var pm in GridInventorySystem.Instance.placedModules)
        {
            ModuleDataSO m = pm.moduleData;
            if (m != null && m.category == ModuleCategory.Mechanism && m.TargetsWeapon(weaponType) && !effects.Contains(m.effect)) effects.Add(m.effect);
        }
        return effects;
    }

    // 모듈 배치·제거 시 장착 슬롯 아이콘(작동 모듈 변형) 갱신
    private void OnEnable()
    {
        GridInventorySystem.OnModulesChanged += RefreshEquipAndVaultUI;
    }

    private void OnDisable()
    {
        GridInventorySystem.OnModulesChanged -= RefreshEquipAndVaultUI;
    }

    // 장착 슬롯 1개: 테마에 레어도 테두리가 있으면 테두리 + 레어도 색 글자, 없으면 기존처럼 배경을 레어도 색으로
    private void RefreshEquipSlot(Image slotImage, TextMeshProUGUI slotText, WeaponDataSO weapon, int slotNumber)
    {
        UITheme theme = UITheme.Current;
        if (weapon != null)
        {
            bool framed = theme.ApplyRaritySlot(slotImage, weapon.rarity);
            if (!framed && slotImage != null) slotImage.color = weapon.GetRarityColor();

            // 무기 아이콘이 있으면 그림만 (이름·수치는 툴팁). 장착된 작동 모듈에 맞는 변형 아이콘, 모양에 반영 안 된 작동 모듈은 구석 표시
            List<ModuleEffect> mechanisms = GetActiveMechanismEffects(weapon.weaponType);
            Sprite icon = weapon.GetIcon(mechanisms, out int covered);
            SlotContentView view = SlotContentView.Get(slotImage);
            bool hasIcon = view != null && view.ShowWeapon(weapon, icon, mechanisms.Count > covered);
            if (slotText != null)
            {
                slotText.text = hasIcon ? "" : $"[{weapon.weaponType}]\n{weapon.weaponName}";
                slotText.color = framed ? weapon.GetRarityColor() : Color.black;
            }
        }
        else
        {
            // 빈 슬롯: 글자 없이 왼쪽 위 작은 번호만 (사용자 결정)
            SlotContentView view = SlotContentView.Get(slotImage);
            if (view != null) view.ShowEmpty(slotNumber.ToString());

            bool framed = theme.ApplyEmptySlot(slotImage);
            if (!framed && slotImage != null) slotImage.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            if (slotText != null) slotText.text = view != null ? "" : $"[{slotNumber}]";
        }
    }

    /// <summary>
    /// 장착 무기 슬롯 배경색 및 텍스트 시각화 갱신
    /// </summary>
    public void RefreshEquipAndVaultUI()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.equippedWeapons != null)
        {
            // 주무기 슬롯 1·2번 시각화
            var w1 = PlayerInventory.Instance.equippedWeapons.Length > 0 ? PlayerInventory.Instance.equippedWeapons[0] : null;
            var w2 = PlayerInventory.Instance.equippedWeapons.Length > 1 ? PlayerInventory.Instance.equippedWeapons[1] : null;
            RefreshEquipSlot(weapon1SlotImage, weapon1Text, w1, 1);
            RefreshEquipSlot(weapon2SlotImage, weapon2Text, w2, 2);
        }

        // 금고 슬롯 시각화
        for (int i = 0; i < vaultSlotUIArray.Length; i++)
        {
            if (vaultSlotUIArray[i] != null)
            {
                vaultSlotUIArray[i].RefreshSlotVisual();
            }
        }
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return UITheme.Current.GetRarityColor(rarity); // 색은 UI 테마에서 (Resources/UITheme)
    }
}