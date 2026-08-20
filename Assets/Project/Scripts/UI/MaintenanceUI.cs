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
        // 기존 아이콘 청소
        foreach (Transform child in lootContainer)
        {
            Destroy(child.gameObject);
        }

        if (PlayerInventory.Instance == null) return;

        // 1. 주운 무기들 아이콘 생성
        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            CreateLootButton(weapon.weaponName, weapon.GetRarityColor());
        }

        // 2. 주운 모듈들 아이콘 생성
        foreach (var module in PlayerInventory.Instance.collectedModules)
        {
            Color rarityColor = GetRarityColor(module.rarity);
            CreateLootButton(module.moduleName, rarityColor);
        }
    }

    private void CreateLootButton(string itemName, Color rarityColor)
    {
        if (lootItemButtonPrefab == null || lootContainer == null) return;

        GameObject btnObj = Instantiate(lootItemButtonPrefab, lootContainer);
        Image bgImage = btnObj.GetComponent<Image>();
        TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

        if (bgImage != null) bgImage.color = rarityColor;
        if (nameText != null) nameText.text = itemName;
    }

    public void RefreshEquipAndVaultUI()
    {
        if (PlayerInventory.Instance != null)
        {
            weapon1Text.text = PlayerInventory.Instance.equippedWeapons[0] != null ? PlayerInventory.Instance.equippedWeapons[0].weaponName : "빈 슬롯 1";
            weapon2Text.text = PlayerInventory.Instance.equippedWeapons[1] != null ? PlayerInventory.Instance.equippedWeapons[1].weaponName : "빈 슬롯 2";
        }

        if (MaintenanceManager.Instance != null)
        {
            int currentVault = MaintenanceManager.Instance.vaultWeapons.Count + MaintenanceManager.Instance.vaultModules.Count;
            vaultStatusText.text = $"보관함: {currentVault} / {MaintenanceManager.Instance.maxVaultCapacity}";
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