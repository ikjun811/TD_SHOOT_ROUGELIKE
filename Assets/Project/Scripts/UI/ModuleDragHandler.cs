using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정비 UI 상에서 강화모듈의 마우스 드래그, 90도 회전, 그리드 안착을 제어하는 매니저 클래스입니다.
/// </summary>
public class ModuleDragHandler : MonoBehaviour
{
    public static ModuleDragHandler Instance { get; private set; }

    [Header("Current Selected Module")]
    public ModuleDataSO selectedModule;
    public DragSource currentSource = DragSource.LootList; // ⭐ 출처 추적
    public int currentWidth;
    public int currentHeight;
    public bool[] currentShape;

    [Header("Drag Visual UI (Ghost)")]
    [SerializeField] private GameObject dragGhostObject;

    private bool isDragging = false;
    private GridLayoutGroup ghostGridLayout;
    private RectTransform ghostRectTransform;

    public bool IsDragging => isDragging;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (dragGhostObject != null)
        {
            ghostRectTransform = dragGhostObject.GetComponent<RectTransform>();

            ghostGridLayout = dragGhostObject.GetComponent<GridLayoutGroup>();
            if (ghostGridLayout == null)
                ghostGridLayout = dragGhostObject.AddComponent<GridLayoutGroup>();

            ghostGridLayout.cellSize = new Vector2(40, 40);
            ghostGridLayout.spacing = new Vector2(2, 2);
        }
    }

    private void Start()
    {
        if (dragGhostObject != null)
        {
            dragGhostObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isDragging || selectedModule == null) return;

        if (dragGhostObject != null)
        {
            dragGhostObject.transform.position = Input.mousePosition;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectedModule();
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }

    /// <summary>
    /// 모듈 드래그 시작 (출처 지정)
    /// </summary>
    public void StartDragModule(ModuleDataSO module, DragSource source = DragSource.LootList)
    {
        selectedModule = module;
        currentSource = source;
        currentWidth = module.width;
        currentHeight = module.height;
        currentShape = (bool[])module.shapeGrid.Clone();

        isDragging = true;

        if (currentSource == DragSource.LootList && PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.collectedModules.Remove(module);
            if (MaintenanceUI.Instance != null) MaintenanceUI.Instance.RefreshLootPanel();
        }

        if (dragGhostObject != null)
        {
            dragGhostObject.SetActive(true);
            RebuildGhostShapeVisual();
        }

        Debug.Log($"ModuleDragHandler: Selected module {module.moduleName} from {source}.");
    }

    private void RotateSelectedModule()
    {
        if (currentShape == null) return;

        currentShape = GridInventorySystem.RotateShapeClockwise(currentShape, currentWidth, currentHeight, out int newW, out int newH);
        currentWidth = newW;
        currentHeight = newH;

        RebuildGhostShapeVisual();
    }

    private void RebuildGhostShapeVisual()
    {
        if (dragGhostObject == null) return;

        if (ghostRectTransform == null) ghostRectTransform = dragGhostObject.GetComponent<RectTransform>();
        if (ghostRectTransform != null)
        {
            ghostRectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        foreach (Transform child in dragGhostObject.transform)
        {
            Destroy(child.gameObject);
        }

        if (ghostGridLayout != null)
        {
            ghostGridLayout.childAlignment = TextAnchor.MiddleCenter;
            ghostGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            ghostGridLayout.constraintCount = currentWidth;
        }

        Color rarityColor = GetRarityColor(selectedModule.rarity);
        rarityColor.a = 0.65f;

        for (int r = 0; r < currentHeight; r++)
        {
            for (int c = 0; c < currentWidth; c++)
            {
                int index = r * currentWidth + c;

                GameObject tileObj = new GameObject($"GhostTile_{c}_{r}", typeof(RectTransform), typeof(Image));
                tileObj.transform.SetParent(dragGhostObject.transform, false);

                Image img = tileObj.GetComponent<Image>();
                img.raycastTarget = false;

                if (currentShape[index])
                {
                    img.color = rarityColor;
                }
                else
                {
                    img.color = new Color(0, 0, 0, 0);
                }
            }
        }
    }

    public bool TryPlaceOnGrid(int clickedX, int clickedY)
    {
        if (!isDragging || selectedModule == null) return false;

        if (GridInventorySystem.Instance == null) return false;

        int startX = clickedX - (currentWidth / 2);
        int startY = clickedY - (currentHeight / 2);

        bool success = GridInventorySystem.Instance.TryPlaceModule(selectedModule, startX, startY, currentWidth, currentHeight, currentShape);

        if (!success)
        {
            success = GridInventorySystem.Instance.TryPlaceModule(selectedModule, clickedX, clickedY, currentWidth, currentHeight, currentShape);
        }

        if (success)
        {
            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
            }

            if (GridPanelUI.Instance != null)
            {
                GridPanelUI.Instance.RefreshGridVisuals();
            }

            EndDrag();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 드래그 취소 시 출처에 따른 복귀 처리 (금고 출처는 금고 보존 ⭐)
    /// </summary>
    public void CancelDrag()
    {
        if (selectedModule != null)
        {
            if (currentSource == DragSource.VaultSlot && MaintenanceManager.Instance != null)
            {
                bool restoredToVault = false;
                foreach (var slot in MaintenanceManager.Instance.vaultSlots)
                {
                    if (slot.IsEmpty)
                    {
                        slot.module = selectedModule;
                        restoredToVault = true;
                        break;
                    }
                }

                if (!restoredToVault && PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.collectedModules.Add(selectedModule);
                }
            }
            else if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.collectedModules.Add(selectedModule);
            }

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
                MaintenanceUI.Instance.RefreshEquipAndVaultUI();
            }

            Debug.Log($"ModuleDragHandler: Returned module {selectedModule.moduleName} to source.");
        }

        EndDrag();
    }

    public void EndDrag()
    {
        isDragging = false;
        selectedModule = null;
        currentShape = null;

        if (dragGhostObject != null)
        {
            dragGhostObject.SetActive(false);
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