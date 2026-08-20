using UnityEngine;
using UnityEngine.UI;

public class GridPanelUI : MonoBehaviour
{
    [Header("Grid UI Settings")]
    [SerializeField] private GameObject gridSlotPrefab; // 1칸 타일 프리팹
    [SerializeField] private Transform gridSlotParent;  // GridLayoutGroup이 붙은 부모 패널

    private void Start()
    {
        GenerateGridSlots();
    }

    public void GenerateGridSlots()
    {
        if (GridInventorySystem.Instance == null || gridSlotPrefab == null || gridSlotParent == null) return;

        // 기존 슬롯 삭제
        foreach (Transform child in gridSlotParent)
        {
            Destroy(child.gameObject);
        }

        int width = GridInventorySystem.Instance.gridWidth;   // 8
        int height = GridInventorySystem.Instance.gridHeight; // 8

        // 8x8 = 64개 타일 UI 생성
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject slot = Instantiate(gridSlotPrefab, gridSlotParent);
                slot.name = $"Slot_{x}_{y}";
            }
        }
    }
}   