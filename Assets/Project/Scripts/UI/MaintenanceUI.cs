using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaintenanceUI : MonoBehaviour
{
    public static MaintenanceUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject maintenancePanel;
    [SerializeField] private Transform lootContainer;          // Content 오브젝트 연결 ⭐
    [SerializeField] private GameObject lootItemButtonPrefab;

    [Header("Equipped & Vault Slots")]
    [SerializeField] private TextMeshProUGUI weapon1Text;
    [SerializeField] private TextMeshProUGUI weapon2Text;
    [SerializeField] private TextMeshProUGUI vaultStatusText;

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
            Debug.Log("🛠️ [MaintenanceUI] 정비 UI 패널이 활성화되었습니다!");
        }

        RefreshLootPanel();
        RefreshEquipAndVaultUI();
    }

    // ⭐ [완벽 해결] 즉시 강제 레이아웃 재계산(LayoutRebuilder)으로 쏠림/겹침 100% 원천 차단!
    public void RefreshLootPanel()
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }

        if (lootContainer == null) return;

        // 1. Content 내부 서브 컨테이너 (무기 / 모듈) 찾기 또는 생성
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

        // 기존 자식 청소 (DestroyImmediate 사용으로 즉시 삭제 ⭐)
        for (int i = weaponSection.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(weaponSection.GetChild(i).gameObject);
        }
        for (int i = moduleSection.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(moduleSection.GetChild(i).gameObject);
        }

        if (PlayerInventory.Instance == null) return;

        // 2. 무기류 생성
        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            if (weapon != null)
                CreateWeaponBannerButton(weapon, weaponSection);
        }

        // 3. 모듈류 생성
        foreach (var module in PlayerInventory.Instance.collectedModules)
        {
            if (module != null)
                CreateModuleSquareButton(module, moduleSection);
        }

        // ⭐ [핵심 무적 코드] 유니티 UGUI 레이아웃 엔진 강제 즉시 갱신! (1프레임 지연 겹침/쏠림 버그 100% 차단)
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

        if (MaintenanceManager.Instance != null && vaultStatusText != null)
        {
            int vaultWeaponCount = MaintenanceManager.Instance.vaultWeapons != null ? MaintenanceManager.Instance.vaultWeapons.Count : 0;
            int vaultModuleCount = MaintenanceManager.Instance.vaultModules != null ? MaintenanceManager.Instance.vaultModules.Count : 0;
            int maxCap = MaintenanceManager.Instance.maxVaultCapacity;

            vaultStatusText.text = $"보관함(Vault): {vaultWeaponCount + vaultModuleCount} / {maxCap}";
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