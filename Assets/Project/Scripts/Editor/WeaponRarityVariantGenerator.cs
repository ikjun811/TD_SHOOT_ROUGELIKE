using System.IO;
using UnityEditor;
using UnityEngine;

// 무기 레어도 변형본 일괄 생성 (메뉴: Tools > TD Shoot > Generate Weapon Rarity Variants)
// ScriptableObjects 폴더의 일반(Common) 무기 에셋마다 희귀·엘리트·레전더리 복사본을 만듦 (5종 x 3 = 15개)
// 수치·아이콘·JU 연결은 원본을 그대로 복사하고 레어도만 변경 (레어도는 피해 배율에만 적용, GDD 8-6 #6)
// 같은 이름의 에셋이 이미 있으면 덮어쓰지 않고 건너뜀. 원본 수치를 바꾼 뒤 다시 맞추려면 변형본을 지우고 재생성
public static class WeaponRarityVariantGenerator
{
    private const string SourceFolder = "Assets/Project/Scripts/ScriptableObjects";
    private const string OutputFolder = "Assets/Project/Scripts/ScriptableObjects/Weapons";

    [MenuItem("Tools/TD Shoot/Generate Weapon Rarity Variants")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(SourceFolder, "Weapons");
        }

        int created = 0;
        int skipped = 0;

        // 원본: ScriptableObjects 폴더 바로 아래의 일반 등급 무기 (변형본 폴더 제외)
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDataSO", new[] { SourceFolder }))
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(sourcePath).Replace('\\', '/') != SourceFolder) continue;

            WeaponDataSO source = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(sourcePath);
            if (source == null || source.rarity != RarityType.Common) continue;

            foreach (RarityType rarity in new[] { RarityType.Rare, RarityType.Elite, RarityType.Legendary })
            {
                string path = $"{OutputFolder}/{source.name}_{rarity}.asset";
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                WeaponDataSO variant = Object.Instantiate(source);
                variant.rarity = rarity;

                AssetDatabase.CreateAsset(variant, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WeaponRarityVariantGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
