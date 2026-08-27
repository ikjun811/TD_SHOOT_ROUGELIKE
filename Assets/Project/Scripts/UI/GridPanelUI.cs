using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridPanelUI : MonoBehaviour
{
    public static GridPanelUI Instance { get; private set; }

    [Header("Grid UI Settings")]
    [SerializeField] private GameObject gridSlotPrefab;
    [SerializeField] private Transform gridSlotParent;

    private Image[,] slotImages; // 8x8 타일 이미지 배열

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

                slotImages[x, y] = slot.GetComponent<Image>();
            }
        }

        RefreshGridVisuals();
    }

    //  안착된 모듈 모양대로 타일 색상을 채워주는 핵심 시각화 함수
    public void RefreshGridVisuals()
    {
        if (slotImages == null || GridInventorySystem.Instance == null) return;

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        // 1. 모든 타일을 기본 빈 타일 색상(어두운 회색)으로 초기화
        Color defaultTileColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (slotImages[x, y] != null)
                    slotImages[x, y].color = defaultTileColor;
            }
        }

        // 2. 그리드에 안착된 모든 모듈 모양 영역의 타일 색상을 레어도 색상으로 채움!
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
            RarityType.Rare => new Color(0.2f, 0.6f, 1f),       // 파란색
            RarityType.Elite => new Color(0.7f, 0.3f, 0.9f),     // 보라색
            RarityType.Legendary => new Color(1f, 0.8f, 0.1f),   // 노란색
            _ => Color.cyan
        };
    }
}
