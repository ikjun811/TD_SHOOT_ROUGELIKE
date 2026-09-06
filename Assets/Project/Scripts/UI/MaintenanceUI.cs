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
    [SerializeField] private Transform lootContainer;          // Content 오브젝트
    [SerializeField] private GameObject lootItemButtonPrefab;

    [Header("Equipped Weapon Slots")]
    [SerializeField] private TextMeshProUGUI weapon1Text;
    [SerializeField] private TextMeshProUGUI weapon2Text;

    [Header("Vault Slots (시각적 금고 슬롯 3개 연결)")]
    [SerializeField] private VaultSlotUI[] vaultSlotUIArray = new VaultSlotUI[3];

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(false);
        }
    }

    public void OpenUI()
    {
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
            GameObject mObj = new GameObject("ModuleSection", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            mObj.transform.SetParent(lootContainer, false);
            moduleSection = mObj.transform;

            RectTransform mRect = mObj.GetComponent<RectTransform>();
            mRect.anchorMin = new Vector2(0f, 1f);
            mRect.anchorMax = new Vector2(1f, 1f);
            mRect.pivot = new Vector2(0.5f, 1f);
            mRect.anchoredPosition = Vector2.zero;

            GridLayoutGroup glg = mObj.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(80f, 80f);
            glg.spacing = new Vector2(6f, 6f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;

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

        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            if (weapon != null)
                CreateWeaponBannerButton(weapon, weaponSection);
        }

        foreach (var module in PlayerInventory.Instance.collectedModules)
        {
            if (module != null)
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

    private void CreateModuleSquareButton(ModuleDataSO module, Transform parent)
    {
        if (lootItemButtonPrefab == null || parent == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(80f, 80f);
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

        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(false);
        }

        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.CompleteMaintenanceAndStartNextRound();
        }
    }

    /// <summary>
    /// 장착 무기 슬롯 및 시각적 금고 슬롯 UI 갱신
    /// </summary>
    public void RefreshEquipAndVaultUI()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.equippedWeapons != null)
        {
            if (weapon1Text != null)
            {
                var w1 = PlayerInventory.Instance.equippedWeapons.Length > 0 ? PlayerInventory.Instance.equippedWeapons[0] : null;
                weapon1Text.text = (w1 != null) ? $"[1] {w1.weaponName}" : "[1] 빈 무기 슬롯";
            }

            if (weapon2Text != null)
            {
                var w2 = PlayerInventory.Instance.equippedWeapons.Length > 1 ? PlayerInventory.Instance.equippedWeapons[1] : null;
                weapon2Text.text = (w2 != null) ? $"[2] {w2.weaponName}" : "[2] 빈 무기 슬롯";
            }
        }

        // 금고 시각적 3개 슬롯 UI 시각화 갱신
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