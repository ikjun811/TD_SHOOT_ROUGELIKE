using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridPanelUI : MonoBehaviour
{
    public static GridPanelUI Instance { get; private set; }

    [Header("Grid UI Settings")]
    [SerializeField] private GameObject gridSlotPrefab;
    [SerializeField] private Transform gridSlotParent;

    private Image[,] slotImages;
    private Color defaultSlotColor = Color.white; // 원본 슬롯 색상 보존용

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        GenerateGridSlots();
    }

    public void GenerateGridSlots()
    {
        if (GridInventorySystem.Instance == null || gridSlotPrefab == null || gridSlotParent == null) return;

        foreach (Transform child in gridSlotParent)
        {
            Destroy(child.gameObject);
        }

        GridLayoutGroup gridLayout = gridSlotParent.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.spacing = new Vector2(2f, 2f);
        }

        int width = GridInventorySystem.Instance.gridWidth;   // 8
        int height = GridInventorySystem.Instance.gridHeight; // 8
        slotImages = new Image[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject slot = Instantiate(gridSlotPrefab, gridSlotParent);
                slot.name = $"Slot_{x}_{y}";

                GridSlotUI slotUI = slot.GetComponent<GridSlotUI>();
                if (slotUI == null) slotUI = slot.AddComponent<GridSlotUI>();
                slotUI.SetupCoordinates(x, y);

                Image slotImg = slot.GetComponent<Image>();
                slotImages[x, y] = slotImg;

                // 원본 슬롯 인스펙터 색상 보존
                if (x == 0 && y == 0 && slotImg != null)
                {
                    defaultSlotColor = slotImg.color;
                }
            }
        }

        RefreshGridVisuals();
    }

    // 마우스 드래그 미리보기 하이라이트 (초록/빨강)
    public void HighlightPlacementPreview(int hoverX, int hoverY, int shapeW, int shapeH, bool[] shape)
    {
        RefreshGridVisuals();

        if (GridInventorySystem.Instance == null) return;

        int startX = hoverX - (shapeW / 2);
        int startY = hoverY - (shapeH / 2);

        bool canPlace = GridInventorySystem.Instance.CanPlaceModule(startX, startY, shapeW, shapeH, shape);
        Color previewColor = canPlace ? new Color(0.2f, 1f, 0.3f, 0.8f) : new Color(1f, 0.2f, 0.2f, 0.8f);

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        for (int r = 0; r < shapeH; r++)
        {
            for (int c = 0; c < shapeW; c++)
            {
                int shapeIndex = r * shapeW + c;
                if (shape[shapeIndex])
                {
                    int gridX = startX + c;
                    int gridY = startY + r;

                    if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height)
                    {
                        if (slotImages[gridX, gridY] != null)
                            slotImages[gridX, gridY].color = previewColor;
                    }
                }
            }
        }
    }

    // ⭐ [깔끔 처리] 바닥 슬롯 디자인은 그대로 유지하고, 장착된 모듈 구역만 레어도 색상으로 채움!
    public void RefreshGridVisuals()
    {
        if (slotImages == null || GridInventorySystem.Instance == null) return;

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        // 1. 바닥 타일은 인스펙터에 지정하신 원래 슬롯 색상/디자인으로 100% 복원!
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (slotImages[x, y] != null)
                {
                    slotImages[x, y].color = defaultSlotColor;

                    Outline outline = slotImages[x, y].GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
                }
            }
        }

        // 2. 안착된 모듈 영역 타일만 레어도 색상으로 채움
        foreach (var placed in GridInventorySystem.Instance.placedModules)
        {
            Color rarityColor = GetRarityColor(placed.moduleData.rarity);

            for (int r = 0; r < placed.currentHeight; r++)
            {
                for (int c = 0; c < placed.currentWidth; c++)
                {
                    int shapeIndex = r * placed.currentWidth + c;
                    if (placed.currentShape[shapeIndex])
                    {
                        int gridX = placed.startX + c;
                        int gridY = placed.startY + r;

                        if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height)
                        {
                            Image tileImg = slotImages[gridX, gridY];
                            if (tileImg != null)
                            {
                                tileImg.color = rarityColor; // 레어도 색상 할당
                            }
                        }
                    }
                }
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
            _ => Color.cyan
        };
    }
}