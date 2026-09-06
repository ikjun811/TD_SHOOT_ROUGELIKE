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
    private Color defaultSlotColor = new Color(0.18f, 0.18f, 0.22f, 0.95f); // 깔끔한 회색톤 바닥

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

                if (x == 0 && y == 0 && slotImg != null)
                {
                    defaultSlotColor = slotImg.color;
                }
            }
        }

        RefreshGridVisuals();
    }

    // 마우스 미리보기 하이라이트 (초록/빨강)
    public void HighlightPlacementPreview(int hoverX, int hoverY, int shapeW, int shapeH, bool[] shape)
    {
        RefreshGridVisuals();

        if (GridInventorySystem.Instance == null) return;

        int startX = hoverX - (shapeW / 2);
        int startY = hoverY - (shapeH / 2);

        bool canPlace = GridInventorySystem.Instance.CanPlaceModule(startX, startY, shapeW, shapeH, shape);
        Color previewColor = canPlace ? new Color(0.2f, 1f, 0.3f, 0.85f) : new Color(1f, 0.2f, 0.2f, 0.85f);

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

    // ⭐ [고대비 흰색 테두리 적용]
    public void RefreshGridVisuals()
    {
        if (slotImages == null || GridInventorySystem.Instance == null) return;

        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        // 1. 바닥 타일 원본 회색톤으로 복원
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

        // 2. 안착된 각 모듈별 고대비 외곽선 및 색상 적용
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
                                tileImg.color = rarityColor;

                                // ⭐ 이 타일이 모듈의 '외곽 둘레 타일'인지 검사
                                bool isOuterBorder = IsTileOnModuleOuterBorder(placed, c, r);

                                if (isOuterBorder)
                                {
                                    Outline outline = tileImg.GetComponent<Outline>();
                                    if (outline == null) outline = tileImg.gameObject.AddComponent<Outline>();

                                    // ⭐ [고대비 핵심] 쨍하고 선명한 밝은 흰색/골드 테두리선 부여!
                                    outline.enabled = true;
                                    outline.effectColor = new Color(1f, 1f, 1f, 0.95f); // 100% 쨍한 흰색 선
                                    outline.effectDistance = new Vector2(3f, -3f);     // 3px 선명한 두께
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // 타일이 모듈의 외곽 둘레인지 검사하는 연산
    private bool IsTileOnModuleOuterBorder(GridInventorySystem.PlacedModule pm, int col, int row)
    {
        int w = pm.currentWidth;
        int h = pm.currentHeight;

        bool hasTop = (row > 0) && pm.currentShape[(row - 1) * w + col];
        bool hasBottom = (row < h - 1) && pm.currentShape[(row + 1) * w + col];
        bool hasLeft = (col > 0) && pm.currentShape[row * w + (col - 1)];
        bool hasRight = (col < w - 1) && pm.currentShape[row * w + (col + 1)];

        return !(hasTop && hasBottom && hasLeft && hasRight);
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