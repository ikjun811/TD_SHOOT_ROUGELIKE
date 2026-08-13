using System.Collections.Generic;
using UnityEngine;

public class GridInventorySystem : MonoBehaviour
{
    public static GridInventorySystem Instance { get; private set; }

    [Header("Grid Capacity Settings")]
    public int gridWidth = 8;  // 기본 8x8 그리드 (영구 강화로 확장 가능)
    public int gridHeight = 8;

    private bool[,] gridMatrix; // true: 칸 차있음, false: 빈 칸
    public List<PlacedModule> placedModules = new List<PlacedModule>();

    [System.Serializable]
    public class PlacedModule
    {
        public ModuleDataSO moduleData;
        public int startX;
        public int startY;
        public int currentWidth;
        public int currentHeight;
        public bool[] currentShape;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid(gridWidth, gridHeight);
    }

    public void InitializeGrid(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        gridMatrix = new bool[gridWidth, gridHeight];
    }

    // 모듈 90도 시계방향 회전 연산 유틸리티
    public static bool[] RotateShapeClockwise(bool[] original, int width, int height, out int newWidth, out int newHeight)
    {
        newWidth = height;
        newHeight = width;
        bool[] rotated = new bool[newWidth * newHeight];

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                int oldIndex = r * width + c;
                int newRow = c;
                int newCol = height - 1 - r;
                int newIndex = newRow * newWidth + newCol;
                rotated[newIndex] = original[oldIndex];
            }
        }
        return rotated;
    }

    // 그리드 해당 위치에 모듈을 배치할 수 있는지 검증
    public bool CanPlaceModule(int startX, int startY, int shapeWidth, int shapeHeight, bool[] shape)
    {
        for (int r = 0; r < shapeHeight; r++)
        {
            for (int c = 0; c < shapeWidth; c++)
            {
                int shapeIndex = r * shapeWidth + c;
                if (shape[shapeIndex]) // 모듈 파편이 존재하는 칸만 체크
                {
                    int gridX = startX + c;
                    int gridY = startY + r;

                    // 그리드 경계 초과 체크
                    if (gridX < 0 || gridX >= gridWidth || gridY < 0 || gridY >= gridHeight)
                        return false;

                    // 이미 다른 모듈이 차지하고 있는지 체크
                    if (gridMatrix[gridX, gridY])
                        return false;
                }
            }
        }
        return true;
    }

    // 모듈 배치 및 그리드 점유 처리
    public bool TryPlaceModule(ModuleDataSO module, int startX, int startY, int shapeWidth, int shapeHeight, bool[] shape)
    {
        if (!CanPlaceModule(startX, startY, shapeWidth, shapeHeight, shape))
            return false;

        // 그리드 매트릭스 점유 처리
        for (int r = 0; r < shapeHeight; r++)
        {
            for (int c = 0; c < shapeWidth; c++)
            {
                int shapeIndex = r * shapeWidth + c;
                if (shape[shapeIndex])
                {
                    gridMatrix[startX + c, startY + r] = true;
                }
            }
        }

        // 배치 목록에 추가
        PlacedModule placed = new PlacedModule
        {
            moduleData = module,
            startX = startX,
            startY = startY,
            currentWidth = shapeWidth,
            currentHeight = shapeHeight,
            currentShape = shape
        };
        placedModules.Add(placed);

        RecalculateTotalStats();
        Debug.Log($"🧩 [그리드] {module.moduleName} 모듈 배치 성공! (위치: {startX}, {startY})");
        return true;
    }

    // 모듈 제거
    public void RemoveModule(PlacedModule module)
    {
        if (!placedModules.Contains(module)) return;

        for (int r = 0; r < module.currentHeight; r++)
        {
            for (int c = 0; c < module.currentWidth; c++)
            {
                int shapeIndex = r * module.currentWidth + c;
                if (module.currentShape[shapeIndex])
                {
                    gridMatrix[module.startX + c, module.startY + r] = false;
                }
            }
        }

        placedModules.Remove(module);
        RecalculateTotalStats();
        Debug.Log($"🧩 [그리드] {module.moduleData.moduleName} 모듈 제거됨.");
    }

    // 장착된 모든 모듈의 최종 스탯 합산 연산
    public void RecalculateTotalStats()
    {
        float totalAtkPercent = 0f;
        float totalHealthBonus = 0f;
        float totalArmorBonus = 0f;

        foreach (var pm in placedModules)
        {
            float statVal = pm.moduleData.GetFinalStatValue();
            switch (pm.moduleData.statType)
            {
                case ModuleStatType.AttackDamagePercent: totalAtkPercent += statVal; break;
                case ModuleStatType.MaxHealthBonus: totalHealthBonus += statVal; break;
                case ModuleStatType.ArmorBonus: totalArmorBonus += statVal; break;
            }
        }

        Debug.Log($"📊 [최종 스탯 갱신] 공격력: +{totalAtkPercent * 100}%, 추가 체력: +{totalHealthBonus}, 추가 방어력: +{totalArmorBonus}");
        // TODO: 캐릭터 스탯 컴포넌트에 최종 스탯 전달
    }
}