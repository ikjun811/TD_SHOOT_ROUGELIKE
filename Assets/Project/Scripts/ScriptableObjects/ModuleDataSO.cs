using UnityEngine;

[CreateAssetMenu(fileName = "NewModuleData", menuName = "GameData/Module Data")]
public class ModuleDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string moduleName;
    public RarityType rarity;
    public Sprite moduleIcon;
    public bool isUniqueModule; // 유니크 전용 모듈 여부

    [Header("Tetris Shape Settings")]
    [Tooltip("그리드 모양 (예: 2x2, 3x1 일자 등) 가로/세로 칸 수")]
    public int width = 2;
    public int height = 2;

    [Tooltip("모듈 모양 배열 (true: 칸 있음, false: 빈 공간)")]
    // 1차원 배열로 저장 후 2D로 사용 (인스펙터 편의성)
    public bool[] shapeGrid = new bool[4] { true, true, true, true };

    [Header("Stat Boost")]
    public ModuleStatType statType;
    public float statValue; // 예: 0.15 (+15% 공격력) 또는 +20 (체력)

    // 레어도에 따른 계수 보정 연산
    public float GetFinalStatValue()
    {
        float multiplier = rarity switch
        {
            RarityType.Common => 1.0f,
            RarityType.Rare => 1.3f,
            RarityType.Elite => 1.7f,
            RarityType.Legendary => 2.5f,
            _ => 1.0f
        };

        return statValue * multiplier;
    }
}