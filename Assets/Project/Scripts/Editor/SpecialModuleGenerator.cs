using System.IO;
using UnityEditor;
using UnityEngine;

// 특수 모듈 에셋 일괄 생성 (메뉴: Tools > TD Shoot > Generate Special Modules)
// A3a: 도탄·관통 x 레어도 4종 = 8개 (GDD 8-6 #17). 같은 이름의 에셋이 이미 있으면 건너뜀
// 칸 수 7/6/5/4 (일반/희귀/엘리트/레전더리), 레어도별 횟수는 effectCount, 이어갈 때 피해 배율은 effectValue
// A3b·A3c 특수 모듈도 이 목록에 추가
public static class SpecialModuleGenerator
{
    private const string ParentFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";
    private const string OutputFolder = ParentFolder + "/Special";

    private class Spec
    {
        public string fileKey;
        public string displayName;
        public ModuleEffect effect;
        public ModuleTarget target = ModuleTarget.All;
        public MechanismSlot slot = MechanismSlot.None;
        public int[] counts;            // 레어도별 effectCount
        public float effectValue;
        public string descriptionFormat; // {0} = effectCount
    }

    private static readonly Spec[] Specs =
    {
        new Spec { fileKey = "Ricochet", displayName = "도탄", effect = ModuleEffect.Ricochet, slot = MechanismSlot.BulletPath,
            counts = new[] { 1, 1, 2, 4 }, effectValue = 0.7f,
            descriptionFormat = "적에게 맞으면 12m 안의 다른 적에게 튕김 ({0}회, 튕길 때마다 피해 70%).\n관통과 함께 배치 불가. 산탄총에는 효과 없음." },
        new Spec { fileKey = "Pierce", displayName = "관통", effect = ModuleEffect.Pierce, slot = MechanismSlot.BulletPath,
            counts = new[] { 1, 2, 2, 4 }, effectValue = 0.85f,
            descriptionFormat = "탄이 적을 관통해 계속 날아감 ({0}회, 관통할 때마다 피해 85%).\n도탄과 함께 배치 불가." },
    };

    private static readonly string[] RarityNames = { "일반", "희귀", "엘리트", "레전더리" };
    private static readonly int[] CellCounts = { 7, 6, 5, 4 }; // 일반/희귀/엘리트/레전더리

    private struct Shape
    {
        public int width, height;
        public bool[] cells;
        public Shape(int width, int height, bool[] cells) { this.width = width; this.height = height; this.cells = cells; }
    }

    // 칸 수별 모양 2종 (모듈마다 번갈아 사용)
    private static Shape GetShape(int cellCount, int variant)
    {
        bool T = true, F = false;
        switch (cellCount)
        {
            case 7:
                return variant == 0
                    ? new Shape(3, 3, new[] { T, T, T, T, T, T, F, T, F })   // 3x3 - 아래 두 모서리
                    : new Shape(3, 3, new[] { T, T, F, T, T, T, F, T, T });  // 3x3 - 대각 두 모서리
            case 6:
                return variant == 0
                    ? new Shape(2, 3, new[] { T, T, T, T, T, T })            // 2x3
                    : new Shape(3, 2, new[] { T, T, T, T, T, T });           // 3x2
            case 5:
                return variant == 0
                    ? new Shape(2, 3, new[] { T, T, T, T, T, F })            // P자
                    : new Shape(3, 2, new[] { T, F, T, T, T, T });           // U자
            default:
                return variant == 0
                    ? new Shape(3, 2, new[] { T, T, T, F, T, F })            // T자
                    : new Shape(2, 2, new[] { T, T, T, T });                 // 정사각형
        }
    }

    [MenuItem("Tools/TD Shoot/Generate Special Modules")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(ParentFolder))
        {
            AssetDatabase.CreateFolder("Assets/Project/Scripts/ScriptableObjects", "Modules");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(ParentFolder, "Special");
        }

        int created = 0;
        int skipped = 0;

        for (int i = 0; i < Specs.Length; i++)
        {
            Spec spec = Specs[i];
            foreach (RarityType rarity in System.Enum.GetValues(typeof(RarityType)))
            {
                string path = $"{OutputFolder}/Module_Special_{spec.fileKey}_{rarity}.asset";
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                int count = spec.counts[(int)rarity];
                Shape shape = GetShape(CellCounts[(int)rarity], i % 2);

                ModuleDataSO module = ScriptableObject.CreateInstance<ModuleDataSO>();
                module.moduleName = $"{spec.displayName} ({RarityNames[(int)rarity]})";
                module.rarity = rarity;
                module.category = ModuleCategory.Special;
                module.target = spec.target;
                module.mechanismSlot = spec.slot;
                module.effect = spec.effect;
                module.effectCount = count;
                module.effectValue = spec.effectValue;
                module.description = string.Format(spec.descriptionFormat, count);
                module.width = shape.width;
                module.height = shape.height;
                module.shapeGrid = (bool[])shape.cells.Clone();
                module.statValue = 0f;

                AssetDatabase.CreateAsset(module, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SpecialModuleGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
