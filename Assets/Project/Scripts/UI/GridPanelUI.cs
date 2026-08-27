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

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;
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

                slotImages[x, y] = slot.GetComponent<Image>();
            }
        }

        RefreshGridVisuals();
    }

    // ⭐ 그리드 안착 미리보기 하이라이트 (초록색 / 빨간색)
    public void HighlightPlacementPreview(int hoverX, int hoverY, int shapeW, int shapeH, bool[] shape)
    {
        RefreshGridVisuals(); // 기본 배경 복구 후 하이라이트 덮어씌움

        if (GridInventorySystem.Instance == null) return;

        int startX = hoverX - (shapeW / 2);
        int startY = hoverY - (shapeH / 2);

        bool canPlace = GridInventorySystem.Instance.CanPlaceModule(startX, startY, shapeW, shapeH, shape);
        Color previewColor = canPlace ? new Color(0.2f, 1f, 0.3f, 0.85f) : new Color(1f, 0.2f, 0.2f, 0.85f); // 초록 / 빨강

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

    public void RefreshGridVisuals()
    {
        if (slotImages == null || GridInventorySystem.Instance == null) return;

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        Color defaultTileColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (slotImages[x, y] != null)
                    slotImages[x, y].color = defaultTileColor;
            }
        }

        foreach (var placed in GridInventorySystem.Instance.placedModules)
        {
            Color moduleColor = GetRarityColor(placed.moduleData.rarity);

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
                            if (slotImages[gridX, gridY] != null)
                                slotImages[gridX, gridY].color = moduleColor;
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