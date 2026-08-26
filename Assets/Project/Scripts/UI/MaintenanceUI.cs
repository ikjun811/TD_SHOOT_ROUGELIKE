using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaintenanceUI : MonoBehaviour
{
    public static MaintenanceUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject maintenancePanel; // 메인 정비 UI 패널
    [SerializeField] private Transform lootContainer;     // 획득 전리품 아이콘 부모
    [SerializeField] private GameObject lootItemButtonPrefab; // 전리품 아이콘 프리팹

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
            maintenancePanel.SetActive(false); // 시작 시 숨김
        }
    }

    // MaintenanceManager에서 정비 단계 진입 시 호출
    public void OpenUI()
    {
        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(true);
        }

        RefreshLootPanel();
        RefreshEquipAndVaultUI();
    }

    // 획득한 전리품 패널 UI 갱신
    public void RefreshLootPanel()
    {
        if (lootContainer == null) return;

        foreach (Transform child in lootContainer)
        {
            Destroy(child.gameObject);
        }

        if (PlayerInventory.Instance == null) return;

        // 1. 주운 무기들 생성
        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            if (weapon != null)
                CreateLootButton(weapon, null);
        }

        // 2. 주운 모듈들 생성
        foreach (var module in PlayerInventory.Instance.collectedModules)
        {
            if (module != null)
                CreateLootButton(null, module);
        }
    }


    private void CreateLootButton(WeaponDataSO weapon, ModuleDataSO module)
    {
        if (lootItemButtonPrefab == null || lootContainer == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, lootContainer);
        Image bgImage = btnObj.GetComponent<Image>();
        TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

        LootItemButtonUI hoverUI = btnObj.GetComponent<LootItemButtonUI>();
        if (hoverUI == null) hoverUI = btnObj.AddComponent<LootItemButtonUI>();

        if (weapon != null)
        {
            if (bgImage != null) bgImage.color = weapon.GetRarityColor();
            if (nameText != null) nameText.text = weapon.weaponName;
            hoverUI.Setup(weapon); // 툴팁에 무기 데이터 연결
        }
        else if (module != null)
        {
            Color rarityColor = GetRarityColor(module.rarity);
            if (bgImage != null) bgImage.color = rarityColor;
            if (nameText != null) nameText.text = module.moduleName;
            hoverUI.Setup(module); // 툴팁에 모듈 데이터 연결
        }
    }

    public void RefreshEquipAndVaultUI()
    {
        // 1. 장착 무기 슬롯 텍스트 갱신 (안전망 처리)
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.equippedWeapons != null)
        {
            if (weapon1Text != null)
            {
                var w1 = PlayerInventory.Instance.equippedWeapons.Length > 0 ? PlayerInventory.Instance.equippedWeapons[0] : null;
                weapon1Text.text = (w1 != null) ? w1.weaponName : "빈 슬롯 1";
            }

            if (weapon2Text != null)
            {
                var w2 = PlayerInventory.Instance.equippedWeapons.Length > 1 ? PlayerInventory.Instance.equippedWeapons[1] : null;
                weapon2Text.text = (w2 != null) ? w2.weaponName : "빈 슬롯 2";
            }
        }

        // 2. 금고(Vault) 상태 텍스트 갱신 (안전망 처리)
        if (MaintenanceManager.Instance != null && vaultStatusText != null)
        {
            int vaultWeaponCount = MaintenanceManager.Instance.vaultWeapons != null ? MaintenanceManager.Instance.vaultWeapons.Count : 0;
            int vaultModuleCount = MaintenanceManager.Instance.vaultModules != null ? MaintenanceManager.Instance.vaultModules.Count : 0;
            int maxCap = MaintenanceManager.Instance.maxVaultCapacity;

            vaultStatusText.text = $"보관함: {vaultWeaponCount + vaultModuleCount} / {maxCap}";
        }
    }

    // '다음 라운드 시작' 버튼 클릭 이벤트
    public void OnClickStartNextRound()
    {
        if (maintenancePanel != null)
        {
            maintenancePanel.SetActive(false);
        }

        // 정비 완료 및 전투 재개
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.CompleteMaintenanceAndStartNextRound();
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