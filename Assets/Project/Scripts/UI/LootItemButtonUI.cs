using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class LootItemButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private WeaponDataSO weaponData;
    private ModuleDataSO moduleData;

    [Header("Mini Shape Parent")]
    [SerializeField] private Transform miniShapeParent;

    public void Setup(WeaponDataSO weapon)
    {
        weaponData = weapon;
        moduleData = null;

        ClearMiniShape();

        Image btnBg = GetComponent<Image>();
        if (btnBg != null) btnBg.color = weapon.GetRarityColor();

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = $"[{weapon.weaponType}]\n{weapon.weaponName}";
            btnText.fontSize = 13;
            btnText.alignment = TextAlignmentOptions.Left;
        }
    }

    public void Setup(ModuleDataSO module)
    {
        moduleData = module;
        weaponData = null;

        BuildMiniShapeVisual(module);
    }

    private void BuildMiniShapeVisual(ModuleDataSO module)
    {
        if (module == null) return;

        if (miniShapeParent == null)
        {
            GameObject container = new GameObject("MiniShapeContainer", typeof(RectTransform), typeof(GridLayoutGroup));
            container.transform.SetParent(transform, false);
            miniShapeParent = container.transform;

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80, 60);
            rect.anchoredPosition = new Vector2(0, 8);
        }

        ClearMiniShape();

        float containerW = 76f;
        float containerH = 56f;
        float spacing = 2f;

        float maxTileW = (containerW - (spacing * (module.width - 1))) / module.width;
        float maxTileH = (containerH - (spacing * (module.height - 1))) / module.height;
        float dynamicTileSize = Mathf.Clamp(Mathf.Min(maxTileW, maxTileH), 8f, 22f);

        GridLayoutGroup grid = miniShapeParent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = module.width;
            grid.cellSize = new Vector2(dynamicTileSize, dynamicTileSize);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        Image btnBg = GetComponent<Image>();
        if (btnBg != null)
        {
            btnBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        }

        Color rarityColor = GetRarityColor(module.rarity);

        for (int r = 0; r < module.height; r++)
        {
            for (int c = 0; c < module.width; c++)
            {
                int index = r * module.width + c;

                GameObject tileObj = new GameObject($"MiniTile_{c}_{r}", typeof(RectTransform), typeof(Image), typeof(Outline));
                tileObj.transform.SetParent(miniShapeParent, false);

                Image img = tileObj.GetComponent<Image>();
                img.raycastTarget = false;

                Outline outline = tileObj.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(1, -1);
                }

                if (module.shapeGrid[index])
                {
                    img.color = rarityColor;
                }
                else
                {
                    img.color = new Color(0, 0, 0, 0);
                }
            }
        }

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = module.moduleName;
            btnText.fontSize = 11;
            btnText.color = rarityColor;
            btnText.alignment = TextAlignmentOptions.Bottom;
            btnText.transform.SetAsLastSibling();
        }
    }

    private void ClearMiniShape()
    {
        if (miniShapeParent != null)
        {
            foreach (Transform child in miniShapeParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    // ⭐ [선제적 방어 적용] 새 아이템 집기 전 기존 들고 있던 아이템 가방 자동 복귀!
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // ⭐ 1. 이미 손에 모듈을 들고 있다면 기존 모듈 가방에 먼저 안전 복귀!
            if (ModuleDragHandler.Instance != null && ModuleDragHandler.Instance.IsDragging)
            {
                ModuleDragHandler.Instance.CancelDrag();
            }

            // ⭐ 2. 이미 손에 무기를 들고 있다면 기존 무기 가방에 먼저 안전 복귀!
            if (WeaponDragHandler.Instance != null && WeaponDragHandler.Instance.IsDragging)
            {
                WeaponDragHandler.Instance.CancelDrag();
            }

            // 3. 그 후 클릭한 새 아이템 집어 올리기!
            if (moduleData != null && ModuleDragHandler.Instance != null)
            {
                ModuleDragHandler.Instance.StartDragModule(moduleData, fromLootList: true);
            }
            else if (weaponData != null && WeaponDragHandler.Instance != null)
            {
                WeaponDragHandler.Instance.StartDragWeapon(weaponData, fromLootList: true);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance == null) return;

        if (weaponData != null)
            ItemTooltipUI.Instance.ShowWeaponTooltip(weaponData);
        else if (moduleData != null)
            ItemTooltipUI.Instance.ShowModuleTooltip(moduleData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.HideTooltip();
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