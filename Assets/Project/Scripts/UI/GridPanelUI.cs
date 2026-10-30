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
    private Image[,,] slotEdges; // 칸마다 위·아래·왼쪽·오른쪽 경계선 (모듈 경계에만 켬)
    private Image[,,] slotBevels; // 경계선 안쪽 픽셀 베벨 (위·왼쪽 밝게, 아래·오른쪽 어둡게 — 블록처럼 보이게)
    private Color defaultSlotColor = new Color(0.18f, 0.18f, 0.22f, 0.95f); // 깔끔한 회색톤 바닥
    private Sprite defaultSlotSprite; // 테마에 빈 칸 스프라이트가 없을 때 쓰는 프리팹 원래 스프라이트
    private float defaultSlotPpuMultiplier = 1f;
    private Image.Type defaultSlotType = Image.Type.Simple;
    private GridInventorySystem.PlacedModule[,] cellOwners; // 칸마다 놓인 모듈 (경계 판정용)

    private const int EdgeTop = 0, EdgeBottom = 1, EdgeLeft = 2, EdgeRight = 3;

    // 적용 효과 패널에서 마우스를 올린 효과의 모듈 (밝게 + 강조 경계선, 나머지는 어둡게)
    private readonly HashSet<GridInventorySystem.PlacedModule> highlightedModules = new HashSet<GridInventorySystem.PlacedModule>();

    public void SetHighlightedModules(IEnumerable<GridInventorySystem.PlacedModule> modules)
    {
        highlightedModules.Clear();
        if (modules != null)
        {
            foreach (var module in modules)
            {
                if (module != null) highlightedModules.Add(module);
            }
        }
        RefreshGridVisuals();
    }

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
            float spacing = UITheme.Current.gridSpacing; // 0 = 모듈 안쪽 틈 없음 (UI 테마)
            gridLayout.spacing = new Vector2(spacing, spacing);
        }

        int width = GridInventorySystem.Instance.gridWidth;   // 8
        int height = GridInventorySystem.Instance.gridHeight; // 8
        slotImages = new Image[width, height];
        slotEdges = new Image[width, height, 4];
        slotBevels = new Image[width, height, 4];
        cellOwners = new GridInventorySystem.PlacedModule[width, height];

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
                    defaultSlotSprite = slotImg.sprite;
                    defaultSlotPpuMultiplier = slotImg.pixelsPerUnitMultiplier;
                    defaultSlotType = slotImg.type;
                }

                // 이전 방식의 칸 전체 Outline은 사용하지 않음 (모든 변에 선이 생겨 모듈 모양이 안 보이던 원인)
                Outline oldOutline = slot.GetComponent<Outline>();
                if (oldOutline != null) oldOutline.enabled = false;

                for (int e = 0; e < 4; e++)
                {
                    slotEdges[x, y, e] = CreateEdge(slot.transform, e);
                }
                for (int e = 0; e < 4; e++)
                {
                    slotBevels[x, y, e] = CreateEdge(slot.transform, e);
                    slotBevels[x, y, e].name = $"Bevel_{e}";
                }
            }
        }

        RefreshGridVisuals();
    }

    // 마우스 미리보기 하이라이트 (초록: 배치 가능 / 노랑: 배치 가능하지만 같은 효과 중복 / 빨강: 공간 부족·작동 모듈 충돌)
    public void HighlightPlacementPreview(int hoverX, int hoverY, int shapeW, int shapeH, bool[] shape, ModuleDataSO module = null)
    {
        RefreshGridVisuals();

        if (GridInventorySystem.Instance == null) return;

        int startX = hoverX - (shapeW / 2);
        int startY = hoverY - (shapeH / 2);

        bool canPlace = GridInventorySystem.Instance.CanPlaceModule(startX, startY, shapeW, shapeH, shape, module);
        bool duplicate = GridInventorySystem.Instance.FindSameEffect(module) != null;
        Color previewColor = !canPlace ? new Color(1f, 0.2f, 0.2f, 0.85f)
            : duplicate ? new Color(1f, 0.85f, 0.2f, 0.85f)
            : new Color(0.2f, 1f, 0.3f, 0.85f);

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

    // 경계선 1개 생성 (칸 안쪽 가장자리에 붙은 얇은 막대, 클릭은 칸이 받음)
    private Image CreateEdge(Transform slot, int edge)
    {
        GameObject obj = new GameObject($"Edge_{edge}", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(slot, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        switch (edge)
        {
            case EdgeTop: rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f); break;
            case EdgeBottom: rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(0.5f, 0f); break;
            case EdgeLeft: rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 0.5f); break;
            default: rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 0.5f); break;
        }
        rect.anchoredPosition = Vector2.zero;

        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    // 그리드 다시 그리기: 빈 칸은 테마 빈 칸 스프라이트, 모듈 칸은 레어도 색 채움 + 다른 모듈·빈 칸과 맞닿은 변에만 경계선
    public void RefreshGridVisuals()
    {
        if (slotImages == null || GridInventorySystem.Instance == null) return;

        UITheme theme = UITheme.Current;
        int width = GridInventorySystem.Instance.gridWidth;
        int height = GridInventorySystem.Instance.gridHeight;

        // 1. 칸별 소유 모듈 표
        System.Array.Clear(cellOwners, 0, cellOwners.Length);
        foreach (var placed in GridInventorySystem.Instance.placedModules)
        {
            for (int r = 0; r < placed.currentHeight; r++)
            {
                for (int c = 0; c < placed.currentWidth; c++)
                {
                    if (!placed.currentShape[r * placed.currentWidth + c]) continue;
                    int gridX = placed.startX + c;
                    int gridY = placed.startY + r;
                    if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height) cellOwners[gridX, gridY] = placed;
                }
            }
        }

        // 2. 칸 그리기
        bool outlined = theme.gridOutlinedStyle;
        float borderWidth = outlined ? theme.gridOutlinedBorderWidth : theme.gridModuleBorderWidth;
        float bevelWidth = outlined ? theme.gridOutlinedInnerWidth : theme.gridBevelWidth;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Image tile = slotImages[x, y];
                if (tile == null) continue;

                var owner = cellOwners[x, y];
                Color fill = Color.clear;
                Color rarity = Color.clear;
                bool highlighted = owner != null && highlightedModules.Contains(owner);
                bool dimmed = owner != null && highlightedModules.Count > 0 && !highlighted;
                if (owner == null)
                {
                    if (!theme.ApplySprite(tile, theme.gridEmptySprite, theme.gridPixelScale))
                    {
                        tile.sprite = defaultSlotSprite;
                        tile.type = defaultSlotType;
                        tile.pixelsPerUnitMultiplier = defaultSlotPpuMultiplier;
                        tile.color = defaultSlotColor;
                    }
                    else
                    {
                        tile.color = outlined ? theme.gridOutlinedEmptyColor : theme.gridEmptyColor;
                    }
                }
                else
                {
                    // 채움은 스프라이트 없이 단색 (칸 테두리가 생기면 모듈 모양이 다시 안 보임)
                    tile.sprite = null;
                    tile.type = Image.Type.Simple;
                    rarity = GetRarityColor(owner.moduleData.rarity);
                    if (outlined)
                    {
                        // 외곽선 스타일: 패널 남색에 레어도 색을 섞은 바탕 (흰색 일반 모듈도 회색이 아닌 남색 계열로)
                        fill = Color.Lerp(theme.gridOutlinedBaseColor, rarity, highlighted ? theme.gridOutlinedFillHighlight : theme.gridOutlinedFill);
                        fill.a = theme.gridOutlinedFillAlpha * (dimmed ? theme.gridDimBrightness : 1f);
                    }
                    else
                    {
                        float brightness = highlightedModules.Count == 0 ? theme.gridFillBrightness
                            : (highlighted ? 1f : theme.gridDimBrightness);
                        fill = rarity * brightness;
                        fill.a = 1f;
                    }
                    tile.color = fill;
                }

                // 경계선: 모듈 칸이고 이웃이 다른 모듈·빈 칸·그리드 밖이면 그 변에 선
                // 그리드 Y는 위에서 아래로 증가 (y - 1 = 위쪽 칸)
                bool top = owner != null && GetOwner(x, y - 1, width, height) != owner;
                bool bottom = owner != null && GetOwner(x, y + 1, width, height) != owner;
                bool left = owner != null && GetOwner(x - 1, y, width, height) != owner;
                bool right = owner != null && GetOwner(x + 1, y, width, height) != owner;

                Color edgeColor;
                Color light, shadow;
                float bevelInset = borderWidth;
                if (outlined)
                {
                    // 외곽선 = 레어도 색 (강조 시 흰빛 쪽으로), 안쪽에 간격을 두고 흐린 두 번째 선 (장비 슬롯 테두리 느낌)
                    edgeColor = highlighted ? Color.Lerp(rarity, Color.white, 0.5f) : rarity;
                    edgeColor.a = dimmed ? theme.gridDimBrightness : 1f;
                    light = shadow = new Color(rarity.r, rarity.g, rarity.b, theme.gridOutlinedInnerAlpha * (dimmed ? theme.gridDimBrightness : 1f));
                    bevelInset = borderWidth + theme.gridOutlinedInnerGap;
                }
                else
                {
                    edgeColor = highlighted ? theme.gridHighlightBorderColor : theme.gridModuleBorderColor;
                    // 픽셀 베벨: 경계선 바로 안쪽, 채움 색 기준 (강조·어둡게도 따라감)
                    light = Color.Lerp(fill, Color.white, theme.gridBevelLight);
                    shadow = fill * (1f - theme.gridBevelShadow);
                    light.a = shadow.a = 1f;
                }
                SetEdge(x, y, EdgeTop, top, borderWidth, edgeColor);
                SetEdge(x, y, EdgeBottom, bottom, borderWidth, edgeColor);
                SetEdge(x, y, EdgeLeft, left, borderWidth, edgeColor);
                SetEdge(x, y, EdgeRight, right, borderWidth, edgeColor);

                bool bevel = bevelWidth > 0f;
                SetBevel(x, y, EdgeTop, bevel && top, left, right, bevelInset, bevelWidth, light);
                SetBevel(x, y, EdgeLeft, bevel && left, top, bottom, bevelInset, bevelWidth, light);
                SetBevel(x, y, EdgeBottom, bevel && bottom, left, right, bevelInset, bevelWidth, shadow);
                SetBevel(x, y, EdgeRight, bevel && right, top, bottom, bevelInset, bevelWidth, shadow);
            }
        }
    }

    // 베벨 1개: 해당 변 경계선 안쪽에 붙임. 길이 방향 양 끝은 그쪽 경계선이 있으면 그만큼 비켜 감
    private void SetBevel(int x, int y, int edge, bool visible, bool startBorder, bool endBorder, float borderWidth, float bevelWidth, Color color)
    {
        Image line = slotBevels[x, y, edge];
        if (line == null) return;

        line.enabled = visible;
        if (!visible) return;

        line.color = color;
        RectTransform rect = line.rectTransform;
        float start = startBorder ? borderWidth : 0f;
        float end = endBorder ? borderWidth : 0f;
        switch (edge)
        {
            case EdgeTop:
                rect.offsetMin = new Vector2(start, -borderWidth - bevelWidth);
                rect.offsetMax = new Vector2(-end, -borderWidth);
                break;
            case EdgeBottom:
                rect.offsetMin = new Vector2(start, borderWidth);
                rect.offsetMax = new Vector2(-end, borderWidth + bevelWidth);
                break;
            case EdgeLeft: // 길이 방향 = 아래(end) → 위(start)
                rect.offsetMin = new Vector2(borderWidth, end);
                rect.offsetMax = new Vector2(borderWidth + bevelWidth, -start);
                break;
            default:
                rect.offsetMin = new Vector2(-borderWidth - bevelWidth, end);
                rect.offsetMax = new Vector2(-borderWidth, -start);
                break;
        }
    }

    private GridInventorySystem.PlacedModule GetOwner(int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return cellOwners[x, y];
    }

    private void SetEdge(int x, int y, int edge, bool visible, float borderWidth, Color color)
    {
        Image line = slotEdges[x, y, edge];
        if (line == null) return;

        line.enabled = visible;
        if (!visible) return;

        line.color = color;
        RectTransform rect = line.rectTransform;
        bool horizontal = edge == EdgeTop || edge == EdgeBottom;
        rect.sizeDelta = horizontal ? new Vector2(0f, borderWidth) : new Vector2(borderWidth, 0f);
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return UITheme.Current.GetRarityColor(rarity); // 색은 UI 테마에서 (Resources/UITheme)
    }
}