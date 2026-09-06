using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaintenanceUI : MonoBehaviour
{
    public static MaintenanceUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject maintenancePanel;
    [SerializeField] private Transform lootContainer;
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

    public void RefreshLootPanel()
    {
        // ⭐ [안전망 2] 패널이 새로 그려질 때 멈춰있던 툴팁 강제 숨김
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }

        if (lootContainer == null) return;

        foreach (Transform child in lootContainer)
        {
            Destroy(child.gameObject);
        }

        if (PlayerInventory.Instance == null) return;

        foreach (var weapon in PlayerInventory.Instance.collectedWeapons)
        {
            if (weapon != null)
                CreateLootButton(weapon, null);
        }

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
            hoverUI.Setup(weapon);
        }
        else if (module != null)
        {
            Color rarityColor = GetRarityColor(module.rarity);
            if (bgImage != null) bgImage.color = rarityColor;
            if (nameText != null) nameText.text = module.moduleName;
            hoverUI.Setup(module);
        }
    }

    // ⭐ 가방에서 무기를 클릭했을 때 장착 처리
    public void OnClickWeaponFromLoot(WeaponDataSO weapon)
    {
        if (PlayerInventory.Instance == null) return;

        // 빈 슬롯 1번 ➔ 슬롯 2번 순서로 장착 시도
        if (PlayerInventory.Instance.equippedWeapons[0] == null)
        {
            PlayerInventory.Instance.equippedWeapons[0] = weapon;
            PlayerInventory.Instance.collectedWeapons.Remove(weapon);
            Debug.Log($"⚔️ [무기 장착] 슬롯 1번에 {weapon.weaponName} 장착 완료!");
        }
        else if (PlayerInventory.Instance.equippedWeapons[1] == null)
        {
            PlayerInventory.Instance.equippedWeapons[1] = weapon;
            PlayerInventory.Instance.collectedWeapons.Remove(weapon);
            Debug.Log($"⚔️ [무기 장착] 슬롯 2번에 {weapon.weaponName} 장착 완료!");
        }
        else
        {
            Debug.LogWarning("⚠️ [무기 장착 실패] 이미 무기 슬롯 2개가 가득 찼습니다! 기존 무기를 해제하세요.");
        }

        RefreshLootPanel();
        RefreshEquipAndVaultUI();
    }

    // ⭐ 장착 무기 슬롯 1번 클릭 시 해제
    public void OnClickUnequipWeapon1()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.equippedWeapons[0] != null)
        {
            var unequipped = PlayerInventory.Instance.equippedWeapons[0];
            PlayerInventory.Instance.equippedWeapons[0] = null;
            PlayerInventory.Instance.collectedWeapons.Add(unequipped);

            Debug.Log($"⚔️ [무기 해제] 슬롯 1번 {unequipped.weaponName} 해제됨.");
            RefreshLootPanel();
            RefreshEquipAndVaultUI();
        }
    }

    // ⭐ 장착 무기 슬롯 2번 클릭 시 해제
    public void OnClickUnequipWeapon2()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.equippedWeapons[1] != null)
        {
            var unequipped = PlayerInventory.Instance.equippedWeapons[1];
            PlayerInventory.Instance.equippedWeapons[1] = null;
            PlayerInventory.Instance.collectedWeapons.Add(unequipped);

            Debug.Log($"⚔️ [무기 해제] 슬롯 2번 {unequipped.weaponName} 해제됨.");
            RefreshLootPanel();
            RefreshEquipAndVaultUI();
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

    public void OnClickStartNextRound()
    {
        // ⭐ [안전망 3] 다음 라운드 시작 시 툴팁 무조건 숨김
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