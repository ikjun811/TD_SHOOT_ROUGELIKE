using System.IO;
using UnityEditor;
using UnityEngine;

// 유니크 모듈 에셋 생성 (메뉴: Tools > TD Shoot > Generate Unique Modules)
// A5b 산탄총 도탄 1개 (레전더리 전용, 6칸, GDD 8-6 #17). 같은 이름의 에셋이 이미 있으면 건너뜀
// 동작은 PlayerStatAndWeaponSync가 산탄총 탄 템플릿에 BulletChain(도탄, 가까운 3명 중 무작위)을 붙여 처리
public static class UniqueModuleGenerator
{
    private const string ParentFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";
    private const string OutputFolder = ParentFolder + "/Unique";

    [MenuItem("Tools/TD Shoot/Generate Unique Modules")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(ParentFolder))
        {
            AssetDatabase.CreateFolder("Assets/Project/Scripts/ScriptableObjects", "Modules");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(ParentFolder, "Unique");
        }

        int created = 0;
        int skipped = 0;
        bool T = true, F = false;

        string path = $"{OutputFolder}/Module_Unique_ShotgunRicochet_Legendary.asset";
        if (File.Exists(path))
        {
            skipped++;
        }
        else
        {
            ModuleDataSO module = ScriptableObject.CreateInstance<ModuleDataSO>();
            module.moduleName = "산탄총 도탄 (유니크)";
            module.rarity = RarityType.Legendary;
            module.category = ModuleCategory.Unique;
            module.target = ModuleTarget.Shotgun;
            module.mechanismSlot = MechanismSlot.BulletPath; // 관통·도탄과 동시 배치 불가
            module.effect = ModuleEffect.ShotgunRicochet;
            module.effectCount = 1;      // 산탄당 튕김 횟수
            module.effectValue = 0.5f;   // 튕긴 탄 피해 배율
            module.description = "산탄 하나하나가 적중 후 12m 안의 다른 적에게 1회 튕김 (피해 50%).\n가까운 적 3명 중 무작위로 튕겨 여러 적에게 나뉨. 슬러그탄에도 적용.\n관통·도탄과 함께 배치 불가.";
            module.width = 3;
            module.height = 3;
            module.shapeGrid = new[] { T, T, F, F, T, T, F, T, T }; // 6칸
            module.statValue = 0f;

            AssetDatabase.CreateAsset(module, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UniqueModuleGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
