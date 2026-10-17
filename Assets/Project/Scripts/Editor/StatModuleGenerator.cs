using System.IO;
using UnityEditor;
using UnityEngine;

// 스탯 모듈 에셋 일괄 생성 (메뉴: Tools > TD Shoot > Generate Stat Modules)
// 스탯 8종 x 레어도 4종 = 32개. 같은 이름의 에셋이 이미 있으면 덮어쓰지 않고 건너뜀
// 수치·모양은 임시값 (GDD 8-4 스탯 상한 미정). 최종값 = 기본값 x 레어도 계수 (ModuleDataSO.GetFinalStatValue)
public static class StatModuleGenerator
{
    private const string OutputFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";

    private struct StatModuleSpec
    {
        public ModuleStatType statType;
        public string fileKey;
        public string displayName;
        public float baseValue;
        public int width;
        public int height;
        public bool[] shape;

        public StatModuleSpec(ModuleStatType statType, string fileKey, string displayName, float baseValue, int width, int height, bool[] shape)
        {
            this.statType = statType;
            this.fileKey = fileKey;
            this.displayName = displayName;
            this.baseValue = baseValue;
            this.width = width;
            this.height = height;
            this.shape = shape;
        }
    }

    // % 스탯은 0.1 = 10%, 체력·방어력은 고정 수치
    private static readonly StatModuleSpec[] Specs =
    {
        new StatModuleSpec(ModuleStatType.AttackDamagePercent, "Attack",   "공격력",     0.10f, 3, 1, new[] { true, true, true }),               // I 3칸
        new StatModuleSpec(ModuleStatType.FireRatePercent,     "FireRate", "연사력",     0.10f, 2, 2, new[] { true, false, true, true }),        // L 3칸
        new StatModuleSpec(ModuleStatType.ReloadSpeedPercent,  "Reload",   "장전 속도",  0.15f, 2, 1, new[] { true, true }),                     // 가로 2칸
        new StatModuleSpec(ModuleStatType.CriticalChance,      "Crit",     "치명타 확률", 0.05f, 1, 2, new[] { true, true }),                     // 세로 2칸
        new StatModuleSpec(ModuleStatType.SkillCooldownReduce, "Cooldown", "수류탄 쿨감", 0.10f, 2, 1, new[] { true, true }),                     // 가로 2칸
        new StatModuleSpec(ModuleStatType.MaxHealthBonus,      "Health",   "최대 체력",  20f,   1, 3, new[] { true, true, true }),               // 세로 3칸
        new StatModuleSpec(ModuleStatType.ArmorBonus,          "Armor",    "방어력",     1f,    1, 2, new[] { true, true }),                     // 세로 2칸 (피격당 고정 감소)
        new StatModuleSpec(ModuleStatType.MoveSpeedPercent,    "Speed",    "이동 속도",  0.05f, 1, 1, new[] { true }),                           // 1칸
    };

    private static readonly string[] RarityNames = { "일반", "희귀", "엘리트", "레전더리" };

    [MenuItem("Tools/TD Shoot/Generate Stat Modules")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Project/Scripts/ScriptableObjects", "Modules");
        }

        int created = 0;
        int skipped = 0;

        foreach (StatModuleSpec spec in Specs)
        {
            foreach (RarityType rarity in System.Enum.GetValues(typeof(RarityType)))
            {
                string path = $"{OutputFolder}/Module_{spec.fileKey}_{rarity}.asset";
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                ModuleDataSO module = ScriptableObject.CreateInstance<ModuleDataSO>();
                module.moduleName = $"{spec.displayName} 모듈 ({RarityNames[(int)rarity]})";
                module.rarity = rarity;
                module.isUniqueModule = false;
                module.width = spec.width;
                module.height = spec.height;
                module.shapeGrid = (bool[])spec.shape.Clone();
                module.statType = spec.statType;
                module.statValue = spec.baseValue;

                AssetDatabase.CreateAsset(module, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StatModuleGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
