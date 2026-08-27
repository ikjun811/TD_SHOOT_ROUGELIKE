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

    [Header("Drag Visual UI")]
    [SerializeField] private GameObject dragGhostObject;
    [SerializeField] private RectTransform ghostRectTransform;

    private bool isDragging = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

        //  1. 마우스 위치 화면 동기화
        if (ghostRectTransform != null)
        {
            ghostRectTransform.position = Input.mousePosition;
        }

        //  2. [R]키 누를 시 90도 회전
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectedModule();
        }

        //  3. 우클릭 시 취소
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
            Image ghostImage = dragGhostObject.GetComponent<Image>();
            if (ghostImage != null)
            {
                ghostImage.color = new Color(0.2f, 0.8f, 1f, 0.6f); // 반투명 고스트 색상
            }
        }

        Debug.Log($" [모듈 선택] {module.moduleName} 선택됨 (R키로 회전 가능)");
    }

    private void RotateSelectedModule()
    {
        if (currentShape == null) return;

        currentShape = GridInventorySystem.RotateShapeClockwise(currentShape, currentWidth, currentHeight, out int newW, out int newH);
        currentWidth = newW;
        currentHeight = newH;

        Debug.Log($" [모듈 회전] 새로운 크기: {currentWidth}x{currentHeight}");
    }

    public bool TryPlaceOnGrid(int gridX, int gridY)
    {
        if (!isDragging || selectedModule == null) return false;

        if (GridInventorySystem.Instance == null) return false;

        bool success = GridInventorySystem.Instance.TryPlaceModule(selectedModule, gridX, gridY, currentWidth, currentHeight, currentShape);

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

            //  그리드 타일 시각화 색상 갱신!
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
        Debug.Log(" [모듈 선택 취소]");
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
}