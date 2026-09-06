using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaintenanceUI : MonoBehaviour
{
    public static MaintenanceUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject maintenancePanel;
    [SerializeField] private Transform lootContainer;          // 전리품 메인 가방 패널
    [SerializeField] private GameObject lootItemButtonPrefab;  // 기본 버튼 프리팹

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

    // ⭐ [완벽 분리] 무기 영역(가로 와이드) + 모듈 영역(정사각형 그리드)
    public void RefreshLootPanel()
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }

        if (lootContainer == null) return;

        // 1. 무기 전용 서브 컨테이너 & 모듈 전용 서브 컨테이너 자동 생성/찾기 ⭐
        Transform weaponSection = lootContainer.Find("WeaponSection");
        Transform moduleSection = lootContainer.Find("ModuleSection");

        if (weaponSection == null)
        {
            GameObject wObj = new GameObject("WeaponSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
            wObj.transform.SetParent(lootContainer, false);
            weaponSection = wObj.transform;

            VerticalLayoutGroup vlg = wObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
        }

        if (moduleSection == null)
        {
            GameObject mObj = new GameObject("ModuleSection", typeof(RectTransform), typeof(GridLayoutGroup));
            mObj.transform.SetParent(lootContainer, false);
            moduleSection = mObj.transform;

            GridLayoutGroup glg = mObj.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(80f, 80f); // 모듈용 80x80 정사각형! ⭐
            glg.spacing = new Vector2(8f, 8f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3; // 가로 3개씩 차곡차곡 정렬
        }

        // 기존 자식 아이콘 청소
        foreach (Transform child in weaponSection) Destroy(child.gameObject);
        foreach (Transform child in moduleSection) Destroy(child.gameObject);

        if (PlayerInventory.Instance == null) return;

        // 2. 무기류 ➔ WeaponSection 생성 (가로 길쭉한 일러스트 배너)
        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            if (weapon != null)
                CreateWeaponBannerButton(weapon, weaponSection);
        }

        // 3. 강화모듈류 ➔ ModuleSection 생성 (정사각형 80x80 미니 그리드 타일)
        foreach (var module in PlayerInventory.Instance.collectedModules)
        {
            if (module != null)
                CreateModuleSquareButton(module, moduleSection);
        }
    }

    // 무기용 와이드 배너 버튼 생성 (260 x 55)
    private void CreateWeaponBannerButton(WeaponDataSO weapon, Transform parent)
    {
        if (lootItemButtonPrefab == null || parent == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(260f, 55f); // 가로 길쭉한 무기 배너 크기
        }

        Image bgImage = btnObj.GetComponent<Image>();
        TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

        LootItemButtonUI hoverUI = btnObj.GetComponent<LootItemButtonUI>();
        if (hoverUI == null) hoverUI = btnObj.AddComponent<LootItemButtonUI>();

        if (bgImage != null) bgImage.color = weapon.GetRarityColor();
        if (nameText != null)
        {
            nameText.text = $"[{weapon.weaponType}] {weapon.weaponName}";
            nameText.fontSize = 13;
            nameText.alignment = TextAlignmentOptions.Left; // 좌측 정렬
        }

        hoverUI.Setup(weapon);
    }

    // 모듈용 정사각형 버튼 생성 (80 x 80)
    private void CreateModuleSquareButton(ModuleDataSO module, Transform parent)
    {
        if (lootItemButtonPrefab == null || parent == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(80f, 80f); // 80x80 정사각형 크기 고정! ⭐
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