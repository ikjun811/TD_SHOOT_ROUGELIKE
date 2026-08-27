using UnityEngine;
using UnityEngine.UI;

public class ModuleDragHandler : MonoBehaviour
{
    public static ModuleDragHandler Instance { get; private set; }

    [Header("Current Selected Module")]
    public ModuleDataSO selectedModule;
    public int currentWidth;
    public int currentHeight;
    public bool[] currentShape;

    [Header("Drag Visual UI (Ghost)")]
    [SerializeField] private GameObject dragGhostObject;

    private bool isDragging = false;
    public bool IsDragging => isDragging;
    private GridLayoutGroup ghostGridLayout;
    private RectTransform ghostRectTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (dragGhostObject != null)
        {
            ghostRectTransform = dragGhostObject.GetComponent<Rigidbody2D>() != null ? null : dragGhostObject.GetComponent<RectTransform>();

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

        //  마우스 커서 정중앙 밀착 추종
        if (dragGhostObject != null)
        {
            dragGhostObject.transform.position = Input.mousePosition;
        }

        // [R]키 누를 시 제자리 90도 회전
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectedModule();
        }

        // 우클릭 시 선택 취소
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }

    public void StartDragModule(ModuleDataSO module)
    {
        selectedModule = module;
        currentWidth = module.width;
        currentHeight = module.height;
        currentShape = (bool[])module.shapeGrid.Clone();

        isDragging = true;

        if (dragGhostObject != null)
        {
            dragGhostObject.SetActive(true);
            RebuildGhostShapeVisual();
        }

        Debug.Log($"🧩 [모듈 선택] {module.moduleName} 선택됨 (R키로 제자리 회전)");
    }

    private void RotateSelectedModule()
    {
        if (currentShape == null) return;

        currentShape = GridInventorySystem.RotateShapeClockwise(currentShape, currentWidth, currentHeight, out int newW, out int newH);
        currentWidth = newW;
        currentHeight = newH;

        RebuildGhostShapeVisual();

        Debug.Log($"🔄 [모듈 회전] 새로운 크기: {currentWidth}x{currentHeight}");
    }

    //  도형 정중앙(Center) 피벗 고정 연산
    private void RebuildGhostShapeVisual()
    {
        if (dragGhostObject == null) return;

        // 고스트 피벗을 도형 정중앙(0.5, 0.5)으로 설정하여 마우스가 모듈 정중앙을 잡도록 변경
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
            ghostGridLayout.childAlignment = TextAnchor.MiddleCenter; // 정중앙 정렬
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
                    img.color = new Color(0, 0, 0, 0); // 빈 공간 투명
                }
            }
        }
    }

    //  타일 클릭 시 도형 중앙 기준으로 안착 좌표 자동 보정 연산
    public bool TryPlaceOnGrid(int clickedX, int clickedY)
    {
        if (!isDragging || selectedModule == null) return false;

        if (GridInventorySystem.Instance == null) return false;

        // 클릭한 타일에 모듈의 중앙이 오도록 좌상단(startX, startY) 좌표 보정
        int startX = clickedX - (currentWidth / 2);
        int startY = clickedY - (currentHeight / 2);

        // 1차 중앙 보정 시도
        bool success = GridInventorySystem.Instance.TryPlaceModule(selectedModule, startX, startY, currentWidth, currentHeight, currentShape);

        // 그리드 경계면 근처라 중앙 보정이 실패한 경우, 클릭한 타일을 바로 좌상단으로 2차 시도
        if (!success)
        {
            success = GridInventorySystem.Instance.TryPlaceModule(selectedModule, clickedX, clickedY, currentWidth, currentHeight, currentShape);
        }

        if (success)
        {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.collectedModules.Remove(selectedModule);
            }

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

    public void CancelDrag()
    {
        if (selectedModule != null)
        {
            // ⭐ 드래그 취소 시 전리품 가방으로 무사히 복귀!
            if (PlayerInventory.Instance != null)
            {
                if (!PlayerInventory.Instance.collectedModules.Contains(selectedModule))
                {
                    PlayerInventory.Instance.AddModule(selectedModule);
                }
            }

            if (MaintenanceUI.Instance != null)
            {
                MaintenanceUI.Instance.RefreshLootPanel();
            }

            Debug.Log($"❌ [모듈 취소] {selectedModule.moduleName} 모듈이 가방으로 복귀했습니다.");
        }

        EndDrag();
    }

    private void EndDrag()
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
